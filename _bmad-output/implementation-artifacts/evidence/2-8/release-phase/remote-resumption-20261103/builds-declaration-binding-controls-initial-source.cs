using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

string evidence = Path.GetFullPath(args[0]);
string catalog = Path.GetFullPath(args[1]);
string root = Directory.CreateTempSubdirectory("story-2-8-released-bindings-").FullName;
List<object> outcomes = [];
try
{
    string snapshots = Path.Combine(root, "src", "Hexalith.Platform.Composition", "Declarations");
    Directory.CreateDirectory(snapshots);
    string catalogCopy = Path.Combine(root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
    Directory.CreateDirectory(Path.GetDirectoryName(catalogCopy)!);
    File.Copy(catalog, catalogCopy);
    JsonObject provenance = [];
    foreach (JsonNode? release in JsonNode.Parse(File.ReadAllText(Path.Combine(evidence, "released-declaration-identities.json")))!.AsArray())
    {
        string repository = release!["repository"]!.GetValue<string>().Split('/')[1];
        if (repository == "Hexalith.Parties") continue;
        string owner = release["moduleIds"]![0]!.GetValue<string>();
        byte[] bytes = File.ReadAllBytes(Path.Combine(evidence, release["snapshotFile"]!.GetValue<string>()));
        File.WriteAllBytes(Path.Combine(snapshots, owner + ".json"), bytes);
        provenance[owner] = new JsonObject
        {
            ["repository"] = repository,
            ["version"] = release["version"]!.GetValue<string>(),
            ["commit"] = release["source"]!.GetValue<string>(),
            ["source"] = "module/hexalith.module-manifest.v2.json",
            ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)),
        };
    }
    string provenancePath = Path.Combine(snapshots, "provenance.json");
    string originalProvenance = provenance.ToJsonString();
    File.WriteAllText(provenancePath, originalProvenance);
    SourceMapping mapping = new(WorkspaceMode.Package, root, "Hexalith.Platform", [], true);
    CompositionDeclarationOptions options = new(mapping, []) { PlatformRoot = root };
    string[] ids = CompositionDeclarationClosure.Load(options).Select(item => CompositionDeclarationClosure.Id(item.Declaration)).Order(StringComparer.Ordinal).ToArray();
    if (!ids.SequenceEqual(new[] { "eventstore", "memories", "tenants" })) throw new InvalidOperationException("Exact released closure was not selected.");
    outcomes.Add(new { name = "exact-released-closure-accepted-by-default-authorities", passed = true });

    provenance["tenants"]!["commit"] = new string('0', 40);
    File.WriteAllText(provenancePath, provenance.ToJsonString());
    RequireRefusal("changed-release-source-refused", "Hexalith.Tenants");
    File.WriteAllText(provenancePath, originalProvenance);

    string eventstore = Path.Combine(snapshots, "eventstore.json");
    byte[] originalSnapshot = File.ReadAllBytes(eventstore);
    File.AppendAllText(eventstore, " ");
    provenance = JsonNode.Parse(originalProvenance)!.AsObject();
    provenance["eventstore"]!["sha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(eventstore)));
    File.WriteAllText(provenancePath, provenance.ToJsonString());
    RequireRefusal("changed-bytes-with-coherent-caller-hash-refused", "Hexalith.EventStore");
    File.WriteAllBytes(eventstore, originalSnapshot);
    File.WriteAllText(provenancePath, originalProvenance);

    XDocument unqualified = XDocument.Load(catalogCopy);
    unqualified.Descendants("HexalithEventStoreVersion").First().Value = "0.0.0-unqualified-fixture";
    unqualified.Save(catalogCopy);
    RequireRefusal("unknown-catalog-release-refused", "Hexalith.EventStore");
    Console.WriteLine(JsonSerializer.Serialize(new { passed = outcomes.Count, failed = 0, controls = outcomes, servicesAllocated = false, packageRuntimeQualification = false }));

    void RequireRefusal(string name, string owner)
    {
        try
        {
            _ = CompositionDeclarationClosure.Load(options);
        }
        catch (InvalidDataException failure) when (failure.Message.Contains(owner, StringComparison.Ordinal))
        {
            outcomes.Add(new { name, passed = true });
            return;
        }
        throw new InvalidOperationException("Required production refusal was not observed: " + name);
    }
}
finally
{
    Directory.Delete(root, recursive: true);
}
