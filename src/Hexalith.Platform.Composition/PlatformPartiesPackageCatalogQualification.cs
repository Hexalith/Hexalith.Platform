namespace Hexalith.Platform.Composition;

using System.Runtime.InteropServices;
using Hexalith.Builds.Tooling.Manifest;

/// <summary>Checks the run-authorized Parties closure and required local topology.</summary>
internal static class PlatformPartiesPackageCatalogQualification
{
    private static readonly string[] _modules = ["eventstore", "memories", "parties", "tenants"];
    private static readonly string[] _servers =
    [
        "eventstore/gateway", "memories/clock", "memories/domain", "memories/server", "memories/telemetry",
        "parties/domain", "tenants/api", "tenants/domain",
    ];

    /// <summary>Checks topology and authenticated declarations before provisioning or probing a registry.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun)
        => Bind(options, declarations, servers, developmentRun, OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture);

    /// <summary>Checks the same guard with the supplied runtime scope.</summary>
    internal static IReadOnlyList<PlatformServerBinding> Bind(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations,
        IReadOnlyList<PlatformServerBinding> servers, bool developmentRun, bool linux, Architecture architecture)
    {
        if (!developmentRun || !linux || architecture != Architecture.X64 || options.PartiesRunProof?.Matches(options) != true
            || options.Projects != PlatformServerBindings.ReferenceProjects || options.Images != PlatformServerBindings.ReferenceImages
            || options.TimesheetsEnabled || options.UsePrebuiltProjects
            || !PlatformPackageCatalogQualification.MatchesTopology(options, declarations, servers, _modules, _servers))
            throw new InvalidDataException("The Parties package profile requires its authenticated runner handoff and declared EventStore, Tenants, Memories and Parties topology.");
        return servers;
    }
}
