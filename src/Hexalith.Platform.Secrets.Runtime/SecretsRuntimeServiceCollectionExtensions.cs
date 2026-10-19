using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Registers an adapter only from valid explicit configuration. Replay and compromise stay out of general resolution.</summary>
public static class SecretsRuntimeServiceCollectionExtensions
{
    /// <summary>Registers the Dapr HMAC binding, or returns the specific missing input. No option is reported as the profile reference unless that input is the one missing.</summary>
    public static string? AddExplicitHmac(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (!SecretsProvisioning.IsReference(configuration.ProviderBinding)) { return "runtime.providerBinding"; }
        if (!SecretsProvisioning.IsReference(configuration.HmacCredentialReference)) { return "runtime.hmacCredentialReference"; }
        if (!SecretsProvisioning.IsReference(configuration.CurrentInventoryAuthorityReference)) { return "runtime.currentInventoryAuthorityReference"; }
        if (!SecretsProvisioning.IsReference(configuration.ProfileReference)) { return "runtime.profileReference"; }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return "runtime.callTimeout"; }
        if (!SecretsProvisioning.IsReference(configuration.Issuer)) { return "runtime.issuer"; }
        if (!SecretsProvisioning.IsReference(configuration.Audience)) { return "runtime.audience"; }
        if (!SecretsProvisioning.IsReference(configuration.ProfileVersion)) { return "runtime.profileVersion"; }
        if (configuration.ProfileNotBefore is null) { return "runtime.profileNotBefore"; }
        if (configuration.ProfileValidUntil is null) { return "runtime.profileValidUntil"; }
        if (configuration.ExpectedProfile is null) { return "runtime.profileNotBefore"; }
        var options = new DaprPlatformHmacOptions(configuration.ProviderBinding!, configuration.HmacCredentialReference!,
            configuration.CurrentInventoryAuthorityReference!, configuration.ProfileReference!, configuration.ExpectedProfile, timeout);
        services.AddDaprPlatformHmac(options, new DaprHmacAuthorityAdapter(configuration, invoker, clock));
        return null;
    }

    /// <summary>Registers inventory, decision, trust, revocation, and signing adapters whose references are present. Replay and compromise are omitted.</summary>
    public static IServiceCollection AddExplicitSecretsRuntime(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (Ready(configuration.CurrentInventoryAuthorityReference, configuration)) { services.AddSingleton<IPlatformKeyInventoryAuthority>(new DaprInventoryAuthorityAdapter(configuration, invoker, clock)); }
        if (Ready(configuration.ActorRoleAuthorityReference, configuration)) { services.AddSingleton<IIndependentDecisionAuthority>(new DaprDecisionAuthorityAdapter(configuration, invoker, clock)); }
        if (Ready(configuration.PublicTrustReference, configuration)) { services.AddSingleton<IDeletionCapabilitySigningTrustProvider>(new DaprSigningTrustAdapter(configuration, invoker, clock)); }
        if (Ready(configuration.CurrentRevocationReference, configuration)) { services.AddSingleton<IDeletionCapabilityRevocationAuthenticator>(new DaprRevocationAuthenticatorAdapter(configuration, invoker, clock)); }
        if (PrivateReady(configuration.CapabilitySignerReference, configuration.CapabilitySignerCredentialReference, configuration, invoker)) { services.AddSingleton<IDeletionCapabilitySigningProvider>(new DaprSigningProviderAdapter(configuration, invoker, clock)); }
        return services;
    }

    /// <summary>Adds only the private replay verifier when its reference is explicit. The registrar is not a general service.</summary>
    public static string? AddPrivateReplay(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (!SecretsProvisioning.IsDaprApplicationId(configuration.ReplayRegistrarReference)) { return "runtime.replayRegistrarReference"; }
        if (!SecretsProvisioning.IsReference(configuration.ReplayRegistrarCredentialReference)) { return "runtime.replayRegistrarCredentialReference"; }
        if (invoker is not ISecretsCredentialBoundDaprInvoker) { return "runtime.privateCredentialBoundTransport"; }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return "runtime.callTimeout"; }
        services.AddPrivateTrustedEnvelopeVerifier(new DaprReplayRegistrarAdapter(configuration, invoker, clock));
        return null;
    }

    /// <summary>Returns the missing concrete binding; reference checks alone cannot compose an authenticated subscriber.</summary>
    public static string? AddPrivateCompromise(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        string? missing = ValidatePrivateCompromise(services, configuration, invoker);
        return missing ?? "runtime.compromiseSubscriberBinding";
    }

    /// <summary>Composes only the authenticated subscriber with separately supplied enrolled source/target and mirror. The registrar remains a private constructor capture.</summary>
    public static string? AddPrivateCompromise(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker,
        DeletionCapabilityRevocationSubscription subscription, IDeletionCapabilityGuardRevocationMirror mirror, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(mirror);
        string? missing = ValidatePrivateCompromise(services, configuration, invoker);
        if (missing is not null) { return missing; }
        if (!SecretsProvisioning.IsReference(subscription.Issuer) || !SecretsProvisioning.IsReference(subscription.Audience)
            || !SecretsProvisioning.IsReference(subscription.TenantId)
            || subscription.Issuer != configuration.Issuer || subscription.Audience != configuration.Audience)
        { return "runtime.compromiseSubscriberBinding"; }
        services.AddSingleton(provider =>
        {
            TimeProvider actualClock = clock ?? provider.GetService<TimeProvider>() ?? TimeProvider.System;
            return new DeletionCapabilityRevocationSubscriber(subscription,
                new DaprRevocationAuthenticatorAdapter(configuration, invoker, actualClock),
                new DaprCompromiseRegistrarAdapter(configuration, invoker, actualClock), mirror, actualClock);
        });
        return null;
    }

    private static string? ValidatePrivateCompromise(IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (!SecretsProvisioning.IsDaprApplicationId(configuration.CompromiseSubscriptionReference)) { return "runtime.compromiseSubscriptionReference"; }
        if (!SecretsProvisioning.IsReference(configuration.CompromiseSubscriberCredentialReference)) { return "runtime.compromiseSubscriberCredentialReference"; }
        if (!SecretsProvisioning.IsReference(configuration.CompromiseGuardMirrorReference)) { return "runtime.compromiseGuardMirrorReference"; }
        if (!SecretsProvisioning.IsReference(configuration.CurrentRevocationReference)) { return "runtime.currentRevocationReference"; }
        if (invoker is not ISecretsCredentialBoundDaprInvoker) { return "runtime.privateCredentialBoundTransport"; }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return "runtime.callTimeout"; }
        return null;
    }

    private static bool Ready(string? reference, SecretsRuntimeConfiguration configuration)
        => SecretsProvisioning.IsReference(reference) && configuration.CallTimeout is { } timeout && timeout > TimeSpan.Zero && timeout <= TimeSpan.FromSeconds(30);

    private static bool PrivateReady(string? appId, string? credentialReference, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker)
        => SecretsProvisioning.IsDaprApplicationId(appId) && Ready(credentialReference, configuration) && invoker is ISecretsCredentialBoundDaprInvoker;
}
