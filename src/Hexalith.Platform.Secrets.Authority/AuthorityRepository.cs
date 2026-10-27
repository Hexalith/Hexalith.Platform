using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Authority;

/// <summary>The authority never returns cached state after a storage failure.</summary>
public interface IAuthorityRepository
{
    /// <summary>Reads one coherent durable generation.</summary>
    Task<AuthorityState> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Atomically checks the original operation and commits state and audit.</summary>
    Task<AuthorityReceipt> AdministerAsync(string administratorId, AuthorityAdministration operation, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Linux durable single-writer file storage, with atomic rename, file/directory fsync, and process fencing.</summary>
public partial class AuthorityRepository(string directory) : IAuthorityRepository, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _uncertain;
    private readonly string _directory = Path.GetFullPath(directory);
    private const int MaximumStateBytes = 16 * 1024 * 1024;
    private sealed record Envelope(string Sha256, AuthorityState State);

    /// <inheritdoc/>
    public Task<AuthorityState> ReadAsync(CancellationToken cancellationToken)
        => LockedAsync(state => (state, state, false), cancellationToken);

    /// <inheritdoc/>
    public Task<AuthorityReceipt> AdministerAsync(string administratorId, AuthorityAdministration operation, DateTimeOffset now, CancellationToken cancellationToken)
        => LockedAsync(state =>
        {
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(operation, AuthorityJson.Options)));
            AuthorityReceipt? original = state.Audit.SingleOrDefault(item => item.OperationId == operation.OperationId);
            if (original is not null)
            {
                if (original.AdministratorId != administratorId || original.PayloadSha256 != hash) { throw new AuthorityConflictException(); }
                return (state, original, false);
            }
            if (!AuthorityEvaluator.Valid(operation)) { throw new ArgumentException("Invalid authority administration."); }
            bool matched = operation.ExpectedRevision == state.Revision;
            AuthorityState next = matched ? Apply(state, operation) : state;
            var receipt = new AuthorityReceipt(operation.OperationId, administratorId, hash, operation.Kind, state.Revision,
                next.Revision, matched ? "Applied" : "RevisionConflict", now);
            next = next with { Audit = [.. next.Audit, receipt] };
            return (next, receipt, true);
        }, cancellationToken);

    private async Task<T> LockedAsync<T>(Func<AuthorityState, (AuthorityState Next, T Result, bool Write)> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_uncertain || !OperatingSystem.IsLinux()) { throw new IOException("Authority storage is unavailable."); }
            Directory.CreateDirectory(_directory);
            // The lock inode is permanent. Never unlink it: another process could otherwise lock a different inode.
            using FileStream lease = await LeaseAsync(cancellationToken).ConfigureAwait(false);
            string path = Path.Combine(_directory, "authority.json");
            AuthorityState state;
            if (File.Exists(path))
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length > MaximumStateBytes) { throw new IOException("Authority state exceeds its bound."); }
                Envelope envelope = JsonSerializer.Deserialize<Envelope>(input, AuthorityJson.Options) ?? throw new IOException("Missing authority state.");
                string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(envelope.State, AuthorityJson.Options)));
                if (hash != envelope.Sha256 || !ValidState(envelope.State)) { throw new IOException("Invalid authority state."); }
                state = envelope.State;
            }
            else
            {
                // A lost/deleted snapshot must never silently reset a formerly initialized store.
                if (File.Exists(Path.Combine(_directory, "initialized"))) { throw new IOException("Authority state is missing."); }
                state = AuthorityState.Empty();
                using var marker = new FileStream(Path.Combine(_directory, "initialized"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                marker.Flush(flushToDisk: true);
                FlushDirectory();
                Save(path, state);
            }
            (AuthorityState next, T result, bool write) = operation(state);
            if (write)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Save(path, next);
            }
            return result;
        }
        catch (JsonException) { throw new IOException("Authority state is unreadable."); }
        finally { _gate.Release(); }
    }

    private async Task<FileStream> LeaseAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(_directory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 200) { await Task.Delay(10, cancellationToken).ConfigureAwait(false); }
        }
    }

    private void Save(string path, AuthorityState state)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(state, AuthorityJson.Options))), state), AuthorityJson.Options);
        if (bytes.Length > MaximumStateBytes) { throw new IOException("Authority state exceeds its bound."); }
        string temporary = Path.Combine(_directory, "transaction-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool renamed = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { file.Write(bytes); file.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
            renamed = true;
            FlushDirectory();
        }
        catch
        {
            // The original operation may have committed. No query or new operation is allowed until restart/reconciliation.
            if (renamed) { _uncertain = true; }
            throw;
        }
        finally { if (!renamed && File.Exists(temporary)) { File.Delete(temporary); } }
    }

    /// <summary>Flushes the rename's containing directory. A failure leaves this process unavailable.</summary>
    protected virtual void FlushDirectory()
    {
        int descriptor = OpenDirectory(_directory, 0x10000 | 0x80000);
        if (descriptor < 0) { throw new IOException("Authority directory cannot be opened."); }
        try { if (Fsync(descriptor) != 0) { throw new IOException("Authority directory flush failed."); } }
        finally { Close(descriptor); }
    }

    private static AuthorityState Apply(AuthorityState state, AuthorityAdministration operation)
    {
        state = state with { Generation = checked(state.Generation + 1) };
        return operation.Kind switch
        {
            "PublishProfile" => state with { Profile = operation.Profile },
            "SetCaller" => state with { Callers = [.. state.Callers.Where(item => item.CallerId != operation.Caller!.CallerId), operation.Caller!] },
            "SetInventoryHost" => state with { Hosts = [.. state.Hosts.Where(item => item.InventoryAuthorityReference != operation.Host!.InventoryAuthorityReference), operation.Host!] },
            "SetKeyStatus" => state with { KeyStatuses = [.. state.KeyStatuses.Where(item => item.TenantId != operation.KeyStatus!.TenantId
                || item.Purpose != operation.KeyStatus.Purpose || item.Version != operation.KeyStatus.Version), operation.KeyStatus!] },
            _ => throw new ArgumentException("Unknown authority administration."),
        };
    }

    private static bool ValidState(AuthorityState state) => Guid.TryParseExact(state.InstanceId, "N", out _) && state.Generation >= 0
        && state.Callers is not null && state.Hosts is not null && state.KeyStatuses is not null && state.Audit is not null
        && state.Callers.Select(item => item.CallerId).Distinct(StringComparer.Ordinal).Count() == state.Callers.Length
        && state.Hosts.Select(item => item.InventoryAuthorityReference).Distinct(StringComparer.Ordinal).Count() == state.Hosts.Length
        && state.KeyStatuses.Select(item => (item.TenantId, item.Purpose, item.Version)).Distinct().Count() == state.KeyStatuses.Length
        && state.Audit.Select(item => item.OperationId).Distinct(StringComparer.Ordinal).Count() == state.Audit.Length;

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenDirectory(string path, int flags);
    [LibraryImport("libc", EntryPoint = "fsync")]
    private static partial int Fsync(int descriptor);
    [LibraryImport("libc", EntryPoint = "close")]
    private static partial int Close(int descriptor);

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();
}

/// <summary>An original operation ID cannot be used by another administrator or with a different payload.</summary>
public sealed class AuthorityConflictException : Exception;
