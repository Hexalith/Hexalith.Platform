namespace Hexalith.Platform.Protection;

/// <summary>Closed actual technical operations available to an isolated owner fixture; no expected verdict or synthetic provider operation exists.</summary>
public enum ProtectionQualificationNativeCallKind
{
    /// <summary>Actual field engine and native source transaction.</summary>
    SealEvent,
    /// <summary>Actual trusted reader.</summary>
    UnsealEvent,
    /// <summary>Actual erased replay and persisted projection.</summary>
    ReplayEvent,
    /// <summary>Actual single-root snapshot engine.</summary>
    SealSnapshot,
    /// <summary>Native lifecycle pin/unpin or exact coverage read.</summary>
    Lifecycle,
    /// <summary>Independent exact lifecycle fence authorization without applying an unpin.</summary>
    AuthorizeLifecycle,
    /// <summary>Conditional real guard operation.</summary>
    Guard,
    /// <summary>Actual protection registration.</summary>
    Register,
    /// <summary>Actual same-owner reservation and atomic native provider.</summary>
    Reserve,
    /// <summary>Actual post-dispatch admission block.</summary>
    Block,
    /// <summary>Actual authenticated compromise subscriber and guard mirror.</summary>
    Compromise,
    /// <summary>Actual conditional same-batch activation.</summary>
    Activate,
    /// <summary>Actual native capability signer.</summary>
    Sign,
    /// <summary>Actual exact obsolete-unissued signing outcome.</summary>
    ObsoleteUnissued,
    /// <summary>Exact original owner lookup.</summary>
    Lookup,
    /// <summary>Read actual scoped native source cells into an isolated backup.</summary>
    Backup,
    /// <summary>Restore only original scoped EventStore/source cells; ERC restore is forbidden.</summary>
    Restore,
    /// <summary>Arm an exact isolated native failure boundary.</summary>
    Fault,
}
