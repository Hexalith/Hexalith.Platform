using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class CustodyGuardInstallationTests
{
    [Fact]
    public async Task ExactCurrentAnchorsPermitReadAndReceiptWhileRestoredAbsentOrChangedStateDenies()
    {
        var target=new GuardedStateTransactionTarget("tenant","component","partition","installation","guard","revoked-writers","authority-v1",DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddSeconds(25));
        var current=Substitute.For<ICustodyOwnerStateAuthority>();var cellOwner=Substitute.For<ICustodyOwnerStateAuthority>();var receiptOwner=Substitute.For<ICustodyOwnerStateAuthority>();
        string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        current.ValidateStateAsync(7,Hash(target),Arg.Any<CancellationToken>()).Returns(true);current.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        cellOwner.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);receiptOwner.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        var installation=new CustodyGuardedStateInstallation(target,7,current,new Dictionary<string,ICustodyOwnerStateAuthority>{{"guard",cellOwner}},new Dictionary<string,ICustodyOwnerStateAuthority>{{"operation",receiptOwner}},TimeProvider.System);
        var authority=new GuardedStateTransactionAuthority(installation);
        (await authority.GetCurrentAsync("tenant",TestContext.Current.CancellationToken)).ShouldBe(target);
        (await authority.GetCurrentAsync("foreign",TestContext.Current.CancellationToken)).ShouldBeNull();
        var cell=new GuardedStateCell("tenant","installation","guard",4,"actual-protected-cell"u8.ToArray());cellOwner.ValidateStateAsync(4,Hash(cell),Arg.Any<CancellationToken>()).Returns(true);
        (await authority.ValidateCellAsync(target,cell,"guard",TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateCellAsync(target,cell with { Value="substituted"u8.ToArray() },"guard",TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.ValidateCellAsync(target,null,"guard",TestContext.Current.CancellationToken)).ShouldBeFalse();
        var receipt=new GuardedStateCommitReceipt("tenant","installation","operation",new string('A',64),4,"receipt") { LogicalIntentDigest=new string('B',64),Outcome="opaque-outcome"u8.ToArray(),LogicalGuardHighWater=3 };
        receiptOwner.ValidateStateAsync(4,Hash(receipt),Arg.Any<CancellationToken>()).Returns(true);
        (await authority.ValidateReceiptAsync(target,receipt,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateReceiptAsync(target,receipt with { Outcome="changed-outcome"u8.ToArray() },TestContext.Current.CancellationToken)).ShouldBeFalse();
        current.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(false);
        (await authority.AuthorizeReadAsync(target,"guard",TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.ValidateReceiptAsync(target,receipt,TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    [Fact]
    public async Task NoRevisionZeroGenesisIsCreatedWhenIndependentAbsenceAnchorIsMissing()
    {
        var target=new GuardedStateTransactionTarget("tenant","component","partition","installation","guard","revoked-writers","authority-v1",DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddSeconds(25));
        var current=Substitute.For<ICustodyOwnerStateAuthority>();var cellOwner=Substitute.For<ICustodyOwnerStateAuthority>();
        current.ValidateStateAsync(Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);current.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);cellOwner.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        var installation=new CustodyGuardedStateInstallation(target,1,current,new Dictionary<string,ICustodyOwnerStateAuthority>{{"guard",cellOwner},{"source",cellOwner}},new Dictionary<string,ICustodyOwnerStateAuthority>(),TimeProvider.System);
        (await installation.ReadCellAsync(target,"source",null,TestContext.Current.CancellationToken)).ShouldBe(GuardedCellHistory.RestoredAbsent);
        await cellOwner.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>());
    }
}
