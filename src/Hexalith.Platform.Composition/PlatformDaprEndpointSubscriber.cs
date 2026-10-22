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
            catch (InvalidDataException failure)
            {
                if (workspace is not null) File.WriteAllText(Path.Combine(workspace, "composition-failure.txt"), failure.Message);
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
            ExecutableResource sidecar = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == application.Name + "-dapr-cli");
            if (sentryAddress is not null)
            {
                string runtimeHome = application.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations
                    .OfType<DaprSidecarOptionsAnnotation>().Single().Options.RuntimePath
                    ?? throw new InvalidDataException("An isolated Sentry endpoint requires the selected Dapr runtime home.");
                string runtime = Path.Combine(runtimeHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
                if (!File.Exists(runtime)) throw new InvalidDataException($"The selected Dapr native runtime is unavailable at '{runtime}'. Install the catalog-pinned runtime; isolated Sentry requires native daprd.");
                sidecar.Annotations.OfType<ExecutableAnnotation>().Last().Command = runtime;
                sidecar.Annotations.Add(new CommandLineArgsCallbackAnnotation((IList<object> arguments) =>
                {
                    // Dapr CLI 1.18.0 hardcodes localhost:50001 when it enables mTLS. Use the
                    // same verified native runtime and generated callbacks, with its supported flag.
                    if (arguments.Count == 0 || arguments[0] is not "run"
                        || arguments.OfType<string>().Any(argument => argument is "--run-file" or "--"))
                        throw new InvalidDataException("The selected isolated sidecar requires a sidecar-only Dapr run command.");
                    arguments.RemoveAt(0);
                    int runtimePath = arguments.IndexOf("--runtime-path");
                    if (runtimePath < 0 || runtimePath + 1 >= arguments.Count)
                        throw new InvalidDataException("The selected isolated sidecar has no explicit Dapr runtime path.");
                    arguments.RemoveAt(runtimePath + 1);
                    arguments.RemoveAt(runtimePath);
                    arguments.Add("--enable-mtls");
                    arguments.Add("--sentry-address");
                    // Qualification adapters may replace the model endpoint after composition.
                    int port = model.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations
                        .OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port
                        ?? throw new InvalidDataException("The current Sentry listener has no allocated host port.");
                    arguments.Add(sentryAddress[..(sentryAddress.LastIndexOf(':') + 1)] + port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (workspace is not null && servers.Count > 0)
                        File.WriteAllText(ListenerInventoryPath(workspace, application.Name), System.Text.Json.JsonSerializer.Serialize(new
                        {
                            Resource = application.Name,
                            Listeners = NativeListenerPorts(arguments),
                        }));
                }));
            }
            foreach (EndpointAnnotation endpoint in sidecar.Annotations.OfType<EndpointAnnotation>().Where(endpoint => endpoint.Name is "http" or "grpc" or "metrics"))
            {
                endpoint.TargetPort = endpoint.Port;
                endpoint.IsProxied = false;
            }
            sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                foreach (string token in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
                    if (server is not null && server.Environment.TryGetValue(token, out string? value)) context.EnvironmentVariables[token] = value;
                if (certificateDirectory is not null)
                {
                    foreach (var credential in new[] { ("DAPR_TRUST_ANCHORS", "ca.crt"), ("DAPR_CERT_CHAIN", "issuer.crt"), ("DAPR_CERT_KEY", "issuer.key") })
                        context.EnvironmentVariables[credential.Item1] = await DaprSelfHostedMtls.ReadCredentialAsync(certificateDirectory, credential.Item2, 20, TimeSpan.FromMilliseconds(500), static (delay, token) => Task.Delay(delay, token), context.CancellationToken).ConfigureAwait(false);
                    context.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"] = "localhost";
                    context.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"] = "default";
                    context.EnvironmentVariables["NAMESPACE"] = "default";
                }
            }));
        }
    }
    /// <summary>Gets the metadata-only receipt path for an actual native sidecar launch.</summary>
    internal static string ListenerInventoryPath(string workspace, string resource) => Path.Combine(workspace, "native-listeners." + resource + ".json");

    /// <summary>Reads the four explicit native listener arguments without retaining other process arguments.</summary>
    internal static IReadOnlyDictionary<string, int> NativeListenerPorts(IList<object> arguments)
    {
        Dictionary<string, int> listeners = [];
        foreach (string flag in new[] { "--dapr-http-port", "--dapr-grpc-port", "--dapr-internal-grpc-port", "--metrics-port" })
        {
            int index = arguments.IndexOf(flag);
            if (index < 0 || index + 1 >= arguments.Count || arguments.Count(argument => Equals(argument, flag)) != 1
                || !int.TryParse(Convert.ToString(arguments[index + 1], System.Globalization.CultureInfo.InvariantCulture), out int port) || port is < 1 or > 65535)
                throw new InvalidDataException($"The native sidecar requires exactly one explicit valid {flag} listener.");
            listeners.Add(flag, port);
        }
        return listeners;
    }
}
