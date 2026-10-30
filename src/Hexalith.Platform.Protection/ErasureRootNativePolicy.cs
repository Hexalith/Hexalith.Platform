namespace Hexalith.Platform.Protection;

/// <summary>Fresh content-free native transit lifecycle policy; head epoch alone cannot establish absence of rotation or trim.</summary>
public sealed record ErasureRootNativePolicy(int LatestVersion, int MinEncryptionVersion, int MinDecryptionVersion, int MinAvailableVersion,
    bool Derived, bool Exportable, bool AllowPlaintextBackup, bool DeletionAllowed);
