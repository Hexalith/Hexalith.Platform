namespace Hexalith.Platform.Composition;

using System.Reflection;
using Hexalith.Builds.Tooling.Runtime;
using NuGet.Versioning;

/// <summary>Recognizes only the existing published AppHost process that exits before Build/Run during validation.</summary>
internal static class PlatformPartiesValidationContext
{
    /// <summary>Admits a paired AppHost/Tooling build whose shared released version lies in the pinned range.</summary>
    /// <param name="appHostVersion">The executing entry's informational version, <c>&lt;version&gt;+&lt;commit&gt;</c>.</param>
    /// <param name="toolingVersion">The loaded Tooling informational version.</param>
    /// <param name="toolVersionRange">The <c>hexalith-platform.json</c> range, <c>[pin, next major)</c>.</param>
    /// <remarks>Integrity comes from the generated tool lock and the build-output comparison; this rule only refuses split, unparseable or out-of-range pairs.</remarks>
    internal static bool IsPublishedPair(string? appHostVersion, string? toolingVersion, string? toolVersionRange)
        => PairRefusal(appHostVersion, toolingVersion, toolVersionRange) is null;

    /// <summary>Names the versions of a refused AppHost/Tooling pair; null when the pair is admitted.</summary>
    /// <param name="appHostVersion">The executing entry's informational version.</param>
    /// <param name="toolingVersion">The loaded Tooling informational version.</param>
    /// <param name="toolVersionRange">The pinned range.</param>
    internal static string? PairRefusal(string? appHostVersion, string? toolingVersion, string? toolVersionRange)
    {
        if (string.IsNullOrWhiteSpace(appHostVersion) || string.IsNullOrWhiteSpace(toolingVersion))
            return $"The Builds AppHost/Tooling pair has a missing version (AppHost '{appHostVersion}', Tooling '{toolingVersion}').";
        if (!string.Equals(appHostVersion, toolingVersion, StringComparison.Ordinal))
            return $"The Builds AppHost {appHostVersion} and Tooling {toolingVersion} are a split pair.";
        if (!IsPinnedRange(toolVersionRange, out VersionRange? range))
            return $"The tool version range '{toolVersionRange}' is not [pin, next major).";
        if (!NuGetVersion.TryParseStrict(appHostVersion, out NuGetVersion? version))
            return $"The Builds tool version '{appHostVersion}' is unparseable.";
        return range.Satisfies(version) ? null : $"The Builds tool version {appHostVersion} is outside {toolVersionRange}.";
    }

    /// <summary>Requires the exact <c>[pin, next major)</c> shape the version pin files declare.</summary>
    private static bool IsPinnedRange(string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out VersionRange? range)
        => VersionRange.TryParse(value ?? string.Empty, out range) && range.Float is null
            && range.MinVersion is { IsPrerelease: false, HasMetadata: false } minimum && range.IsMinInclusive
            && range.MaxVersion is { } maximum && !range.IsMaxInclusive
            && maximum.ToFullString() == (minimum.Major + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".0.0-0";

    /// <summary>Requires the genuine validation-only entry, its in-range version pair, exact existing arguments and consumed parent environment handoff.</summary>
    /// <param name="plan">The supplied run plan.</param>
    /// <param name="toolVersionRange">The selected Platform identity's tool version range.</param>
    internal static bool IsCurrentPublishedValidation(CompositionRunPlan plan, string? toolVersionRange)
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        Assembly tooling = typeof(CompositionRunPlan).Assembly;
        string[] arguments = Environment.GetCommandLineArgs();
        string document = Path.Combine(plan.Workspace, "plan.json");
        if (entry?.GetName().Name != "Hexalith.Builds.Module.AppHost"
            || arguments.Length != 2 || arguments[0] != entry.Location || arguments[1] != "--validate-platform"
            || Environment.GetEnvironmentVariable(CompositionEnvironment.PlanPath) != document
            || Environment.GetEnvironmentVariable(CompositionEnvironment.RunId) != plan.RunId
            || Environment.GetEnvironmentVariable(CompositionEnvironment.SigningKey) is not null
            || !IsRegularFile(entry.Location) || !IsRegularFile(tooling.Location))
            return false;
        // The genuine validation entry refuses an inadmissible pair by naming its versions.
        string? refusal = PairRefusal(InformationalVersion(entry), InformationalVersion(tooling), toolVersionRange);
        return refusal is null ? true : throw new InvalidDataException(refusal);
    }

    /// <summary>Compares the complete canonical document after projecting only its nonempty absolute claim directory to null.</summary>
    internal static bool IsExactClaimProjection(CompositionRunPlan recorded, CompositionRunPlan supplied)
        => recorded.ResourceClaimStateDirectory is not null && Path.IsPathFullyQualified(recorded.ResourceClaimStateDirectory)
            && supplied.ResourceClaimStateDirectory is null
            && CompositionDocumentStore.Serialize(recorded with { ResourceClaimStateDirectory = null }) == CompositionDocumentStore.Serialize(supplied);

    /// <summary>Reads the embedded <c>version+commit</c> identity of a loaded assembly.</summary>
    internal static string? InformationalVersion(Assembly assembly)
        => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private static bool IsRegularFile(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.LinkTarget is not null) return false;
        for (DirectoryInfo? parent = file.Directory; parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) return false;
        return true;
    }
}
