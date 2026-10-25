using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public installation bundle, independently enrolled before startup. It contains no signing key or bearer bytes.</summary>
public sealed record QualificationInstallation(string Role, IndependentDaprAuthorityBinding ScopeAuthority,
    IndependentDaprAuthorityBinding OriginalAuthority, IndependentDaprAuthorityBinding Owner,
    IReadOnlyDictionary<string, QualificationInstalledAnchor> PublicAnchors);

/// <summary>Exact externally accepted public anchor bytes and identity.</summary>
public sealed record QualificationInstalledAnchor(string Identity, string Version, string PublicKeyPem, string Sha256);

/// <summary>Owning-process role composition. Missing installation remains an HTTP refusal; construction performs no enrollment.</summary>
public static class InstalledQualificationComposition
{
    /// <summary>Loads only an explicit installed bundle; verifies independent public keys and registers authenticated concrete adapters.</summary>
    public static string? AddInstalledQualificationRole(this IServiceCollection services, string role, string? installationPath)
    {
        if (string.IsNullOrWhiteSpace(installationPath)) return "qualification." + role + ".installationBundle";
        try
        {
            byte[] bytes = File.ReadAllBytes(installationPath);
            if (bytes.Length is 0 or > 1024 * 1024) return "qualification." + role + ".boundedInstallationBundle";
            CustodyBindingContract.RejectDuplicateProperties(bytes);
            var installation = JsonSerializer.Deserialize<QualificationInstallation>(bytes, CustodyBindingContract.Json);
            if (installation is null || installation.Role != role || installation.PublicAnchors.Count != 3
                || installation.ScopeAuthority.AppId == installation.Owner.AppId || installation.OriginalAuthority.AppId == installation.Owner.AppId)
                return "qualification." + role + ".independentInstallationBindings";
            var publicBindings = new Dictionary<string, QualificationProofBinding>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in new[] { "executor", "observer", "enrollment" })
            {
                var anchor = installation.PublicAnchors[name];
                using var key = ECDsa.Create(); key.ImportFromPem(anchor.PublicKeyPem);
                if (!identities.Add(Convert.ToHexString(key.ExportSubjectPublicKeyInfo()))) return "qualification.independentPublicTrust";
                publicBindings.Add(name, new(anchor.Identity, anchor.Version, Encoding.ASCII.GetBytes(anchor.PublicKeyPem), anchor.Sha256));
            }
            services.AddSingleton<IQualificationPublicProofVerifier>(new QualificationProofVerifier(publicBindings));
            services.AddSingleton<IQualificationCallerAuthenticator>(provider =>
                new InstalledQualificationCallerAuthenticator(Authority(provider, installation.ScopeAuthority)));
            services.AddSingleton<IQualificationOriginalStore>(provider =>
                new InstalledQualificationOriginalStore(Authority(provider, installation.OriginalAuthority)));
            if (role == "executor") services.AddSingleton<IQualificationExecutorProvider>(provider =>
                new InstalledQualificationExecutor(Authority(provider, installation.ScopeAuthority), Authority(provider, installation.Owner)));
            else if (role == "observer") services.AddSingleton<IQualificationObserverProvider>(provider =>
                new InstalledQualificationObserver(Authority(provider, installation.ScopeAuthority), Authority(provider, installation.Owner)));
            else if (role == "enrollment") services.AddSingleton<IQualificationEnrollmentProvider>(provider =>
                new InstalledQualificationEnrollment(Authority(provider, installation.ScopeAuthority), Authority(provider, installation.Owner)));
            else return "qualification.role";
            return null;
        }
        catch (Exception) { return "qualification." + role + ".validEnrolledInstallationBundle"; }
    }
    private static IndependentDaprAuthority Authority(IServiceProvider services, IndependentDaprAuthorityBinding binding)
        => new(new DaprSecretsInvoker(services.GetRequiredService<DaprClient>()), binding, services.GetService<TimeProvider>() ?? TimeProvider.System);
}

/// <summary>Workload bearer hash is checked by the independent current scope owner; request content grants nothing.</summary>
public sealed class InstalledQualificationCallerAuthenticator(IndependentDaprAuthority scopes) : IQualificationCallerAuthenticator
{
    /// <inheritdoc />
    public Task<QualificationCallerContext?> AuthenticateAsync(HttpContext context, string role, CancellationToken cancellationToken)
    {
        var values = context.Request.Headers.Authorization;
        if (values.Count != 1 || values[0] is not { Length: > 7 and <= 8199 } value || !value.StartsWith("Bearer ", StringComparison.Ordinal))
            return Task.FromResult<QualificationCallerContext?>(null);
        byte[] bearer = Encoding.UTF8.GetBytes(value[7..]);
        try { return scopes.InvokeAsync<QualificationCallerContext>("qualification-authenticate", new { role, bearerSha256 = Convert.ToHexString(SHA256.HashData(bearer)) }, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(bearer); }
    }
}

/// <summary>Atomic original publication at an independently authenticated nonrollback service, never process dictionaries.</summary>
public sealed class InstalledQualificationOriginalStore(IndependentDaprAuthority originals) : IQualificationOriginalStore
{
    /// <inheritdoc />
    public async Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken)
    {
        var result = await originals.InvokeAsync<OriginalAdmission>("qualification-begin-original", new { role, requestId, original }, cancellationToken).ConfigureAwait(false);
        if (result is null || !result.Exact) throw new InvalidOperationException("Durable original publication is unknown or conflicting.");
        return result.First;
    }
    /// <inheritdoc />
    public async Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken)
    {
        if (await originals.InvokeAsync<OriginalAdmission>("qualification-retain-result", new { role, requestId, result }, cancellationToken).ConfigureAwait(false) is not { Exact: true })
            throw new InvalidOperationException("Durable original result publication is unknown or conflicting.");
    }
    private sealed record OriginalAdmission(bool Exact, bool First);
}

/// <summary>Actual role owner calls with separate current authorization and signed public results; no expected result is generated here.</summary>
public abstract class InstalledQualificationProvider(IndependentDaprAuthority scopes, IndependentDaprAuthority owner, string role) : IQualificationRoleProvider
{
    /// <inheritdoc />
    public string Role => role;
    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(JsonObject request, QualificationCallerContext caller, CancellationToken cancellationToken)
        => await scopes.InvokeAsync<Permission>("qualification-authorize", new { role, request, caller }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken)
        => operation is "execute" or "lookup" or "observe" or "enroll"
            ? owner.InvokeAsync<JsonObject>("qualification-" + role + "-" + operation, original, cancellationToken) : Task.FromResult<JsonObject?>(null);
    private sealed record Permission(bool Allowed);
}
/// <summary>Executor role restricted to its independent installed owner.</summary>
public sealed class InstalledQualificationExecutor(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "executor"), IQualificationExecutorProvider;
/// <summary>Observer role restricted to persisted owner reads.</summary>
public sealed class InstalledQualificationObserver(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "observer"), IQualificationObserverProvider;
/// <summary>Enrollment role verifies externally provisioned trust; it cannot create trust at startup.</summary>
public sealed class InstalledQualificationEnrollment(IndependentDaprAuthority scopes, IndependentDaprAuthority owner)
    : InstalledQualificationProvider(scopes, owner, "enrollment"), IQualificationEnrollmentProvider;
