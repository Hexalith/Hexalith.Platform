namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Names one absent EXT-SECRETS-1 input and the provisioning action that would supply it. No custody effect is authorized.</summary>
public sealed class SecretsDependencyNotAvailableException : InvalidOperationException
{
    /// <summary>Creates a fail-closed result for one missing public reference.</summary>
    public SecretsDependencyNotAvailableException(string input, string provisioningAction)
        : base($"DependencyNotAvailable: EXT-SECRETS-1 {input}. {provisioningAction}")
    {
        Input = input;
        ProvisioningAction = provisioningAction;
    }

    /// <summary>Gets the missing public input name.</summary>
    public string Input { get; }

    /// <summary>Gets the owner provisioning action for that input.</summary>
    public string ProvisioningAction { get; }
}

/// <summary>Public reference checks and the provisioning action for each named input.</summary>
public static class SecretsProvisioning
{
    /// <summary>Returns whether a value is a bounded public reference and not an invented secret.</summary>
    public static bool IsReference(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && !value.Any(char.IsControl);

    /// <summary>Accepts an explicit Kubernetes Dapr application name; secret references, paths and URLs are not destinations.</summary>
    public static bool IsDaprApplicationId(string? value) => value is { Length: > 0 and <= 63 }
        && char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1])
        && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    /// <summary>Returns the owner action that must supply the named input. This text does not invent an identifier.</summary>
    public static string Action(string input) => input switch
    {
        "runtime.providerBinding" => "Enroll the existing OpenBao hexalith-keys store through a Dapr secret-store component on 192.168.1.30 and return its public component name.",
        "runtime.hmacCredentialReference" => "Enroll a new restricted application credential for that Dapr secret store and return its public reference. Do not reuse the operator or Memories token.",
        "runtime.currentInventoryAuthorityReference" => "Install the independent current key-inventory authority and return its public Dapr app id.",
        "runtime.tenantInventoryReference" => "Publish the current tenant inventory and return its public reference. Infrastructure readiness is not an inventory.",
        "runtime.profileReference" => "Publish the independent issuer, audience, and profile and return the public profile reference.",
        "runtime.callTimeout" => "Set an explicit resolution budget greater than zero and at most 30 seconds.",
        "runtime.issuer" => "Enroll the independent decision issuer outside Platform and Agents and return its public issuer reference.",
        "runtime.audience" => "Enroll the independent decision audience and return its public audience reference.",
        "runtime.profileVersion" => "Publish the current profile version and return that public version.",
        "runtime.profileNotBefore" => "Publish the current profile validity start as a UTC instant.",
        "runtime.profileValidUntil" => "Publish the current profile exclusive validity end as a UTC instant.",
        "runtime.actorRoleAuthorityReference" => "Install the independent actor-role authority and return its public Dapr app id. Decision verification has no signing method.",
        "runtime.independentSignerCredentialReference" => "Enroll the independently governed decision-approval issuer outside Platform and Agents and return its reference; Platform must not load or use this credential.",
        "runtime.capabilitySignerReference" => "Install the Platform deletion-capability signing owner and return its Dapr application id; do not use the independent approval issuer.",
        "runtime.capabilitySignerCredentialReference" => "Mount a restricted deletion-capability signer workload bearer credential and return file:/absolute/path; enroll the receiving owner ACL separately.",
        "runtime.publicTrustReference" => "Publish the independent signing-trust profile and return its public reference.",
        "runtime.currentRevocationReference" => "Publish the independent revocation authority and return its public reference.",
        "runtime.replayRegistrarCredentialReference" => "Install a private registrar credential restricted to system/trusted-envelope-replay and return its reference. Do not register it for general resolution.",
        "runtime.replayRegistrarReference" => "Install the private exact replay registrar and return its Dapr application id, separately from its mounted credential reference.",
        "runtime.compromiseSubscriberCredentialReference" => "Install the private compromise subscriber credential and return its reference. Do not register it for general resolution.",
        "runtime.compromiseSubscriptionReference" => "Enroll the compromise subscription and return its public reference.",
        "runtime.compromiseGuardMirrorReference" => "Enroll the independent guard-revocation mirror and return its public reference.",
        "runtime.compromiseSubscriberBinding" => "Compose the exact independently enrolled revocation subscription and guard mirror with AddPrivateCompromise; reference strings alone do not install a subscriber.",
        "runtime.privateCredentialBoundTransport" => "Install the mounted-credential Dapr transport; private requests must authenticate a workload bearer separately from the Dapr app-channel token.",
        "runtime.nonrollbackJournalReference" => "Qualify the existing nonrollback journal and return its public reference. Do not substitute a local restore.",
        "runtime.allCopyLifecycleAuthorityReference" => "Qualify the existing all-copy lifecycle provider and return its public reference. Do not add a database or substitute provider.",
        "acceptedReplacementCommit" => "The owner fills acceptedReplacementCommit with the accepted SHA and accepts it before any Live run.",
        _ => "Provision the named public reference on 192.168.1.30. Do not invent an identifier.",
    };
}
