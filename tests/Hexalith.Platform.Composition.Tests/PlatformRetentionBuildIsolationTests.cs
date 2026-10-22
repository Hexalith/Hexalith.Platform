namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public async Task RetentionProjectArgumentsIsolateAllFourOwnerBuildsAcrossRunsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "retention-build-isolation-" + Guid.NewGuid().ToString("N"));
        HashSet<string> paths = new(StringComparer.Ordinal);
        try
        {
            for (int run = 0; run < 2; run++)
            {
                CompositionRunPlan plan = await BuildIsolationPlanAsync(Path.Combine(workspace, run.ToString(System.Globalization.CultureInfo.InvariantCulture)), selected: true);
                IDistributedApplicationBuilder builder = Builder();
                builder.Environment.EnvironmentName = "Development";
                PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
                ProjectResource[] projects = builder.Resources.OfType<ProjectResource>().OrderBy(resource => resource.Name, StringComparer.Ordinal).ToArray();
                projects.Select(resource => resource.Name).ShouldBe(["eventstore", "memories", "memories-access-telemetry", "memories-access-telemetry-clock"]);
                foreach (ProjectResource project in projects)
                {
                    PlatformServerBinding binding = project.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
                    string owner = Path.Combine(Root, "references", project.Name == "eventstore" ? "Hexalith.EventStore" : "Hexalith.Memories");
                    string expected = Path.Combine(owner, "artifacts", "retention", plan.RunId, "build", project.Name);
                    string[] arguments = await BuildIsolationArgumentsAsync(project);
                    arguments.ShouldBe(["run", "--project", binding.ProjectPath!, "--configuration", "Debug", "--no-launch-profile", "--artifacts-path", expected]);
                    project.Annotations.OfType<ExecutableAnnotation>().Last().Command.ShouldBe("dotnet");
                    Path.IsPathFullyQualified(expected).ShouldBeTrue();
                    Path.GetRelativePath(owner, expected).ShouldBe(Path.Combine("artifacts", "retention", plan.RunId, "build", project.Name));
                    paths.Add(expected).ShouldBeTrue();
                    Directory.Exists(expected).ShouldBeFalse();
                }
            }

            paths.Count.ShouldBe(8);
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonSelectedProjectArgumentsKeepNormalBuildOutputsAsync(bool persistedRun)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "default-build-arguments-" + Guid.NewGuid().ToString("N"));
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            if (persistedRun)
            {
                CompositionRunPlan plan = await BuildIsolationPlanAsync(workspace, selected: false);
                PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), null);
            }
            else PlatformAppModel.Compose(builder, Options(active: "memories"));

            ProjectResource[] projects = builder.Resources.OfType<ProjectResource>().ToArray();
            projects.Length.ShouldBe(4);
            foreach (ProjectResource project in projects)
            {
                PlatformServerBinding binding = project.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
                (await BuildIsolationArgumentsAsync(project)).ShouldBe(["run", "--project", binding.ProjectPath!, "--configuration", "Debug", "--no-launch-profile"]);
            }
        }
        finally
        {
            if (!persistedRun)
            {
                using ServiceProvider cleanup = builder.Services.BuildServiceProvider();
                _ = cleanup.GetService<PlatformCompositionWorkspace>();
            }

            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData("run", "../../outside")]
    [InlineData("run", "/outside")]
    [InlineData("run", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("run", "")]
    [InlineData("resource", "../outside")]
    [InlineData("resource", "/outside")]
    [InlineData("resource", "memories\\outside")]
    [InlineData("resource", "")]
    public void RetentionBuildPathsRefuseInvalidRunOrResourceSegments(string field, string value)
    {
        PlatformCompositionOptions options = Options(active: "memories") with { RunPlan = RetentionPlan() };
        PlatformServerBinding server = PlatformAppModel.Prepare(options).Single(binding => binding.ResourceId == "eventstore");
        if (field == "run") options = options with { RunPlan = options.RunPlan! with { RunId = value } };
        else server = server with { ResourceId = value };

        Should.Throw<InvalidDataException>(() => PlatformAppModel.RetentionArtifactsPath(options, server));
    }

    private static async Task<CompositionRunPlan> BuildIsolationPlanAsync(string workspace, bool selected)
    {
        CompositionRunPlan initial = RetentionPlan();
        SourceMapping mapping = initial.SourceMapping! with { Entries = [.. initial.SourceMapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
        CompositionRunPlan plan = CompositionRunPlanFactory.Create(initial.RunId, workspace, Path.Combine(workspace, "dapr-home"), CompositionPortAllocator.Allocate(3), [], [], mapping,
            initial.PlatformDeclarationPaths, "memories", typeof(PlatformAppModel).Assembly.Location) with { SelectedTest = selected ? initial.SelectedTest : null };
        await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
        return plan;
    }

    private static async Task<string[]> BuildIsolationArgumentsAsync(ProjectResource project)
    {
        List<object> arguments = [];
        foreach (CommandLineArgsCallbackAnnotation callback in project.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
        return arguments.Select(argument => argument.ToString()!).ToArray();
    }
}
