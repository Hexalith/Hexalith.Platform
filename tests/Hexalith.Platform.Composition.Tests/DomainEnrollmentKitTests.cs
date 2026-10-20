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

    [Theory]
    [InlineData("human")]
    [InlineData("json")]
    public async Task CandidateToolValidatesDisposableWorkspaceWithoutWrites(string format)
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
                string project = Path.Combine(temporary, entry!["path"]!.GetValue<string>());
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
            string candidate = Environment.GetEnvironmentVariable("HEXALITH_ENROLLMENT_CANDIDATE") ?? Path.Combine(root, "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/bin/Release/net10.0/Hexalith.Builds.Module.Cli.dll");
            File.Exists(candidate).ShouldBeTrue("Build the Builds Module CLI Release candidate before running enrollment acceptance.");
            (int code, string output) = await RunAsync(temporary, "dotnet", candidate, "validate", "--manifest", "module/hexalith.module-manifest.v2.json", "--adoption", "--output", format);
            code.ShouldBe(0, output);
            output.ShouldContain("SM-2 remains separately unverified");
            if (format == "json")
            {
                using JsonDocument report = JsonDocument.Parse(output);
                report.RootElement.GetProperty("adoption").GetArrayLength().ShouldBe(9);
            }
            else output.ShouldContain("PASS hosting-inventory");
            (await GitAsync(temporary, "status", "--porcelain=v1", "--untracked-files=all")).ShouldBe(before);
            Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).ShouldBe(filesBefore, ignoreOrder: true);
            File.WriteAllText(Path.Combine(temporary, ".config/dotnet-tools.json"), "{}");
            (int failedCode, string failedOutput) = await RunAsync(temporary, "dotnet", candidate, "validate", "--manifest", "module/hexalith.module-manifest.v2.json", "--adoption", "--output", format);
            failedCode.ShouldBe(1, failedOutput);
            failedOutput.ShouldContain("tool-pin");
            if (format == "human") failedOutput.ShouldContain("FAIL tool-pin");
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
        (int code, string output) = await RunAsync(directory, "git", arguments);
        code.ShouldBe(0, output);
        return output;
    }

    private static async Task<(int Code, string Output)> RunAsync(string directory, string command, params string[] arguments)
    {
        ProcessStartInfo start = new(command) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return (process.ExitCode, await stdout + await stderr);
    }
}
