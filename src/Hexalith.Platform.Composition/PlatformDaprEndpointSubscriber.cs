namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

/// <summary>Applies allocated Dapr listener ports and each server's channel tokens to the actual CLI process.</summary>
internal sealed class PlatformDaprEndpointSubscriber(IReadOnlyList<PlatformServerBinding> servers, string? certificateDirectory = null) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _ = eventing.Subscribe<BeforeStartEvent>((startup, _) =>
        {
            Apply(startup.Model, servers, certificateDirectory);
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    /// <summary>Configures generated CLI resources after the Dapr integration creates them.</summary>
    internal static void Apply(DistributedApplicationModel model, IReadOnlyList<PlatformServerBinding> servers, string? certificateDirectory = null)
    {
        foreach (PlatformServerBinding server in servers)
        {
            ExecutableResource sidecar = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == server.ResourceId + "-dapr-cli");
            foreach (EndpointAnnotation endpoint in sidecar.Annotations.OfType<EndpointAnnotation>().Where(endpoint => endpoint.Name is "http" or "grpc" or "metrics"))
            {
                endpoint.TargetPort = endpoint.Port;
                endpoint.IsProxied = false;
            }
            sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
            {
                foreach (string token in new[] { "APP_API_TOKEN", "DAPR_API_TOKEN" })
                    if (server.Environment.TryGetValue(token, out string? value)) context.EnvironmentVariables[token] = value;
                if (certificateDirectory is not null)
                {
                    foreach (var credential in new[] { ("DAPR_TRUST_ANCHORS", "ca.crt"), ("DAPR_CERT_CHAIN", "issuer.crt"), ("DAPR_CERT_KEY", "issuer.key") })
                        context.EnvironmentVariables[credential.Item1] = await DaprSelfHostedMtls.ReadCredentialAsync(certificateDirectory, credential.Item2, 20, TimeSpan.FromMilliseconds(500), static (delay, token) => Task.Delay(delay, token), context.CancellationToken);
                    context.EnvironmentVariables["DAPR_CONTROLPLANE_TRUST_DOMAIN"] = "localhost";
                    context.EnvironmentVariables["DAPR_CONTROLPLANE_NAMESPACE"] = "default";
                    context.EnvironmentVariables["NAMESPACE"] = "default";
                }
            }));
        }
    }
}
