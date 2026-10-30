using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Protection;

/// <summary>Validates externally authenticated permissions against actual current workload credentials and installed native/API scopes.</summary>
public static class ProtectionQualificationObserverPermissions
{
    /// <summary>Reads the actual configured Dapr client's channel credential, which may differ from a later environment value after construction.</summary>
    internal static string DaprCredential(Dapr.Client.DaprClient client, string appId)
    {
        using var request = client.CreateInvokeMethodRequest(HttpMethod.Post, appId, "platform-independent/v1/erc-read-observer-permissions");
        if (request.Headers.TryGetValues("dapr-api-token", out var requestValues) && requestValues.ToArray() is { Length: 1 } requestTokens) { return requestTokens[0]; }
        using var configured = client.CreateInvokableHttpClient(appId);
        return configured.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var values) && values.ToArray() is { Length: 1 } tokens ? tokens[0] : "";
    }
    /// <summary>Current isolated denial checks must cover every mutation/secret-release surface; a role label supplies no authority.</summary>
    public static bool Matches(ProtectionQualificationObserverPermission proof, ProtectionQualificationServiceScope scope, ErasureRootHostInstallation host,
        string workload, string nativeCredential, string daprCredential, string roleCredential, IReadOnlySet<string> components, DateTimeOffset now)
    {
        string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        string tenantPath = host.Native.HeadMount + "/data/tenants/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope.TenantId)));
        string archivePath = host.Native.HeadMount + "/data/archives/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope.TenantId))) + "/*";
        string[] denied = ["erc-head-write", "erc-archive-write", "transit-encrypt", "transit-decrypt", "transit-rewrap", "transit-rotate", "transit-trim", "dapr-state-write", "dapr-state-transaction", "dapr-invoke-mutation", "dapr-secret-read"];
        return !string.IsNullOrWhiteSpace(workload) && !string.IsNullOrWhiteSpace(nativeCredential) && !string.IsNullOrWhiteSpace(daprCredential) && !string.IsNullOrWhiteSpace(roleCredential)
            && proof.TenantId == scope.TenantId && proof.StoreId == host.Native.StoreId && proof.EnrollmentId == host.Native.EnrollmentId
            && proof.WorkloadIdentity == workload && proof.RoleIdentity == scope.Identity && proof.BindingDigest == scope.BindingDigest
            && proof.NativeCredentialSha256 == Fingerprint(nativeCredential) && proof.DaprCredentialSha256 == Fingerprint(daprCredential) && proof.RoleCredentialSha256 == Fingerprint(roleCredential)
            && proof.CredentialScope == "ReadPersistedStateOnly" && proof.ObservedAt <= now && proof.ValidUntil > now
            && proof.NativeReadPaths is { } paths && paths.ToHashSet(StringComparer.Ordinal).SetEquals([tenantPath, archivePath])
            && proof.DaprReadComponents is { } reads && reads.ToHashSet(StringComparer.Ordinal).SetEquals(components)
            && proof.DaprApiScopes is { } apis && apis.ToHashSet(StringComparer.Ordinal).SetEquals(["state.get", "invoke:" + ErasureRootHostComposition.TenantBindings(host)[scope.TenantId].PrivateAuthority.AppId + "/platform-independent/v1/erc-read-observer-permissions"])
            && proof.DeniedCapabilities is { } denials && denials.ToHashSet(StringComparer.Ordinal).SetEquals(denied) && !string.IsNullOrWhiteSpace(proof.ReceiptId);
    }
}
