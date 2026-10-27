namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit installed component, public trust and restricted lookup authorizer bindings; never enrollment authority.</summary>
/// <param name="BindingName">Installed Dapr semantic binding.</param>
/// <param name="OwnerPurposeScope">Enrolled lookup scope.</param>
/// <param name="EnrollmentJson">Exact public component enrollment.</param>
/// <param name="PublicKeyPem">Enrolled component response verification key.</param>
/// <param name="AuthorizerAppId">Installed independent authorizer.</param>
/// <param name="AuthorizerCredentialReference">Restricted mounted lookup credential reference.</param>
public sealed record CustodyRegistrationVerifierInstallation(string BindingName, string OwnerPurposeScope, string EnrollmentJson,
    string PublicKeyPem, string AuthorizerAppId, string AuthorizerCredentialReference);
