using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>One custody-lifecycle-v1 call. Lookup sets <see cref="Effect"/> false and must not start destruction.</summary>
/// <param name="Operation">One of pin, unpin, destroy-manifest, or lookup-original.</param>
/// <param name="Effect">False for lookup. True does not authorize a transport body as a receipt.</param>
/// <param name="Lifecycle">Pin, unpin, or root destroy request.</param>
/// <param name="RequestDigest">Caller digest. The binding does not recompute a sorted manifest.</param>
/// <param name="Manifest">Already admitted original, in its supplied order.</param>
internal sealed record CustodyLifecycleBindingCommand(string Operation, bool Effect, CustodyKeyLifecycleRequest? Lifecycle, string RequestDigest, CustodyOriginalManifest? Manifest);
