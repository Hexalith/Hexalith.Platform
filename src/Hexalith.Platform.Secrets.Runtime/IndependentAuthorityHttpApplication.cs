using Microsoft.AspNetCore.Hosting;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit independent service executable composition. The named durable state must already be enrolled externally.</summary>
public static class IndependentAuthorityHttpApplication
{
    /// <summary>Serves bounded authenticated authority operations; no state initialization, enrollment or fixture fallback.</summary>
    public static async Task<int> RunAsync(string installedProfilePath, IPrivateRecordingEventStore? recordingSource = null, IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers = null,Microsoft.Extensions.Hosting.IHostedService? holderCompletionWorker=null)
    {
        byte[] profileBytes = await File.ReadAllBytesAsync(installedProfilePath).ConfigureAwait(false);
        if (profileBytes.Length is 0 or > 1024 * 1024) return 2;
        CustodyBindingContract.RejectDuplicateProperties(profileBytes);
        var profile = JsonSerializer.Deserialize<IndependentAuthorityReceiverInstallation>(profileBytes, CustodyBindingContract.Json);
        if (profile is null || !SecretsProvisioning.IsReference(profile.Component) || !SecretsProvisioning.IsReference(profile.StateKey)) return 2;
        using var signer = ECDsa.Create(); signer.ImportFromPem(await File.ReadAllTextAsync(profile.SigningKeyFile).ConfigureAwait(false));
        using var verifier = ECDsa.Create(); verifier.ImportFromPem(profile.PublicKeyPem);
        if (signer.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7"
            || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(verifier.ExportSubjectPublicKeyInfo())) return 2;
        using var dapr = new DaprClientBuilder().Build();
        var receiver = new IndependentAuthorityReceiver(dapr, profile, signer, TimeProvider.System, recordingSource, ownerHandlers);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=CustodyBindingContract.MaximumIndependentAuthorityFrameBytes-1); builder.Logging.ClearProviders();
        if(holderCompletionWorker is not null) Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(builder.Services,holderCompletionWorker);
        var app = builder.Build();
        app.MapPost("/platform-independent/v1/{method}", (HttpContext context, string method, CancellationToken token) => receiver.HandleAsync(context, method, token));
        await app.RunAsync().ConfigureAwait(false); return 0;
    }
}
