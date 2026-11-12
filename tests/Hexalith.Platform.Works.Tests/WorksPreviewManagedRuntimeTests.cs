namespace Hexalith.Platform.Works.Tests;

using System.Text.Json;
using Hexalith.Platform.Composition;
using Shouldly;

/// <summary>Checks finite Development runtime alignment without granting a source, image or native behavior proof.</summary>
/// <param name="output">The actual managed host identity output.</param>
public sealed class WorksPreviewManagedRuntimeTests(ITestOutputHelper output)
{
    private const string Inventory = "Microsoft.NETCore.App 10.0.15 [/runtime/shared/Microsoft.NETCore.App]\n"
        + "Microsoft.NETCore.App 11.0.0 [/runtime/shared/Microsoft.NETCore.App]\n"
        + "Microsoft.AspNetCore.App 10.0.15 [/runtime/shared/Microsoft.AspNetCore.App]\n"
        + "Microsoft.AspNetCore.App 11.0.0 [/runtime/shared/Microsoft.AspNetCore.App]\n";

    /// <summary>The test host must actually use the qualified shared Core and JSON identities.</summary>
    [Fact]
    public void ActualHostUsesQualifiedSharedCoreAndJson()
    {
        WorksPreviewManagedRuntime.Require();
        output.WriteLine("runtime=" + Environment.Version);
        output.WriteLine("core=" + typeof(object).Assembly.Location);
        output.WriteLine("json=" + typeof(JsonElement).Assembly.FullName + "@" + typeof(JsonElement).Assembly.Location);
    }

    /// <summary>Known older installations may coexist without changing the exact current highest Core/ASP.NET Core selection.</summary>
    [Fact]
    public void QualifiedHighestRuntimeAcceptsKnownOlderInstallations()
        => WorksPreviewManagedRuntime.ValidateInventory(Inventory);

    /// <summary>A compatible assembly name cannot authorize a package-local exporter or a different shared runtime.</summary>
    /// <param name="fault">The deliberately unqualified host field.</param>
    [Theory]
    [InlineData("core-version")]
    [InlineData("json-version")]
    [InlineData("json-path")]
    [InlineData("core-path")]
    [InlineData("core-directory")]
    [InlineData("core-family")]
    public void UnqualifiedHostOrPackageJsonRefuses(string fault)
    {
        string core = Path.Combine(Path.GetTempPath(), "works-runtime-control", "shared", "Microsoft.NETCore.App", "11.0.0", "System.Private.CoreLib.dll");
        string json = Path.Combine(Path.GetDirectoryName(core)!, "System.Text.Json.dll");
        Version runtime = new(11, 0, 0);
        Version jsonVersion = new(11, 0, 0, 0);
        WorksPreviewManagedRuntime.ValidateHost(runtime, core, jsonVersion, json);
        if (fault == "core-version") runtime = new(10, 0, 15);
        if (fault == "json-version") jsonVersion = new(10, 0, 0, 0);
        if (fault == "json-path") json = Path.Combine(Path.GetTempPath(), "package-local", "System.Text.Json.dll");
        if (fault == "core-path") core = Path.Combine(Path.GetDirectoryName(core)!, "Other.dll");
        if (fault == "core-directory") core = core.Replace("11.0.0", "12.0.0", StringComparison.Ordinal);
        if (fault == "core-family") core = core.Replace("Microsoft.NETCore.App", "PrivateFramework", StringComparison.Ordinal);
        Should.Throw<InvalidOperationException>(() => WorksPreviewManagedRuntime.ValidateHost(runtime, core, jsonVersion, json));
    }

    /// <summary>LatestMajor remains bounded: absent, lower, future, preview and malformed selected inventories refuse.</summary>
    /// <param name="fault">The deliberately unqualified installed inventory.</param>
    [Theory]
    [InlineData("missing-core")]
    [InlineData("missing-asp")]
    [InlineData("future-core")]
    [InlineData("future-asp")]
    [InlineData("old-core")]
    [InlineData("preview-core")]
    [InlineData("malformed-core")]
    public void UnqualifiedHighestRuntimeInventoryRefuses(string fault)
    {
        string inventory = fault switch
        {
            "missing-core" => string.Join('\n', Inventory.Split('\n').Where(line => !line.StartsWith("Microsoft.NETCore.App", StringComparison.Ordinal))),
            "missing-asp" => string.Join('\n', Inventory.Split('\n').Where(line => !line.StartsWith("Microsoft.AspNetCore.App", StringComparison.Ordinal))),
            "future-core" => Inventory + "Microsoft.NETCore.App 12.0.0 [/runtime/shared/Microsoft.NETCore.App]\n",
            "future-asp" => Inventory + "Microsoft.AspNetCore.App 12.0.0 [/runtime/shared/Microsoft.AspNetCore.App]\n",
            "old-core" => Inventory.Replace("Microsoft.NETCore.App 11.0.0", "Microsoft.NETCore.App 10.0.14", StringComparison.Ordinal),
            "preview-core" => Inventory.Replace("Microsoft.NETCore.App 11.0.0", "Microsoft.NETCore.App 12.0.0-preview.1", StringComparison.Ordinal),
            "malformed-core" => Inventory.Replace("Microsoft.NETCore.App 11.0.0 [", "Microsoft.NETCore.App 11.0.0 ", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        Should.Throw<InvalidOperationException>(() => WorksPreviewManagedRuntime.ValidateInventory(inventory));
    }
}
