using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Runs the shipped policy renderer with its adjacent deny fragment, without native operations.</summary>
internal static class OpenBaoShippedPolicyRenderer
{
    internal static async Task ExtractAsync(string directory, CancellationToken token)
    {
        foreach (var resource in new[] { (Name: "ErcShippedBootstrapScript", File: "bootstrap.py"), (Name: "ErcShippedCustodyPolicy", File: "custody-policy.hcl") })
        {
            using var input = typeof(OpenBaoShippedPolicyRenderer).Assembly.GetManifestResourceStream(resource.Name)!;
            await using var output = File.Create(Path.Combine(directory, resource.File));
            await input.CopyToAsync(output, token);
        }
    }

    internal static async Task<IReadOnlyDictionary<string, JsonElement>> RenderAsync(string directory, ErasureRootInstallation installation,
        IReadOnlyDictionary<string, string> scopes, CancellationToken token)
    {
        var start = new ProcessStartInfo("python3") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import json,runpy,sys; m=runpy.run_path(sys.argv[1]); v=json.load(sys.stdin); print(json.dumps({t:{'tenantId':t,'scope':s,'name':m['policy_name'](t,s),'policy':m['render_policy'](v['installation'],t,s)} for t,s in v['scopes'].items()}))");
        start.ArgumentList.Add(Path.Combine(directory, "bootstrap.py"));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Shipped renderer unavailable.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(token);
            var error = process.StandardError.ReadToEndAsync(token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { installation = new { nativeKeys = installation.NativeKeys, headMount = installation.HeadMount, transitMount = installation.TransitMount }, scopes }).AsMemory(), token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(token);
            if (process.ExitCode != 0 || !string.IsNullOrEmpty(await error)) { throw new InvalidOperationException("Shipped renderer refused."); }
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await output) ?? throw new InvalidOperationException("Rendered tenant policies required.");
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
    }
}
