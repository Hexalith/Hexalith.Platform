using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Independent witness outside the restored volume. A null witness is not this type.</summary>
internal interface ICustodyOutsideWitness
{
    /// <summary>False during witness outage. Outage is not authoritative absence.</summary>
    bool Available { get; }

    /// <summary>True only when the witness already stored this exact outcome. The outcome argument is not evidence.</summary>
    bool Confirms(CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome outcome);
}
