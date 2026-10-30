namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Platform-owned Agents composition. A missing accepted profile keeps the existing refusal and adds no resource.</summary>
public static class AgentsHostComposition
{
    /// <summary>Configuration key for the reference-only deployment profile path.</summary>
    public const string ProfilePathKey = "Platform:Agents:DeploymentProfile";

    /// <summary>Begins deterministic ownership before composition. Use its Build method and dispose the returned application.</summary>
    public static AcceptedLaunchLifetime OwnLaunch(IDistributedApplicationBuilder builder)=>new(builder??throw new ArgumentNullException(nameof(builder)));

    private static readonly (string Service, string Relative, string Resource)[] Projects =
    [
        ("eventStore", "src/Hexalith.EventStore/Hexalith.EventStore.csproj", "eventstore"),
        ("tenants", "src/Hexalith.Tenants/Hexalith.Tenants.csproj", "tenants"),
        ("tenants", "src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj", "tenants-api"),
        ("parties", "src/Hexalith.Parties/Hexalith.Parties.csproj", "parties"),
        ("conversations", "src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj", "conversations"),
        ("agents", "src/Hexalith.Agents.Server/Hexalith.Agents.Server.csproj", "agents"),
        ("agents", "src/Hexalith.Agents.UI/Hexalith.Agents.UI.csproj", "agents-ui"),
    ];

    /// <summary>Composes sibling services only when the configured profile validates. Returns false when the profile is absent or rejected.</summary>
    /// <param name="builder">The platform application builder.</param>
    public static bool TryCompose(IDistributedApplicationBuilder builder)
        =>TryCompose(builder,CancellationToken.None,TimeSpan.FromSeconds(30));
    internal static bool TryCompose(IDistributedApplicationBuilder builder,CancellationToken cancellationToken,TimeSpan startupTimeout,
        Func<string,int,CancellationToken,Task<byte[]>>? acquire=null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(startupTimeout);var utf8=new UTF8Encoding(false,true);
        byte[] Read(string path)=>(acquire is not null?acquire(path,1024*1024,budget.Token):builder.Environment.IsDevelopment()?BoundedDevelopmentFile.ReadAsync(path,1024*1024,budget.Token):BoundedRegularFile.ReadAsync(path,1024*1024,budget.Token)).WaitAsync(budget.Token).GetAwaiter().GetResult();
        string? configured = builder.Configuration[ProfilePathKey];
        if (string.IsNullOrWhiteSpace(configured))
            return false;
        AgentsDeploymentProfileModel profile;
        try
        {
            string path = Path.GetFullPath(configured, builder.AppHostDirectory);
            string json;
            try { json=utf8.GetString(Read(path)); }
            catch(FileNotFoundException) when(builder.Environment.IsDevelopment()) { return false; }
            if (builder.Environment.IsDevelopment()) profile = AgentsDeploymentProfile.Validate(json);
            else
            {
                string? registerPath = builder.Configuration["Platform:Agents:AcceptedRegister"];
                string? packetPath = builder.Configuration["Platform:Agents:AcceptancePacket"];
                if (string.IsNullOrWhiteSpace(registerPath) || string.IsNullOrWhiteSpace(packetPath)) return false;
                profile = AgentsDeploymentProfile.ValidateAcceptedContext(json,utf8.GetString(Read(Path.GetFullPath(registerPath,builder.AppHostDirectory))),utf8.GetString(Read(Path.GetFullPath(packetPath,builder.AppHostDirectory))));
            }
            ComposeCore(builder,profile,capabilities:null,budget.Token,Read);
            return true;
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            if(builder.Environment.IsDevelopment() && exception is FileNotFoundException) throw;
            return false;
        }
    }

    /// <summary>Adds the five sibling services and binds tenant, identity, and credential references. Private objects are registered only when the caller already holds them.</summary>
    /// <param name="builder">The platform application builder.</param>
    /// <param name="profile">A validated reference-only profile.</param>
    /// <param name="capabilities">Existing private registrar and spool. Null installs neither credentials nor spool storage.</param>
    public static void Compose(IDistributedApplicationBuilder builder, AgentsDeploymentProfileModel profile, AgentsPrivateCapabilities? capabilities)
    {
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ComposeCore(builder,profile,capabilities,budget.Token,path=>(builder.Environment.IsDevelopment()?BoundedDevelopmentFile.ReadAsync(path,1024*1024,budget.Token):BoundedRegularFile.ReadAsync(path,1024*1024,budget.Token)).GetAwaiter().GetResult());
    }
    private static void ComposeCore(IDistributedApplicationBuilder builder,AgentsDeploymentProfileModel profile,AgentsPrivateCapabilities? capabilities,CancellationToken token,Func<string,byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);
        profile=profile.CompositionSnapshot(!builder.Environment.IsDevelopment());
        if (string.IsNullOrWhiteSpace(profile.SpoolBackend) || string.IsNullOrWhiteSpace(profile.ReplayReference)
            || string.IsNullOrWhiteSpace(profile.RecorderReference) || string.IsNullOrWhiteSpace(profile.WorkerReference))
            throw new InvalidDataException("Agents deployment profile requires every reference slot.");
        if (!builder.Environment.IsDevelopment() && !profile.AcceptedContextValidated)
            throw new InvalidDataException("Production composition requires the coordinated accepted register/packet/profile context.");
        bool production=!builder.Environment.IsDevelopment();
        var lifetime=builder.Services.LastOrDefault(value=>value.ServiceType==typeof(AcceptedLaunchLifetime))?.ImplementationInstance as AcceptedLaunchLifetime;
        List<(string Resource,string Project,AcceptedLaunchArtifactLease? Lease)> projects=[];
        IReadOnlyDictionary<string,AcceptedResourceListener>? listeners=null;
        try
        {
            using var inventory=profile.AcceptedLaunchInventoryJson is null?null:System.Text.Json.JsonDocument.Parse(profile.AcceptedLaunchInventoryJson);
            if(production && inventory is null) throw new InvalidDataException("External.AcceptedCompleteImmutableLaunchGraphV1");
            if(production && (inventory!.RootElement.GetProperty("resources").GetArrayLength()!=Projects.Length
                || inventory.RootElement.GetProperty("resources").EnumerateArray().Select(value=>value.GetProperty("resource").GetString()).Distinct(StringComparer.Ordinal).Count()!=Projects.Length))
                throw new InvalidDataException("Exact complete selected launch graph required.");
            if(production) listeners=AcceptedListenerBindings.Validate(inventory!.RootElement,profile);
            if(listeners is not null && builder.Resources.SelectMany(resource=>resource.Annotations.OfType<EndpointAnnotation>())
                .Any(endpoint=>listeners.Values.Any(listener=>endpoint.Port==listener.Port || endpoint.TargetPort==listener.Port)))
                throw new InvalidDataException(AcceptedListenerBindings.Requirement+": existing endpoint owns an accepted listener port.");
            foreach((string service,string relative,string resource) in Projects)
            {
                if(production)
                {
                    if(lifetime is null) throw new InvalidDataException("Accepted launch composition requires an explicit OwnLaunch lifetime and its Build boundary.");
                    if(!profile.Services.TryGetValue(service,out var sourceRoot)) throw new InvalidDataException("Accepted sibling release root required.");
                    var lease=AcceptedLaunchArtifactLease.Acquire(inventory!.RootElement,resource,Path.GetFullPath(sourceRoot,builder.AppHostDirectory),token);
                    lifetime.Add(lease);
                    projects.Add((resource,lease.EntryPoint,lease));
                }
                else projects.Add((resource,RequireProject(builder,profile,service,relative,read),null));
            }
            if(Projects.Any(project=>builder.Resources.Any(existing=>string.Equals(existing.Name,project.Resource,StringComparison.OrdinalIgnoreCase)
                || !production && string.Equals(existing.Name,project.Resource+"-rebuilder",StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("An intended sibling resource name is already registered.");
            token.ThrowIfCancellationRequested();
        }
        catch { foreach(var project in projects) project.Lease?.Dispose();throw; }
        void Configure<T>(IResourceBuilder<T> resourceBuilder,string resource) where T:IResourceWithEnvironment
        {
            var added=resourceBuilder
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

            if(resource is "tenants" or "tenants-api") added.WithEnvironment("Tenants__BootstrapGlobalAdminEnabled","true").WithEnvironment("Tenants__BootstrapGlobalAdminUserId",profile.GlobalAdministrator);
        }
        try
        {
        foreach(var project in projects)
        {
            if(project.Lease is { } lease)
            {
                var listener=listeners![project.Resource];
                Configure(builder.AddExecutable(project.Resource,lease.EntryPoint,lease.Root,"--urls",listener.Url)
                    .WithEnvironment("ASPNETCORE_URLS",listener.Url)
                    .WithHttpEndpoint(port:listener.Port,targetPort:listener.Port,name:listener.EndpointName,isProxied:false)
                    .WithEndpoint(listener.EndpointName,endpoint=>endpoint.TargetHost=listener.Address).WithAnnotation(lease),project.Resource);
            }
            else Configure(builder.AddProject(project.Resource,project.Project,launchProfileName:null),project.Resource);
        }
        if (capabilities is not null)
            BindPrivateCapabilities(builder.Services, capabilities);
        }
        catch { foreach(var project in projects) project.Lease?.Dispose();throw; }
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
        CancellationToken cancellationToken = default,AgentsIngressDenialContext? denialContext=null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(spool);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(construct);
        bool ready=true;
        TrustedEnvelopeResult verified = await verifier.VerifyForAdmissionAsync(envelope, expected,async token=>ready=await spool.IsReadyAsync(token).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        if(!ready) return AgentsAdmissionResult.Blocked("SecurityAuditUnavailable");
        if (verified.Status != CustodyStatus.Succeeded || verified.Envelope is null
            || !verified.Envelope.Identity.Principal.IsValid(expected.TargetTenantId))
        {
            if(denialContext is null) return AgentsAdmissionResult.Blocked("SecurityAuditUnavailable");
            string reason=verified.DenialCause==TrustedEnvelopeDenialCause.ReplayConflictV1?"replay-conflict":verified.Status switch { CustodyStatus.InvalidTag=>"invalid-tag",CustodyStatus.ScopeMismatch=>"scope-mismatch",CustodyStatus.StaleProfile=>"stale-authority",_=>"unavailable-owner" };
            var observation=await denialContext.Recorder.ObserveAsync(denialContext.RetainedServerReceiptId,reason,denialContext.UntrustedFields,
                denialContext.OriginalDigestKeyVersion,envelope,expected,cancellationToken).ConfigureAwait(false);
            return observation is null?AgentsAdmissionResult.Blocked("SecurityAuditUnavailable"):
                new AgentsAdmissionResult(false,verified.Status.ToString()) { DenialOriginal=observation };
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new AgentsAdmissionResult(true, construct(verified.Envelope));
    }

    private static string RequireProject(IDistributedApplicationBuilder builder, AgentsDeploymentProfileModel profile, string service,string relative,Func<string,byte[]> read)
    {
        if (!profile.Services.TryGetValue(service, out string? root) || string.IsNullOrWhiteSpace(root))
            throw new FileNotFoundException("Agents Platform composition requires the sibling source checkout.", service);
        string project = Path.GetFullPath(Path.Combine(root, relative), builder.AppHostDirectory);
        try { _=read(project); }
        catch(IOException exception) { throw new FileNotFoundException("Agents Platform composition requires the sibling source checkout.",project,exception); }
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
    /// <summary>Exact durable content-free denial accepted before this classified result was constructed; null never establishes a processed denial.</summary>
    public SecurityObservationRecord? DenialOriginal { get; init; }
    /// <summary>Builds a blocked admission that did not construct a command.</summary>
    /// <param name="outcome">Safe outcome name.</param>
    public static AgentsAdmissionResult Blocked(string outcome) => new(false, outcome);
}
