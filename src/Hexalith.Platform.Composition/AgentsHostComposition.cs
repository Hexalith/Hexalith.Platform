namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Platform-owned Agents composition. A missing accepted profile keeps the existing refusal and adds no resource.</summary>
public static class AgentsHostComposition
{
    /// <summary>Configuration key for the reference-only deployment profile path.</summary>
    public const string ProfilePathKey = "Platform:Agents:DeploymentProfile";

    private static readonly (string Service, string Relative, string Resource)[] Projects =
    [
        ("eventStore", "src/Hexalith.EventStore/Hexalith.EventStore.csproj", "eventstore"),
        ("tenants", "src/Hexalith.Tenants/Hexalith.Tenants.csproj", "tenants"),
        ("tenants", "src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj", "tenants-api"),
        ("parties", "src/Hexalith.Parties/Hexalith.Parties.csproj", "parties"),
        ("conversations", "src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj", "conversations"),
        ("agents", "src/Hexalith.Agents.Server/Hexalith.Agents.Server.csproj", "agents"),
    ];

    /// <summary>Composes sibling services only when the configured profile validates. Returns false when the profile is absent or rejected.</summary>
    /// <param name="builder">The platform application builder.</param>
    public static bool TryCompose(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string? path = builder.Configuration[ProfilePathKey];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        AgentsDeploymentProfileModel profile;
        try
        {
            profile = AgentsDeploymentProfile.Validate(File.ReadAllText(path));
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        Compose(builder, profile, capabilities: null);
        return true;
    }

    /// <summary>Adds the five sibling services and binds tenant, identity, and credential references. Private objects are registered only when the caller already holds them.</summary>
    /// <param name="builder">The platform application builder.</param>
    /// <param name="profile">A validated reference-only profile.</param>
    /// <param name="capabilities">Existing private registrar and spool. Null installs neither credentials nor spool storage.</param>
    public static void Compose(IDistributedApplicationBuilder builder, AgentsDeploymentProfileModel profile, AgentsPrivateCapabilities? capabilities)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);
        List<(string Resource, string Project)> projects = [];
        foreach ((string service, string relative, string resource) in Projects)
            projects.Add((resource, RequireProject(builder, profile, service, relative)));
        RequireProject(builder, profile, "agents", "src/Hexalith.Agents.UI/Hexalith.Agents.UI.csproj");
        foreach ((string resource, string project) in projects)
        {
            IResourceBuilder<ProjectResource> added = builder.AddProject(resource, project, launchProfileName: null)
                .WithEnvironment("Agents__CurrentTenant", profile.CurrentTenant)
                .WithEnvironment("Agents__HumanIdentity", profile.HumanIdentity)
                .WithEnvironment("Agents__ApproverEvidence", profile.ApproverEvidence)
                .WithEnvironment("Agents__Replay__Reference", profile.ReplayReference)
                .WithEnvironment("Agents__Recorder__Reference", profile.RecorderReference)
                .WithEnvironment("Agents__Worker__Reference", profile.WorkerReference)
                .WithEnvironment("Agents__Spool__Backend", profile.SpoolBackend)
                .WithEnvironment("Agents__Spool__ReplicaCount", profile.ReplicaCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .WithEnvironment("Agents__Spool__FailureModel", profile.FailureModel)
                .WithEnvironment("Agents__Protection__Engine", profile.ProtectionEngine)
                .WithEnvironment("Agents__Protection__Observer", profile.ProtectionObserver)
                .WithEnvironment("Agents__Parties__Binding", profile.PartiesBinding)
                .WithEnvironment("Agents__Conversations__Binding", profile.ConversationsBinding);
            if (resource is "tenants" or "tenants-api")
            {
                added.WithEnvironment("Tenants__BootstrapGlobalAdminEnabled", "true")
                    .WithEnvironment("Tenants__BootstrapGlobalAdminUserId", profile.GlobalAdministrator);
            }
        }
        if (capabilities is not null)
            BindPrivateCapabilities(builder.Services, capabilities);
    }

    /// <summary>Captures the registrar, spool, and worker in the existing private composition. Raw credentials are not registered for general resolution.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="capabilities">Caller-owned private objects. This method does not mint them.</param>
    public static void BindPrivateCapabilities(IServiceCollection services, AgentsPrivateCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(capabilities);
        services.AddPrivateTrustedEnvelopeVerifier(capabilities.Registrar);
        services.AddPrivateSecurityDenialRecorder(capabilities.Spool);
        services.AddPrivateSecurityObservationDrainWorker(capabilities.Spool, capabilities.Interval, capabilities.MaximumCount);
    }

    /// <summary>Verifies the signed envelope through the existing replay verifier, then builds a target command only when the spool is ready.</summary>
    /// <param name="verifier">Existing verifier. Issuer and nonce registration happens inside it, after cryptographic validation, and before this method builds a command.</param>
    /// <param name="spool">Existing replicated spool. A spool that is not ready blocks the command with <c>SecurityAuditUnavailable</c>.</param>
    /// <param name="envelope">Signed delivery.</param>
    /// <param name="expected">Independent principal and scope.</param>
    /// <param name="construct">Target-command factory. It is not called when verification fails or the spool is unavailable.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    public static async Task<AgentsAdmissionResult> AdmitAsync(
        TrustedEnvelopeReplayVerifier verifier,
        ReplicatedSecurityObservationSpool spool,
        TrustedEnvelope envelope,
        TrustedEnvelopeIdentity expected,
        Func<TrustedEnvelope, string> construct,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(spool);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(construct);
        if (!await spool.IsReadyAsync(cancellationToken).ConfigureAwait(false))
            return AgentsAdmissionResult.Blocked("SecurityAuditUnavailable");
        TrustedEnvelopeResult verified = await verifier.VerifyAsync(envelope, expected, cancellationToken).ConfigureAwait(false);
        if (verified.Status != CustodyStatus.Succeeded || verified.Envelope is null
            || !verified.Envelope.Identity.Principal.IsValid(expected.TargetTenantId))
            return AgentsAdmissionResult.Blocked(verified.Status.ToString());
        if (!await spool.IsReadyAsync(cancellationToken).ConfigureAwait(false))
            return AgentsAdmissionResult.Blocked("SecurityAuditUnavailable");
        return new AgentsAdmissionResult(true, construct(verified.Envelope));
    }

    private static string RequireProject(IDistributedApplicationBuilder builder, AgentsDeploymentProfileModel profile, string service, string relative)
    {
        if (!profile.Services.TryGetValue(service, out string? root) || string.IsNullOrWhiteSpace(root))
            throw new FileNotFoundException("Agents Platform composition requires the sibling source checkout.", service);
        string project = Path.GetFullPath(Path.Combine(root, relative), builder.AppHostDirectory);
        if (!File.Exists(project))
            throw new FileNotFoundException("Agents Platform composition requires the sibling source checkout.", project);
        return project;
    }
}

/// <summary>Caller-owned private replay, spool, and worker handles. Composition stores them as constructor captures and does not mint replacements.</summary>
/// <param name="Registrar">Existing replay registrar.</param>
/// <param name="Spool">Existing replicated spool.</param>
/// <param name="Interval">Existing worker interval.</param>
/// <param name="MaximumCount">Existing bounded drain batch size.</param>
public sealed record AgentsPrivateCapabilities(
    ITrustedEnvelopeReplayRegistrar Registrar,
    ReplicatedSecurityObservationSpool Spool,
    TimeSpan Interval,
    int MaximumCount);

/// <summary>Admission outcome. <see cref="Command"/> is present only after verification and a ready spool.</summary>
/// <param name="CommandConstructed">Whether the target command factory ran.</param>
/// <param name="Command">Constructed command, or the blocked outcome name.</param>
public sealed record AgentsAdmissionResult(bool CommandConstructed, string? Command)
{
    /// <summary>Builds a blocked admission that did not construct a command.</summary>
    /// <param name="outcome">Safe outcome name.</param>
    public static AgentsAdmissionResult Blocked(string outcome) => new(false, outcome);
}
