using System.Text.Json;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.Composition;

string root = args[0];
string[] owners = ["EventStore", "Tenants", "Memories", "Parties"];
SourceMappingEntry[] entries = owners.Select(name =>
    new SourceMappingEntry("Hexalith." + name, "source", Path.Combine(root, "references", "Hexalith." + name))).ToArray();
PlatformCompositionOptions options = new()
{
    PlatformRoot = root,
    Mapping = new SourceMapping(WorkspaceMode.Source, root, "Hexalith.Platform", entries, false),
    ActiveModule = "parties",
    DeclarationPaths = [Path.Combine(root, "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json")],
};
PlatformApplicationGraph graph = PlatformApplicationGraph.Resolve(options);
JsonElement actual = PlatformHostedPublishing.Describe(graph);
File.WriteAllText(args[1], actual.GetRawText() + Environment.NewLine);
using JsonDocument expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eng/helm-qualification/chart/reference-graph.json")));
bool equal = JsonElement.DeepEquals(actual, expected.RootElement);
Console.WriteLine($"Graph parity: {equal}. Workloads: {string.Join(", ", graph.Workloads.Select(w => w.Identity))}");
return equal ? 0 : 1;
