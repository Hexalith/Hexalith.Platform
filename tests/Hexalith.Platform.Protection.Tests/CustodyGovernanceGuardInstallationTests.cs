using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class CustodyGovernanceGuardInstallationTests
{
    [Theory]
    [InlineData("valid")][InlineData("restored-revision")][InlineData("digest-substitution")][InlineData("late-revocation")][InlineData("absent")]
    public async Task ActualGovernanceInstallationAndAuthorityPropagateIndependentCurrentStateRefusal(string mutation)
    {
        var clock=TimeProvider.System;var now=clock.GetUtcNow();
        var transition=new GovernanceGuardTransition("tenant","operation",GovernanceGuardOperation.AppendWrite,4,"epoch","deletion",null,null,null,null,null,null,"authorization","reference");
        var state=new TenantGovernanceGuardState("tenant","installation",4,"epoch","legacy","writer",null,[],[],[],[],[],[]);
        var original=new GovernanceGuardEvidence("tenant","intent","mutation","authority-v4","receipt",now.AddSeconds(-1),now.AddMinutes(1),[],[],[],"writer","legacy","zero",0,"allowed","v1",[],"block") { AppendResourceId="exact-resource" };
        var dapr=Substitute.For<DaprClient>();var owner=Substitute.For<ICustodyOwnerStateAuthority>();bool current=true;int authorizations=0;
        owner.AuthorizeOperationAsync("operation",Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=> { authorizations++;return current; });
        string hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(original)));
        owner.ValidateStateAsync(Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<long>(0)==4 && call.ArgAt<string>(1)==hash);
        dapr.GetStateAsync<GovernanceGuardEvidence>("independent","exact-source",Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(_=>
        {
            if(mutation=="late-revocation") current=false;
            return Task.FromResult(mutation=="absent"?null:mutation=="digest-substitution"?original with { AuthorityReceiptId="substituted" }:original)!;
        });
        var enrollment=new QualificationGuardEvidenceEnrollment("exact-source",mutation=="restored-revision"?3:4);
        var installation=new CustodyGovernanceGuardInstallation(dapr,"independent",new Dictionary<string,QualificationGuardEvidenceEnrollment>{{"operation",enrollment}},new Dictionary<string,ICustodyOwnerStateAuthority>{{"operation",owner}},clock);
        var authority=new GovernanceGuardAuthority(installation);
        var result=await authority.ReadAsync(transition,"intent","mutation",state,TestContext.Current.CancellationToken);
        if(mutation=="valid") { result.ShouldBe(original);authorizations.ShouldBe(2); }
        else result.ShouldBeNull();
        current=true;authorizations=0;
        var lookup=await authority.ReadLookupAsync(transition,"intent","mutation",TestContext.Current.CancellationToken);
        if(mutation=="valid") { lookup.ShouldBe(original);authorizations.ShouldBe(2); }
        else lookup.ShouldBeNull();
        await dapr.Received(2).GetStateAsync<GovernanceGuardEvidence>("independent","exact-source",Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>());
        await owner.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(),Arg.Any<CancellationToken>());
    }
}
