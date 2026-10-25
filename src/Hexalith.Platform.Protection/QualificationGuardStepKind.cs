using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Closed technical owner operations available to isolated qualification. No approval issuance or general dispatcher operation exists.</summary>
public enum QualificationGuardStepKind
{
    /// <summary>Conditional guard authorize/effect/result, admission, hold, migration or completion transition.</summary>
    Guard,
    /// <summary>Register the exact signed/issued protection batch.</summary>
    Register,
    /// <summary>Irreversible same-owner reserve/consume.</summary>
    Reserve,
    /// <summary>Same-owner integrity cancellation.</summary>
    Block,
    /// <summary>Authenticated private compromise registration.</summary>
    Compromise,
    /// <summary>Replacement-key block-set compare and activation.</summary>
    Activate,
    /// <summary>Exact deterministic signing request.</summary>
    Sign,
    /// <summary>Exact independently proven obsolete-unissued signing result.</summary>
    ObsoleteUnissued,
}
