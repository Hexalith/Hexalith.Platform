namespace Hexalith.Platform.Composition.Tests;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Shouldly;

public sealed class DomainEnrollmentKitTests
{
    [Fact]
    public void StarterValidatesWithRealTechnicalDeclarations()
    {
        string root = Root();
        string starter = Path.Combine(root, "templates/domain-module-enrollment/hexalith.module-manifest.v2.json");
        string[] manifests = [starter, .. new[] { "EventStore", "Tenants", "Memories", "McpCli" }.Select(name => Path.Combine(root, "references/Hexalith." + name + "/module/hexalith.module-manifest.v2.json"))];
        PlatformManifestValidationResult report = PlatformManifestValidator.Validate(manifests, TestContext.Current.CancellationToken);
        report.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, report.Diagnostics));
        report.Declarations!.Select(declaration => declaration.Declaration.GetProperty("identity").GetProperty("moduleId").GetString()).ShouldContain("sample");
    }

    [Fact]
    public void InventoryAndHumanChecklistKeepUnobservedEvidencePending()
    {
        string kit = Path.Combine(Root(), "templates/domain-module-enrollment");
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(kit, "hosting-inventory.json")));
        JsonElement[] projects = [.. inventory.RootElement.GetProperty("projects").EnumerateArray()];
        projects.Select(project => project.GetProperty("kind").GetString()).ShouldBe(["AppHost", "Aspire", "ServiceDefaults"]);
        foreach (JsonElement project in projects)
        {
            project.GetProperty("state").GetString().ShouldBe("frozen");
            project.GetProperty("owner").GetString().ShouldStartWith("pending");
            project.GetProperty("consumers").GetArrayLength().ShouldBeGreaterThan(0);
            project.GetProperty("retirementPlan").GetString()!.ShouldContain("no retirement authority");
            foreach (JsonProperty gate in project.GetProperty("gates").EnumerateObject()) gate.Value.GetString().ShouldStartWith("pending");
        }
        string checklist = File.ReadAllText(Path.Combine(kit, "sm-2-checklist.md"));
        checklist.ShouldContain("SM-2 unverified");
        checklist.ShouldContain("Story 3.11");
        checklist.ShouldContain("Breakpoint");
        checklist.ShouldContain("runtime effect");
        checklist.ShouldNotContain("[x]");
    }

    [Fact]
    public async Task AcceptedToolPinEqualsSupportedRangeFloorAsync()
    {
        string root = Root();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".config/dotnet-tools.json")));
        using JsonDocument identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "hexalith-platform.json")));
        JsonElement tool = manifest.RootElement.GetProperty("tools").GetProperty("hexalith.builds.module.cli");
        NuGet.Versioning.NuGetVersion version = NuGet.Versioning.NuGetVersion.Parse(tool.GetProperty("version").GetString()!);
        NuGet.Versioning.VersionRange range = NuGet.Versioning.VersionRange.Parse(identity.RootElement.GetProperty("toolVersionRange").GetString()!);
        range.Satisfies(version).ShouldBeTrue();
        range.IsMinInclusive.ShouldBeTrue();
        range.MinVersion.ShouldBe(version);
        tool.GetProperty("rollForward").GetBoolean().ShouldBeFalse();
        tool.GetProperty("commands").EnumerateArray().Select(command => command.GetString()).ShouldBe(["hexalith-module"]);
        string recordedBuilds = (await GitAsync(root, "rev-parse", "HEAD:references/Hexalith.Builds")).Trim();
        string releaseBuilds = (await GitAsync(Path.Combine(root, "references/Hexalith.Builds"), "rev-parse", "v" + version.ToNormalizedString() + "^{commit}")).Trim();
        recordedBuilds.ShouldBe(releaseBuilds, "The Platform gitlink must identify the exact pinned published Builds release.");
    }

    [Theory]
    [InlineData("human")]
    [InlineData("json")]
    public async Task ReleasedPinnedToolValidatesDisposableWorkspaceWithoutWrites(string format)
    {
        string root = Root();
        string temporary = Path.Combine(Path.GetTempPath(), "platform-enrollment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            await GitAsync(temporary, "init", "-q");
            foreach (string name in new[] { "Platform", "Builds", "McpCli", "EventStore", "Tenants", "Memories" })
            {
                string path = Path.Combine(temporary, "references/Hexalith." + name);
                Directory.CreateDirectory(Path.Combine(path, ".config"));
                File.Copy(Path.Combine(root, ".config/dotnet-tools.json"), Path.Combine(path, ".config/dotnet-tools.json"));
                await GitAsync(path, "init", "-q");
                await GitAsync(path, "add", ".");
                await GitAsync(path, "-c", "user.name=Enrollment Test", "-c", "user.email=enrollment@example.invalid", "commit", "-qm", "fixture identity");
                string commit = (await GitAsync(path, "rev-parse", "HEAD")).Trim();
                await GitAsync(temporary, "update-index", "--add", "--cacheinfo", "160000," + commit + ",references/Hexalith." + name);
                File.AppendAllText(Path.Combine(temporary, ".gitmodules"), $"[submodule \"references/Hexalith.{name}\"]\n\tpath = references/Hexalith.{name}\n\turl = https://example.invalid/Hexalith.{name}.git\n");
            }
            Directory.CreateDirectory(Path.Combine(temporary, "module"));
            Directory.CreateDirectory(Path.Combine(temporary, ".config"));
            File.Copy(Path.Combine(root, ".config/dotnet-tools.json"), Path.Combine(temporary, ".config/dotnet-tools.json"));
            string kit = Path.Combine(root, "templates/domain-module-enrollment");
            File.Copy(Path.Combine(kit, "hexalith.module-manifest.v2.json"), Path.Combine(temporary, "module/hexalith.module-manifest.v2.json"));
            JsonNode inventory = JsonNode.Parse(File.ReadAllText(Path.Combine(kit, "hosting-inventory.json")))!;
            foreach (JsonNode? entry in inventory["projects"]!.AsArray())
            {
                entry!["owner"] = "Enrollment fixture owner";
                entry["consumers"] = new JsonArray("none");
                string project = Path.Combine(temporary, entry["path"]!.GetValue<string>());
                Directory.CreateDirectory(Path.GetDirectoryName(project)!);
                File.WriteAllText(project, "<Project />");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs"), "app.MapSubscribeHandler(); // frozen legacy host");
            }
            File.WriteAllText(Path.Combine(temporary, "module/hosting-inventory.json"), inventory.ToJsonString());
            await GitAsync(temporary, "add", ".gitmodules", ".config", "module", "src");
            string before = await GitAsync(temporary, "status", "--porcelain=v1", "--untracked-files=all");
            string[] filesBefore = Directory.GetFiles(temporary, "*", SearchOption.AllDirectories);
            PlatformAdoptionValidationResult staticReport = await PlatformAdoptionValidator.ValidateAsync([Path.Combine(temporary, "module/hexalith.module-manifest.v2.json")], temporary, TestContext.Current.CancellationToken);
            staticReport.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, staticReport.Rows.Where(row => !row.Passed)));
            // Restore the exact Platform pin before this test: dotnet tool restore (from Platform root).
            (int versionCode, string versionOutput, string versionError) = await RunAsync(temporary, "dotnet", "tool", "run", "hexalith-module", "--version");
            versionCode.ShouldBe(0, "Run dotnet tool restore from the Platform root before enrollment acceptance. " + versionError);
            using JsonDocument pin = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".config/dotnet-tools.json")));
            string exactVersion = pin.RootElement.GetProperty("tools").GetProperty("hexalith.builds.module.cli").GetProperty("version").GetString()!;
            string[] versionIdentity = versionOutput.Trim().Split('+');
            versionIdentity.Length.ShouldBe(2);
            versionIdentity[0].ShouldBe(exactVersion);
            versionIdentity[1].ShouldBe((await GitAsync(root, "rev-parse", "HEAD:references/Hexalith.Builds")).Trim());
            (int code, string output, string error) = await RunAsync(temporary, "dotnet", "tool", "run", "hexalith-module", "--", "validate", "--manifest", "module/hexalith.module-manifest.v2.json", "--adoption", "--output", format);
            code.ShouldBe(0, output + error);
            output.ShouldContain("SM-2 remains separately unverified");
            if (format == "json")
            {
                using JsonDocument report = JsonDocument.Parse(output);
                report.RootElement.GetProperty("adoption").GetArrayLength().ShouldBe(9);
            }
            else output.ShouldContain("PASS hosting-inventory");
            (await GitAsync(temporary, "status", "--porcelain=v1", "--untracked-files=all")).ShouldBe(before);
            Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).ShouldBe(filesBefore, ignoreOrder: true);
            File.Delete(Path.Combine(temporary, "module/hosting-inventory.json"));
            (int failedCode, string failedOutput, string failedError) = await RunAsync(temporary, "dotnet", "tool", "run", "hexalith-module", "--", "validate", "--manifest", "module/hexalith.module-manifest.v2.json", "--adoption", "--output", format);
            failedCode.ShouldBe(1, failedOutput + failedError);
            failedOutput.ShouldContain("hosting-inventory");
            if (format == "human") failedOutput.ShouldContain("FAIL hosting-inventory");
            else
            {
                using JsonDocument failedReport = JsonDocument.Parse(failedOutput);
                JsonElement failedRoot = failedReport.RootElement;
                failedRoot.GetProperty("adoptionScope").GetString().ShouldBe(PlatformAdoptionValidator.Scope);
                JsonElement[] failedRows = [.. failedRoot.GetProperty("adoption").EnumerateArray()];
                failedRows.Select(row => row.GetProperty("requirement").GetString()).Distinct().ShouldBe(
                    ["workspace", "declaration", "references", "gitlinks", "nested-references", "tool-pin", "hosting-inventory", "dependency-selectors", "dapr-subscriptions"], ignoreOrder: true);
                foreach (JsonElement row in failedRows)
                {
                    row.GetProperty("file").GetString().ShouldNotBeNullOrWhiteSpace();
                    row.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
                    if (row.GetProperty("requirement").GetString() is not "hosting-inventory" and not "dapr-subscriptions")
                    {
                        row.GetProperty("passed").GetBoolean().ShouldBeTrue();
                    }
                }

                JsonElement hosting = failedRows.Single(row => row.GetProperty("requirement").GetString() == "hosting-inventory");
                hosting.GetProperty("passed").GetBoolean().ShouldBeFalse();
                hosting.GetProperty("file").GetString().ShouldBe("module/hosting-inventory.json");
            }
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "hexalith-platform.json"))) directory = directory.Parent;
        return directory!.FullName;
    }

    private static async Task<string> GitAsync(string directory, params string[] arguments)
    {
        (int code, string output, string error) = await RunAsync(directory, "git", arguments).ConfigureAwait(true);
        code.ShouldBe(0, output + error);
        return output;
    }

    private static async Task<(int Code, string Output, string Error)> RunAsync(string directory, string command, params string[] arguments)
    {
        ProcessStartInfo start = new(command) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (process.ExitCode, await stdout.ConfigureAwait(true), await stderr.ConfigureAwait(true));
    }
}
