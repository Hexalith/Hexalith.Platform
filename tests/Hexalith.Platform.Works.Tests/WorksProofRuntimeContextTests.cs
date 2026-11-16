using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.Platform.Composition;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Platform.Works.Tests;

/// <summary>Checks the immutable, explicit proof dependency context without launching a topology.</summary>
public sealed class WorksProofRuntimeContextTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("works-proof-runtime-").FullName;

    /// <summary>An ordinary composition does not acquire proof build properties.</summary>
    [Fact]
    public void NoExplicitManifestAddsNoProofContext()
        => WorksProofRuntimeContext.Load(null, _root).ShouldBeNull();

    /// <summary>Only the declared fixture properties are added to the existing selected launch.</summary>
    [Fact]
    public async Task DeclaredContextPreservesTheActualSelectedLaunchAsync()
    {
        string manifest = Fixture();
        var context = WorksProofRuntimeContext.Load(manifest, _root).ShouldNotBeNull();
        string project = Path.Combine(_root, "Selected.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var selected = builder.AddProject("selected", project);
#pragma warning disable ASPIREEXTENSION001
        var launch = (ProjectLaunchConfiguration)await selected.Resource.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
#pragma warning restore ASPIREEXTENSION001
        launch.BuildEnvironment = new Dictionary<string, string> { ["ExistingOwnerInput"] = "preserved" };
        context.Configure(launch);
        launch.ProjectPath.ShouldBe(project);
        launch.BuildEnvironment["ExistingOwnerInput"].ShouldBe("preserved");
        launch.BuildEnvironment["CustomAfterMicrosoftCommonTargets"].ShouldBe(Targets);
        launch.BuildEnvironment["HexalithWorksProofAssembly"].ShouldBe(Assembly);
        context.Verify();
    }

    /// <summary>Retained enrollment can admit only a byte-identical copy of the explicitly bound fixture DLL.</summary>
    [Fact]
    public void OnlyTheBoundFixtureAssemblyMatchesDeployment()
    {
        var context = WorksProofRuntimeContext.Load(Fixture(), _root).ShouldNotBeNull();
        string deployed = Path.Combine(Directory.CreateDirectory(Path.Combine(_root, "deployment")).FullName,
            "Hexalith.Works.IntegrationTests.dll");
        File.Copy(Assembly, deployed);
        context.MatchesProofAssembly(deployed).ShouldBeTrue();
        context.MatchesConsumerArtifact(Assembly).ShouldBeTrue();
        context.MatchesConsumerArtifact(deployed).ShouldBeFalse();
        string foreign = Path.Combine(_root, "Foreign.dll");
        File.Copy(Assembly, foreign);
        context.MatchesProofAssembly(foreign).ShouldBeFalse();
        File.AppendAllText(deployed, "changed");
        context.MatchesProofAssembly(deployed).ShouldBeFalse();
    }

    /// <summary>The actual prestart event refuses changed inputs only for explicitly enrolled resources.</summary>
    [Fact]
    public async Task EnrolledResourceRechecksOriginalInputsAtTheActualPrestartBoundaryAsync()
    {
        var context = WorksProofRuntimeContext.Load(Fixture(), _root).ShouldNotBeNull();
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var selected = builder.AddResource(new ProjectResource("enrolled"));
        var other = builder.AddResource(new ProjectResource("unrelated"));
        context.ObserveStarts(builder, [selected.Resource]);
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(selected.Resource, services),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        File.AppendAllText(Targets, "<!-- changed after preparation -->");
        await builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(other.Resource, services),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        var refusal = await Should.ThrowAsync<InvalidOperationException>(() => builder.Eventing.PublishAsync(
            new BeforeResourceStartedEvent(selected.Resource, services), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        refusal.Message.ShouldContain("original Works proof runtime");
    }

    /// <summary>Every original input and the full file set remain bound after preparation and before startup.</summary>
    /// <param name="change">The changed original input.</param>
    [Theory]
    [InlineData("manifest")]
    [InlineData("targets")]
    [InlineData("assembly")]
    [InlineData("dependency")]
    [InlineData("missing")]
    [InlineData("added")]
    public void ChangedOriginalInputsAreRefused(string change)
    {
        string manifest = Fixture();
        var context = WorksProofRuntimeContext.Load(manifest, _root).ShouldNotBeNull();
        switch (change)
        {
            case "manifest": File.AppendAllText(manifest, " "); break;
            case "targets": File.AppendAllText(Targets, "<!-- changed -->"); break;
            case "assembly": File.AppendAllText(Assembly, "changed"); break;
            case "dependency": File.AppendAllText(Path.Combine(Consumer, "Dependency.dll"), "changed"); break;
            case "missing": File.Delete(Path.Combine(Consumer, "Dependency.dll")); break;
            case "added": File.WriteAllText(Path.Combine(Consumer, "Added.dll"), "added"); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Should.Throw<InvalidOperationException>(context.Verify).Message.ShouldContain("changed");
    }

    /// <summary>A different target or incomplete inventory cannot establish an initial context.</summary>
    /// <param name="change">The malformed manifest input.</param>
    [Theory]
    [InlineData("foreign-target")]
    [InlineData("assembly-digest")]
    [InlineData("missing-runtime")]
    [InlineData("extra-field")]
    public void MalformedContextIsRefused(string change)
    {
        string manifest = Fixture();
        JsonObject value = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        switch (change)
        {
            case "foreign-target": value["targetsPath"] = Path.Combine(_root, "foreign.targets"); break;
            case "assembly-digest": value["assemblySha256"] = new string('0', 64); break;
            case "missing-runtime": value["files"]!.AsObject().Remove(Path.ChangeExtension(Assembly, ".runtimeconfig.json")); break;
            case "extra-field": value["extra"] = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        File.WriteAllText(manifest, value.ToJsonString());
        Should.Throw<InvalidOperationException>(() => WorksProofRuntimeContext.Load(manifest, _root));
    }

    private string Targets => Path.Combine(_root, "tests", "Hexalith.Works.IntegrationTests", "WorksPlatformProofRuntime.targets");
    private string Consumer => Path.Combine(_root, "consumer");
    private string Assembly => Path.Combine(Consumer, "Hexalith.Works.IntegrationTests.dll");
    private string Fixture()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Targets)!);
        File.WriteAllText(Targets, "<Project />");
        Directory.CreateDirectory(Consumer);
        foreach (string path in new[] { Assembly, Path.ChangeExtension(Assembly, ".deps.json"),
            Path.ChangeExtension(Assembly, ".runtimeconfig.json"), Path.Combine(Consumer, "Dependency.dll") })
            File.WriteAllText(path, Path.GetFileName(path));
        string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        string manifest = Path.Combine(_root, "manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            schema = "hexalith.works-proof-runtime.v1", targetsPath = Targets, targetsSha256 = Hash(Targets),
            assemblyPath = Assembly, assemblySha256 = Hash(Assembly),
            files = Directory.EnumerateFiles(Consumer).ToDictionary(path => path, Hash, StringComparer.Ordinal),
        }));
        return manifest;
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
