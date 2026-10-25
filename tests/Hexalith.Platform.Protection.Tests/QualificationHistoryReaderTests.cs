using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class QualificationHistoryReaderTests
{
    [Fact]
    public async Task ActualDirectoryMetadataAndEveryExactOriginalAreReadOnlyAndChangedOriginalDenies()
    {
        var actor=Substitute.For<IDirectoryMigrationBoundaryActor>();var outcome=new DirectoryBoundaryOutcome("installation",new string('A',64),DirectoryBoundaryOutcomeState.Installed,1);
        var ledger=new DirectoryEpochLedger("tenant",1,"epoch",null,[new("tenant","installation","epoch",0,"legacy-revocation","all-writers")],[],[],[],[outcome]);
        actor.ReadAsync("tenant").Returns(ledger);actor.LookupAsync("tenant",outcome.OperationId,outcome.RequestDigest).Returns(outcome);
        var reader=new QualificationDirectoryBoundaryOriginalReader(tenant=> { tenant.ShouldBe("tenant");return actor; });
        var actual=await reader.ReadAsync("tenant",TestContext.Current.CancellationToken);actual.ShouldNotBeNull();actual.Ledger.ShouldBe(ledger);actual.Originals.Single().ShouldBe(outcome);
        actor.LookupAsync("tenant",outcome.OperationId,outcome.RequestDigest).Returns(outcome with { RequestDigest=new string('B',64) });
        (await reader.ReadAsync("tenant",TestContext.Current.CancellationToken)).ShouldBeNull();
        actor.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name == "ReadAsync" || call.GetMethodInfo().Name == "LookupAsync");
    }
    [Fact]
    public async Task ActualNativeGuardLookupPreservesPhysicalPositionDistinctFromLogicalHighWaterAndRefusesSubstitutedIntent()
    {
        using var dapr=Substitute.For<DaprClient>();var authority=Substitute.For<IGuardedStateTransactionAuthority>();
        var target=new GuardedStateTransactionTarget("tenant","native-component","partition","installation","guard","revoked-writers","v1",DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddSeconds(25));
        authority.GetCurrentAsync("tenant",Arg.Any<CancellationToken>()).Returns(target);
        authority.AuthorizeLookupAsync(target,"stale-operation",Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        authority.ValidateReceiptAsync(target,Arg.Any<GuardedStateCommitReceipt>(),Arg.Any<CancellationToken>()).Returns(true);
        var transition=new GovernanceGuardTransition("tenant","stale-operation",GovernanceGuardOperation.RecordBatchIssuanceStale,2,"epoch","deletion",null,null,null,null,null,null,"authorization","reference");
        var targets=Array.Empty<GuardedStateMutation>();string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        string intent=Hash(new { Transition=transition,TargetMutationDigest=Hash(targets) });var logical=new GovernanceProtocolReceipt("stale-operation",intent,"IssuanceStale",2,0,"reference","logical-receipt");
        var original=new GuardedStateCommitReceipt("tenant","installation","stale-operation",new string('A',64),9,"physical-receipt") { LogicalIntentDigest=intent,LogicalGuardHighWater=2,Outcome=JsonSerializer.SerializeToUtf8Bytes(logical) };
        dapr.GetStateAsync<GuardedStateCommitReceipt>("native-component",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(_=>original);
        var reader=new QualificationGuardedOriginalReader(new DaprGuardedStateTransaction(dapr,TimeProvider.System,authority));
        var measured=await reader.ReadAsync("tenant",[new(QualificationGuardStepKind.Guard,Transition:transition,Targets:targets)],TestContext.Current.CancellationToken);
        measured.ShouldNotBeNull();measured.Single().Receipt.GuardRevision.ShouldBe(9);measured.Single().Outcome.GuardHighWater.ShouldBe(2);
        original=original with { LogicalIntentDigest=new string('B',64) };
        (await reader.ReadAsync("tenant",[new(QualificationGuardStepKind.Guard,Transition:transition,Targets:targets)],TestContext.Current.CancellationToken)).ShouldBeNull();
        dapr.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(DaprClient.GetStateAsync));
    }
}
