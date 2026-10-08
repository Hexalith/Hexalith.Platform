namespace Hexalith.Platform.Custody;

/// <summary>Current independently resolved private machine enrollment/ACL. Caller-supplied profile or actor address cannot create a grant.</summary>
/// <param name="MachineIssuer">Authenticated transport issuer.</param>
/// <param name="MachineSubject">Current dedicated service-account subject.</param>
/// <param name="MachineClient">Current dedicated authenticated client.</param>
/// <param name="MachineAudience">Exact private transport audience.</param>
/// <param name="Scope">Only this exact owner request is permitted.</param>
/// <param name="AuthorityReference">Independent current enrollment/operation-ACL source.</param>
/// <param name="BindingRevision">Current independently recorded binding revision.</param>
/// <param name="IsDedicatedServiceAccount">Independent classification; never inferred from a name or human/Workflow token.</param>
/// <param name="NotBefore">Inclusive authority validity.</param>
/// <param name="ValidUntil">Exclusive authority validity.</param>
public sealed record PrivateOwnerOperationGrant(string MachineIssuer, string MachineSubject, string MachineClient, string MachineAudience,
    PrivateOwnerOperationScope Scope, string AuthorityReference, long BindingRevision, bool IsDedicatedServiceAccount, DateTimeOffset NotBefore, DateTimeOffset ValidUntil);
