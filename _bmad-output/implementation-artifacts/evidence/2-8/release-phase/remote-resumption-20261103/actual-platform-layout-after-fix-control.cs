// <copyright file="Program.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

string root = Path.GetFullPath(args[0]);
string platform = Path.GetFullPath(args[1]);
string workspace = Path.GetFullPath(args[2]);
SourceMapping mapping = new(WorkspaceMode.Package, root, "Hexalith.Platform", [], true)
{
    PlatformIdentity = new PlatformIdentity(
        "f90e68a0c0305d18dc2cde8d4d4c856aa5a49725",
        "f90e68a0c0305d18dc2cde8d4d4c856aa5a49725",
        "f90e68a0c0305d18dc2cde8d4d4c856aa5a49725",
        platform,
        null,
        "fc743715f7d0993392dee34b2ef9c4eb85cdd399",
        null,
        string.Empty,
        true),
};
mapping = SourceMapping.LoadVerified(Path.Combine(workspace, "source-mapping", "mapping.json"), mapping.ContentHash);
Dictionary<string, string> releaseCommits = new(CompositionDeclarationOptions.DefaultReleaseCommits, StringComparer.Ordinal);
Dictionary<string, string> releaseHashes = new(CompositionDeclarationOptions.DefaultReleaseSnapshotHashes, StringComparer.Ordinal);
foreach (JsonNode? release in JsonNode.Parse(await File.ReadAllTextAsync(args[3]).ConfigureAwait(false))!.AsArray())
{
    if (release!["repository"]!.GetValue<string>() == "Hexalith/Hexalith.Parties")
    {
        continue;
    }

    string key = release["moduleIds"]![0]!.GetValue<string>() + "/" + release["version"]!.GetValue<string>();
    releaseCommits[key] = release["source"]!.GetValue<string>();
    releaseHashes[key] = release["declarationSha256"]!.GetValue<string>();
}

CompositionDeclarationOptions options = new(mapping, [])
{
    PlatformRoot = platform,
    Workspace = workspace,
    ReleaseCommits = releaseCommits,
    ReleaseSnapshotHashes = releaseHashes,
};
try
{
    string[] owners = [.. CompositionDeclarationClosure.Load(options).Select(item => CompositionDeclarationClosure.Id(item.Declaration))];
    Console.WriteLine(JsonSerializer.Serialize(new { result = "loaded", owners, verifiedPendingPr153BindingsSuppliedThroughOptions = true, dependencyTestFilesCopied = false, servicesAllocated = false, acceptedToolDecisionClaimed = false }));
}
catch (InvalidDataException refusal)
{
    Console.WriteLine(JsonSerializer.Serialize(new { result = "refused", message = refusal.Message, dependencyTestFilesCopied = false, servicesAllocated = false, acceptedToolDecisionClaimed = false }));
    return 1;
}

return 0;