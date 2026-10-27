using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed value-free registration publication request.</summary>
/// <param name="original">The exact retained wrap original.</param>
internal sealed record CustodyRegistrationPublication(CustodyKeyRegistration original);
