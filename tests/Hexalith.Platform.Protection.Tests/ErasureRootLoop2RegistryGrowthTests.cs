using System.Text.Json;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Ordinary real engine/registry writes accumulate beyond 8 MiB through the ERC-backed actor and retain every sealed original.</summary>
public sealed class ErasureRootLoop2RegistryGrowthTests
{
    [Fact]
    public async Task OrdinaryCumulativeOccurrenceWritesCrossEightMiBWithoutAnAdapterCliffOrGrowingCasPayloads()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var writes = new List<(ErasureRootOccurrenceEnrollment Enrollment, PayloadProtectionWriteResult Write)>();
        for (int index = 0; index < 10; index++)
        {
            string reference = "01ARZ3NDEKTSV4RRFFQ69G5FA" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var enrolled = fixture.Enroll("ordinary-writer-" + index, reference, (ulong)index + 1);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { Safe = index, ProtectedContent = new { Target = enrolled.Binding.Identity.Target, Value = new string('x', 512 * 1024) } });
            var write = await fixture.Engine.ProtectEventPayloadAsync(enrolled.Binding.Identity.Owner, Substitute.For<IEventPayload>(), enrolled.Binding.Identity.PayloadTypeId,
                payload, "json", ErasureRootOccurrenceBindings.Occurrence(enrolled.Binding.Identity), token);
            await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
            fixture.Source.Persisted.Add(enrolled.Binding.OperationId, write.ProtectionResult.PayloadBytes);
            await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
            await fixture.Keys.CompleteDisposedUsesAsync(token); writes.Add((enrolled, write));
            (await fixture.Store.ReadHeadAsync("tenant-a", token))!.Bytes.Length.ShouldBeLessThan(65536);
        }
        var state = JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(JsonSerializer.Serialize(fixture.RegistryState.CommittedState["candidate-interaction-occurrences-v1"]))!;
        int actualBytes = JsonSerializer.SerializeToUtf8Bytes(state).Length;
        actualBytes.ShouldBeGreaterThan(8 * 1024 * 1024); actualBytes.ShouldBeLessThan(RecoverableAnchoredState.MaximumPendingBytes);
        state.Records.Count.ShouldBe(10); state.Records.ShouldAllBe(record => record.WriterState == InteractionOccurrenceWriterState.Active);
        foreach (var write in writes)
        {
            var retained = (await fixture.Registry.LookupAsync(write.Enrollment.Binding.Identity, token)).Record!;
            retained.Sealed!.PayloadBytes.ShouldBe(write.Write.ProtectionResult.PayloadBytes);
        }
        var first = writes[0];
        var opened = await fixture.Engine.TryUnprotectEventPayloadAsync(first.Enrollment.Binding.Identity.Owner, first.Enrollment.Binding.Identity.PayloadTypeId,
            first.Write.ProtectionResult.PayloadBytes, first.Write.ProtectionResult.SerializationFormat, first.Write.ProtectionResult.Metadata,
            ErasureRootOccurrenceBindings.Occurrence(first.Enrollment.Binding.Identity), token);
        opened.IsReadable.ShouldBeTrue(); using var document = JsonDocument.Parse(opened.PayloadBytes!);
        document.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetString()!.Length.ShouldBe(512 * 1024);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(opened.PayloadBytes!);
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_REGISTRY_GROWTH_RESULT") is { Length: > 0 } path)
        { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { records = state.Records.Count, actualCurrentRegistryBytes = actualBytes,
            casHeadBytes = (await fixture.Store.ReadHeadAsync("tenant-a", token))!.Bytes.Length, upstreamMaximumPendingBytes = RecoverableAnchoredState.MaximumPendingBytes,
            actualEngineRegistryWrites = true, sourceComponentOnly = true, liveExecuted = false }), token); }
    }
}
