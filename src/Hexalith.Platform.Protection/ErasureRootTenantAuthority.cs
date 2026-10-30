using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Routes each exact nested tenant identity to its independently enrolled authority credential. No wildcard or cross-tenant fallback exists.</summary>
public sealed class ErasureRootTenantAuthority
{
    private readonly IndependentDaprAuthority? _single;
    private readonly IReadOnlyDictionary<string, IndependentDaprAuthority>? _tenants;
    /// <summary>Preserves an existing independently enrolled single-tenant adapter.</summary>
    public ErasureRootTenantAuthority(IndependentDaprAuthority authority) => _single = authority;
    /// <summary>Captures the complete independently enrolled tenant routing map.</summary>
    public ErasureRootTenantAuthority(IReadOnlyDictionary<string, IndependentDaprAuthority> tenants) => _tenants = tenants;
    /// <summary>Preserves explicit single-binding construction.</summary>
    public ErasureRootTenantAuthority(ISecretsCredentialBoundDaprInvoker transport, IndependentDaprAuthorityBinding binding, TimeProvider clock)
        : this(new IndependentDaprAuthority(transport, binding, clock)) { }
    /// <summary>Preserves existing single-tenant callers.</summary>
    public static implicit operator ErasureRootTenantAuthority(IndependentDaprAuthority authority) => new(authority);
    /// <summary>Invokes only the credential enrolled for all reached native tenant identities.</summary>
    public Task<T?> InvokeAsync<T>(string method, object payload, CancellationToken token = default)
    {
        if (_single is not null) { return _single.InvokeAsync<T>(method, payload, token); }
        var captured = JsonSerializer.SerializeToElement(payload, CustodyBindingContract.Json);
        var tenants = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name is "tenantId" or "TenantId") { tenants.Add(property.Value.GetString() ?? ""); }
                    else if (property.Name == "ScopeId")
                    { foreach (string tenant in _tenants!.Keys.Where(tenant => property.Value.GetString()!.StartsWith(tenant + ":", StringComparison.Ordinal))) { tenants.Add(tenant); } }
                    else { Visit(property.Value); }
                }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var child in value.EnumerateArray()) { Visit(child); } }
        }
        Visit(captured);
        if (method == "erc-current-protocol-evidence")
        {
            var bytes = captured.GetProperty("transition").GetProperty("TargetBytes").GetBytesFromBase64();
            string[] parts = System.Text.Encoding.UTF8.GetString(bytes).Split('\n');
            if (parts.Length == 5) { tenants.Add(parts[1]); }
        }
        return tenants.Count == 1 && _tenants!.TryGetValue(tenants.Single(), out var authority)
            ? authority.InvokeAsync<T>(method, payload, token) : Task.FromResult<T?>(default);
    }
}
