namespace Hexalith.Platform.Identity;

/// <summary>Either one complete observation or a refusal. A refusal never carries an observation.</summary>
public sealed record P1ReceiptHttpResult(P1ReceiptHttpFailure Failure, P1ReceiptHttpObservation? Observation)
{
    /// <summary>Whether one complete observation was returned.</summary>
    public bool Succeeded => Failure == P1ReceiptHttpFailure.None && Observation is not null;
}
