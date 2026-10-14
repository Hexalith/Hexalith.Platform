namespace Hexalith.Platform.Composition.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public void FileEntryResolvesDefaultMappingAndComposesSixServers()
    {
        string[] variables = ["HEXALITH_SOURCE_MAPPING_PATH", "HEXALITH_SOURCE_MAPPING_HASH"];
        string?[] original = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            foreach (string variable in variables) Environment.SetEnvironmentVariable(variable, null);
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
                ProjectDirectory = Root, DisableDashboard = true, AllowUnsecuredTransport = true,
            });
            builder.Environment.EnvironmentName = Environments.Development;
            foreach (string key in new[] { "Platform:ActiveModule", "Platform:DeclarationPath", "Platform:SourceMapping:Path", "Platform:SourceMapping:Hash", "Platform:Works:Enabled", "Platform:Agents:Enabled", "Platform:Timesheets:Enabled" })
                builder.Configuration[key] = null;
            PlatformAppModel.ComposeEntry(builder);
            builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any())
                .Select(resource => resource.Name).Order(StringComparer.Ordinal)
                .ShouldBe(new[] { "eventstore", "tenants", "tenants-api", "memories", "memories-access-telemetry-clock", "memories-access-telemetry" }.Order(StringComparer.Ordinal));
            using ServiceProvider provider = builder.Services.BuildServiceProvider();
            provider.GetRequiredService<PlatformCompositionWorkspace>().Dispose();
        }
        finally
        {
            for (int index = 0; index < variables.Length; index++) Environment.SetEnvironmentVariable(variables[index], original[index]);
        }
    }

    [Fact]
    public void MissingSharedComponentNamesThePathBeforeAddingResources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-missing-components-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string file in new[] { "accesscontrol.eventstore.yaml", "statestore.yaml", "pubsub.yaml" })
                File.Copy(Path.Combine(Root, "DaprComponents", "reference", file), Path.Combine(directory, file));
            IDistributedApplicationBuilder builder = Builder();
            FileNotFoundException refusal = Should.Throw<FileNotFoundException>(() => PlatformAppModel.Compose(builder, Options(active: "eventstore") with { ComponentsDirectory = directory }));
            refusal.Message.ShouldContain("Platform shared local Dapr component");
            refusal.Message.ShouldContain(Path.Combine(directory, "configstore.yaml"));
            refusal.FileName.ShouldBe(Path.Combine(directory, "configstore.yaml"));
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }
}
