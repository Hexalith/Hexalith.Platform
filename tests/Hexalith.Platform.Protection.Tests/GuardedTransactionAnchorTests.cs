using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class GuardedTransactionAnchorTests
{
    [Theory]
    [InlineData("normal")][InlineData("native-ack")][InlineData("authority-ack")][InlineData("receipt-read")]
    public async Task ActualNativeTransactionPublishesExactCellAndReceiptAnchorsAndRecoversLostAcknowledgments(string loss)
    {
        using var fixture=new Fixture();fixture.Guard.Revision.ShouldBe(1);fixture.Loss=loss;var request=fixture.Request();
        var result=await fixture.Native().CommitAsync(request,TestContext.Current.CancellationToken);
        result.Status.ShouldBeOneOf(GuardedStateCommitStatus.Committed,GuardedStateCommitStatus.Unavailable);
        fixture.Transactions.ShouldBe(1);
        var restarted=fixture.Native();var recovered=await restarted.LookupAsync(request,TestContext.Current.CancellationToken);
        recovered.Status.ShouldBe(GuardedStateCommitStatus.Committed);recovered.Receipt.ShouldNotBeNull().RequestDigest.ShouldBe(Hash(request));
        var guard=await restarted.ReadGuardAsync("tenant",TestContext.Current.CancellationToken);guard.ShouldNotBeNull().Revision.ShouldBe(2);
        fixture.Guard.Revision.ShouldBe(2);fixture.Source.Revision.ShouldBe(1);fixture.Receipt.Revision.ShouldBe(1);
        var again=await restarted.CommitAsync(request,TestContext.Current.CancellationToken);again.Status.ShouldBe(GuardedStateCommitStatus.Committed);fixture.Transactions.ShouldBe(1);
        var changed=request with { Outcome="changed"u8.ToArray() };(await restarted.CommitAsync(changed,TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);fixture.Transactions.ShouldBe(1);
    }
    [Fact]
    public async Task RestoredMissingNativeOutcomeRemainsUnknownWithoutAnchoringPrimaryBytesOrRepeatingTransaction()
    {
        using var fixture=new Fixture();fixture.Loss="restore-before-witness";var request=fixture.Request();
        (await fixture.Native().CommitAsync(request,TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unknown);
        var restarted=fixture.Native();
        (await restarted.CommitAsync(request,TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);
        (await restarted.LookupAsync(request,TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unknown);
        (await restarted.ReadGuardAsync("tenant",TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Transactions.ShouldBe(1);fixture.Guard.Revision.ShouldBe(1);fixture.Source.Revision.ShouldBe(0);fixture.Receipt.Revision.ShouldBe(0);
    }
    [Theory]
    [InlineData("partial")][InlineData("foreign-receipt")]
    public async Task IncompleteOrForeignNativeOutcomeCannotPublishAnyIndependentAnchor(string loss)
    {
        using var fixture=new Fixture();fixture.Loss=loss;var request=fixture.Request();
        (await fixture.Native().CommitAsync(request,TestContext.Current.CancellationToken)).Status.ShouldBe(GuardedStateCommitStatus.Unavailable);
        fixture.Guard.Revision.ShouldBe(1);fixture.Source.Revision.ShouldBe(0);fixture.Receipt.Revision.ShouldBe(0);fixture.Transactions.ShouldBe(1);
    }
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private sealed class Fixture:IDisposable
    {
        internal readonly ReceivingAuthorityFixture Originals=new("guarded-transaction");internal readonly Owner Guard,Source,Receipt;
        private readonly DaprClient _dapr=Substitute.For<DaprClient>();private readonly Dictionary<string,byte[]> _state=[];private readonly ICustodyOwnerStateAuthority _target=Substitute.For<ICustodyOwnerStateAuthority>();
        private readonly GuardedStateTransactionTarget _installation=new("tenant","native","partition","installation","guard","revoked-writers","current-policy",DateTimeOffset.UtcNow.AddSeconds(-1),DateTimeOffset.UtcNow.AddMinutes(5));
        internal string Loss="normal";internal int Transactions;private bool _lostRead;
        private string Key(string kind,string id)=>"governed/"+Hash(new[]{_installation.TenantId,_installation.InstallationId,kind,id});
        internal Fixture()
        {
            var guard=new GuardedStateCell("tenant","installation","guard",1,"before"u8.ToArray());_state.Add(Key("cell","guard"),JsonSerializer.SerializeToUtf8Bytes(guard));
            Guard=new("guard-scope",1,Hash(guard));Source=new("source-scope",0,Hash<object?>(null));Receipt=new("receipt-scope",0,Hash<object?>(null));
            _target.ValidateStateAsync(1,Hash(_installation),Arg.Any<CancellationToken>()).Returns(true);_target.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
            _dapr.GetStateAndETagAsync<GuardedStateCell>("native",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=> { var cell=Read<GuardedStateCell>(call.ArgAt<string>(1));return (cell!,cell?.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)??""); });
            _dapr.GetStateAsync<GuardedStateCell>("native",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>Read<GuardedStateCell>(call.ArgAt<string>(1))!);
            _dapr.GetStateAsync<GuardedStateCommitReceipt>("native",Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                if(Loss=="receipt-read" && Transactions>0 && !_lostRead) { _lostRead=true;throw new IOException("isolated lost native receipt read"); }
                return Read<GuardedStateCommitReceipt>(call.ArgAt<string>(1))!;
            });
            _dapr.ExecuteStateTransactionAsync("native",Arg.Any<IReadOnlyList<StateTransactionRequest>>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var old=_state.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray());var operations=call.Arg<IReadOnlyList<StateTransactionRequest>>();
                foreach(var operation in operations) if(operation.ETag is { } etag && Read<GuardedStateCell>(operation.Key)?.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)!=etag) throw new IOException("exact atomic compare lost");
                foreach(var operation in operations) _state[operation.Key]=operation.Value!.ToArray();Transactions++;
                if(Loss=="partial") _state.Remove(Key("cell","source"));
                if(Loss=="foreign-receipt") { var receipt=Read<GuardedStateCommitReceipt>(Key("outcome","operation"))!;_state[Key("outcome","operation")]=JsonSerializer.SerializeToUtf8Bytes(receipt with { TenantId="foreign" }); }
                if(Loss=="restore-before-witness") { _state.Clear();foreach(var pair in old) _state.Add(pair.Key,pair.Value); }
                if(Loss=="authority-ack") Guard.LoseAcknowledgment=true;
                return Loss is "native-ack" or "restore-before-witness"?Task.FromException(new IOException("isolated native ack loss")):Task.CompletedTask;
            });
        }
        private T? Read<T>(string key)=>_state.TryGetValue(key,out var bytes)?JsonSerializer.Deserialize<T>(bytes):default;
        internal GuardedStateCommitRequest Request()=>new("tenant","operation",new("guard",1,Convert.ToHexString(SHA256.HashData("before"u8)),"after"u8.ToArray()),[new("source",0,Convert.ToHexString(SHA256.HashData([])),"sealed"u8.ToArray())]) { Outcome="exact-outcome"u8.ToArray() };
        internal DaprGuardedStateTransaction Native()
        {
            var cells=new Dictionary<string,ICustodyOwnerStateAuthority>{{"guard",Guard},{"source",Source}};var receipts=new Dictionary<string,ICustodyOwnerStateAuthority>{{"operation",Receipt}};
            var anchors=new CustodyGuardedTransactionAnchors(_dapr,new InstalledQualificationOriginalStore(Originals.Authority),new Dictionary<string,string>{{"guard",Guard.Scope},{"source",Source.Scope}},new Dictionary<string,string>{{"operation",Receipt.Scope}});
            return new(_dapr,TimeProvider.System,new GuardedStateTransactionAuthority(new CustodyGuardedStateInstallation(_installation,1,_target,cells,receipts,TimeProvider.System,anchors)));
        }
        public void Dispose() { Originals.Dispose();_dapr.Dispose(); }
    }
    private sealed class Owner(string scope,long revision,string digest):ICustodyOwnerStateAuthority
    {
        internal string Scope=scope;internal long Revision=revision;private string _digest=digest;private AnchoredStateTransition? _admitted,_recorded;internal bool LoseAcknowledgment;
        public Task<bool> AuthorizeOperationAsync(string identity,string operation,CancellationToken token)=>Task.FromResult(true);
        public Task<bool> ValidateStateAsync(long revision,string digest,CancellationToken token)=>Task.FromResult(revision==Revision && digest==_digest);
        public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition,CancellationToken token)
        { if(transition.ScopeId!=Scope || transition.ExpectedRevision!=Revision || transition.PredecessorDigest!=_digest || _admitted is not null && Hash(_admitted)!=Hash(transition)) return Task.FromResult(false);_admitted=transition;return Task.FromResult(true); }
        public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition,CancellationToken token)
        {
            if(_admitted is null || Hash(_admitted)!=Hash(transition)) return Task.FromResult(false);
            Revision=transition.TargetRevision;_digest=transition.TargetDigest;_recorded=transition;
            if(LoseAcknowledgment) { LoseAcknowledgment=false;return Task.FromResult(false); }return Task.FromResult(true);
        }
        public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition,CancellationToken token)=>Task.FromResult(_recorded is not null && Hash(_recorded)==Hash(transition));
        public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition,CancellationToken token)=>Task.FromResult(_recorded is not null && Hash(_recorded)==Hash(transition));
    }
}
