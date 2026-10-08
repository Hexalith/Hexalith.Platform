namespace Hexalith.Platform.Custody;

/// <summary>Only current-version installation/routine rotation and irreversible emergency revocation.</summary>
public enum PlatformKeyInventoryAction
{
    /// <summary>Installs an authenticated version; a prior healthy current version becomes retained.</summary>
    InstallCurrent,
    /// <summary>Irreversibly marks the exact version revoked; retained verifier evidence remains present.</summary>
    Revoke,
}
