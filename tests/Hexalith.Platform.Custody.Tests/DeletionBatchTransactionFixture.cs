using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Serialized synthetic multi-cell backend with all-or-none ETag compare and independent state provenance; no actual Dapr/store qualification.</summary>
internal sealed class DeletionBatchTransactionFixture
{
    internal DaprClient Client { get; } = Substitute.For<DaprClient>();
    internal IGuardedStateTransactionAuthority Authority { get; } = Substitute.For<IGuardedStateTransactionAuthority>();
    internal GuardedStateTransactionTarget Target { get; }
    internal DaprGuardedStateTransaction Owner { get; }
    internal Dictionary<string, byte[]> Stored { get; } = [];
    internal bool LoseAcknowledgement { get; set; }
    internal int CommittedTransactions { get; private set; }
    internal int TransactionCalls { get; private set; }
    internal Func<Task>? BeforeTransaction { get; set; }
    internal Func<GuardedStateCommitReceipt?, Task<GuardedStateCommitReceipt?>>? ReceiptRead { get; set; }
    internal DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
    internal DeletionBatchTransactionFixture()
    {
        Target = new("tenant-a", "qualified-synthetic-component", "installed-tenant-partition", "source-installation", "tenant-guard",
            "complete-writer-revocation", "independent-current-authority", Now, Now.AddMinutes(1));
        var guard = new GuardedStateCell(Target.TenantId, Target.InstallationId, Target.GuardCellId, 1, "initial-guard"u8.ToArray());
        Stored[Key(guard.CellId)] = JsonSerializer.SerializeToUtf8Bytes(guard);
        Authority.GetCurrentAsync(Target.TenantId, Arg.Any<CancellationToken>()).Returns(Target);
        Authority.AuthorizeReadAsync(Target, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.AuthorizeLookupAsync(Target, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.AuthorizeAsync(Target, Arg.Any<GuardedStateCommitRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateCellAsync(Target, Arg.Any<GuardedStateCell?>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateReceiptAsync(Target, Arg.Any<GuardedStateCommitReceipt>(), Arg.Any<CancellationToken>()).Returns(true);
        Client.GetStateAndETagAsync<GuardedStateCell>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            lock (Stored)
            {
                var cell = Stored.TryGetValue(call.ArgAt<string>(1), out var value) ? JsonSerializer.Deserialize<GuardedStateCell>(value) : null;
                return (cell!, cell?.Revision.ToString(CultureInfo.InvariantCulture) ?? "");
            }
        });
        Client.GetStateAsync<GuardedStateCommitReceipt>(Target.ComponentName, Arg.Any<string>(), Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            GuardedStateCommitReceipt? receipt;
            lock (Stored) { receipt = Stored.TryGetValue(call.ArgAt<string>(1), out var value) ? JsonSerializer.Deserialize<GuardedStateCommitReceipt>(value) : null; }
            return ReceiptRead is null ? receipt! : (await ReceiptRead(receipt).ConfigureAwait(false))!;
        });
        Client.ExecuteStateTransactionAsync(Target.ComponentName, Arg.Any<IReadOnlyList<StateTransactionRequest>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            TransactionCalls++;
            if (BeforeTransaction is { } before) { await before().ConfigureAwait(false); }
            var operations = call.Arg<IReadOnlyList<StateTransactionRequest>>();
            lock (Stored)
            {
                foreach (var operation in operations)
                {
                    if (operation.ETag is null) { continue; } // Null is unconditional, exactly as documented; the required guard compare serializes creation.
                    var cell = Stored.TryGetValue(operation.Key, out var bytes) ? JsonSerializer.Deserialize<GuardedStateCell>(bytes) : null;
                    if (cell?.Revision.ToString(CultureInfo.InvariantCulture) != operation.ETag) { throw new InvalidOperationException("Synthetic atomic ETag compare lost."); }
                }
                foreach (var operation in operations) { Stored[operation.Key] = operation.Value!.ToArray(); }
                CommittedTransactions++;
            }
            if (LoseAcknowledgement) { throw new IOException("Controlled commit succeeded before acknowledgement loss."); }
        });
        Owner = new(Client, TimeProvider.System, Authority);
    }
    internal GuardedStateCommitRequest Request(string id = "original-effect", string cell = "source-a", string value = "sealed-content")
        => new(Target.TenantId, id, new(Target.GuardCellId, 1, Hash("initial-guard"u8.ToArray()), "next-guard"u8.ToArray()),
            [new(cell, 0, Hash([]), System.Text.Encoding.UTF8.GetBytes(value))]);
    internal string Key(string id) => "governed/" + Hash(JsonSerializer.SerializeToUtf8Bytes(new[] { Target.TenantId, Target.InstallationId, "cell", id }));
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
