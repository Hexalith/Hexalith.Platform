using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record CustodyRoleInstallation(string Role, string SignerId, string SigningKeyFile, string AppChannelTokenFile,
    IndependentDaprAuthorityBinding CallerScopeAuthority, IndependentDaprAuthorityBinding OriginalAuthority);
