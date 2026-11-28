namespace Hexalith.Platform.Composition;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Issues realm-signed relay credentials: operator tokens from the operator client and Tenants workload assertions from its realm client.</summary>
/// <param name="realmOperator">The generated realm's operator client.</param>
/// <param name="authority">The generated realm authority.</param>
/// <param name="workload">The Tenants workload client and Dapr application identity.</param>
/// <param name="workloadSecret">The Tenants realm client secret, held only in the AppHost.</param>
/// <param name="userAudience">The gateway user audience of the shared JWT contract.</param>
internal sealed class PlatformRealmOperatorCredentials(PlatformRealmOperator realmOperator, string authority, string workload,
    string workloadSecret, string userAudience) : IPlatformOperatorCredentials
{
    /// <inheritdoc />
    public string OperatorName => PlatformRealmOperator.ClientId;

    /// <inheritdoc />
    public Task<string> OperatorTokenAsync(IReadOnlyList<PlatformOperatorGrant> grants, CancellationToken cancellationToken) =>
        realmOperator.TokenAsync(grants, cancellationToken);

    /// <inheritdoc />
    public async Task<string?> WorkloadAssertionAsync(IHostEnvironment environment, string audience, string domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string contract = EventStoreWorkloadAuthenticationDefaults.JwtContractSection + ":";
        string issuer = WorkloadAssertionIssuerOptions.SectionName + ":";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [contract + "Authority"] = authority,
            [contract + "Issuer"] = authority,
            [contract + "Audience"] = userAudience,
            [contract + "ValidAudiences:0"] = userAudience,
            [contract + "SigningKey"] = string.Empty,
            [contract + "AllowInsecureSymmetricKey"] = "false",
            [contract + "RequireHttpsMetadata"] = "false",
            [contract + "AllowedAlgorithms:0"] = "RS256",
            [issuer + "TokenEndpoint"] = authority + "/protocol/openid-connect/token",
            [issuer + "Workload"] = workload,
            [issuer + "ClientId"] = workload,
            [issuer + "ClientSecret"] = workloadSecret,
            [issuer + "EnableResourceBindingRequests"] = "false",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        services.AddLogging();
        services.AddEventStoreWorkloadAssertionIssuer(configuration, options => options.Workload = workload);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            // The Tenants realm client is not a bound client: the assertion names only the audience and operation.
            return await provider.GetRequiredService<IWorkloadAssertionIssuer>()
                .IssueAsync(new(audience, EventStoreGatewayWorkloadOperations.CommandSubmit), cancellationToken).ConfigureAwait(false);
        }
    }
}
