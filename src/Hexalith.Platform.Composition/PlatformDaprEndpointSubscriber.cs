namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;

/// <summary>Applies allocated Dapr listener ports and each server's channel tokens to the actual CLI process.</summary>
internal sealed class PlatformDaprEndpointSubscriber(IReadOnlyList<PlatformServerBinding> servers, string? certificateDirectory = null, string? sentryAddress = null, string? workspace = null) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _ = eventing.Subscribe<BeforeStartEvent>((startup, _) =>
        {
            try { Apply(startup.Model, servers, certificateDirectory, sentryAddress, workspace); }
            catch (Exception failure) when (workspace is not null)
            {
                PlatformCompositionWorkspace.MarkFailed(workspace, failure.Message);
                throw;
            }
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    /// <summary>Configures generated CLI resources after the Dapr integration creates them.</summary>
    internal static void Apply(DistributedApplicationModel model, IReadOnlyList<PlatformServerBinding> servers, string? certificateDirectory = null, string? sentryAddress = null, string? workspace = null)
    {
        IEnumerable<IResource> applications = servers.Count == 0
            ? model.Resources.Where(resource => resource.Annotations.OfType<DaprSidecarAnnotation>().Any())
            : servers.Select(server => model.Resources.Single(resource => resource.Name == server.ResourceId));
        foreach (IResource application in applications.ToArray())
        {
            PlatformServerBinding? server = servers.SingleOrDefault(server => server.ResourceId == application.Name);
            ExecutableResource sidecar = DaprSelfHostedMtls.BindGeneratedSidecar(model, application, certificateDirectory, sentryAddress);
            if (sentryAddress is not null && workspace is not null && servers.Count > 0)
                sidecar.Annotations.Add(new CommandLineArgsCallbackAnnotation(async context =>
                    File.WriteAllText(ListenerInventoryPath(workspace, application.Name), System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Resource = application.Name,
                        Listeners = await NativeListenerPortsAsync(context.Args, context.CancellationToken).ConfigureAwait(false),
                    }))));
            sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                foreach (string token in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
                    if (server is not null && server.Environment.TryGetValue(token, out string? value)) context.EnvironmentVariables[token] = value;
            }));
        }
    }
    /// <summary>Gets the metadata-only receipt path for an actual native sidecar launch.</summary>
    internal static string ListenerInventoryPath(string workspace, string resource) => Path.Combine(workspace, "native-listeners." + resource + ".json");

    /// <summary>Reads the four explicit native listener arguments without retaining other process arguments.</summary>
    internal static async Task<IReadOnlyDictionary<string, int>> NativeListenerPortsAsync(IList<object> arguments, CancellationToken cancellationToken)
    {
        Dictionary<string, int> listeners = [];
        foreach (string flag in new[] { "--dapr-http-port", "--dapr-grpc-port", "--dapr-internal-grpc-port", "--metrics-port" })
        {
            int index = arguments.IndexOf(flag);
            if (index < 0 || index + 1 >= arguments.Count || arguments.Count(argument => Equals(argument, flag)) != 1)
                throw new InvalidDataException($"The native sidecar requires exactly one explicit valid {flag} listener.");
            string? value = arguments[index + 1] is IValueProvider provider
                ? await provider.GetValueAsync(cancellationToken).ConfigureAwait(false)
                : Convert.ToString(arguments[index + 1], System.Globalization.CultureInfo.InvariantCulture);
            if (!int.TryParse(value, out int port) || port is < 1 or > 65535)
                throw new InvalidDataException($"The native sidecar requires exactly one explicit valid {flag} listener.");
            listeners.Add(flag, port);
        }
        return listeners;
    }
}
