namespace Hexalith.Platform.Custody;

/// <summary>Independent current dedicated-machine/tenant/resource/method/payload ACL lookup. Missing real enrollment/configuration disables private credentials.</summary>
public interface IPrivateOwnerOperationGrantSource
{
    /// <summary>Authenticates current machine/service-account classification and exact operation grant independently of the presented tag.</summary>
    Task<PrivateOwnerOperationGrant?> ResolveCurrentAsync(string machineIssuer, string subject, string client, PrivateOwnerOperationScope scope, CancellationToken cancellationToken = default);
}
