using Dapr.Client;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Independent enrolled current head and append-only originals. No genesis, repair, restore or automatic retry API.
internal sealed class NativeDaprAuthorityStore(ComponentProfile profile, NativeInstallation installation, DaprClient dapr, TimeProvider clock) : IAuthorityStore
{
    private string Key(string tenant) => installation.AuthorityKeyPrefix + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(tenant));
    public async Task<CurrentState> ReadAsync(string tenant, CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        if (!profile.Tenants.Contains(tenant, StringComparer.Ordinal)) throw new AuthorityUnavailableException("Tenant not enrolled.");
        await ValidateArchiveEnrollmentAsync(token).ConfigureAwait(false);
        var (head, etag) = await dapr.GetStateAndETagAsync<AuthorityHead>(installation.AuthorityComponent, Key(tenant), ConsistencyMode.Strong, cancellationToken: token);
        if (head is null || string.IsNullOrWhiteSpace(etag)) throw new AuthorityUnavailableException("Independent current authority/genesis unavailable.");
        byte[] bytes = await ReadImmutableAsync(head.Checkpoint, token);
        var checkpoint = await CheckpointArchive.DecodeAsync(bytes, ReadImmutableAsync, token);
        if (head.EnrollmentDigest != profile.EnrollmentDigest || checkpoint.EnrollmentDigest != profile.EnrollmentDigest
            || checkpoint.Revision != head.Revision || checkpoint.TenantId != tenant || head.Checkpoint.Sha256 != head.Digest)
            throw new AuthorityUnavailableException("Current enrolled checkpoint mismatch.");
        return new(head, checkpoint, etag, CustodyJson.Read<CheckpointArchiveV1>(bytes));
    }
    public Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken token)
        => PutAtAsync(bytes, category + "/" + CustodyJson.Hash(bytes.Span), token);
    public Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes, string locator, CancellationToken token)
        => locator.StartsWith("journal/native-result/", StringComparison.Ordinal) ? PutAtAsync(bytes, locator, token)
            : throw new AuthorityUnavailableException("Invalid native original locator.");
    public async Task<ObjectVersion?> FindIntentResultAsync(string locator, CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        await ValidateArchiveEnrollmentAsync(token).ConfigureAwait(false);
        if (!locator.StartsWith("journal/native-result/", StringComparison.Ordinal)) throw new AuthorityUnavailableException("Invalid native original locator.");
        string path = Resolve(locator);
        byte[] bytes;
        try { bytes=await ReadBoundedAsync(path,token).ConfigureAwait(false); }
        catch(FileNotFoundException) { return null; }
        return new(locator, CustodyJson.Hash(bytes), CustodyJson.Hash(bytes), bytes.Length, profile.RetainUntil);
    }
    public async Task<ObjectVersion> PutCompletionImmutableAsync(ReadOnlyMemory<byte> bytes,string category,string tenant,string operationId,CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        await EnsureCompletionAsync(tenant,operationId,token).ConfigureAwait(false);
        return await PutAtAsync(bytes,category+"/"+CustodyJson.Hash(bytes.Span),token,NativeArchiveCapacity.Key(tenant,operationId)).ConfigureAwait(false);
    }
    public async Task<ObjectVersion> PutIntentCompletionAsync(ReadOnlyMemory<byte> bytes,string locator,string tenant,string operationId,CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        if(!locator.StartsWith("journal/native-result/",StringComparison.Ordinal)) throw new AuthorityUnavailableException("Invalid native original locator.");
        await EnsureCompletionAsync(tenant,operationId,token).ConfigureAwait(false);
        return await PutAtAsync(bytes,locator,token,NativeArchiveCapacity.Key(tenant,operationId)).ConfigureAwait(false);
    }
    private async Task EnsureCompletionAsync(string tenant,string operation,CancellationToken token)
    {
        var current=await ReadAsync(tenant,token).ConfigureAwait(false);
        bool admitted=operation.StartsWith("intent:",StringComparison.Ordinal)?current.Checkpoint.NativeIntents.GetValueOrDefault(operation[7..]) is { Phase:not CustodyBindingIntentPhase.Closed }
            :operation.StartsWith("original:",StringComparison.Ordinal) && current.Checkpoint.Originals.GetValueOrDefault(operation[9..]) is { Response:null };
        if(!admitted) throw new AuthorityUnavailableException("Exact admitted native completion identity required.");
        var capacity=new NativeArchiveCapacity(profile,installation);
        await capacity.WithFenceAsync(async state=> { var effectToken=BoundedDurableFileOperation.CurrentToken; await capacity.EnsurePendingAsync(state,current.Checkpoint,effectToken).ConfigureAwait(false);return true; },token).ConfigureAwait(false);
    }
    private async Task<ObjectVersion> PutAtAsync(ReadOnlyMemory<byte> bytes, string key, CancellationToken token,string? completionKey=null)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        await ValidateArchiveEnrollmentAsync(token).ConfigureAwait(false);
        if (bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes || profile.RetainUntil <= clock.GetUtcNow())
            throw new AuthorityUnavailableException("Original capacity/retention unavailable.");
        string path = Resolve(key);
        var capacity=new NativeArchiveCapacity(profile,installation);
        await capacity.WithFenceAsync(async state=> { var effectToken=BoundedDurableFileOperation.CurrentToken; await capacity.PublishAsync(state,path,key,bytes,completionKey,effectToken).ConfigureAwait(false);return true; },token).ConfigureAwait(false);
        return new(key, CustodyJson.Hash(bytes.Span), CustodyJson.Hash(bytes.Span), bytes.Length, profile.RetainUntil);
    }
    public async Task<byte[]> ReadImmutableAsync(ObjectVersion version, CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        await ValidateArchiveEnrollmentAsync(token).ConfigureAwait(false);
        if (version.VersionId != version.Sha256 || version.RetainUntil > profile.RetainUntil || version.RetainUntil <= clock.GetUtcNow())
            throw new AuthorityUnavailableException("Pinned retained original required.");
        byte[] bytes = await ReadBoundedAsync(Resolve(version.Key), token);
        if (bytes.Length != version.Bytes || CustodyJson.Hash(bytes) != version.Sha256) throw new AuthorityUnavailableException("Original bytes substituted.");
        return bytes;
    }
    public async Task<CurrentState> CommitAsync(CurrentState expected, Checkpoint next, CancellationToken token)
    {
        using var operationBudget=CancellationTokenSource.CreateLinkedTokenSource(token);operationBudget.CancelAfter(TimeSpan.FromSeconds(30));token=operationBudget.Token;
        if (next.TenantId != expected.Checkpoint.TenantId || next.EnrollmentDigest != profile.EnrollmentDigest
            || next.Revision != expected.Head.Revision + 1 || next.PredecessorDigest != expected.Head.Digest
            || expected.Archive is null || string.IsNullOrWhiteSpace(expected.ExactHeadAttribute))
            throw new AuthorityUnavailableException("Exact forward authority transition required.");
        bool admission = next.Originals.Count > expected.Checkpoint.Originals.Count || next.NativeIntents.Count > expected.Checkpoint.NativeIntents.Count
            || next.Deliveries.Count > expected.Checkpoint.Deliveries.Count || next.AllocationExclusions.Count > expected.Checkpoint.AllocationExclusions.Count;
        var plan = CheckpointArchive.Prepare(next, expected.Archive, 32 * 1024 * 1024 - (admission ? profile.CompletionReserveBytes : 0));
        var capacity=new NativeArchiveCapacity(profile,installation);
        await capacity.WithFenceAsync(async state=>
        {
            var effectToken=BoundedDurableFileOperation.CurrentToken;
            // A prior final head may have committed before its acknowledgement/settlement was lost.
            // Reconcile that exact surviving head before quota checks for any new admission.
            var current=await ReadAsync(next.TenantId,effectToken).ConfigureAwait(false);
            await capacity.SettleAsync(state,current.Checkpoint,effectToken).ConfigureAwait(false);
            if(current.Head!=expected.Head || current.ExactHeadAttribute!=expected.ExactHeadAttribute) throw new AuthorityConflictException();
            await capacity.EnsurePendingAsync(state,current.Checkpoint,effectToken).ConfigureAwait(false);
            await capacity.EnsurePendingAsync(state,next,effectToken).ConfigureAwait(false);
            return true;
        },token).ConfigureAwait(false);
        var changed=next.NativeIntents.Where(pair=>expected.Checkpoint.NativeIntents.TryGetValue(pair.Key,out var prior) && prior!=pair.Value && prior.Phase!=CustodyBindingIntentPhase.Closed).OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>"intent:"+pair.Key).ToArray();
        var changedOriginals=next.Originals.Where(pair=>expected.Checkpoint.Originals.TryGetValue(pair.Key,out var prior) && prior.Response is null && prior.Response!=pair.Value.Response).Select(pair=>"original:"+pair.Key).ToArray();
        string? completion=!admission && changed.Length>0?changed[0]:!admission && changedOriginals.Length==1?changedOriginals[0]:!admission && next.Cycle?.OperationId is { } cycle && expected.Checkpoint.Originals.GetValueOrDefault(cycle) is { Response:null }?"original:"+cycle:null;
        Task<ObjectVersion> Put(ReadOnlyMemory<byte> body,string category,CancellationToken ct)=>completion is null?PutImmutableAsync(body,category,ct):PutAtAsync(body,category+"/"+CustodyJson.Hash(body.Span),ct,NativeArchiveCapacity.Key(next.TenantId,completion));
        byte[] bytes = await CheckpointArchive.WriteAsync(plan, Put, token);
        var version = await Put(bytes, "checkpoint", token);
        var head = new AuthorityHead(next.Revision, version.Sha256, version, profile.EnrollmentDigest);
        return await capacity.WithFenceAsync(async state=>
        {
            var effectToken=BoundedDurableFileOperation.CurrentToken;
            // Another completed transition may have released reservations for this still-uncommitted proposal
            // while its archives were published. Re-establish them before admitting its head.
            await capacity.EnsurePendingAsync(state,next,effectToken).ConfigureAwait(false);
            if (!await dapr.TrySaveStateAsync(installation.AuthorityComponent, Key(next.TenantId), head, expected.ExactHeadAttribute,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, cancellationToken: effectToken))
                throw new AuthorityConflictException();
            // Commit, current reread and settlement share one fence. A later admission cannot have its
            // completion reserve released by this earlier snapshot. Lost acknowledgement remains charged.
            var committed=await ReadAsync(next.TenantId,effectToken).ConfigureAwait(false);
            await capacity.SettleAsync(state,committed.Checkpoint,effectToken).ConfigureAwait(false);
            return committed;
        },token).ConfigureAwait(false);
    }
    private async Task ValidateArchiveEnrollmentAsync(CancellationToken token)
        =>_=await BoundedDurableFileOperation.RunAsync(async ct=> { await ValidateArchiveEnrollmentCoreAsync(ct).ConfigureAwait(false);return true; },token).ConfigureAwait(false);
    private async Task ValidateArchiveEnrollmentCoreAsync(CancellationToken token)
    {
        foreach (string directory in new[] { installation.CheckpointArchiveDirectory, installation.OriginalArchiveDirectory })
        {
            _=ArchiveFileLease.FilesystemIdentity(directory);
            var info = new DirectoryInfo(directory);
            var enrollment = new FileInfo(Path.Combine(directory, "enrollment.json"));
            if (!info.Exists || info.LinkTarget is not null || !enrollment.Exists || enrollment.LinkTarget is not null || enrollment.Length is <= 0 or > 65536
                || CustodyJson.Hash(await BoundedRegularFile.ReadAsync(enrollment.FullName,65536,token).ConfigureAwait(false)) != installation.ArchiveEnrollmentSha256)
                throw new AuthorityUnavailableException("Independent archive enrollment/placement unavailable.");
        }
    }
    private string Resolve(string key)
    {
        if (key.Length > 2048 || key.Contains("..", StringComparison.Ordinal) || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-')))
            throw new AuthorityUnavailableException("Invalid immutable locator.");
        string root = key.StartsWith("checkpoint/", StringComparison.Ordinal) ? installation.CheckpointArchiveDirectory : installation.OriginalArchiveDirectory;
        string path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new AuthorityUnavailableException("Archive boundary denied.");
        for (DirectoryInfo? parent = new(Path.GetDirectoryName(path)!); parent is not null && parent.FullName.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal); parent = parent.Parent)
            if (parent.Exists && parent.LinkTarget is not null) throw new AuthorityUnavailableException("Archive symbolic link denied.");
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null) throw new AuthorityUnavailableException("Immutable object symbolic link denied.");
        return path;
    }
    private static Task<byte[]> ReadBoundedAsync(string path, CancellationToken token)
        => BoundedRegularFile.ReadAsync(path,CustodyBindingContract.MaximumOriginalBytes,token);
}
