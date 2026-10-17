namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Preserves SDK readiness status when its HTTP contract returns 200 for Degraded.</summary>
public sealed class SdkReadinessHealthCheck(EndpointReference endpoint, IHttpClientFactory clients) : IHealthCheck
{
    /// <summary>Attaches the SDK readiness response to the Platform resource's health.</summary>
    /// <param name="builder">The Platform builder.</param>
    /// <param name="resource">The resource exposing the SDK readiness route.</param>
    public static void Attach(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> resource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        string name = resource.Resource.Name + "/ready";
        builder.Services.AddHttpClient("platform-sdk-readiness", client => client.Timeout = TimeSpan.FromSeconds(5));
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(name,
            services => new SdkReadinessHealthCheck(resource.GetEndpoint("http"), services.GetRequiredService<IHttpClientFactory>()),
            HealthStatus.Unhealthy, tags: null));
        resource.WithHealthCheck(name);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        CancellationToken boundedToken = deadline.Token;
        try
        {
            string? address = await endpoint.GetValueAsync(boundedToken).ConfigureAwait(false);
            using HttpClient client = clients.CreateClient("platform-sdk-readiness");
            using HttpResponseMessage response = await client.GetAsync(new Uri(new Uri(address!), "/ready"),
                HttpCompletionOption.ResponseHeadersRead, boundedToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HealthCheckResult.Unhealthy("SDK readiness transport refused admission.");
            if (response.Content.Headers.ContentLength is > 65536)
                return HealthCheckResult.Unhealthy("SDK readiness response exceeds the Platform bound.");
            Stream stream = await response.Content.ReadAsStreamAsync(boundedToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                byte[] buffer = new byte[65537];
                int count = 0;
                while (count < buffer.Length)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(count), boundedToken).ConfigureAwait(false);
                    if (read == 0) break;
                    count += read;
                }
                if (count > 65536) return HealthCheckResult.Unhealthy("SDK readiness response exceeds the Platform bound.");
                return EvaluateBody(System.Text.Encoding.UTF8.GetString(buffer, 0, count));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("SDK readiness is unavailable.");
        }
    }

    /// <summary>Accepts the SDK default text writer and its optional Development JSON writer.</summary>
    /// <param name="body">The bounded SDK readiness response.</param>
    public static HealthCheckResult EvaluateBody(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        string status = body.Trim();
        if (status is "Healthy" or "Degraded" or "Unhealthy")
        {
            using JsonDocument text = JsonDocument.Parse(JsonSerializer.Serialize(new { status }));
            return Evaluate(text.RootElement);
        }
        try
        {
            using JsonDocument json = JsonDocument.Parse(body);
            return Evaluate(json.RootElement);
        }
        catch (JsonException) { return HealthCheckResult.Unhealthy("SDK readiness status is absent."); }
    }

    /// <summary>Maps only explicit SDK statuses; missing or unfamiliar statuses fail closed.</summary>
    /// <param name="response">The bounded metadata-only SDK response.</param>
    public static HealthCheckResult Evaluate(JsonElement response)
        => response.ValueKind == JsonValueKind.Object && response.TryGetProperty("status", out JsonElement status)
            && status.ValueKind == JsonValueKind.String ? status.GetString() switch
            {
                "Healthy" => HealthCheckResult.Healthy("SDK readiness is healthy."),
                "Degraded" => HealthCheckResult.Degraded("SDK reports unresolved or incomplete recovery."),
                _ => HealthCheckResult.Unhealthy("SDK readiness is unhealthy or unrecognized."),
            } : HealthCheckResult.Unhealthy("SDK readiness status is absent.");
}
