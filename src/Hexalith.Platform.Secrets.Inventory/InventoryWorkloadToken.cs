using System.Net.Http.Headers;
using System.Text;

using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Uses only explicitly supplied independent workload credential inputs; there are no registry defaults.</summary>
public sealed class InventoryWorkloadToken(IConfiguration configuration) : IInventoryWorkloadToken
{
    /// <inheritdoc/>
    public bool Configured => Uri.TryCreate(configuration["Inventory:WorkloadTokenEndpoint"], UriKind.Absolute, out Uri? endpoint)
        && endpoint.Scheme == "https" && !string.IsNullOrWhiteSpace(configuration["Inventory:WorkloadClientId"])
        && File.Exists(configuration["Inventory:WorkloadCredentialFile"]) && File.Exists(configuration["Inventory:WorkloadCaFile"]);

    /// <inheritdoc/>
    public async Task<string?> GetAsync(CancellationToken cancellationToken)
    {
        if (!Configured) { return null; }
        using HttpClient client = InventorySources.VerifiedClient(configuration["Inventory:WorkloadCaFile"]!);
        string secret = await File.ReadAllTextAsync(configuration["Inventory:WorkloadCredentialFile"]!, cancellationToken).ConfigureAwait(false);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials",
            ["client_id"] = configuration["Inventory:WorkloadClientId"]!, ["client_secret"] = secret.Trim() });
        using HttpResponseMessage response = await client.PostAsync(configuration["Inventory:WorkloadTokenEndpoint"], body, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576) { return null; }
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] data = new byte[1048577];
        int count = 0;
        while (count < data.Length)
        {
            int read = await stream.ReadAsync(data.AsMemory(count), cancellationToken).ConfigureAwait(false);
            if (read == 0) { return InventorySources.ParseObject(Encoding.UTF8.GetString(data, 0, count))?["access_token"]?.GetValue<string>(); }
            count += read;
        }
        return null;
    }
}
