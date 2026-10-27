using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

/// <summary>Binds local peer identities after the Dapr integration creates the generated executables.</summary>
/// <param name="applications">The applications sharing the private control plane.</param>
/// <param name="certificateDirectory">The private Sentry issuer directory.</param>
/// <param name="sentryAddress">The allocated Sentry listener.</param>
internal sealed class DaprSelfHostedMtlsSubscriber(
    IReadOnlyList<ProjectResource> applications,
    string certificateDirectory,
    string sentryAddress) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _ = eventing.Subscribe<BeforeStartEvent>((startup, token) =>
        {
            foreach (ProjectResource application in applications)
                _ = DaprSelfHostedMtls.BindGeneratedSidecar(startup.Model, application, certificateDirectory, sentryAddress);
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }
}
