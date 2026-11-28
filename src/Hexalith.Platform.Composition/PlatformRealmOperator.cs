namespace Hexalith.Platform.Composition;

using System.Net;
using System.Text;
using System.Text.Json;

/// <summary>Obtains single-purpose client-credentials tokens for the operator principal of one generated local realm instance.</summary>
/// <remarks>The operator exists only in the generated instance, never in the shared realm contract, and its secret stays in the AppHost.</remarks>
/// <param name="tokenEndpoint">The generated realm's loopback token endpoint.</param>
/// <param name="secret">The run-generated operator client secret.</param>
/// <param name="handler">An optional transport for tests.</param>
internal sealed class PlatformRealmOperator(Uri tokenEndpoint, string secret, Func<HttpMessageHandler>? handler = null)
{
    /// <summary>The confidential operator client of the generated local realm instance.</summary>
    internal const string ClientId = "platform-operator";

    private const int MaximumResponseBytes = 64 * 1024;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly string[] ForbiddenClaims = ["eventstore:tenant", "eventstore:domain", "eventstore:workload", "eventstore:operation", "eventstore:channel", "eventstore:surface"];

    /// <summary>Gets the token endpoint, for diagnostics and tests.</summary>
    internal Uri TokenEndpoint => tokenEndpoint;

    /// <summary>Requests a token carrying exactly the given grants, retrying only while the realm is unavailable.</summary>
    /// <exception cref="InvalidOperationException">The realm refused the operator client or issued different grants.</exception>
    internal async Task<string> TokenAsync(IReadOnlyList<PlatformOperatorGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        if (grants.Count == 0) throw new ArgumentException("An operator token needs at least one grant.", nameof(grants));
        string scope = string.Join(' ', grants.Select(grant => grant.Scope).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        using var client = new HttpClient(handler?.Invoke() ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = MaximumResponseBytes };
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HttpResponseMessage response;
            try
            {
                using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = ClientId, ["client_secret"] = secret, ["scope"] = scope,
                });
                response = await client.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // The realm is not listening yet; keep trying within the caller's bound.
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"The generated local realm refused operator client '{ClientId}' (HTTP {(int)response.StatusCode}).");
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
                if (!document.RootElement.TryGetProperty("access_token", out JsonElement value) || value.ValueKind != JsonValueKind.String
                    || value.GetString() is not { Length: > 0 } token)
                    throw new InvalidOperationException($"The generated local realm returned no token for operator client '{ClientId}'.");
                RequireExactGrants(token, grants);
                return token;
            }
        }
    }

    /// <summary>Refuses a token whose permission and role claims are not exactly the requested grants.</summary>
    internal static void RequireExactGrants(string token, IReadOnlyList<PlatformOperatorGrant> grants)
    {
        using JsonDocument payload = Payload(token);
        JsonElement claims = payload.RootElement;
        string[] permissions = Values(claims, PlatformOperatorGrant.PermissionClaimType);
        string[] roles = [.. Values(claims, PlatformOperatorGrant.RoleClaimType), .. Values(claims, "roles")];
        string[] expectedPermissions = [.. grants.Where(grant => !grant.IsGlobalAdministrator).Select(grant => grant.ClaimValue).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        string[] expectedRoles = grants.Any(grant => grant.IsGlobalAdministrator) ? [PlatformOperatorGrant.GlobalAdministrator.ClaimValue] : [];
        if (!permissions.Order(StringComparer.Ordinal).SequenceEqual(expectedPermissions, StringComparer.Ordinal)
            || !roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).SequenceEqual(expectedRoles, StringComparer.Ordinal)
            || roles.Length != expectedRoles.Length
            || !claims.TryGetProperty("azp", out JsonElement party) || party.GetString() != ClientId
            || !claims.TryGetProperty("sub", out JsonElement subject) || subject.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(subject.GetString())
            || ForbiddenClaims.Any(claim => claims.TryGetProperty(claim, out _)))
            throw new InvalidOperationException($"Operator client '{ClientId}' received a token whose grants differ from the request.");
    }

    /// <summary>Reads the authenticated subject the issuer assigned to an operator token.</summary>
    internal static string Subject(string token)
    {
        using JsonDocument payload = Payload(token);
        return payload.RootElement.TryGetProperty("sub", out JsonElement subject) && subject.GetString() is { Length: > 0 } value
            ? value : throw new InvalidOperationException("The operator token has no subject.");
    }

    private static JsonDocument Payload(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3 || parts[1].Length is 0 or > MaximumResponseBytes)
            throw new InvalidOperationException("The operator token is not a compact JWT.");
        string padded = parts[1].Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        try
        {
            return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidOperationException("The operator token payload is unreadable.", exception);
        }
    }

    private static string[] Values(JsonElement claims, string name) =>
        !claims.TryGetProperty(name, out JsonElement value) ? []
        : value.ValueKind == JsonValueKind.String ? [value.GetString()!]
        : value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetRawText())]
        : [value.GetRawText()];
}
