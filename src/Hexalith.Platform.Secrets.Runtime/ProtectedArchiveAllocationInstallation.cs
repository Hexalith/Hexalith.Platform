namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Existing independently provisioned private written fixed archive resources. Every identity is public enrollment, never discovered genesis.</summary>
public sealed record ProtectedArchiveAllocationInstallation(string Directory,string EnrollmentSha256,string DataIdentity,string Metadata0Identity,string Metadata1Identity,
    int ChunkBytes,int ChunkCount,int MaximumMetadataBytes,IReadOnlyDictionary<string,string> ArchiveRoots,string StorageProtocol="Ext4PrivateWrittenNoSnapshotsV1");
