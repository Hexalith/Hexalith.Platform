using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// Public installation/resource pins. This is never generated or enrolled by application startup.
internal sealed record NativeInstallation(string Backend, string AuthorityComponent, string AuthorityKeyPrefix,
    string CheckpointArchiveDirectory, string OriginalArchiveDirectory, string ArchiveEnrollmentSha256,
    string OpenBaoHttpsUrl, string TransitMount, string CredentialFile,
    Dictionary<string, string> NativeKeyMetadataSha256, string PhysicalQualificationEvidenceSha256,
    string ProducerIncarnation = "", string TransportIncarnation = "", IndependentDaprAuthorityBinding? NativeOriginalAuthority = null);
