using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyLifecyclePublicContractTests
{
    [Fact]
    public void OwnedKeyCannotSerializeAndDisposalClearsBorrowedBackingMemory()
    {
        byte[] bytes = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        string encoded = Convert.ToBase64String(bytes);
        var key = new CustodyReleasedKey(bytes);
        var result = new CustodyKeyUseResult("Released", key);
        JsonSerializer.Serialize(key).ShouldBe("{}");
        JsonSerializer.Serialize(result).ShouldNotContain(encoded);
        ReadOnlyMemory<byte> borrowed = key.Bytes;
        key.Dispose(); key.Dispose();
        borrowed.ToArray().ShouldBe(new byte[32]);
        Should.Throw<ObjectDisposedException>(() => { _ = key.Bytes; });
        JsonSerializer.Serialize(result).ShouldNotContain(encoded);
    }

    [Fact]
    public async Task AuthorizerUsesOnlyCredentialBoundExactValueFreeIntentAndRejectsDuplicateResponseFields()
    {
        var transport = new PrivateTransport();
        var client = new DaprCustodyAuthorizerClient(transport,
            new Dictionary<string, string> { [DaprCustodyAuthorizerClient.LogicalRole] = "independent-authorizer" }, "file:/private/scoped-credential");
        var intent = new CustodyCallIntent("unwrap", "tenant", "owner", "operation", new string('A', 64),
            "{\"Registration\":\"opaque-registration\",\"KeyUse\":\"exact-delivery\"}", "attempt", "challenge");
        var prepared = new CustodyPreparedCall("public-envelope", 4194304, new string('B', 64), new string('C', 64));
        transport.Response = JsonSerializer.Serialize(prepared);
        (await client.PrepareAsync(intent, TestContext.Current.CancellationToken)).ShouldBe(prepared);
        string hash = new('D', 64);
        transport.Response = JsonSerializer.Serialize(new CustodyCallAuthorization(hash, new Dictionary<string, string> { ["custody-operation-proof"] = "signed" }));
        (await client.AuthorizeAsync(intent, prepared, hash, TestContext.Current.CancellationToken))!.RequestSha256.ShouldBe(hash);
        transport.Calls.Count.ShouldBe(2);
        transport.Calls.All(call => call.App == "independent-authorizer" && call.Reference == "file:/private/scoped-credential").ShouldBeTrue();
        transport.Calls[1].Json.ShouldContain(hash);
        transport.Calls[1].Json.ShouldNotContain("RootKey");
        transport.Response = "{\"RequestSha256\":\"one\",\"RequestSha256\":\"two\",\"Metadata\":{}}";
        (await client.AuthorizeAsync(intent, prepared, hash, TestContext.Current.CancellationToken)).ShouldBeNull();
        var missing = new DaprCustodyAuthorizerClient(transport, new Dictionary<string, string>(), "file:/private/scoped-credential");
        int count = transport.Calls.Count;
        (await missing.PrepareAsync(intent, TestContext.Current.CancellationToken)).ShouldBeNull();
        transport.Calls.Count.ShouldBe(count);
    }

    private sealed class PrivateTransport : ISecretsCredentialBoundDaprInvoker
    {
        internal string? Response;
        internal List<(string App, string Reference, string Method, string Json)> Calls = [];
        public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Uncredentialed fallback attempted.");
        public Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add((appId, credentialReference, method, jsonBody));
            return Task.FromResult(Response);
        }
    }
}
