using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Installed public lifecycle bindings. Private credential references remain constructor captures.</summary>
public sealed record CustodyLifecycleInstallation(string TenantId, string OwnerScope, string EnrollmentJson,
    string PublicKeyPem, string BindingName, string AuthorizerAppId, string AuthorizerCredentialReference,
    IndependentDaprAuthorityBinding OriginalAuthority, IndependentDaprAuthorityBinding OwnerAuthority);
