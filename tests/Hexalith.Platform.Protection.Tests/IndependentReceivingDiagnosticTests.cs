using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class IndependentReceivingDiagnosticTests
{
    [Theory]
    [InlineData("storage",IndependentReceivingFailure.StorageFailure)]
    [InlineData("handler",IndependentReceivingFailure.HandlerFailure)]
    [InlineData("malformed",IndependentReceivingFailure.MalformedEnrolledState)]
    public async Task ActualIndependentBoundaryUsesFiniteCountersWithoutExceptionOrIdentityContent(string fault,IndependentReceivingFailure expected)
    {
        const string sensitive="fixture-sensitive-credential-tenant-locator-hash-never-a-label";
        using var receiving=new ReceivingAuthorityFixture("executor",handlers:[new Failure(fault,sensitive)]);
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["fixture-diagnostic"] }).ToArray() };
        long before=IndependentReceivingDiagnostics.Count(expected);
        (await receiving.Authority.InvokeAsync<JsonElement>("fixture-diagnostic",new { untrusted=sensitive },TestContext.Current.CancellationToken)).ValueKind.ShouldBe(JsonValueKind.Undefined);
        IndependentReceivingDiagnostics.Count(expected).ShouldBeGreaterThan(before);receiving.Methods.ShouldContain("status:503");
        Enum.GetNames<IndependentReceivingFailure>().Length.ShouldBe(10);
        foreach(var code in Enum.GetNames<IndependentReceivingFailure>()) code.ShouldNotContain(sensitive);
        receiving.Methods.ShouldAllBe(value=>!value.Contains(sensitive,StringComparison.Ordinal));
    }
    [Fact]
    public async Task ActualConditionalAdmissionConflictHasItsOwnContentFreeRecoveryCounter()
    {
        using var receiving=new ReceivingAuthorityFixture("executor");
        long before=IndependentReceivingDiagnostics.Count(IndependentReceivingFailure.ConditionalWriteConflict);
        receiving.BeforeConditionalSave=()=> { receiving.BeforeConditionalSave=null;receiving.RevokeWorkload("recording-owner"); };
        (await receiving.Authority.InvokeAsync<JsonElement>("qualification-begin-original",new { role="executor",requestId="fixture-conflict",original=new { input="fixture-sensitive-never-diagnostic" } },TestContext.Current.CancellationToken)).ValueKind.ShouldBe(JsonValueKind.Undefined);
        IndependentReceivingDiagnostics.Count(IndependentReceivingFailure.ConditionalWriteConflict).ShouldBeGreaterThan(before);
        receiving.Methods.ShouldContain("status:503");
    }
    private sealed class Failure(string fault,string sensitive):IIndependentOwnerOperationHandler
    {
        public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fixture-diagnostic"]);
        public bool IsReadOnly(string method)=>true;
        public Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
            =>Task.FromException<object?>(fault=="storage"?new IOException(sensitive):fault=="malformed"?new JsonException(sensitive):new InvalidOperationException(sensitive));
    }
}
