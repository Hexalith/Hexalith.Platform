using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent source-service enrollment and read-only resource bindings. No phase owner key or command templates enter this process.</summary>
public sealed record JointPhaseSourceInstallation(string ReceivingInstallationPath,string RuntimeConfigurationFile,QualificationProtectionInstallation Protection,
    IndependentDaprAuthorityBinding ReadOnlyOriginalAuthority,Dictionary<string,JointPhaseNativeSourceScope> SourceScopes,string SourceArchiveDirectory,
    string SourceSigningKeyFile,string SourcePublicKeyFile,CustodyLifecycleInstallation SourceReadAuthority);
