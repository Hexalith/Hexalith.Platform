using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Registers the real lifecycle client/provider/authority in the owning service; no startup installation or enrollment.</summary>
public static class InstalledCustodyLifecycle
{
    /// <summary>Composes explicit current independent authorizers, exact original locators and value-free lifecycle ports.</summary>
    public static IServiceCollection AddInstalledCustodyLifecycle(this IServiceCollection services, DaprClient dapr,
        CustodyLifecycleInstallation installation, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(installation);
        TimeProvider actualClock = clock ?? TimeProvider.System;
        var transport = new DaprSecretsInvoker(dapr);
        var originals = new DaprCustodyOriginalLocators(new(transport, installation.OriginalAuthority, actualClock));
        var callSource = new DaprCustodyAuthorizerClient(transport,
            new Dictionary<string, string> { [DaprCustodyAuthorizerClient.LogicalRole] = installation.AuthorizerAppId }, installation.AuthorizerCredentialReference);
        var client = new CustodyLifecycleClient(dapr, new Dictionary<string, string> { [CustodyLifecycleClient.LogicalRole] = installation.BindingName },
            installation.OwnerScope, installation.EnrollmentJson, installation.PublicKeyPem, callSource);
        var owner = new CustodyOwnerAuthority(dapr, installation.BindingName, installation.TenantId, installation.OwnerScope,
            installation.EnrollmentJson, installation.PublicKeyPem, new DaprCustodyOwnerCallSource(new(transport, installation.OwnerAuthority, actualClock)));
        var lifecycle = new DaprCustodyKeyLifecycleProvider(client, originals);
        services.AddSingleton<ICustodyKeyLifecycleProvider>(lifecycle);
        services.AddSingleton<ICustodyKeyLifecycleAuthority>(new DaprCustodyKeyLifecycleAuthority(installation.TenantId, owner, client, lifecycle, originals));
        return services;
    }
}
