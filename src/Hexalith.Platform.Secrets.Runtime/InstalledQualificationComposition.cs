using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

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
