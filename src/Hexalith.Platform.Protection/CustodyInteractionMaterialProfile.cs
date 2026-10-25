using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently pre-admitted native root delivery and tenant digest reference for one occurrence/read-or-write. It supplies no key bytes or closure assertions.</summary>
public sealed record CustodyInteractionMaterialProfile(CustodyKeyRegistration Registration,CustodyKeyUse KeyUse,
    CustodyInteractionDigestReference DigestReference);
