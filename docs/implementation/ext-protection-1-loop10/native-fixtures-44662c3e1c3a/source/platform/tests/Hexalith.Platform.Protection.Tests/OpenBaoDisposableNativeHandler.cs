using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Real native API transport through the independently owned network-disabled disposable container; never a simulated response or an external HTTP connection.</summary>
internal sealed class OpenBaoDisposableNativeHandler : HttpMessageHandler
{
    internal const string Image = "quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641";
    internal long ArchiveWriteBytes { get; private set; }
    internal int ArchiveWriteCount { get; private set; }
    internal List<string> PublicStages { get; } = [];
    private readonly OpenBaoNativeCommandChannel _channel;
    private OpenBaoDisposableNativeHandler(string container) { _channel = new(container); }
    internal static async Task<OpenBaoDisposableNativeHandler> OpenAsync(CancellationToken token)
    {
        string container = Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_CONTAINER") ?? string.Empty;
        string nonce = Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_NONCE") ?? string.Empty;
        if (!Regex.IsMatch(container, "^[0-9a-f]{64}$") || !Regex.IsMatch(nonce, "^[0-9a-f]{32}$")) { throw new InvalidOperationException("Owned disposable native input required."); }
        var inspected = await RunAsync(["inspect", container], null, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(inspected.Output); var value = document.RootElement[0];
        var host = value.GetProperty("HostConfig"); var config = value.GetProperty("Config");
        if (inspected.Code != 0 || document.RootElement.GetArrayLength() != 1 || value.GetProperty("Id").GetString() != container
            || config.GetProperty("Image").GetString() != Image || config.GetProperty("Labels").GetProperty("hexalith.ext-protection-1.raft-probe").GetString() != nonce
            || host.GetProperty("NetworkMode").GetString() != "none" || !host.GetProperty("ReadonlyRootfs").GetBoolean()
            || host.GetProperty("LogConfig").GetProperty("Type").GetString() != "none" || config.GetProperty("User").GetString() != "65532:65532"
            || value.GetProperty("Mounts").EnumerateArray().Any(mount => mount.GetProperty("Type").GetString() != "tmpfs")
            || value.GetProperty("NetworkSettings").GetProperty("Ports").EnumerateObject().Any())
        { throw new InvalidOperationException("Disposable native ownership/isolation mismatch."); }
        return new(container);
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        if (!path.StartsWith("/v1/", StringComparison.Ordinal) || !Regex.IsMatch(path, "^/v1/[A-Za-z0-9_/-]+$") || request.Method != HttpMethod.Get && request.Method != HttpMethod.Post && request.Method != HttpMethod.Delete)
        { throw new InvalidOperationException("Closed disposable native request required."); }
        string credential = request.Headers.GetValues("X-Vault-Token").Single();
        string payload = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = await _channel.SendAsync(request.Method.Method, path[4..], credential, payload, cancellationToken).ConfigureAwait(false);
        int status = result.Code == 0 ? 200 : Regex.Match(result.Error, @"Code:\s*([1-5][0-9]{2})") is { Success: true } match ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            : result.Error.Contains("No value found", StringComparison.Ordinal) ? 404 : 503;
        string body = result.Output;
        if (result.Code != 0)
        {
            string[] errors = result.Error.Split('\n').Where(line => line.StartsWith("* ", StringComparison.Ordinal)).Select(line => line[2..].Trim()).ToArray();
            body = JsonSerializer.Serialize(new { errors });
        }
        if (string.IsNullOrWhiteSpace(body)) { status = 204; body = "{}"; }
        string category = path == "/v1/sys/health" ? "health" : path.Contains("/data/tenants/", StringComparison.Ordinal) ? "head"
            : path.Contains("/keys/", StringComparison.Ordinal) ? path.EndsWith("/rotate", StringComparison.Ordinal) ? "key-rotate"
                : path.EndsWith("/config", StringComparison.Ordinal) ? "key-config" : path.EndsWith("/trim", StringComparison.Ordinal) ? "key-trim" : "key-metadata"
            : path.Split('/')[3];
        if (result.Code != 0 && category == "head")
        {
            string error = result.Error.ToLowerInvariant();
            if (error.Contains("too large", StringComparison.Ordinal) || error.Contains("too big", StringComparison.Ordinal)
                || error.Contains("maximum allowed", StringComparison.Ordinal) || error.Contains("max entry", StringComparison.Ordinal)) { category = "head-entry-limit"; }
            else if (error.Contains("no space left", StringComparison.Ordinal)) { category = "head-storage-full"; }
            else if (error.Contains("check-and-set", StringComparison.Ordinal)) { category = "head-cas"; }
            else if (error.Contains("json", StringComparison.Ordinal) || error.Contains("unexpected eof", StringComparison.Ordinal)) { category = "head-json-input"; }
            else if (error.Contains("size", StringComparison.Ordinal) || error.Contains("limit", StringComparison.Ordinal)) { category = "head-value-limit"; }
            else if (error.Contains("permission", StringComparison.Ordinal)) { category = "head-permission"; }
            lock (PublicStages) { PublicStages.Add(request.Method.Method + ":head-bytes-" + Encoding.UTF8.GetByteCount(payload).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + status.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        }
        if (result.Code == 0 && request.Method == HttpMethod.Post && path.Contains("/data/archives/", StringComparison.Ordinal))
        { ArchiveWriteBytes += Encoding.UTF8.GetByteCount(payload); ArchiveWriteCount++; }
        lock (PublicStages) { PublicStages.Add(request.Method.Method + ":" + category + ":" + status.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        return new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _channel.Dispose(); }
        base.Dispose(disposing);
    }
    private static async Task<(int Code, string Output, string Error)> RunAsync(string[] arguments, string? input, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo("docker") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--host"); start.ArgumentList.Add("unix:///var/run/docker.sock"); foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Local native process unavailable.");
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(budget.Token), error = process.StandardError.ReadToEndAsync(budget.Token);
            if (input is not null) { await process.StandardInput.WriteAsync(input.AsMemory(), budget.Token).ConfigureAwait(false); } process.StandardInput.Close();
            await process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            string actual = await output.ConfigureAwait(false), failure = await error.ConfigureAwait(false);
            if (actual.Length > 32 * 1024 * 1024 || failure.Length > 1024 * 1024) { throw new InvalidOperationException("Bounded native response required."); }
            return (process.ExitCode, actual, failure);
        }
        catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); } throw; }
    }
}
