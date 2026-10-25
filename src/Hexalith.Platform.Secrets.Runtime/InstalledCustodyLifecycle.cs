using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Bounded authenticated locators. Returned complete originals remain subject to custody verification.</summary>
public sealed class DaprCustodyOriginalLocators(IndependentDaprAuthority authority) : ICustodyKeyRegistrationSource, ICustodyReservedManifestSource
{
    /// <inheritdoc />
    public async Task<CustodyKeyRegistration?> FindAsync(CustodyKeyObjectIdentity identity, CancellationToken cancellationToken)
    {
        var original = await authority.InvokeAsync<CustodyKeyRegistration>("custody-registration-original", new { identity }, cancellationToken).ConfigureAwait(false);
        return original?.Identity == identity ? original : null;
    }
    /// <inheritdoc />
    public async Task<DeletionBatchConsumptionRequest?> FindAsync(string reservationReceiptId, CancellationToken cancellationToken)
    {
        if (!CustodyBindingContract.Text(reservationReceiptId)) return null;
        return await authority.InvokeAsync<DeletionBatchConsumptionRequest>("custody-reserved-manifest-original", new { reservationReceiptId }, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Exact owner-call framing from a separately authenticated current authority; no caller-supplied envelope is trusted.</summary>
public sealed class DaprCustodyOwnerCallSource(IndependentDaprAuthority authority) : ICustodyOwnerCallSource
{
    /// <inheritdoc />
    public Task<CustodyOwnerAuthorizedCall?> AuthorizeAsync(string phase, string tenant, string ownerScope,
        string? operationIdentity, string? transitionJson, CancellationToken cancellationToken)
        => phase is "read-current" or "admit-transition" or "record-transition" or "recover-transition" or "verify-transition"
            ? authority.InvokeAsync<CustodyOwnerAuthorizedCall>("custody-owner-call", new { phase, tenant, ownerScope, operationIdentity, transitionJson }, cancellationToken)
            : Task.FromResult<CustodyOwnerAuthorizedCall?>(null);
}

/// <summary>Installed public lifecycle bindings. Private credential references remain constructor captures.</summary>
public sealed record CustodyLifecycleInstallation(string TenantId, string OwnerScope, string EnrollmentJson,
    string PublicKeyPem, string BindingName, string AuthorizerAppId, string AuthorizerCredentialReference,
    IndependentDaprAuthorityBinding OriginalAuthority, IndependentDaprAuthorityBinding OwnerAuthority);

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
        services.AddSingleton(client);
        services.AddSingleton<ICustodyKeyLifecycleProvider>(lifecycle);
        services.AddSingleton<ICustodyKeyLifecycleAuthority>(new DaprCustodyKeyLifecycleAuthority(installation.TenantId, owner, client, lifecycle, originals));
        return services;
    }
}
