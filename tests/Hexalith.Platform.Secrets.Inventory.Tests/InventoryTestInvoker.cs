using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

internal sealed class InventoryTestInvoker(PlatformSigningProfile profile) : ISecretsDaprInvoker
{
    public bool Revoked { get; init; }
    public bool Stale { get; init; }
    public bool HostEnrolled { get; init; } = true;
    public string? MutatedEcho { get; init; }

    public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
    {
        if (method == "query-profile")
        {
            return Task.FromResult<string?>(new JsonObject { ["issuer"] = profile.Issuer, ["audience"] = profile.Audience,
                ["version"] = profile.Version, ["notBefore"] = profile.NotBefore.ToString("O"), ["validUntil"] = profile.ValidUntil.ToString("O") }.ToJsonString());
        }
        if (method != "query-hmac-revocation") { return Task.FromResult<string?>(null); }
        JsonObject proof = JsonNode.Parse(jsonBody)!.AsObject();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        proof["status"] = "Current";
        proof["revoked"] = Revoked;
        proof["inventoryHostEnrolled"] = HostEnrolled;
        proof["revision"] = "offline-independent-revocation-revision";
        proof["observedAt"] = now.AddSeconds(Stale ? -40 : -1).ToString("O");
        proof["validUntil"] = now.AddSeconds(Stale ? -10 : 20).ToString("O");
        if (MutatedEcho is not null) { proof[MutatedEcho] = "foreign-binding"; }
        return Task.FromResult<string?>(proof.ToJsonString());
    }
}
