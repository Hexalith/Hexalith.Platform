namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Finite content-free receiving outcomes. No request, exception or identity text is retained.</summary>
public enum IndependentReceivingFailure
{
    /// <summary>The single acquisition budget elapsed.</summary>
    AcquisitionTimeout,
    /// <summary>All finite receiving owners remain charged.</summary>
    CapacitySaturated,
    /// <summary>The expected current head lost conditional admission.</summary>
    ConditionalWriteConflict,
    /// <summary>Bounded protocol input is malformed.</summary>
    MalformedRequest,
    /// <summary>Enrolled state or configuration is malformed.</summary>
    MalformedEnrolledState,
    /// <summary>Current enrolled configuration or state is incomplete or unavailable.</summary>
    EnrolledStateUnavailable,
    /// <summary>Current independently enrolled authority refused the operation.</summary>
    AuthorityRefused,
    /// <summary>A storage operation failed.</summary>
    StorageFailure,
    /// <summary>An installed handler failed without a safe original result.</summary>
    HandlerFailure,
    /// <summary>The original caller withdrew its request.</summary>
    CallerCancelled,
}
