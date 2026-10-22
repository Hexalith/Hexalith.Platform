namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Shouldly;

/// <summary>Owns synthetic declaration files and their physical prerequisites; never represents live owner acceptance.</summary>
internal sealed class PlatformOwnerFixture : IDisposable
{
    private PlatformOwnerFixture(string root, string manifest)
    {
        Root = root;
        Manifest = manifest;
    }

    /// <summary>Gets the isolated owner root.</summary>
    public string Root { get; }

    /// <summary>Gets the copied declaration path.</summary>
    public string Manifest { get; }

    /// <summary>Copies an owner declaration with a root marker, physical declared entries and server projects.</summary>
    public static PlatformOwnerFixture Copy(string platformRoot, string module, string? rootDirectory = null)
    {
        string repository = PlatformServerBindings.Repository(module);
        string original = Path.Combine(platformRoot, "references", repository);
        string root = rootDirectory ?? Path.Combine(Path.GetTempPath(), "platform-synthetic-owner-" + Guid.NewGuid().ToString("N"));
        string manifest = Path.Combine(root, "module", "hexalith.module-manifest.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.Copy(Path.Combine(original, "module", "hexalith.module-manifest.v2.json"), manifest);
        MaterializeDeclaredFiles(root, JsonNode.Parse(File.ReadAllText(manifest))!);
        foreach (var project in PlatformServerBindings.ReferenceProjects.Where(pair => pair.Key.StartsWith(module + "/", StringComparison.Ordinal)))
        {
            string target = Path.Combine(root, project.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(original, project.Value), target, true);
        }
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate([manifest]);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return new PlatformOwnerFixture(root, manifest);
    }

    /// <summary>Creates a minimal unregistered owner for composition-only enrollment assertions.</summary>
    public static PlatformOwnerFixture CreateSample(string platformRoot, string module)
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-synthetic-enrollment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string sample = File.ReadAllText(Path.Combine(platformRoot, "references", "Hexalith.Builds", "test", "fixtures", "module", "platform", "valid.json"));
        JsonNode document = JsonNode.Parse(sample.Replace("sample", module, StringComparison.Ordinal))!;
        document["modules"]![0]!["lifecycle"]!["tasks"] = new JsonArray();
        document["modules"]![0]!["lifecycle"]!["recoveryHooks"] = new JsonArray();
        document["modules"]![0]!["lifecycle"]!["fenceHooks"] = new JsonArray();
        MaterializeDeclaredFiles(root, document);
        string manifest = Path.Combine(root, "module.json");
        File.WriteAllText(manifest, document.ToJsonString());
        File.WriteAllText(Path.Combine(root, "server.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        return new PlatformOwnerFixture(root, manifest);
    }

    /// <summary>Materializes only test placeholders needed for static declaration validation.</summary>
    public static void MaterializeDeclaredFiles(string root, JsonNode document)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Hexalith.Builds.slnx"), "<Solution />");
        void Visit(JsonNode? node)
        {
            if (node is JsonObject properties)
            {
                foreach (var property in properties)
                {
                    if (property.Key is "entry" or "executable" && property.Value is JsonValue value && value.TryGetValue(out string? relative))
                    {
                        string path = Path.GetFullPath(Path.Combine(root, relative));
                        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            throw new InvalidDataException("Synthetic declared file escapes its fixture root: " + relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        if (!File.Exists(path)) File.WriteAllText(path, path.EndsWith(".csproj", StringComparison.Ordinal) ? "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>" : "# synthetic declaration prerequisite\n");
                    }
                    else Visit(property.Value);
                }
            }
            else if (node is JsonArray children) foreach (JsonNode? child in children) Visit(child);
        }
        Visit(document);
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Root, true);
}
