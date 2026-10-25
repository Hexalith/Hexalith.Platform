using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Selected actual safety-verdict consumer/read protocol, absent in the published production ports. No default or self-reported success exists.</summary>
public interface IQualificationSafetyInvalidationPort
{
    /// <summary>Names unselected native delivery/read/rotation-control inputs before effects.</summary>
    Task<IReadOnlyList<string>> PreflightAsync(QualificationInventoryRotationInput input,CancellationToken cancellationToken);
    /// <summary>Delivers one exact real actor signal under its original operation identity.</summary>
    Task<string?> PublishAsync(PlatformKeyInventorySignal original,CancellationToken cancellationToken);
    /// <summary>Recovers only that signal's actual original consumption receipt after uncertainty.</summary>
    Task<string?> LookupAsync(PlatformKeyInventorySignal original,CancellationToken cancellationToken);
    /// <summary>Reads all actual still-current verdicts for the original tenant/purpose/alias scope.</summary>
    Task<IReadOnlyList<QualificationSafetyVerdict>?> ReadAsync(PlatformKeyVersion scope,CancellationToken cancellationToken);
}
