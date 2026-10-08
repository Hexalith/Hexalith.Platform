using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Exact actor-history custody contracts, without a registered history provider.</summary>
public sealed class IdentityHistoryCustodyPolicyTests
{
    private static readonly DateTimeOffset EffectiveAt = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = EffectiveAt.AddDays(1);
    private static readonly AggregateIdentity Identity = new("tenant-a", "party", "party-1");
    private static readonly IdentityHistoryPolicy Policy = new(
        IdentityHistoryCustodyOptions.PolicyId,
        IdentityHistoryCustodyOptions.Retention,
        IdentityHistoryCustodyOptions.ExpiryTrigger);

    /// <summary>Approved admission keeps UTC scope, evidence, and a 365-fixed-day deadline.</summary>
    [Fact]
    public void ApprovedAdmission_PreservesUtcScopeEvidenceAndDeadline()
    {
        IdentityHistoryCustodyOptions.Retention.ShouldBe(TimeSpan.FromDays(365));
        IdentityHistoryCustodyOptions.Retention.TotalSeconds.ShouldBe(31_536_000d);
        DateTimeOffset offsetEquivalent = new(2026, 10, 8, 2, 0, 0, TimeSpan.FromHours(2));
        IdentityHistoryCustodyOptions options = new(Policy, IdentityHistoryCustodyOptions.Purpose);
        IdentityHistoryCustodyUnit admitted = options.Admit(Identity, "env-a", "instance-a", offsetEquivalent, Now, "evidence-7", 4);
        IdentityHistoryCustodyUnit again = options.Admit(Identity, "env-a", "instance-a", EffectiveAt, Now, "evidence-7", 4);

        admitted.ShouldBe(again);
        admitted.BindingEffectiveAt.ShouldBe(EffectiveAt);
        admitted.BindingEffectiveAt.Offset.ShouldBe(TimeSpan.Zero);
        admitted.ExpiresAt.ShouldBe(EffectiveAt.Add(TimeSpan.FromDays(365)));
        admitted.ExpiresAt.Offset.ShouldBe(TimeSpan.Zero);
        admitted.Identity.TenantId.ShouldBe("tenant-a");
        admitted.Identity.Domain.ShouldBe("party");
        admitted.Identity.AggregateId.ShouldBe("party-1");
        admitted.Environment.ShouldBe("env-a");
        admitted.Instance.ShouldBe("instance-a");
        admitted.EvidenceId.ShouldBe("evidence-7");
        admitted.AdmissionRevision.ShouldBe(4);
        admitted.PolicyId.ShouldBe(IdentityHistoryCustodyOptions.PolicyId);
        admitted.Purpose.ShouldBe(IdentityHistoryCustodyOptions.Purpose);
    }

    /// <summary>A party domain is accepted only after aggregate-identity lowercasing.</summary>
    [Theory]
    [InlineData("party")]
    [InlineData("Party")]
    [InlineData("PARTY")]
    public void PartyDomain_IsAcceptedAfterLowercasing(string domain)
    {
        IdentityHistoryCustodyUnit unit = Admit(identity: new AggregateIdentity("tenant-a", domain, "party-1"));
        unit.Identity.Domain.ShouldBe("party");
    }

    /// <summary>Wrong or blank policy, purpose, trigger, or retention does not construct a gate.</summary>
    [Theory]
    [InlineData("id")]
    [InlineData("blank-id")]
    [InlineData("purpose")]
    [InlineData("blank-purpose")]
    [InlineData("trigger")]
    [InlineData("blank-trigger")]
    [InlineData("retention")]
    [InlineData("zero")]
    [InlineData("negative")]
    public void UnapprovedPolicy_DoesNotConstruct(string field)
    {
        IdentityHistoryPolicy policy = field switch
        {
            "id" => Policy with { PolicyId = "other-policy" },
            "blank-id" => Policy with { PolicyId = " " },
            "trigger" => Policy with { ExpiryTrigger = "profile-erasure" },
            "blank-trigger" => Policy with { ExpiryTrigger = " " },
            "retention" => Policy with { Retention = TimeSpan.FromDays(365).Add(TimeSpan.FromTicks(1)) },
            "zero" => Policy with { Retention = TimeSpan.Zero },
            "negative" => Policy with { Retention = TimeSpan.FromDays(-1) },
            _ => Policy,
        };
        string purpose = field switch
        {
            "purpose" => "party-profile",
            "blank-purpose" => " ",
            _ => IdentityHistoryCustodyOptions.Purpose,
        };

        Should.Throw<ArgumentException>(() => new IdentityHistoryCustodyOptions(policy, purpose))
            .ShouldBeOfType<ArgumentException>();
    }

    /// <summary>Null policy and purpose are rejected before a gate exists.</summary>
    [Fact]
    public void NullPolicyOrPurpose_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryCustodyOptions(null!, IdentityHistoryCustodyOptions.Purpose));
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryCustodyOptions(Policy, null!));
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryCustodyOptions(null!, null!));
    }

    /// <summary>An equivalent retention built from seconds is still the approved policy.</summary>
    [Fact]
    public void RetentionFromSeconds_MatchesTheApprovedPolicy()
    {
        var policy = new IdentityHistoryPolicy(IdentityHistoryCustodyOptions.PolicyId, TimeSpan.FromSeconds(31_536_000), IdentityHistoryCustodyOptions.ExpiryTrigger);
        var options = new IdentityHistoryCustodyOptions(policy, IdentityHistoryCustodyOptions.Purpose);
        options.Policy.Retention.ShouldBe(TimeSpan.FromDays(365));
    }

    /// <summary>Adding 365 fixed days to the last representable instant has no deadline.</summary>
    [Fact]
    public void Overflow_ThrowsArgumentOutOfRangeException()
    {
        var options = new IdentityHistoryCustodyOptions(Policy, IdentityHistoryCustodyOptions.Purpose);
        DateTimeOffset last = DateTimeOffset.MaxValue - TimeSpan.FromDays(365);
        IdentityHistoryCustodyOptions.DeriveDeadline(last).ShouldBe(DateTimeOffset.MaxValue);
        Should.Throw<ArgumentOutOfRangeException>(() => IdentityHistoryCustodyOptions.DeriveDeadline(last.AddTicks(1)));
        Should.Throw<ArgumentOutOfRangeException>(() => options.Admit(Identity, "env-a", "instance-a", DateTimeOffset.MaxValue, Now, "evidence-7", 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new IdentityHistoryCustodyUnit(
            Identity, "env-a", "instance-a", IdentityHistoryCustodyOptions.PolicyId, IdentityHistoryCustodyOptions.Purpose,
            DateTimeOffset.MaxValue, DateTimeOffset.MaxValue, "evidence-7", 1));
    }

    /// <summary>The 2023-03-01 boundary expires on the following leap day, 365 fixed days later.</summary>
    [Fact]
    public void LeapBoundary_ExpiresOnTheFollowingLeapDay()
    {
        var effective = new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var offsetEquivalent = new DateTimeOffset(2023, 3, 1, 1, 0, 0, TimeSpan.FromHours(1));
        DateTimeOffset expiresAt = IdentityHistoryCustodyOptions.DeriveDeadline(offsetEquivalent);
        expiresAt.ShouldBe(new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero));
        expiresAt.Offset.ShouldBe(TimeSpan.Zero);
        expiresAt.ShouldBe(effective + TimeSpan.FromDays(365));
    }

    /// <summary>Reads are allowed only before the deadline, and new admission at or after it is rejected.</summary>
    [Theory]
    [InlineData(-1L, true)]
    [InlineData(0L, false)]
    [InlineData(1L, false)]
    public void ExclusiveDeadline_IsReadableOnlyBeforeExpiry(long tickOffset, bool readable)
    {
        IdentityHistoryCustodyUnit unit = Admit();
        DateTimeOffset now = unit.ExpiresAt.AddTicks(tickOffset);
        IdentityHistoryCustodyOptions.IsReadable(now, unit.ExpiresAt).ShouldBe(readable);
        IdentityHistoryCustodyOptions options = new(Policy, IdentityHistoryCustodyOptions.Purpose);
        if (readable)
        {
            IdentityHistoryCustodyUnit admitted = options.Admit(Identity, "env-a", "instance-a", EffectiveAt, now, "evidence-7", 4);
            admitted.ExpiresAt.ShouldBe(unit.ExpiresAt);
        }
        else
        {
            Should.Throw<ArgumentException>(() => options.Admit(Identity, "env-a", "instance-a", EffectiveAt, now, "evidence-7", 4))
                .ShouldBeOfType<ArgumentException>();
        }
    }

    /// <summary>An expired unit can still be constructed and compared.</summary>
    [Fact]
    public void ExpiredUnit_RoundTripsWithoutAdmission()
    {
        var effective = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAt = IdentityHistoryCustodyOptions.DeriveDeadline(effective);
        var unit = new IdentityHistoryCustodyUnit(
            Identity, "env-a", "instance-a", IdentityHistoryCustodyOptions.PolicyId, IdentityHistoryCustodyOptions.Purpose,
            effective, expiresAt, "evidence-7", 4);
        IdentityHistoryCustodyOptions.IsReadable(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero), unit.ExpiresAt).ShouldBeFalse();
        var options = new IdentityHistoryCustodyOptions(Policy, IdentityHistoryCustodyOptions.Purpose);
        Should.Throw<ArgumentException>(() => options.Admit(Identity, "env-a", "instance-a", effective, Now, "evidence-7", 4))
            .ShouldBeOfType<ArgumentException>();
        RoundTrip(unit).ShouldBe(unit);
    }

    /// <summary>JSON and the same inputs preserve zero offsets and original facts. A tampered deadline or scope fails.</summary>
    [Fact]
    public void RoundTrip_PreservesFactsAndRejectsTamperedDeadlineOrScope()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        IdentityHistoryLifecycleRecord lifecycle = Forward(ActiveLifecycle(unit).AddGeneration(unit, 1));
        IdentityHistoryCopyRecord copy = Copy(unit, IdentityHistoryCopyRegistration.Completed, "completion-1");
        IdentityHistoryDestructionReceipt receipt = Receipt(unit);

        RoundTrip(unit).ShouldBe(unit);
        RoundTrip(lifecycle).ShouldBe(lifecycle);
        RoundTrip(copy).ShouldBe(copy);
        RoundTrip(receipt).ShouldBe(receipt);
        RoundTrip(unit).BindingEffectiveAt.Offset.ShouldBe(TimeSpan.Zero);
        RoundTrip(unit).ExpiresAt.Offset.ShouldBe(TimeSpan.Zero);
        RoundTrip(receipt).ExpiresAt.ShouldBe(unit.ExpiresAt);
        RoundTrip(receipt).Unit.EvidenceId.ShouldBe(unit.EvidenceId);
        RoundTrip(receipt).Unit.AdmissionRevision.ShouldBe(unit.AdmissionRevision);
        RoundTrip(receipt).Unit.BindingEffectiveAt.ShouldBe(unit.BindingEffectiveAt);
        RoundTrip(receipt).Outcomes.Single().CopyClass.ShouldBe("ledger/v1");
        RoundTrip(receipt).Outcomes.Single().Outcome.ShouldBe("Opaque Result");

        ShouldRejectTamper<IdentityHistoryCustodyUnit>(unit, node => node["ExpiresAt"] = "9998-01-01T00:00:00+00:00");
        ShouldRejectTamper<IdentityHistoryCustodyUnit>(unit, node => node["Identity"]!["Domain"] = "billing");
        ShouldRejectTamper<IdentityHistoryCopyRecord>(copy, node => node["Deadline"] = "9998-01-01T00:00:00+00:00");
        ShouldRejectTamper<IdentityHistoryDestructionReceipt>(receipt, node => node["ExpiresAt"] = "9998-01-01T00:00:00+00:00");
    }

    /// <summary>A different scope fact is a different unit.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("aggregate")]
    [InlineData("environment")]
    [InlineData("instance")]
    [InlineData("evidence")]
    [InlineData("instant")]
    public void ChangedScope_IsUnequal(string field)
    {
        IdentityHistoryCustodyUnit unit = Admit();
        IdentityHistoryCustodyUnit changed = field switch
        {
            "tenant" => Admit(identity: new AggregateIdentity("tenant-b", "party", "party-1")),
            "aggregate" => Admit(identity: new AggregateIdentity("tenant-a", "party", "party-2")),
            "environment" => Admit(environment: "env-b"),
            "instance" => Admit(instance: "instance-b"),
            "evidence" => Admit(evidenceId: "evidence-8"),
            "instant" => Admit(effectiveAt: EffectiveAt.AddTicks(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        changed.Equals(unit).ShouldBeFalse();
    }

    /// <summary>Reverse, skipped, and same-revision moves are rejected and the deadline stays put.</summary>
    [Theory]
    [InlineData(IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.Active, 5L)]
    [InlineData(IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.Destroyed, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.ReceiptFinal, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Expired, IdentityHistoryLifecycleState.Active, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Expired, IdentityHistoryLifecycleState.Destroyed, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Expired, IdentityHistoryLifecycleState.ReceiptFinal, 6L)]
    [InlineData(IdentityHistoryLifecycleState.PendingDestruction, IdentityHistoryLifecycleState.Active, 6L)]
    [InlineData(IdentityHistoryLifecycleState.PendingDestruction, IdentityHistoryLifecycleState.Expired, 6L)]
    [InlineData(IdentityHistoryLifecycleState.PendingDestruction, IdentityHistoryLifecycleState.ReceiptFinal, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Destroyed, IdentityHistoryLifecycleState.PendingDestruction, 6L)]
    [InlineData(IdentityHistoryLifecycleState.ReceiptFinal, IdentityHistoryLifecycleState.Destroyed, 6L)]
    [InlineData(IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.Expired, 5L)]
    public void IllegalLifecycleMove_IsRejectedAndDeadlineStays(
        IdentityHistoryLifecycleState from,
        IdentityHistoryLifecycleState to,
        long revision)
    {
        IdentityHistoryLifecycleRecord lifecycle = Lifecycle(from);
        DateTimeOffset deadline = lifecycle.Unit.ExpiresAt;
        string? operationId = to is IdentityHistoryLifecycleState.PendingDestruction or IdentityHistoryLifecycleState.Destroyed or IdentityHistoryLifecycleState.ReceiptFinal
            ? lifecycle.OperationId ?? "operation-1"
            : null;
        string? receiptReference = to == IdentityHistoryLifecycleState.ReceiptFinal ? "receipt-1" : null;

        Should.Throw<ArgumentException>(() => lifecycle.Move(to, revision, 2, 3, operationId, receiptReference))
            .ShouldBeOfType<ArgumentException>();
        lifecycle.State.ShouldBe(from);
        lifecycle.Unit.ExpiresAt.ShouldBe(deadline);
        lifecycle.LifecycleRevision.ShouldBe(5);
    }

    /// <summary>Legal moves keep the original evidence, admission revision, and deadline, and authority stays separate.</summary>
    [Fact]
    public void ForwardLifecycle_PreservesOriginalFacts()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        IdentityHistoryLifecycleRecord final = Forward(ActiveLifecycle(unit));

        final.State.ShouldBe(IdentityHistoryLifecycleState.ReceiptFinal);
        final.Unit.ShouldBe(unit);
        final.Unit.EvidenceId.ShouldBe("evidence-7");
        final.Unit.AdmissionRevision.ShouldBe(4);
        final.Unit.ExpiresAt.ShouldBe(unit.ExpiresAt);
        final.LifecycleRevision.ShouldBe(9);
        final.Epoch.ShouldBe(3);
        final.Fence.ShouldBe(9);
        final.OperationId.ShouldBe("operation-1");
        final.ReceiptReference.ShouldBe("receipt-1");
        final.LifecycleRevision.ShouldNotBe(final.Unit.AdmissionRevision);
        IdentityHistoryLifecycleRecord direct = ActiveLifecycle(unit)
            .Move(IdentityHistoryLifecycleState.PendingDestruction, 6, 2, 2, "operation-1", null);
        direct.State.ShouldBe(IdentityHistoryLifecycleState.PendingDestruction);
        direct.Unit.ExpiresAt.ShouldBe(unit.ExpiresAt);
        direct.OperationId.ShouldBe("operation-1");
    }

    /// <summary>A generation grows only on the same unit, and a repeat does not mint a revision or deadline.</summary>
    [Fact]
    public void AddGeneration_RequiresTheSameUnitAndDeadline()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        IdentityHistoryLifecycleRecord lifecycle = ActiveLifecycle(unit);
        IdentityHistoryLifecycleRecord grown = lifecycle.AddGeneration(unit, 0).AddGeneration(unit, 2);
        IdentityHistoryLifecycleRecord retry = grown.AddGeneration(unit, 0);

        grown.Generations.ShouldBe([0L, 2L]);
        grown.Unit.ExpiresAt.ShouldBe(unit.ExpiresAt);
        grown.LifecycleRevision.ShouldBe(lifecycle.LifecycleRevision);
        retry.ShouldBeSameAs(grown);
        Should.Throw<ArgumentException>(() => grown.AddGeneration(Admit(evidenceId: "evidence-8"), 3))
            .ShouldBeOfType<ArgumentException>();
        grown.Unit.ExpiresAt.ShouldBe(unit.ExpiresAt);
        grown.Generations.Count.ShouldBe(2);
    }

    /// <summary>A copy deadline after or before the unit deadline is rejected.</summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(-1L)]
    public void CopyDeadline_MustEqualTheUnitDeadline(long ticks)
    {
        IdentityHistoryCustodyUnit unit = Admit();
        Should.Throw<ArgumentException>(() => Copy(unit, IdentityHistoryCopyRegistration.Registered, null, unit.ExpiresAt.AddTicks(ticks)))
            .ShouldBeOfType<ArgumentException>();
    }

    /// <summary>An offset-equivalent deadline is stored on the original UTC deadline, and the class stays opaque.</summary>
    [Fact]
    public void Copy_BindsGenerationClassAndOriginalDeadline()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        DateTime clock = DateTime.SpecifyKind(unit.ExpiresAt.UtcDateTime.AddHours(2), DateTimeKind.Unspecified);
        DateTimeOffset offsetDeadline = new(clock, TimeSpan.FromHours(2));
        IdentityHistoryCopyRecord copy = Copy(unit, IdentityHistoryCopyRegistration.Registered, null, offsetDeadline);

        copy.Deadline.ShouldBe(unit.ExpiresAt);
        copy.Deadline.Offset.ShouldBe(TimeSpan.Zero);
        copy.Generation.ShouldBe(1);
        copy.CopyClass.ShouldBe("Ledger/v1");
        copy.Location.ShouldBe("location-ref");
        copy.Owner.ShouldBe("owner-ref");
        copy.CompletionReference.ShouldBeNull();
    }

    /// <summary>Only a completed copy carries a completion reference.</summary>
    [Theory]
    [InlineData(IdentityHistoryCopyRegistration.Registered)]
    [InlineData(IdentityHistoryCopyRegistration.InFlight)]
    [InlineData(IdentityHistoryCopyRegistration.Invalidated)]
    public void IncompleteCopy_RejectsCompletionReference(IdentityHistoryCopyRegistration registration)
    {
        IdentityHistoryCustodyUnit unit = Admit();
        Should.Throw<ArgumentException>(() => Copy(unit, registration, "completion-1")).ShouldBeOfType<ArgumentException>();
        IdentityHistoryCopyRecord copy = Copy(unit, registration, null);
        copy.Registration.ShouldBe(registration);
        copy.CompletionReference.ShouldBeNull();
    }

    /// <summary>A completed copy requires a completion reference.</summary>
    [Fact]
    public void CompletedCopy_RequiresCompletionReference()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        Should.Throw<ArgumentNullException>(() => Copy(unit, IdentityHistoryCopyRegistration.Completed, null));
        Should.Throw<ArgumentException>(() => Copy(unit, IdentityHistoryCopyRegistration.Completed, " "))
            .ShouldBeOfType<ArgumentException>();
        Copy(unit, IdentityHistoryCopyRegistration.Completed, "completion-1").CompletionReference.ShouldBe("completion-1");
    }

    /// <summary>A receipt keeps original facts, unique class outcomes, and the original expiry.</summary>
    [Fact]
    public void Receipt_PreservesOriginalFactsAndExpiry()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        var confirmed = new DateTimeOffset(2027, 10, 9, 3, 0, 0, TimeSpan.FromHours(3));
        IdentityHistoryClassOutcome first = new("Ledger/v1", "Opaque Result");
        IdentityHistoryClassOutcome second = new("backup-copy", "unrecoverable");
        var receipt = new IdentityHistoryDestructionReceipt(
            unit, "operation-1", 8, 3, 9, 0, [first, second], confirmed, unit.ExpiresAt);

        receipt.ExpiresAt.ShouldBe(unit.ExpiresAt);
        receipt.ExpiresAt.Offset.ShouldBe(TimeSpan.Zero);
        receipt.ConfirmedAt.ShouldBe(confirmed.ToUniversalTime());
        receipt.ConfirmedAt.Offset.ShouldBe(TimeSpan.Zero);
        receipt.InventoryGeneration.ShouldBe(0);
        receipt.Unit.ShouldBe(unit);
        receipt.LifecycleRevision.ShouldBe(8);
        receipt.LifecycleRevision.ShouldNotBe(unit.AdmissionRevision);
        receipt.Outcomes.Count.ShouldBe(2);
        RoundTrip(receipt).ShouldBe(receipt);
    }

    /// <summary>Empty, blank, duplicate, or deadline-changed receipt facts are rejected.</summary>
    [Fact]
    public void Receipt_RejectsEmptyBlankDuplicateOrChangedDeadline()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        DateTimeOffset confirmed = unit.ExpiresAt.AddDays(1);
        Should.Throw<ArgumentException>(() => new IdentityHistoryDestructionReceipt(
            unit, "operation-1", 8, 3, 9, 0, [], confirmed, unit.ExpiresAt)).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentException>(() => new IdentityHistoryClassOutcome(" ", "done")).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentException>(() => new IdentityHistoryClassOutcome("ledger/v1", " ")).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentException>(() => new IdentityHistoryDestructionReceipt(
            unit,
            "operation-1",
            8,
            3,
            9,
            0,
            [new IdentityHistoryClassOutcome("ledger/v1", "one"), new IdentityHistoryClassOutcome("ledger/v1", "two")],
            confirmed,
            unit.ExpiresAt)).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentException>(() => new IdentityHistoryDestructionReceipt(
            unit, "operation-1", 8, 3, 9, 0, [new IdentityHistoryClassOutcome("ledger/v1", "one")], confirmed, unit.ExpiresAt.AddTicks(1)))
            .ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryDestructionReceipt(
            unit, "operation-1", 8, 3, 9, 0, null!, confirmed, unit.ExpiresAt));
        Should.Throw<ArgumentException>(() => new IdentityHistoryDestructionReceipt(
            unit, "operation-1", 8, 3, 9, -1, [new IdentityHistoryClassOutcome("ledger/v1", "one")], confirmed, unit.ExpiresAt))
            .ShouldBeOfType<ArgumentException>();
    }

    /// <summary>Operation identifiers start at pending destruction, and receipt references exist only at receipt final.</summary>
    [Fact]
    public void LifecycleLinkage_FollowsState()
    {
        IdentityHistoryCustodyUnit unit = Admit();
        Should.Throw<ArgumentException>(() => new IdentityHistoryLifecycleRecord(
            unit, IdentityHistoryLifecycleState.Active, 5, 1, 1, "operation-1", null, [])).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentException>(() => new IdentityHistoryLifecycleRecord(
            unit, IdentityHistoryLifecycleState.Active, 5, 1, 1, null, "receipt-1", [])).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryLifecycleRecord(
            unit, IdentityHistoryLifecycleState.PendingDestruction, 5, 1, 1, null, null, []));
        Should.Throw<ArgumentException>(() => new IdentityHistoryLifecycleRecord(
            unit, IdentityHistoryLifecycleState.Destroyed, 5, 1, 1, "operation-1", "receipt-1", [])).ShouldBeOfType<ArgumentException>();
        Should.Throw<ArgumentNullException>(() => new IdentityHistoryLifecycleRecord(
            unit, IdentityHistoryLifecycleState.ReceiptFinal, 5, 1, 1, "operation-1", null, []));
    }

    /// <summary>Replacing the destruction operation identifier is rejected.</summary>
    [Fact]
    public void Move_DoesNotReplaceTheOperationId()
    {
        IdentityHistoryLifecycleRecord pending = Lifecycle(IdentityHistoryLifecycleState.PendingDestruction);
        Should.Throw<ArgumentException>(() => pending.Move(IdentityHistoryLifecycleState.Destroyed, 6, 2, 3, "operation-2", null))
            .ShouldBeOfType<ArgumentException>();
        pending.OperationId.ShouldBe("operation-1");
        pending.Unit.ExpiresAt.ShouldBe(Admit().ExpiresAt);
    }

    /// <summary>Default custody registration still has no history provider and sets no capability flag.</summary>
    [Fact]
    public void AddPlatformCustody_LeavesHistoryCustodyUnregistered()
    {
        using ServiceProvider services = new ServiceCollection().AddPlatformCustody().BuildServiceProvider();
        services.GetService<IIdentityHistoryCustody>().ShouldBeNull();
        services.GetRequiredService<IdentityHistoryCleanup>().ShouldNotBeNull();
        typeof(IdentityHistoryCustodyUnit).Assembly.GetTypes().Any(type =>
            type is { IsClass: true, IsAbstract: false } && typeof(IIdentityHistoryCustody).IsAssignableFrom(type)).ShouldBeFalse();
        string[] flags = ["SourceExpiryEnforced", "RestoreSafe", "DerivedCopiesCovered"];
        foreach (Type type in new[]
        {
            typeof(IdentityHistoryCustodyOptions),
            typeof(IdentityHistoryCustodyUnit),
            typeof(IdentityHistoryLifecycleRecord),
            typeof(IdentityHistoryCopyRecord),
            typeof(IdentityHistoryDestructionReceipt),
        })
        {
            foreach (string flag in flags)
            {
                type.GetProperty(flag, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public).ShouldBeNull();
            }
        }
    }

    private static IdentityHistoryCustodyUnit Admit(
        AggregateIdentity? identity = null,
        string environment = "env-a",
        string instance = "instance-a",
        DateTimeOffset? effectiveAt = null,
        string evidenceId = "evidence-7",
        long admissionRevision = 4)
    {
        DateTimeOffset bindingEffectiveAt = effectiveAt ?? EffectiveAt;
        return new IdentityHistoryCustodyOptions(Policy, IdentityHistoryCustodyOptions.Purpose).Admit(
            identity ?? Identity,
            environment,
            instance,
            bindingEffectiveAt,
            bindingEffectiveAt.AddDays(1),
            evidenceId,
            admissionRevision);
    }

    private static IdentityHistoryLifecycleRecord ActiveLifecycle(IdentityHistoryCustodyUnit unit)
        => new(unit, IdentityHistoryLifecycleState.Active, 5, 1, 1, null, null, []);

    private static IdentityHistoryLifecycleRecord Lifecycle(IdentityHistoryLifecycleState state)
    {
        IdentityHistoryCustodyUnit unit = Admit();
        return state switch
        {
            IdentityHistoryLifecycleState.Active => ActiveLifecycle(unit),
            IdentityHistoryLifecycleState.Expired => new(unit, state, 5, 1, 1, null, null, []),
            IdentityHistoryLifecycleState.PendingDestruction => new(unit, state, 5, 1, 1, "operation-1", null, []),
            IdentityHistoryLifecycleState.Destroyed => new(unit, state, 5, 1, 1, "operation-1", null, []),
            IdentityHistoryLifecycleState.ReceiptFinal => new(unit, state, 5, 1, 1, "operation-1", "receipt-1", []),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static IdentityHistoryLifecycleRecord Forward(IdentityHistoryLifecycleRecord active)
        => active
            .Move(IdentityHistoryLifecycleState.Expired, 6, 2, 4, null, null)
            .Move(IdentityHistoryLifecycleState.PendingDestruction, 7, 3, 9, "operation-1", null)
            .Move(IdentityHistoryLifecycleState.Destroyed, 8, 3, 9, "operation-1", null)
            .Move(IdentityHistoryLifecycleState.ReceiptFinal, 9, 3, 9, "operation-1", "receipt-1");

    private static IdentityHistoryCopyRecord Copy(
        IdentityHistoryCustodyUnit unit,
        IdentityHistoryCopyRegistration registration,
        string? completionReference,
        DateTimeOffset? deadline = null)
        => new(unit, 1, "Ledger/v1", "location-ref", "owner-ref", registration, deadline ?? unit.ExpiresAt, completionReference);

    private static IdentityHistoryDestructionReceipt Receipt(IdentityHistoryCustodyUnit unit)
        => new(
            unit,
            "operation-1",
            8,
            3,
            9,
            0,
            [new IdentityHistoryClassOutcome("ledger/v1", "Opaque Result")],
            unit.ExpiresAt.AddDays(1),
            unit.ExpiresAt);

    private static T RoundTrip<T>(T value)
    {
        string json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private static void ShouldRejectTamper<T>(T value, Action<JsonNode> tamper)
    {
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(value))!;
        tamper(node);
        Exception exception = Should.Throw<Exception>(() => JsonSerializer.Deserialize<T>(node.ToJsonString()));
        Exception root = exception is JsonException && exception.InnerException is not null ? exception.InnerException : exception;
        root.ShouldBeOfType<ArgumentException>();
    }
}
