using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual event replay caller keeps unprotected records readable independently of retired or unavailable custody.</summary>
public sealed class ErasureRootLoop3PlainReplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlainReplayMakesNoCustodyCallWhenRetirementWouldSucceedOrThrow(bool outage)
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var bindings = Substitute.For<IInteractionProtectionBindingProvider>();
        bindings.IsCustodyPermanentlyLostAsync(original.Binding.Identity.Owner, Arg.Any<CancellationToken>())
            .Returns(_ => outage ? Task.FromException<bool>(new IOException("Independent loss proof unavailable")) : Task.FromResult(true));
        var engine = new InteractionPayloadProtectionService(fixture.Registry, fixture.Keys, bindings, TimeProvider.System);
        byte[] plain = "{\"Ordinary\":42}"u8.ToArray(); var token = TestContext.Current.CancellationToken;
        var result = await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, plain, "json", null, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        result.Outcome.IsReadable.ShouldBeTrue(); result.Outcome.PayloadBytes.ShouldBe(plain); result.HasErasedContent.ShouldBeFalse(); bindings.ReceivedCalls().ShouldBeEmpty();
        byte[] mislabeled = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "secret" } });
        var mismatch = await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, mislabeled, "json", null, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        mismatch.Outcome.IsUnreadable.ShouldBeTrue(); mismatch.Outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        await Should.ThrowAsync<ArgumentException>(() => engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, "Mismatched.Event", plain, "json", null, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token));
        bindings.ReceivedCalls().ShouldBeEmpty();
        var protectedResult = await engine.TryReplayEventPayloadAsync(original.Binding.Identity.Owner, original.Binding.Identity.PayloadTypeId, plain, "json+pdenc-v2", new(PayloadProtectionState.Protected, 1, "hexalith-pdenc-v2", "key", null, null), ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        protectedResult.Outcome.IsUnreadable.ShouldBeTrue(); protectedResult.HasErasedContent.ShouldBeFalse();
    }
}
