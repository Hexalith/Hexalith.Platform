namespace Hexalith.Platform.Custody;

/// <summary>One immutable registered wrap reference with restrictive confirmed/pending holds and original physical outcomes; no key material.</summary>
/// <param name="Registration">Original wrap/store proof.</param><param name="Pins">Original confirmed or still-unknown pins.</param><param name="Requests">Immutable original lifecycle requests.</param>
/// <param name="Outcomes">One current original outcome per request; only Unknown can resolve.</param>
public sealed record CustodyKeyLifecycleEntry(CustodyKeyRegistration Registration, IReadOnlyList<CustodyKeyLifecycleRequest> Pins,
    IReadOnlyList<CustodyKeyLifecycleRequest> Requests, IReadOnlyList<CustodyKeyLifecycleOutcome> Outcomes);
