namespace Hexalith.Platform.Composition;

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>The selected, run-owned local experiment. This identity never represents full qualification.</summary>
public sealed class PlatformRetentionProfile : ICompositionTestObservationProvider
{
    /// <summary>The immutable backend selected by the existing approved Memories asset catalog.</summary>
    public const string PostgresImage = "docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722";
    /// <summary>The immutable runtime selected by that same catalog.</summary>
    public const string DaprImage = "ghcr.io/dapr/daprd:1.18.1@sha256:b7f7d296f01f0b4b82bf3c5f087ecf26165ce08caf3e87f94b8c72b9e11873f8";
    private readonly CompositionRunPlan _plan;
    private string _directory = string.Empty;
    private string _password = string.Empty;
    private string _observerPassword = string.Empty;
    private int _postgresPort;
    private readonly Dictionary<string, (IResourceWithEndpoints Resource, int DaprPort, IReadOnlyDictionary<string, string> Environment)> _servers = new(StringComparer.Ordinal);

    private PlatformRetentionProfile(CompositionRunPlan plan)
    {
        _plan = plan;
        CanonicalJson = JsonSerializer.Serialize(new { purpose = "local-functional-retention", runId = plan.RunId, sourceMappingHash = plan.SourceMappingHash, componentType = "state.postgresql/v2", runtimeImage = DaprImage, postgresqlImage = PostgresImage, sslMode = "verify-full", retentionSeconds = 3600, qualificationCredit = false });
        ProfileHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson)));
    }

    /// <summary>Gets the local canonical identity, including run and source mapping.</summary>
    public string CanonicalJson { get; }
    /// <summary>Gets the canonical local profile digest.</summary>
    public string ProfileHash { get; }
    /// <summary>Gets the explicitly selected runtime version.</summary>
    public static string RuntimeVersion => "1.18.1";
    /// <summary>Gets or sets the allocated lifecycle sidecar port, never a caller-supplied endpoint.</summary>
    internal int DaprHttpPort { get; set; }

    /// <summary>Selects only the declared source-mode experiment from a Platform-owned run.</summary>
    public static PlatformRetentionProfile? Select(CompositionRunPlan plan, string platformRoot)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.SelectedTest is not { ModuleId: "memories", Name: "functional-retention" } selected) return null;
        if (selected.Mode != "source" || selected.DaprRuntimeVersion != "1.18.1" || plan.SourceMapping is not { Mode: WorkspaceMode.Source } mapping
            || Path.GetFullPath(mapping.Root) != Path.GetFullPath(platformRoot)
            || mapping.Entries.SingleOrDefault(entry => entry.Identity == "Hexalith.Memories") is not { Origin: "source", Path: { } candidate }
            || Path.GetFullPath(candidate) != Path.Combine(Path.GetFullPath(platformRoot), "references", "Hexalith.Memories"))
            throw new InvalidDataException("The retention experiment requires the directly declared Memories source and Dapr 1.18.1 in the selected Platform workspace.");
        return new(plan);
    }

    /// <summary>Creates inactive, private run-local TLS material only when the runner composes this profile.</summary>
    internal void Prepare(string rendered, int postgresqlPort)
    {
        _directory = Path.Combine(rendered, "retention");
        CompositionWorkspace.CreatePrivateDirectory(_directory);
        _postgresPort = postgresqlPort;
        _password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        _observerPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        using RSA rootKey = RSA.Create(3072);
        CertificateRequest root = new("CN=Hexalith run " + _plan.RunId, rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        root.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using X509Certificate2 ca = root.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), PreStartValidityCeiling.AddMinutes(15));
        using RSA key = RSA.Create(3072);
        CertificateRequest server = new("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        server.CertificateExtensions.Add(names.Build());
        server.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using X509Certificate2 certificate = server.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-1), PreStartValidityCeiling, RandomNumberGenerator.GetBytes(16));
        File.WriteAllText(Path.Combine(_directory, "ca.pem"), ca.ExportCertificatePem());
        File.WriteAllText(Path.Combine(_directory, "server.crt"), certificate.ExportCertificatePem());
        File.WriteAllText(Path.Combine(_directory, "server.key"), key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(_directory, "password"), Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
        File.WriteAllText(Path.Combine(_directory, "pgpass"), "127.0.0.1:5432:telemetry:telemetry_observer:" + _observerPassword + "\n");
        File.WriteAllText(Path.Combine(_directory, "init.sql"), $"CREATE ROLE telemetry LOGIN PASSWORD '{_password}'; CREATE ROLE telemetry_observer LOGIN PASSWORD '{_observerPassword}'; ALTER ROLE telemetry_observer SET default_transaction_read_only=on; CREATE SCHEMA access_telemetry AUTHORIZATION telemetry; GRANT USAGE ON SCHEMA access_telemetry TO telemetry_observer; ALTER DEFAULT PRIVILEGES FOR ROLE telemetry IN SCHEMA access_telemetry GRANT SELECT ON TABLES TO telemetry_observer;\n");
        File.WriteAllText(Path.Combine(_directory, "pg_hba.conf"), "local all postgres trust\nlocal all all reject\nhostssl all all 127.0.0.1/32 scram-sha-256\nhostssl all all 0.0.0.0/0 scram-sha-256\nhost all all 0.0.0.0/0 reject\nhost all all ::0/0 reject\n");
        File.WriteAllText(Path.Combine(_directory, "gate.json"), "{\"schemaVersion\":1,\"state\":\"disabled\",\"profileSha256\":\"\",\"expiresUtcMs\":0}");
        foreach (string file in Directory.EnumerateFiles(_directory)) if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Adds the existing catalog backend as a labelled session resource, with real TLS readiness.</summary>
    internal IResourceBuilder<ContainerResource> AddResource(IDistributedApplicationBuilder builder)
    {
        if (_directory.Length == 0) throw new InvalidOperationException("The selected retention profile has not been prepared.");
        string? user = DaprSelfHostedMtls.ResolveContainerUser();
        string ownership = user is null ? string.Empty : ",uid=" + user.Split(':')[0] + ",gid=" + user.Split(':')[1];
        string check = "retention-postgresql-tls";
        builder.Services.AddHealthChecks().AddCheck(check, new PlatformPostgresqlHealthCheck(_postgresPort, Path.Combine(_directory, "ca.pem")));
        IResourceBuilder<ContainerResource> resource = builder.AddContainer("memories-retention-postgresql", "docker.io/library/postgres", "18.6-trixie")
            .WithImageSHA256(PostgresImage.Split("@sha256:")[1]).WithContainerName("hexalith-retention-" + _plan.RunId)
            .WithEnvironment("POSTGRES_USER", "postgres").WithEnvironment("POSTGRES_DB", "telemetry")
            .WithEnvironment("POSTGRES_PASSWORD_FILE", "/run/retention/password")
            .WithEnvironment("PGDATA", "/var/lib/postgresql/data")
            .WithBindMount(_directory, "/run/retention", isReadOnly: true)
            .WithBindMount(Path.Combine(_directory, "init.sql"), "/docker-entrypoint-initdb.d/retention.sql", isReadOnly: true)
            .WithEndpoint(port: _postgresPort, targetPort: 5432, name: "tcp", scheme: "tcp", isProxied: false)
            .WithArgs("postgres", "-c", "ssl=on", "-c", "ssl_cert_file=/run/retention/server.crt", "-c", "ssl_key_file=/run/retention/server.key", "-c", "hba_file=/run/retention/pg_hba.conf", "-c", "password_encryption=scram-sha-256", "-c", "max_connections=100")
            // These ceilings retain the existing PostgreSQL manifest's 8 CPU / 16 GiB and 400 GiB boundary.
            // They are resource limits, never a claim that capacity is reserved or qualified.
            .WithContainerRuntimeArgs("--label", $"{CompositionEnvironment.ContainerRunLabel}={_plan.RunId}", "--cpus", "8", "--memory", "17179869184", "--memory-swap", "17179869184", "--tmpfs", "/var/lib/postgresql:rw,size=429496729600,mode=0700" + ownership)
            .WithHealthCheck(check).WithLifetime(ContainerLifetime.Session);
        if (user is not null) resource.WithContainerRuntimeArgs("--user", user);
        return resource;
    }

    /// <summary>Renders the scoped Dapr v2 PostgreSQL binding with private connection material and verify-full.</summary>
    internal string RenderComponent(IEnumerable<string> consumers)
        => JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "access-telemetry-store" }, spec = new { type = "state.postgresql", version = "v2", metadata = new[] { new { name = "connectionString", value = $"host=127.0.0.1 port={_postgresPort} user=telemetry password={_password} dbname=telemetry sslmode=verify-full sslrootcert={Path.Combine(_directory, "ca.pem")} " }, new { name = "actorStateStore", value = "true" }, new { name = "maxConns", value = "40" }, new { name = "tablePrefix", value = "access_telemetry.lifecycle_" }, new { name = "metadataTableName", value = "access_telemetry.dapr_metadata" }, new { name = "timeout", value = "3s" }, new { name = "cleanupInterval", value = "5m" } } }, scopes = consumers.Order(StringComparer.Ordinal).ToArray() });

    /// <summary>Applies the same one-hour settings to all three selected Memories services.</summary>
    internal void Configure(Dictionary<string, string> environment, string server)
    {
        environment["AccessTelemetryLifecycle__StateStoreComponentPath"] = Path.Combine(_plan.Workspace, "platform-components", "telemetry-state.yaml");
        environment["retentionSeconds"] = "3600";
        environment["AccessTelemetryLifecycle__Retention"] = "01:00:00";
        environment["AccessTelemetryLifecycle__RetentionSource"] = "DevelopmentDefault";
        environment["AccessTelemetryLifecycle__ComponentProfileHash"] = ProfileHash;
        environment["AccessTelemetryLifecycle__LocalFunctionalProfileJson"] = CanonicalJson;
        environment["AccessTelemetryLifecycle__LocalFunctionalSourceMappingHash"] = _plan.SourceMappingHash!;
        environment["HEXALITH_G4_RUN_ID"] = _plan.RunId;
        environment["HEXALITH_SOURCE_MAPPING_HASH"] = _plan.SourceMappingHash!;
        environment["AccessTelemetryLifecycle__DeploymentId"] = _plan.RunId;
        environment["AccessTelemetryLifecycle__LocalFunctionalRunId"] = _plan.RunId;
        environment["AccessTelemetryLifecycle__Enabled"] = "true";
        environment["AccessTelemetryLifecycle__CapabilityEvidence__ValidUntilUtc"] = PreStartValidityCeiling.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        environment["AccessTelemetryLifecycle__CapabilityEvidence__ExactVersionPinned"] = "true";
        environment["AccessTelemetryLifecycle__CapacityEvidenceId"] = "not-qualified-local-functional";
        environment["AccessTelemetryLifecycle__PhysicalReclamationEvidenceId"] = "not-qualified-local-functional";
        environment["Clock__AllowDevelopmentSources"] = "true";
        environment["AccessTelemetryQualification__GatePath"] = Path.Combine(_plan.Workspace, "platform-components", "retention", "gate.json");
        environment["AccessTelemetryQualification__LocalProfileHash"] = ProfileHash;
        if (server == "server") environment["HEXALITH_QUALIFICATION_WRITER"] = "writer-1";
    }
    /// <summary>Records actual model allocation and the existing scoped app/sidecar credentials.</summary>
    internal void Track(IResourceWithEndpoints resource, int port, IReadOnlyDictionary<string, string> environment)
        => _servers.Add(resource.Name, (resource, port, environment));

    /// <inheritdoc/>
    public async Task<CompositionTestObservation> ObserveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Dictionary<string, Uri> endpoints = new(StringComparer.Ordinal);
        Dictionary<string, object> access = new(StringComparer.Ordinal);
        Dictionary<string, string> loaded = new(StringComparer.Ordinal);
        Dictionary<string, int> retention = new(StringComparer.Ordinal);
        ResourceNotificationService notifications = services.GetRequiredService<ResourceNotificationService>();
        HashSet<string> loadedRuntimes = new(StringComparer.Ordinal);
        string candidate = _plan.SourceMapping!.Entries.Single(entry => entry.Identity == "Hexalith.Memories").Path!;
        foreach (var (name, value) in _servers)
        {
            EndpointReference? http = value.Resource.GetEndpoints().SingleOrDefault(endpoint => endpoint.EndpointName == "http");
            if (http is { IsAllocated: true }) endpoints[name] = new Uri(http.Url);
            endpoints[name + "/dapr"] = new Uri("http://127.0.0.1:" + value.DaprPort);
            access[name] = value.Environment.Where(pair => pair.Key is "APP_API_TOKEN" or "DAPR_API_TOKEN").ToDictionary();
            if (notifications.TryGetCurrentState(name, out ResourceEvent? current))
            {
                string? pid = current.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value?.ToString();
                if (int.TryParse(pid, out int process) && process > 0 && OperatingSystem.IsLinux())
                {
                    string entry = name == "memories" ? "Hexalith.Memories.Server.dll" : name == "memories-access-telemetry-clock" ? "Hexalith.Memories.AccessTelemetry.Clock.dll" : "Hexalith.Memories.AccessTelemetry.dll";
                    int[] applications = ProcessTree(process).Where(pid => File.ReadLines("/proc/" + pid + "/maps").Any(line => line.EndsWith("/" + entry, StringComparison.Ordinal))).ToArray();
                    if (applications.Length != 1) continue;
                    process = applications[0];
                    string[] actualEnvironment = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes("/proc/" + process + "/environ")).Split('\0');
                    if (actualEnvironment.Contains("retentionSeconds=3600") && actualEnvironment.Contains("AccessTelemetryLifecycle__Retention=01:00:00") && actualEnvironment.Contains("AccessTelemetryLifecycle__ComponentProfileHash=" + ProfileHash)) retention[name] = 3600;
                    foreach (string line in File.ReadLines("/proc/" + process + "/maps"))
                    {
                        string[] fields = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length != 6) continue;
                        string path = fields[5];
                        if (path.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFileName(path).StartsWith("Hexalith.Memories", StringComparison.Ordinal) && path.EndsWith(" (deleted)", StringComparison.Ordinal)) throw new InvalidDataException("A candidate mapping was deleted.");
                        if (!path.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Path.GetFileName(path).StartsWith("Hexalith.Memories", StringComparison.Ordinal) || !path.EndsWith(".dll", StringComparison.Ordinal)) continue;
                        string identity = name + "/" + Path.GetRelativePath(candidate, path);
                        if (loaded.ContainsKey(identity)) continue;
                        string digest = PlatformMappedArtifact.Hash(process, path, line);
                        loaded[identity] = digest;
                    }
                }
            }
            if (OperatingSystem.IsLinux() && notifications.TryGetCurrentState(name + "-dapr-cli", out ResourceEvent? sidecar))
            {
                string? ownerPid = sidecar.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value?.ToString();
                if (int.TryParse(ownerPid, out int cliPid) && cliPid > 0)
                {
                    foreach (int child in ProcessTree(cliPid))
                    {
                        string executable = "/proc/" + child + "/exe";
                        string? target = new FileInfo(executable).LinkTarget;
                        if (target != Path.Combine(_plan.DaprHome, ".dapr", "bin", "daprd")) continue;
                        using FileStream runningRuntime = File.OpenRead(executable);
                        loadedRuntimes.Add(name + "/" + Convert.ToHexStringLower(SHA256.HashData(runningRuntime)));
                    }
                }
            }
        }
        string accessPath = Path.Combine(_directory, "access.json");
        File.WriteAllText(accessPath, JsonSerializer.Serialize(access));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(accessPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        string? backend = null;
        string? containerId = null;
        CompositionProcessResult inspection = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", ["inspect", "hexalith-retention-" + _plan.RunId], null, null), TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        if (inspection.ExitCode == 0 && !inspection.TimedOut)
        {
            using JsonDocument document = JsonDocument.Parse(inspection.Output);
            JsonElement container = document.RootElement[0];
            if (container.GetProperty("Config").GetProperty("Labels").GetProperty(CompositionEnvironment.ContainerRunLabel).GetString() == _plan.RunId
                && IsRequestedPostgres(container.GetProperty("Config").GetProperty("Image").GetString()))
            {
                containerId = container.GetProperty("Id").GetString();
                string imageId = container.GetProperty("Image").GetString()!;
                if (!System.Text.RegularExpressions.Regex.IsMatch(imageId, "^sha256:[a-f0-9]{64}$")) throw new InvalidDataException("The loaded PostgreSQL image identity is unavailable.");
                CompositionProcessResult imageResult = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", ["image", "inspect", imageId], null, null), TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                if (imageResult.ExitCode != 0 || imageResult.TimedOut) throw new InvalidDataException("The loaded PostgreSQL image cannot be inspected locally.");
                using JsonDocument actualImage = JsonDocument.Parse(imageResult.Output);
                string actualImageId = BoundBackendImage(container, actualImage.RootElement[0]) ?? throw new InvalidDataException("The loaded PostgreSQL image does not bind the approved digest.");
                CompositionProcessResult sql = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", ["exec", "--env", "PGSSLMODE=verify-full", "--env", "PGSSLROOTCERT=/run/retention/ca.pem", "--env", "PGPASSFILE=/run/retention/pgpass", containerId!, "psql", "-h", "127.0.0.1", "-U", "telemetry_observer", "-d", "telemetry", "-w", "-XAt", "-c", "SELECT current_setting('server_version_num'),ssl,version,current_user,current_setting('default_transaction_read_only') FROM pg_stat_ssl WHERE pid=pg_backend_pid()"], null, null), TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                if (sql.ExitCode == 0 && !sql.TimedOut && sql.Output.Trim() is "180006|t|TLSv1.3|telemetry_observer|on" or "180006|t|TLSv1.2|telemetry_observer|on") backend = PostgresImage + "/" + sql.Output.Trim() + "/" + actualImageId;
            }
        }
        string runtime = Path.Combine(_plan.DaprHome, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
        string[] runtimeHashes = loadedRuntimes.Select(value => value.Split('/')[1]).Distinct(StringComparer.Ordinal).ToArray();
        string? runtimeHash = loadedRuntimes.Count == _servers.Count && runtimeHashes.Length == 1 ? runtimeHashes[0] : null;
        bool runtimeVerified = runtimeHash is not null && File.Exists(runtime) && runtimeHash == Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(runtime))) && CompositionRuntimeArtifactVerifier.Verify(Path.Combine(_plan.DaprHome, ".dapr", "runtime-image.oci.tar"), DaprImage.Split("@sha256:")[1], "edbe3fc30d7efc90869411666fd03b70bb89eafed382bb37ff9a6de2fcab914b", runtime);
        return new CompositionTestObservation
        {
            RunId = _plan.RunId, ModuleId = "memories", ProfileHash = ProfileHash, ProfileJson = CanonicalJson,
            Endpoints = endpoints, LoadedArtifactHashes = loaded, ServiceRetentionSeconds = retention,
            ObservedCandidateHash = _servers.Keys.All(name => loaded.Keys.Any(key => key.StartsWith(name + "/", StringComparison.Ordinal) && Path.GetFileName(key) == (name == "memories" ? "Hexalith.Memories.Server.dll" : name == "memories-access-telemetry-clock" ? "Hexalith.Memories.AccessTelemetry.Clock.dll" : "Hexalith.Memories.AccessTelemetry.dll"))) ? Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(loaded.OrderBy(pair => pair.Key, StringComparer.Ordinal)))) : null,
            ObservedRuntimeHash = runtimeHash, RuntimeArtifactVerified = runtimeVerified,
            ObservedBackendIdentity = backend, PostgresqlContainerId = containerId, AccessPath = accessPath, GatePath = Path.Combine(_directory, "gate.json"), ObservedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Gets a pre-start certificate ceiling; actual access still obeys the activated gate deadline.</summary>
    internal DateTimeOffset PreStartValidityCeiling => (_plan.StartupDeadlineUtc ?? _plan.SelectedTest!.RequestedAtUtc.AddSeconds(600))
        .AddSeconds(_plan.SelectedTests.Count > 0 ? _plan.SelectedTests.Sum(entry => (long)entry.ExecutionTimeoutSeconds + entry.StopTimeoutSeconds) : _plan.SelectedTest!.ExecutionTimeoutSeconds).AddSeconds(1);

    private static bool IsRequestedPostgres(string? reference) => reference == PostgresImage || reference == PostgresImage.Replace(":18.6-trixie@", "@", StringComparison.Ordinal);

    internal static string? BoundBackendImage(JsonElement container, JsonElement image)
    {
        string? id = container.GetProperty("Image").GetString();
        string repositoryDigest = "docker.io/library/postgres@" + PostgresImage.Split('@')[1];
        if (id is null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^sha256:[a-f0-9]{64}$") || image.GetProperty("Id").GetString() != id || !IsRequestedPostgres(container.GetProperty("Config").GetProperty("Image").GetString())) return null;
        return image.GetProperty("RepoDigests").EnumerateArray().Any(value => value.GetString() is { } digest && (digest == repositoryDigest || digest == repositoryDigest["docker.io/".Length..] || digest == repositoryDigest["docker.io/library/".Length..])) ? id : null;
    }

    // Aspire may report the dotnet-run or Dapr CLI owner. Inspect only its bounded
    // descendants, then require the corresponding mapped entry or daprd inode.
    private static IEnumerable<int> ProcessTree(int owner)
    {
        Queue<int> pending = new();
        HashSet<int> seen = [];
        pending.Enqueue(owner);
        while (pending.Count > 0 && seen.Count < 64)
        {
            int process = pending.Dequeue();
            if (!seen.Add(process)) continue;
            yield return process;
            string children = "/proc/" + process + "/task/" + process + "/children";
            if (!File.Exists(children)) continue;
            foreach (string child in File.ReadAllText(children).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(child, out int pid) && pid > 0) pending.Enqueue(pid);
        }
    }

}
