using Hexalith.Platform.Custody;
using System.Text.Json;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyRegistrationPublicationTests
{
    private static CustodyKeyRegistration Original => new(new("tenant", "object", PlatformKeyPurpose.InteractionRootDek,
        "alias", "version", "lifecycle", "store", "contract"), "wrap-original", 7, "kek-original", "opaque-original", "receipt-original");

    [Fact]
    public async Task VerifiedOriginalPersistsAcrossRestartAndLostPublicationAcknowledgement()
    {
        var original = Original;
        var verifier = Substitute.For<ICustodyRegistrationOriginalVerifier>();
        verifier.VerifyAsync(original, Arg.Any<CancellationToken>()).Returns(true);
        using var fixture = new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier);
        Grant(fixture);
        fixture.LoseReply = true;
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(original, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.Values.Single().ShouldBe(original);
        fixture.Restart();
        var locator = new DaprCustodyOriginalLocators(fixture.Client());
        (await locator.FindAsync(original.Identity, TestContext.Current.CancellationToken)).ShouldBe(original);
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(original, TestContext.Current.CancellationToken)).ShouldBe(original);
        fixture.State.Registrations.Count.ShouldBe(1);
        fixture.State.Registrations.Values.Single().ShouldBe(original);
        fixture.Saves.ShouldBe(2);
    }

    [Fact]
    public async Task MissingVerifierOrOriginalAndAllChangedRegistrationFieldsDenyIndexMutation()
    {
        using (var missing = new IndependentAuthorityReceiverTests.Fixture())
        {
            Grant(missing);
            (await new DaprCustodyRegistrationPublisher(missing.Client()).PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBeNull();
            missing.State.Registrations.ShouldBeEmpty(); missing.Saves.ShouldBe(0);
        }
        var verifier = Substitute.For<ICustodyRegistrationOriginalVerifier>();
        verifier.VerifyAsync(Original, Arg.Any<CancellationToken>()).Returns(true);
        using var fixture = new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier); Grant(fixture);
        var publisher = new DaprCustodyRegistrationPublisher(fixture.Client());
        (await publisher.PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBe(Original);
        (await fixture.Client().InvokeAsync<CustodyKeyRegistration>("custody-publish-registration-original",new { original=Original, unexpected="no-index-admission" },TestContext.Current.CancellationToken)).ShouldBeNull();
        foreach (var changed in Changes(Original))
            (await publisher.PublishAsync(changed, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.Values.Single().ShouldBe(Original); fixture.Saves.ShouldBe(1);
        verifier.VerifyAsync(Original, Arg.Any<CancellationToken>()).Returns(false);
        (await publisher.PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Saves.ShouldBe(1);
        // Even a mistaken positive verifier cannot overwrite an already retained identity.
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CancellationToken>()).Returns(true);
        (await publisher.PublishAsync(Original with { WrapReceiptId="substituted" }, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.Values.Single().ShouldBe(Original);
    }

    [Fact]
    public async Task ScopeRoleRevocationAndExpiredCurrentHeadDenyBeforeOriginalVerificationOrDisclosure()
    {
        var verifier = Substitute.For<ICustodyRegistrationOriginalVerifier>();
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(), Arg.Any<CancellationToken>()).Returns(true);
        using var fixture = new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier); Grant(fixture);
        var caller = fixture.State.Callers[0]; var headExpiry = fixture.State.ValidUntil;
        foreach (var denied in new[] { caller with { ScopeReference="foreign" }, caller with { Role="reader" },
            caller with { Revoked=true }, caller with { ValidUntil=DateTimeOffset.UtcNow.AddMinutes(-1) }, caller with { Methods=[] } })
        {
            fixture.State=fixture.State with { Callers=[denied] };
            (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        fixture.State=fixture.State with { Callers=[caller], ValidUntil=DateTimeOffset.UtcNow.AddMinutes(-1) };
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBeNull();
        await verifier.DidNotReceive().VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>());
        fixture.State=fixture.State with { ValidUntil=headExpiry };
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original, TestContext.Current.CancellationToken)).ShouldBe(Original);
        fixture.State=fixture.State with { Callers=[caller with { ScopeReference="foreign" }] };
        (await new DaprCustodyOriginalLocators(fixture.Client()).FindAsync(Original.Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.RevokeCaller();
        (await new DaprCustodyOriginalLocators(fixture.Client()).FindAsync(Original.Identity, TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task CapacityCasLossOutageAndCancellationPreserveOriginalWithoutFabricatedSuccess()
    {
        var verifier=Substitute.For<ICustodyRegistrationOriginalVerifier>();
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>()).Returns(true);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier); Grant(fixture);
        var publisher=new DaprCustodyRegistrationPublisher(fixture.Client());
        for(int i=0;i<1000;i++) fixture.State.Registrations.Add("retained-"+i,Original with { Identity=Original.Identity with { ObjectId="old-"+i }, OperationId="old-wrap-"+i,OpaqueWrappedKeyReference="old-reference-"+i });
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.Count.ShouldBe(1000);fixture.Saves.ShouldBe(0);
        fixture.State=fixture.State with { Registrations=[] };
        fixture.State=fixture.State with { QualificationBindings=new() { ["retained-owner-fact"]=new string('x',32*1024*1024) } };
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.ShouldBeEmpty();fixture.State.QualificationBindings["retained-owner-fact"].Length.ShouldBe(32*1024*1024);fixture.Saves.ShouldBe(0);
        fixture.State=fixture.State with { QualificationBindings=null };
        fixture.RejectHeadPublication=_=>true;
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.ShouldBeEmpty();fixture.Saves.ShouldBe(0);
        fixture.RejectHeadPublication=null;
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>()).Returns(_=>Task.FromException<bool>(new IOException("isolated source unavailable")));
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.ShouldBeEmpty();
        using var cancelled=new CancellationTokenSource();
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>()).Returns(_=> { cancelled.Cancel();return Task.FromCanceled<bool>(cancelled.Token); });
        await Should.ThrowAsync<OperationCanceledException>(()=>publisher.PublishAsync(Original,cancelled.Token));
        fixture.State.Registrations.ShouldBeEmpty();fixture.Saves.ShouldBe(0);
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>()).Returns(true);
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBe(Original);
        fixture.State.Registrations.Values.Single().ShouldBe(Original);
    }

    [Fact]
    public async Task PublicationAdmissionPreservesCompletionReserveAtSerializedHeadBoundary()
    {
        const int absolute=32*1024*1024, admission=absolute-1024*1024;
        foreach(var (size,allowed) in new[] { (admission-1,true),(admission+1,false),(absolute-1,false),(absolute+1,false) })
        {
            var verifier=Substitute.For<ICustodyRegistrationOriginalVerifier>();verifier.VerifyAsync(Original,Arg.Any<CancellationToken>()).Returns(true);
            using var fixture=new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier);Grant(fixture);
            PadPublicationHead(fixture,size,insert:true);
            (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBe(allowed?Original:null);
            fixture.State.Registrations.Count.ShouldBe(allowed?1:0);fixture.Saves.ShouldBe(allowed?1:0);
            if(allowed) JsonSerializer.SerializeToUtf8Bytes(fixture.State,CustodyBindingContract.Json).Length.ShouldBe(size);
            fixture.State.QualificationBindings.ShouldNotBeNull(); // No admission evicts the retained padding fact.
        }
        var duplicateVerifier=Substitute.For<ICustodyRegistrationOriginalVerifier>();duplicateVerifier.VerifyAsync(Original,Arg.Any<CancellationToken>()).Returns(true);
        using var duplicate=new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:duplicateVerifier);Grant(duplicate);
        var publisher=new DaprCustodyRegistrationPublisher(duplicate.Client());
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBe(Original);
        PadPublicationHead(duplicate,admission+512*1024,insert:false);duplicate.Restart();
        (await publisher.PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBe(Original);
        duplicate.State.Registrations.Values.Single().ShouldBe(Original);duplicate.Saves.ShouldBe(2);
        JsonSerializer.SerializeToUtf8Bytes(duplicate.State,CustodyBindingContract.Json).Length.ShouldBe(admission+512*1024);
    }

    [Fact]
    public async Task CancellationAfterDurablePublicationPreservesCallerCancellationAndExactRestartLookup()
    {
        var verifier=Substitute.For<ICustodyRegistrationOriginalVerifier>();verifier.VerifyAsync(Original,Arg.Any<CancellationToken>()).Returns(true);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier);Grant(fixture);
        using var cancelled=new CancellationTokenSource();fixture.AfterRegistrationCommit=()=>cancelled.Cancel();
        var failure=await Should.ThrowAsync<OperationCanceledException>(()=>new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original,cancelled.Token));
        cancelled.IsCancellationRequested.ShouldBeTrue();failure.CancellationToken.IsCancellationRequested.ShouldBeTrue();fixture.State.Registrations.Values.Single().ShouldBe(Original);
        fixture.AfterRegistrationCommit=null;fixture.Restart();
        (await new DaprCustodyOriginalLocators(fixture.Client()).FindAsync(Original.Identity,TestContext.Current.CancellationToken)).ShouldBe(Original);
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBe(Original);
        fixture.State.Registrations.Count.ShouldBe(1);
    }

    private static void PadPublicationHead(IndependentAuthorityReceiverTests.Fixture fixture,int nextSize,bool insert)
    {
        fixture.State=fixture.State with { QualificationBindings=new() { ["retained-padding"]="" } };
        var next=fixture.State with { Revision=fixture.State.Revision+1,Registrations=new(fixture.State.Registrations) };
        if(insert) next.Registrations.Add(CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(Original.Identity,CustodyBindingContract.Json)),Original);
        int padding=nextSize-JsonSerializer.SerializeToUtf8Bytes(next,CustodyBindingContract.Json).Length;
        fixture.State.QualificationBindings["retained-padding"]=new string('x',padding);
        JsonSerializer.SerializeToUtf8Bytes(next,CustodyBindingContract.Json).Length.ShouldBe(nextSize);
    }

    [Fact]
    public async Task RevocationDuringVerificationWinsConditionalPublication()
    {
        var verifier=Substitute.For<ICustodyRegistrationOriginalVerifier>();
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(registrationVerifier:verifier);Grant(fixture);
        verifier.VerifyAsync(Arg.Any<CustodyKeyRegistration>(),Arg.Any<CancellationToken>()).Returns(_=> { fixture.RevokeCaller();return Task.FromResult(true); });
        (await new DaprCustodyRegistrationPublisher(fixture.Client()).PublishAsync(Original,TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.State.Registrations.ShouldBeEmpty();fixture.Saves.ShouldBe(0);
    }

    private static void Grant(IndependentAuthorityReceiverTests.Fixture fixture)
        =>fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="custody-registration-publisher", ScopeReference="tenant",
            Methods=["custody-publish-registration-original","custody-registration-original"] }).ToArray() };

    private static IEnumerable<CustodyKeyRegistration> Changes(CustodyKeyRegistration value)
    {
        yield return value with { OperationId="changed" };yield return value with { ExpectedRevision=value.ExpectedRevision+1 };
        yield return value with { KekVersion="changed" };yield return value with { OpaqueWrappedKeyReference="changed" };yield return value with { WrapReceiptId="changed" };
        foreach(var identity in new[] { value.Identity with { TenantId="foreign" },value.Identity with { ObjectId="changed" },value.Identity with { Purpose=PlatformKeyPurpose.ExportEnvelopeKey },
            value.Identity with { KeyAlias="changed" },value.Identity with { KeyVersion="changed" },value.Identity with { LifecycleVersion="changed" },
            value.Identity with { StoreTarget="changed" },value.Identity with { ContractVersion="changed" } }) yield return value with { Identity=identity };
    }
}
