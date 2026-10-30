namespace Hexalith.Platform.Composition;

using System.Globalization;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Bounds a private package qualification build's actual registry probe to canonical loopback authorities.</summary>
internal static class PlatformLoopbackRegistryProbe
{
    /// <summary>Keeps TLS except for an explicitly compiled qualification build's Development/package loopback registry.</summary>
    internal static IReadOnlyList<string> Arguments(string image, WorkspaceMode mode, bool development)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        int separator = image.IndexOf('/');
        if (mode == WorkspaceMode.Package && development && separator > 0
            && Uri.TryCreate("http://" + image[..separator], UriKind.Absolute, out Uri? registry)
            && registry.UserInfo.Length == 0 && registry.Query.Length == 0 && registry.Fragment.Length == 0)
        {
            string authority = image[..separator];
            string port = registry.Port.ToString(CultureInfo.InvariantCulture);
            if (authority.Equals("localhost:" + port, StringComparison.OrdinalIgnoreCase)
                || authority == "127.0.0.1:" + port || authority == "[::1]:" + port)
                return ["manifest", "inspect", "--insecure", image];
        }
        return ["manifest", "inspect", image];
    }
}
