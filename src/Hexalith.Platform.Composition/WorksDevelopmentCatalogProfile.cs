namespace Hexalith.Platform.Composition;

using System.Text.Json;

using Hexalith.EventStore.Aspire;

using Microsoft.Extensions.Configuration;

/// <summary>Owner-controlled Development inputs selected before the canonical Works model is composed.</summary>
/// <remarks>Use existing retained key material, or provision material with an actually new isolated backend.
/// Neither an empty routing cell nor an absent profile proves that a shared business backend is fresh.</remarks>
public sealed record WorksDevelopmentCatalogProfile(string StateStoreComponentPath, DevelopmentRouteCatalogCompositionOptions Catalog, int Replicas = 1,
    string? PubSubComponentPath = null, string? ConfigStoreComponentPath = null)
{
    internal static WorksDevelopmentCatalogProfile Load(IConfiguration configuration, params string[] checkoutRoots)
    {
        string? selected = configuration["Platform:Works:DevelopmentCatalogProfile"];
        if (string.IsNullOrWhiteSpace(selected))
            throw new InvalidOperationException("Works Development requires Platform:Works:DevelopmentCatalogProfile. Restore the private profile for retained state or choose an owner-provisioned isolated profile. The default external dapr-init Redis may contain business receipts; startup cannot replace its digest keys.");
        RequirePrivateFile(selected, checkoutRoots);
        var profile = JsonSerializer.Deserialize<WorksDevelopmentCatalogProfile>(File.ReadAllBytes(selected))
            ?? throw new InvalidOperationException("The Works Development owner profile is invalid.");
        string ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(selected))!;
        if (profile.Catalog is null || profile.Replicas is < 1 or > 2
            || profile.PubSubComponentPath is not { Length: > 0 } pubSub || profile.ConfigStoreComponentPath is not { Length: > 0 } configStore
            || !Path.IsPathFullyQualified(profile.Catalog.CustodyDirectory)
            || !Path.GetFullPath(profile.Catalog.CustodyDirectory).StartsWith(ownerDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Works Development requires one or two independently enrolled replicas, explicit state/pubsub/config components and custody inside its private owner profile directory.");
        foreach (string component in new[] { profile.StateStoreComponentPath, pubSub, configStore })
        {
            RequirePrivateFile(component, checkoutRoots);
            if (!Path.GetFullPath(component).StartsWith(ownerDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("All selected Works Development components must be retained inside the private owner profile directory.");
        }
        return profile;
    }

    private static void RequirePrivateFile(string path, IReadOnlyList<string> checkouts)
    {
        if (!Path.IsPathFullyQualified(path) || checkouts.Any(root => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new InvalidOperationException("The Works Development owner profile and component must reside outside all selected checkouts.");
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 1024 * 1024 || file.LinkTarget is not null || (file.Attributes & FileAttributes.ReparsePoint) != 0
            || !OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("The Works Development owner profile and component must be bounded, regular owner-private files.");
    }
}
