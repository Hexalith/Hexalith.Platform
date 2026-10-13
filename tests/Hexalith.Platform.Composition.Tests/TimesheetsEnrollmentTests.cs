namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Verifies the offline Timesheets candidate with the shipped Builds validator.</summary>
public sealed class TimesheetsEnrollmentTests
{
    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "apphost.cs")))
            {
                directory = directory.Parent;
            }
            return directory!.FullName;
        }
    }

    private static string CandidatePath => Path.Combine(Root, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json");

    /// <summary>Checks schema validity without treating validation as enrollment.</summary>
    [Fact]
    public void CandidateValidatesAtomicallyWithTheDomainMinimum()
    {
        string[] owners = ["eventstore", "tenants", "memories"];
        string[] paths = [CandidatePath, .. owners.Select(owner => Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations", owner + ".json"))];
        PlatformManifestValidationResult result = PlatformManifestValidator.Validate(paths, TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Field}: {d.Message}")));
        result.Declarations!.Select(d => PlatformDeclarationSet.Id(d.Declaration)).Order().ShouldBe(["eventstore", "memories", "tenants", "timesheets"]);
        JsonElement module = result.Declarations!.Single(d => PlatformDeclarationSet.Id(d.Declaration) == "timesheets").Declaration;
        module.GetProperty("identity").GetProperty("servers").GetArrayLength().ShouldBe(1);
        module.GetProperty("lifecycle").GetProperty("startup").GetProperty("defaultTimeoutSeconds").GetInt32().ShouldBe(600);
        module.GetProperty("runtime").GetProperty("resources")[0].GetProperty("replicas").GetInt32().ShouldBe(1);
        AssertSurfaceBoundaries(module);
    }

    /// <summary>Selects all mandatory domain owners without adding a Timesheets runtime binding.</summary>
    [Fact]
    public void CandidateSelectsTheRequiredClosureOutsideActiveComposition()
    {
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories", "Timesheets" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, "source", Path.Combine(Root, "references", "Hexalith." + name))).ToArray();
        PlatformCompositionOptions options = new()
        {
            PlatformRoot = Root,
            Mapping = new SourceMapping(WorkspaceMode.Source, Root, "Hexalith.Platform", entries, false),
            ActiveModule = "timesheets",
            DeclarationPaths = [CandidatePath],
        };
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(options);
        declarations.Select(d => PlatformDeclarationSet.Id(d.Declaration)).Order().ShouldBe(["eventstore", "memories", "tenants", "timesheets"]);
        PlatformServerBindings.ReferenceProjects.ContainsKey("timesheets/host").ShouldBeFalse();
        PlatformServerBindings.ReferenceImages.ContainsKey("timesheets/host").ShouldBeFalse();
    }

    /// <summary>Exercises the actual embedded schema's atomic refusal of authority claims.</summary>
    [Theory]
    [InlineData("authority-claim")]
    [InlineData("mcp")]
    [InlineData("required-disabled")]
    public void SchemaDriftFailsActualBuildsValidation(string drift)
    {
        JsonNode document = JsonNode.Parse(File.ReadAllText(CandidatePath))!;
        JsonNode module = document["modules"]![0]!;
        if (drift == "authority-claim")
        {
            module["deploymentAuthorized"] = true;
        }
        else if (drift == "mcp")
        {
            module["surfaces"]!["interfaces"]![0]!["protocol"] = "mcp";
        }
        else
        {
            module["identity"]!["servers"]![0]!["enabled"] = false;
        }
        string path = Path.Combine(Path.GetTempPath(), "timesheets-enrollment-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, document.ToJsonString());
            PlatformManifestValidationResult result = PlatformManifestValidator.Validate([path], TestContext.Current.CancellationToken);
            result.IsValid.ShouldBeFalse();
            result.Declarations.ShouldBeNull();
            result.Diagnostics.ShouldNotBeEmpty();
            string rule = drift switch { "authority-claim" => "HXP002", "mcp" => "HXP023", _ => "HXP020" };
            string field = drift switch
            {
                "authority-claim" => "modules[0].deploymentAuthorized",
                "mcp" => "modules[0].surfaces.interfaces[0].protocol",
                _ => "modules[0].identity.servers[0].enabled",
            };
            result.Diagnostics.ShouldContain(d => d.RuleId == rule && d.Field == field);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Confirms the retained plan carries no runtime or acceptance authority.</summary>
    [Fact]
    public void RetainedPlanKeepsPublicAndInternalListenersAndAllGatesClosed()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "eng", "timesheets-deployment", "plan.json")));
        JsonElement plan = document.RootElement;
        plan.GetProperty("deploymentAuthorized").GetBoolean().ShouldBeFalse();
        plan.GetProperty("activationAuthorized").GetBoolean().ShouldBeFalse();
        plan.GetProperty("acceptance").GetString().ShouldBe("FAIL");
        plan.GetProperty("liveEvidenceCollected").GetBoolean().ShouldBeFalse();
        plan.GetProperty("retainedRelease").GetProperty("imageDigest").ValueKind.ShouldBe(JsonValueKind.Null);
        plan.GetProperty("ownership").GetProperty("migrationPrincipalGranted").GetBoolean().ShouldBeFalse();
        JsonElement target = plan.GetProperty("proposedTarget");
        target.GetProperty("clusterContext").GetString().ShouldBe("jpiquot@local");
        target.GetProperty("applicationNamespace").GetString().ShouldBe("hexalith-staging");
        target.GetProperty("dataNamespace").GetString().ShouldBe("hexalith-staging-data");
        target.GetProperty("publicHostname").GetString().ShouldBe("timesheets.hexalith.com");
        JsonElement listeners = plan.GetProperty("listeners");
        listeners.GetProperty("public").GetProperty("port").GetInt32().ShouldBe(8080);
        listeners.GetProperty("internal").GetProperty("port").GetInt32().ShouldBe(8081);
        listeners.GetProperty("internal").GetProperty("routePublished").GetBoolean().ShouldBeFalse();
        listeners.GetProperty("configuration").GetProperty("Timesheets:InternalSurface:AllowOnAnyPort").GetBoolean().ShouldBeFalse();
        plan.GetProperty("componentBindings")[0].GetProperty("configurationKey").ValueKind.ShouldBe(JsonValueKind.Null);
        plan.GetProperty("publicRouteAllowlist").EnumerateArray().ShouldAllBe(route => route.GetProperty("path").GetString() != "/");
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(CandidatePath)));
        plan.GetProperty("declaration").GetProperty("sha256").GetString().ShouldBe(hash);
        plan.GetProperty("prerequisites").EnumerateArray().ShouldAllBe(p => p.GetProperty("status").GetString() == "missing" || p.GetProperty("status").GetString() == "blocked");
    }

    private static void AssertSurfaceBoundaries(JsonElement module)
    {
        string[] prefixes = ["/process", "/replay-state", "/query", "/project", "/admin"];
        JsonElement[] interfaces = module.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray().ToArray();
        foreach (string prefix in prefixes)
        {
            JsonElement surface = interfaces.Single(s => s.GetProperty("routePrefix").GetString() == prefix);
            surface.GetProperty("exposure").GetString().ShouldBe("internal-only");
            surface.GetProperty("server").GetString().ShouldBe("host");
            surface.GetProperty("callers").EnumerateArray().Select(c => c.GetString()).ShouldBe(["eventstore"]);
        }
        interfaces.Where(s => s.GetProperty("exposure").GetString() == "public").ShouldAllBe(s =>
            s.GetProperty("routePrefix").GetString()!.StartsWith("/api/timesheets/magic-links/", StringComparison.Ordinal)
            || s.GetProperty("routePrefix").GetString() == "/metadata/timesheets");
        interfaces.ShouldAllBe(s => s.GetProperty("protocol").GetString() == "http" && s.GetProperty("surfaceClass").GetString() != "agent");
        module.GetProperty("lifecycle").GetProperty("replay").GetProperty("reopenBeforeReplay").GetBoolean().ShouldBeFalse();
    }
}
