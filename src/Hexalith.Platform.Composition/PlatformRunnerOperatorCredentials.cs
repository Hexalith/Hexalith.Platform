namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Tenants.Contracts.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Issues runner-key relay credentials for a composition without the generated realm, matching native preparation.</summary>
/// <param name="signingKey">The per-run signing key, held only in the AppHost.</param>
/// <param name="subject">The run's operator subject.</param>
/// <param name="issuer">The Tenants workload issuer of this run.</param>
/// <param name="workload">The Tenants Dapr application identity.</param>
internal sealed class PlatformRunnerOperatorCredentials(string signingKey, string subject, string issuer, string workload) : IPlatformOperatorCredentials
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public string OperatorName => subject;

    /// <inheritdoc />
    public Task<string> OperatorTokenAsync(IReadOnlyList<PlatformOperatorGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        cancellationToken.ThrowIfCancellationRequested();
        string[] permissions = [.. grants.Where(grant => !grant.IsGlobalAdministrator).Select(grant => grant.ClaimValue).Distinct(StringComparer.Ordinal)];
        return Task.FromResult(CompositionTokenFactory.Create(signingKey,
            new CompositionTokenRequest(subject, [], [], permissions, grants.Any(grant => grant.IsGlobalAdministrator), Lifetime), DateTimeOffset.UtcNow));
    }

    /// <inheritdoc />
    public async Task<string?> WorkloadAssertionAsync(IHostEnvironment environment, string audience, string domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:SigningKey"] = signingKey,
            ["Authentication:JwtBearer:Issuer"] = issuer,
            ["Authentication:JwtBearer:Audience"] = CompositionEnvironment.TokenAudience,
            ["Authentication:JwtBearer:ValidAudiences:0"] = CompositionEnvironment.TokenAudience,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        services.AddLogging();
        services.AddEventStoreWorkloadAssertionIssuer(configuration, options => options.Workload = workload);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            return await provider.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(new(audience,
                EventStoreGatewayWorkloadOperations.CommandSubmit, new Dictionary<string, string>
                {
                    [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = TenantIdentity.DefaultTenantId,
                    [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
                }), cancellationToken).ConfigureAwait(false);
        }
    }
}
