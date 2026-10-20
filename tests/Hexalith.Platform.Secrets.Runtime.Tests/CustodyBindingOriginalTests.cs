using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyBindingOriginalTests
{
    [Theory]
    [InlineData("pre-cancelled")]
    [InlineData("final-cancelled")]
    [InlineData("final-expired")]
    public async Task CompletedTasksCannotBypassCancellationOrDeadlineBeforeConsumed(string fault)
    {
        var request = CustodyBindingFixture.Manifest();
        var (header, bytes) = CustodyBindingFixture.Header(request);
        var pages = new CustodyBindingFixture.Pages(header, bytes);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var authority = new CustodyBindingFixture.Authority(); // current permission stays true in every case
        if (fault == "pre-cancelled") { cancelled.Cancel(); }
        if (fault == "final-cancelled") { authority.OnVerifyOriginal = cancelled.Cancel; }
        if (fault == "final-expired") { authority.OnVerifyOriginal = () => Thread.Sleep(150); }
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority,
            fault == "final-expired" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(5), cancelled.Token);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown); result.Original.ShouldBeNull(); result.ExactOriginalBytes.ShouldBeNull();
        authority.Allowed.ShouldBeTrue();
        if (fault == "pre-cancelled") { pages.Reads.ShouldBe(0); }
        else { pages.Reads.ShouldBeGreaterThan(0); }
    }

    [Fact]
    public async Task ValidPageHashesCannotSubstituteForTheWholeOriginalHash()
    {
        var request = CustodyBindingFixture.Manifest();
        var (header, bytes) = CustodyBindingFixture.Header(request);
        header = header with { OriginalSha256 = new string('B', 64) };
        var pages = new CustodyBindingFixture.Pages(header, bytes); // header identity and every page hash are valid
        bool terminalVerifierCalled = false;
        var authority = new CustodyBindingFixture.Authority { OnVerifyOriginal = () => terminalVerifierCalled = true };
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown); result.Original.ShouldBeNull(); result.ExactOriginalBytes.ShouldBeNull();
        pages.Reads.ShouldBeGreaterThan(0); terminalVerifierCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task OnlyCompleteAuthenticatedOriginalVectorAndSignatureBecomeConsumed(int count)
    {
        var request = CustodyBindingFixture.Manifest(count);
        var (header, bytes) = CustodyBindingFixture.Header(request, CustodyBindingFixture.Original(request, 400));
        var pages = new CustodyBindingFixture.Pages(header, bytes);
        var authority = new CustodyBindingFixture.Authority();
        var assembled = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        assembled.State.ShouldBe(DeletionManifestProviderState.Consumed);
        assembled.ExactOriginalBytes.ShouldBe(bytes);
        assembled.Original!.Result.TargetReceipts.Select(r => r.Target).ShouldBe(request.Manifest!.Original.Targets);
        assembled.Original.Signature.ShouldBe(new byte[] { 7, 8, 9 });
        pages.Reads.ShouldBe((bytes.Length + CustodyBindingContract.MaximumPageBytes - 1) / CustodyBindingContract.MaximumPageBytes);
        authority.PageReads.ShouldBe(pages.Reads);
        if (count == 1000) { pages.Reads.ShouldBeGreaterThan(1); }
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("reference")]
    [InlineData("request")]
    [InlineData("reservation")]
    [InlineData("manifest")]
    [InlineData("count")]
    [InlineData("hole")]
    [InlineData("overlap")]
    [InlineData("truncated")]
    [InlineData("oversized-page")]
    [InlineData("oversized-frame")]
    [InlineData("tamper")]
    [InlineData("hash")]
    [InlineData("unavailable")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    [InlineData("authorization")]
    [InlineData("signature")]
    public async Task InvalidMissingOrUnauthenticatedPagesRemainUnknownWithoutPartialVector(string vector)
    {
        var request = CustodyBindingFixture.Manifest(2);
        var (header, bytes) = CustodyBindingFixture.Header(request);
        var pages = new CustodyBindingFixture.Pages(header, bytes);
        var authority = new CustodyBindingFixture.Authority();
        pages.Change = page => vector switch
        {
            "authority" => page with { Header = header with { Authority = header.Authority with { AuthorityIncarnation = "other" } } },
            "reference" => page with { Header = header with { OriginalResultReference = "other" } },
            "request" => page with { Header = header with { RequestDigest = new string('B', 64) } },
            "reservation" => page with { Header = header with { ReservationReceiptId = "other" } },
            "manifest" => page with { Header = header with { ManifestDigest = new string('B', 64) } },
            "count" => page with { Header = header with { TargetCount = 1 } },
            "hole" => page with { Offset = page.Offset + 1 },
            "overlap" => page with { Offset = page.Offset - 1 },
            "truncated" => page with { Data = page.Data[..^1] },
            "oversized-page" => page with { Data = new byte[CustodyBindingContract.MaximumPageBytes + 1] },
            "tamper" => page with { Data = Enumerable.Repeat((byte)33, page.Data.Length).ToArray() },
            "hash" => page with { PageSha256 = new string('B', 64) },
            _ => page,
        };
        if (vector == "oversized-frame") { pages.Frame = _ => new byte[CustodyBindingContract.MaximumPageFrameBytes]; }
        if (vector == "unknown-field") { pages.Frame = p => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(p.Span).Insert(1, "\"RawKey\":\"forbidden\",")); }
        if (vector == "duplicate-field") { pages.Frame = p => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(p.Span).Insert(1, "\"Offset\":42,")); }
        if (vector == "unavailable") { pages.Unavailable = true; }
        if (vector == "authorization") { authority.Allowed = false; }
        if (vector == "signature") { authority.OriginalAllowed = false; }
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown);
        result.Original.ShouldBeNull(); result.ExactOriginalBytes.ShouldBeNull();
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("vector")]
    [InlineData("signature")]
    [InlineData("restore")]
    [InlineData("key-field")]
    public async Task MatchingWholeHashDoesNotAuthenticateMalformedOriginal(string vector)
    {
        var request = CustodyBindingFixture.Manifest(2);
        var original = CustodyBindingFixture.Original(request);
        original = vector switch
        {
            "identity" => original with { Result = original.Result with { TenantId = "other" } },
            "vector" => original with { Result = original.Result with { TargetReceipts = original.Result.TargetReceipts.Reverse().ToArray() } },
            "signature" => original with { Signature = [1] },
            "restore" => original with { RestoreBarrierReceiptId = "" },
            _ => original,
        };
        var (header, bytes) = CustodyBindingFixture.Header(request, original);
        if (vector == "key-field")
        {
            bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Insert(1, "\"RawKey\":\"forbidden\","));
            header = header with { TotalBytes = bytes.Length, OriginalSha256 = CustodyBindingContract.Hash(bytes) };
        }
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, new CustodyBindingFixture.Pages(header, bytes),
            new CustodyBindingFixture.Authority(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown); result.Original.ShouldBeNull();
    }

    [Fact]
    public async Task CurrentAuthorizationTimeoutAndInterruptedAssemblyCannotExposePartialConsumed()
    {
        var request = CustodyBindingFixture.Manifest(1000);
        var (header, bytes) = CustodyBindingFixture.Header(request, CustodyBindingFixture.Original(request, 400));
        var authority = new CustodyBindingFixture.Authority();
        var pages = new CustodyBindingFixture.Pages(header, bytes);
        pages.Change = p => { authority.Allowed = false; return p; };
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown); pages.Reads.ShouldBe(1);
        authority.Allowed = true;
        result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, new NeverPages(), authority, TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown);
        header = header with { TotalBytes = CustodyBindingContract.MaximumOriginalBytes + 1 };
        result = await CustodyBindingOriginalAssembler.AssembleAsync(request, header, pages, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown);
    }

    private sealed class NeverPages : ICustodyBindingOriginalPageSource
    {
        public Task<ReadOnlyMemory<byte>?> ReadAsync(CustodyBindingPageReadV1 exactRead, CancellationToken cancellationToken)
            => new TaskCompletionSource<ReadOnlyMemory<byte>?>().Task;
    }
}
