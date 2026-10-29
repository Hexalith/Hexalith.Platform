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
    /// <summary>Actual production read-only exact admission block lookup.</summary>
    LookupBlock,
    /// <summary>Actual production read-only exact activation lookup.</summary>
    LookupActivation,
    /// <summary>Actual native original provider lookup without reserving a successor.</summary>
    LookupProvider,
    /// <summary>Actual trusted snapshot/cache replay and external refusal.</summary>
    ReplaySnapshot,
    /// <summary>Every source-owned canonical/header/identity/manifest negative variant against the actual production owner.</summary>
    RejectSubstitutions,
    /// <summary>Actual same-alias foreign-tenant root issue refusal under the current private grant.</summary>
    RejectForeignRoot,
    /// <summary>Actual private rejected-unissued or issued-but-undispatched replacement reconciliation.</summary>
    ReconcileBlockedReplacement,
    /// <summary>Records each authoritative captured owner Effective before content binding; callers cannot supply the owner census or obligation digest.</summary>
    GuardOwnersEffective,
    /// <summary>Actual registration only after the exact native dispatch committed; a proven hold refusal has no owner effect.</summary>
    RegisterIfDispatched,
    /// <summary>Actual reservation only after the exact native dispatch committed; a proven hold refusal has no owner effect.</summary>
    ReserveIfDispatched,
    /// <summary>Read actual scoped native source cells into an isolated backup.</summary>
    Backup,
    /// <summary>Restore only original scoped EventStore/source cells; ERC restore is forbidden.</summary>
    Restore,
    /// <summary>Arm an exact isolated native failure boundary.</summary>
    Fault,
}
