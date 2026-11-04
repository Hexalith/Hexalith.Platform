namespace Hexalith.Platform.Composition;

using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Builds the private public-package reader against the verified run catalog without mapping the active SDK source into it.</summary>
internal static class PlatformPackageCatalogReader
{
    private const string _task = "Hexalith.Builds.Tooling.Workspace.CatalogVersionValidationTask";

    /// <summary>Checks the complete materialized authority before importing it and retains effective package/asset validation in the reader.</summary>
    internal static IReadOnlyList<string> Prepare(XDocument reader, PlatformCompositionOptions options, string workspace, string build, DateTimeOffset deadlineUtc)
    {
        string mapping = Path.Combine(workspace, "source-mapping", "mapping.json");
        _ = SourceMapping.LoadVerified(mapping, options.Mapping.ContentHash);
        string snapshot = Path.Combine(workspace, "source-mapping", "catalog-versions.json");
        string catalog = Path.Combine(workspace, "source-mapping", "pinned-tree", "Props", "Directory.Packages.props");
        string tooling = typeof(SourceMappingMaterializer).Assembly.Location;
        XElement Validation(bool assets) => new(_task,
            new XAttribute("MappingFile", mapping), new XAttribute("MappingHash", options.Mapping.ContentHash), new XAttribute("CatalogFile", snapshot),
            new XAttribute("ProjectName", "$(MSBuildProjectName)"), new XAttribute("TargetFramework", "$(TargetFramework)"), new XAttribute("RuntimeIdentifier", "$(RuntimeIdentifier)"),
            new XAttribute("Packages", "@(PackageReference);@(GlobalPackageReference)"), new XAttribute("PackageVersions", "@(PackageVersion)"),
            new XAttribute("AssetsFile", "$(ProjectAssetsFile)"), new XAttribute("CheckAssets", assets ? "true" : "false"));
        XElement UsingTask() => new("UsingTask", new XAttribute("TaskName", _task), new XAttribute("AssemblyFile", tooling));
        string preflight = Path.Combine(build, "validate-catalog.proj");
        XElement early = Validation(false);
        early.SetAttributeValue("ProjectName", "Hexalith.Platform.CatalogBuilder");
        new XDocument(new XElement("Project", new XAttribute("DefaultTargets", "Validate"), UsingTask(),
            new XElement("Target", new XAttribute("Name", "Validate"), early))).Save(preflight);
        var validation = CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["msbuild", preflight, "-target:Validate", "-nologo", "-m:1", "-nodeReuse:false"], options.PlatformRoot, null),
            PlatformRoutingCatalog.ReaderTimeout(TimeSpan.FromMinutes(5), deadlineUtc, DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
        string log = Path.Combine(build, "catalog-validation.log");
        File.WriteAllText(log, validation.Output + validation.ErrorOutput);
        if (validation.ExitCode != 0 || validation.TimedOut || validation.OutputTruncated)
            throw new InvalidDataException("The private package reader's pinned catalog validation failed; see " + log + ".");

        string common = Path.Combine(build, "reader-common.props");
        new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", Escape(catalog))),
            new XElement("Import", new XAttribute("Project", "$(MSBuildExtensionsPath)/$(MSBuildToolsVersion)/Microsoft.Common.props")))).Save(common);
        XElement root = reader.Root!;
        root.Add(UsingTask());
        root.Add(new XElement("Target", new XAttribute("Name", "ValidatePrivateReaderCatalogBeforeRestore"),
            new XAttribute("BeforeTargets", "Restore;CollectPackageReferences;_GenerateRestoreGraphProjectEntry;CoreCompile"), Validation(false)));
        root.Add(new XElement("Target", new XAttribute("Name", "ValidatePrivateReaderCatalogAfterRestore"),
            new XAttribute("DependsOnTargets", "ResolvePackageAssets"), new XAttribute("BeforeTargets", "CoreCompile"), Validation(true)));
        string? initial = (string?)root.Attribute("InitialTargets");
        root.SetAttributeValue("InitialTargets", "ValidatePrivateReaderCatalogBeforeRestore" + (string.IsNullOrEmpty(initial) ? "" : ";" + initial));
        return ["-p:AlternateCommonProps=" + common, "-p:ImportDirectoryPackagesProps=false", "-p:DirectoryPackagesPropsPath=" + catalog,
            "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true"];
    }

    private static string Escape(string value) => value.Replace("%", "%25", StringComparison.Ordinal).Replace("$", "%24", StringComparison.Ordinal)
        .Replace("@", "%40", StringComparison.Ordinal).Replace("'", "%27", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal)
        .Replace("?", "%3F", StringComparison.Ordinal).Replace("*", "%2A", StringComparison.Ordinal).Replace("(", "%28", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal);
}
