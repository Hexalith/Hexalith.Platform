#if HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Runtime;

/// <summary>Keeps actual private sidecar access separate from serializable endpoint metadata.</summary>
internal sealed class PlatformNativeInvocationAccess
{
    /// <summary>Captures one actual allocation and its existing API credential without a value-bearing representation.</summary>
    /// <param name="endpoint">The actual observed allocation metadata.</param>
    /// <param name="apiToken">The existing private sidecar API credential.</param>
    internal PlatformNativeInvocationAccess(CompositionNativeHttpEndpoint endpoint, string apiToken)
    {
        Endpoint = endpoint;
        ApiToken = apiToken;
    }

    /// <summary>Gets only the metadata suitable for readiness.</summary>
    internal CompositionNativeHttpEndpoint Endpoint { get; }

    /// <summary>Gets the existing opaque credential only for the private channel or actual owning sidecar send.</summary>
    internal string ApiToken { get; }
}

#endif
