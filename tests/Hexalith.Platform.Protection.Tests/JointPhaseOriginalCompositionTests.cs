using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class JointPhaseOriginalCompositionTests
{
    [Fact]
    public async Task ActualCompositionUsesThreeSeparateCredentialRolesAndEveryReceivingBoundaryRejectsRoleSubstitution()
    {
        using var execute=new ReceivingAuthorityFixture("executor");using var secret=new ReceivingAuthorityFixture("joint-phase-secret");using var command=new ReceivingAuthorityFixture("joint-phase-command");
        var destinations=new Dictionary<string,ReceivingAuthorityFixture>{{"executor",execute},{"secret",secret},{"command",command}};var transport=new Routed(destinations);
        // Each fixture independently signs its own installed binding; no signer crosses receiving roles.
        var stores=JointPhaseOriginalStores.Create(transport,execute.Binding("executor"),secret.Binding("secret"),command.Binding("command"),TimeProvider.System);
        foreach(var (store,role) in new[]{(stores.Executor,"executor"),(stores.Secrets,"joint-phase-secret"),(stores.Command,"joint-phase-command")})
        {
            (await store.BeginAsync(role,"same-id",new JsonObject { ["role"]=role },TestContext.Current.CancellationToken)).ShouldBeTrue();
            await store.RetainAsync(role,"same-id",new JsonObject { ["nativeOriginal"]=role },TestContext.Current.CancellationToken);
            (await store.LookupResultAsync(role,"same-id",TestContext.Current.CancellationToken))!["nativeOriginal"]!.GetValue<string>().ShouldBe(role);
            await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync(role=="executor"?"joint-phase-command":"executor","foreign",new(),TestContext.Current.CancellationToken));
        }
        Should.Throw<ArgumentException>(()=>JointPhaseOriginalStores.Create(transport,execute.Binding("executor"),execute.Binding("executor"),command.Binding("command"),TimeProvider.System));
    }
    private sealed class Routed(Dictionary<string,ReceivingAuthorityFixture> destinations):ISecretsCredentialBoundDaprInvoker
    {
        public Task<string?> InvokeAsync(string app,string method,string body,CancellationToken token)=>throw new InvalidOperationException();
        public Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string body,CancellationToken token)
        { credential.ShouldBe("file:/fixture-"+app);return destinations[app].InvokeCredentialBoundAsync(app,credential,method,body,token); }
    }
}
