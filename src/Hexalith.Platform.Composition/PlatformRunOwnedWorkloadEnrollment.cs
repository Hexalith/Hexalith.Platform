using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Composition;

/// <summary>Adopts one explicit disposable launcher lease before the protected consumer verifies enrollment.</summary>
public sealed class PlatformRunOwnedWorkloadEnrollment : IAsyncDisposable
{
    /// <summary>The private receipt environment variable set only by the disposable launcher.</summary>
    public const string ReceiptEnvironmentVariable = "HEXALITH_RUN_OWNED_WORKLOAD_RECEIPT";
    /// <summary>The one-use capability inherited from the disposable launcher.</summary>
    public const string CapabilityEnvironmentVariable = "HEXALITH_RUN_OWNED_WORKLOAD_CAPABILITY";
    private readonly string _directory;
    private readonly string _runId;
    private readonly string _capability;
    private readonly long _deadline;

    private PlatformRunOwnedWorkloadEnrollment(string directory, string runId, string capability, long deadline)
    {
        _directory = directory;
        _runId = runId;
        _capability = capability;
        _deadline = deadline;
    }

    /// <summary>Adopts the final cohort, publishes private manifest references and propagates prelaunch TLS trust.</summary>
    /// <param name="builder">The final disposable development model before catalog capture.</param>
    /// <param name="selectedTenant">The actual selected fixture tenant.</param>
    /// <param name="cancellationToken">The existing startup deadline and caller cancellation.</param>
    /// <param name="hostTrustFile">An optional existing bundle containing the launch trust plus provider trust.</param>
    /// <returns>The owned lease, or null when external owner enrollment remains selected.</returns>
    public static async Task<PlatformRunOwnedWorkloadEnrollment?> AdoptAsync(IDistributedApplicationBuilder builder, string selectedTenant,
        CancellationToken cancellationToken = default, string? hostTrustFile = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string? receiptPath = Environment.GetEnvironmentVariable(ReceiptEnvironmentVariable);
        string? capability = Environment.GetEnvironmentVariable(CapabilityEnvironmentVariable);
        if (receiptPath is null && capability is null) return null;
        if (!OperatingSystem.IsLinux()) throw Refused("linux-launcher-required");
        if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment()) throw Refused("disposable-development-run-required");
        if (!string.IsNullOrEmpty(builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"])
            || !string.IsNullOrEmpty(builder.Configuration[PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"])) throw Refused("mixed-owner-contexts");
        PlatformRunOwnedWorkloadEnrollment? lease = null;
        try
        {
            if (receiptPath is null || capability is null || !Path.IsPathFullyQualified(receiptPath) || Path.GetFileName(receiptPath) != "receipt.json")
                throw Refused("explicit-launcher-receipt-required");
            string directory = Path.GetDirectoryName(receiptPath)!;
            Protected(directory, directory: true);
            using JsonDocument receipt = Parse(ReadProtected(receiptPath));
            JsonElement root = receipt.RootElement;
            if (root.GetProperty("schema").GetString() != "hexalith.run-owned-workload/v1"
                || root.GetProperty("owner").GetString() != "disposable-folders-acceptance"
                || root.GetProperty("directory").GetString() != directory || root.GetProperty("capability").GetString() != capability)
                throw Refused("foreign-launcher-receipt");
            int childPid = root.GetProperty("childPid").GetInt32(), ownerPid = root.GetProperty("ownerPid").GetInt32();
            string childIdentity = root.GetProperty("childIdentity").GetString()!;
            if (childPid != Environment.ProcessId || ProcessIdentity(childPid) != childIdentity
                || ProcessIdentity(ownerPid) != root.GetProperty("ownerIdentity").GetString()
                || ParentProcess(childPid) != ownerPid) throw Refused("owner-child-process-binding");
            long deadline = root.GetProperty("deadline").GetInt64();
            CheckDeadline(deadline);
            string runId = root.GetProperty("runId").GetString()!;
            if (runId.Length != 32 || !runId.All(char.IsAsciiHexDigit) || capability.Length != 64 || !capability.All(char.IsAsciiHexDigit)) throw Refused("owner-identity");
            string trustFile = Path.Combine(directory, "trust.pem");
            if (root.GetProperty("trustFile").GetString() != trustFile || Environment.GetEnvironmentVariable("SSL_CERT_FILE") != trustFile
                || Convert.ToHexString(SHA256.HashData(ReadProtected(trustFile, 4194304))) != root.GetProperty("trustSha256").GetString())
                throw Refused("missing-prelaunch-trust");
            if (hostTrustFile is not null && (!Path.IsPathFullyQualified(hostTrustFile)
                || !File.ReadAllText(hostTrustFile).Contains(File.ReadAllText(trustFile), StringComparison.Ordinal))) throw Refused("host-trust-must-merge-launch-trust");
            JsonElement inventory = PlatformWorkloadEnrollmentInventory.Create(builder, selectedTenant);
            string inventorySha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inventory.GetRawText())));
            byte[] request = JsonSerializer.SerializeToUtf8Bytes(new { runId, capability, childPid, childIdentity, deadline, inventory, inventorySha256 });
            try
            {
                await WriteNewAsync(Path.Combine(directory, "request.json"), request, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (File.Exists(Path.Combine(directory, "request.json"))) { throw Refused("owner-request-already-adopted"); }
            // Only the exclusive request publisher owns release; a refused competing adopter cannot tear it down.
            lease = new(directory, runId, capability, deadline);
            string resultPath = Path.Combine(directory, "result.json");
            while (!File.Exists(resultPath))
            {
                CheckDeadline(deadline);
                if (ProcessIdentity(ownerPid) != root.GetProperty("ownerIdentity").GetString()) throw Refused("launcher-exited");
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            using JsonDocument result = Parse(ReadProtected(resultPath));
            JsonElement output = result.RootElement;
            string manifestPath = Path.Combine(directory, "manifest.json");
            if (output.GetProperty("runId").GetString() != runId || output.GetProperty("deadline").GetInt64() != deadline
                || output.GetProperty("inventorySha256").GetString() != inventorySha256 || output.GetProperty("manifestPath").GetString() != manifestPath)
                throw Refused("foreign-result-or-changed-cohort");
            byte[] manifestBytes = ReadProtected(manifestPath);
            string manifestSha256 = Convert.ToHexString(SHA256.HashData(manifestBytes));
            if (manifestSha256 != output.GetProperty("manifestSha256").GetString()) throw Refused("manifest-binding");
            using JsonDocument manifest = Parse(manifestBytes);
            JsonElement value = manifest.RootElement;
            if (value.GetProperty("expiresAt").GetInt64() != deadline || value.GetProperty("notBefore").GetInt64() >= deadline
                || value.GetProperty("issuer").GetString() is not string issuer || !Uri.TryCreate(issuer, UriKind.Absolute, out Uri? origin)
                || origin.Scheme != "https" || origin.Host != "localhost" || origin.AbsolutePath != "/realms/run-owned") throw Refused("disposable-loopback-authority-required");
            JsonElement[] clients = [.. value.GetProperty("clients").EnumerateArray()];
            if (clients.Length != inventory.GetProperty("clients").GetArrayLength()) throw Refused("changed-cohort");
            foreach (JsonElement expected in inventory.GetProperty("clients").EnumerateArray())
            {
                JsonElement actual = clients.Single(client => client.GetProperty("resource").GetString() == expected.GetProperty("resource").GetString());
                foreach (string field in new[] { "clientId", "workload", "probes" })
                    if (actual.GetProperty(field).GetRawText() != expected.GetProperty(field).GetRawText()) throw Refused("changed-cohort-or-tenant");
                string secret = actual.GetProperty("secretFile").GetString()!;
                if (!Path.IsPathFullyQualified(secret) || Path.GetDirectoryName(secret) != directory || !Path.GetFileName(secret).StartsWith("client-", StringComparison.Ordinal)) throw Refused("foreign-secret-reference");
                _ = ReadProtected(secret, 4096);
            }
            // Re-read the final model: no cohort or selected tenant may change during provisioning.
            if (inventory.GetRawText() != PlatformWorkloadEnrollmentInventory.Create(builder, selectedTenant).GetRawText()) throw Refused("changed-final-cohort");
            CheckDeadline(deadline);
            foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
                builder.CreateResourceBuilder(resource).WithEnvironment("SSL_CERT_FILE", hostTrustFile ?? trustFile);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestPath"] = manifestPath,
                [PlatformWorkloadEnrollment.ConfigurationPrefix + "ManifestSha256"] = manifestSha256,
            });
            Console.WriteLine("WORKLOAD_ENROLLMENT_CONTEXT qualification=run-owned enrollment resources=" + clients.Length);
            return lease;
        }
        catch
        {
            if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Requests exact producer cleanup; directory absence acknowledges both container removal and private-material removal.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!Directory.Exists(_directory)) return;
        try
        {
            string releasePath = Path.Combine(_directory, "release.json");
            if (!File.Exists(releasePath))
            {
                try
                {
                    await WriteNewAsync(releasePath, JsonSerializer.SerializeToUtf8Bytes(new { runId = _runId, capability = _capability }), CancellationToken.None).ConfigureAwait(false);
                }
                catch (IOException) when (File.Exists(releasePath)) { }
            }
            // A successful publish can precede a timeout or another disposer. Reuse only this owner's request.
            if (!Directory.Exists(_directory)) return;
            using JsonDocument release = Parse(ReadProtected(releasePath));
            JsonElement request = release.RootElement;
            if (request.EnumerateObject().Count() != 2 || request.GetProperty("runId").GetString() != _runId
                || request.GetProperty("capability").GetString() != _capability) throw Refused("owned-cleanup-incomplete");
            long cleanupDeadline = Math.Min(_deadline, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30);
            while (Directory.Exists(_directory))
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= cleanupDeadline) throw Refused("owned-cleanup-incomplete");
                await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (Directory.Exists(_directory)) throw Refused("owned-cleanup-incomplete");
        }
    }

    private static void CheckDeadline(long deadline)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (deadline <= now || deadline > now + 86400) throw Refused("fixed-owner-deadline-expired-or-invalid");
    }
    private static string[] ProcessFields(int pid)
    {
        string value = File.ReadAllText("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat");
        return value[(value.LastIndexOf(')') + 2)..].Split(' ');
    }
    private static string ProcessIdentity(int pid) => File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim() + ":" + ProcessFields(pid)[19];
    private static int ParentProcess(int pid) => int.Parse(ProcessFields(pid)[1], CultureInfo.InvariantCulture);
    private static byte[] ReadProtected(string path, int maximum = 65536)
    {
        Protected(path);
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > maximum) throw Refused("artifact-size");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        if (memory.Length > maximum) throw Refused("artifact-size");
        return memory.ToArray();
    }
    private static void Protected(string path, bool directory = false)
    {
        if (!OperatingSystem.IsLinux()) throw Refused("linux-launcher-required");
        if (!Path.IsPathFullyQualified(path) || new FileInfo(path).LinkTarget is not null
            || (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0
            || directory && !Directory.Exists(path)) throw Refused("private-owner-path-required");
    }
    private static async Task WriteNewAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux()) throw Refused("linux-launcher-required");
        string temporary = path + "." + Path.GetRandomFileName() + ".writing";
        try
        {
            await using (FileStream stream = new(temporary, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite }))
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: false);
        }
        finally { File.Delete(temporary); }
    }
    private static JsonDocument Parse(byte[] bytes)
    {
        JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
        void Unique(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                HashSet<string> names = new(StringComparer.Ordinal);
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw Refused("duplicate-artifact-field");
                    Unique(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in value.EnumerateArray()) Unique(item);
        }
        try { Unique(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static InvalidOperationException Refused(string code) => new("Run-owned workload enrollment refused: " + code + ".");
}
