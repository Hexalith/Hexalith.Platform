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
        if (Ready(configuration.IndependentSignerCredentialReference, configuration)) { services.AddSingleton<IDeletionCapabilitySigningProvider>(new DaprSigningProviderAdapter(configuration, invoker, clock)); }
        return services;
    }

    /// <summary>Adds only the private replay verifier when its reference is explicit. The registrar is not a general service.</summary>
    public static string? AddPrivateReplay(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (!SecretsProvisioning.IsReference(configuration.ReplayRegistrarCredentialReference)) { return "runtime.replayRegistrarCredentialReference"; }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return "runtime.callTimeout"; }
        services.AddPrivateTrustedEnvelopeVerifier(new DaprReplayRegistrarAdapter(configuration, invoker, clock));
        return null;
    }

    /// <summary>Checks the compromise subscription and mirror without registering either credential for general resolution.</summary>
    public static string? AddPrivateCompromise(this IServiceCollection services, SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        if (!SecretsProvisioning.IsReference(configuration.CompromiseSubscriptionReference)) { return "runtime.compromiseSubscriptionReference"; }
        if (!SecretsProvisioning.IsReference(configuration.CompromiseGuardMirrorReference)) { return "runtime.compromiseGuardMirrorReference"; }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { return "runtime.callTimeout"; }
        new DaprCompromiseRegistrarAdapter(configuration, invoker, clock).RequireMirror();
        return null;
    }

    private static bool Ready(string? reference, SecretsRuntimeConfiguration configuration)
        => SecretsProvisioning.IsReference(reference) && configuration.CallTimeout is { } timeout && timeout > TimeSpan.Zero && timeout <= TimeSpan.FromSeconds(30);
}
