using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Restricted actual source append/lookup. No caller-provided stream, event type or generic command is exposed.</summary>
public interface IPrivateRecordingEventStore : ITrustedEnvelopeReplayRegistrar, ISecurityObservationRecorder;
