using Microsoft.AspNetCore.Hosting;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Explicit independent service executable composition. The named durable state must already be enrolled externally.</summary>
public static class IndependentAuthorityHttpApplication
{
    /// <summary>Serves bounded authenticated authority operations; no state initialization, enrollment or fixture fallback.</summary>
    public static async Task<int> RunAsync(string installedProfilePath, IPrivateRecordingEventStore? recordingSource = null, IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers = null,Microsoft.Extensions.Hosting.IHostedService? holderCompletionWorker=null,CancellationToken cancellationToken=default)
    {
        using var startupBudget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);startupBudget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
        byte[] profileBytes = await BoundedRegularFile.ReadAsync(installedProfilePath,1024*1024,startupBudget.Token).ConfigureAwait(false);
        CustodyBindingContract.RejectDuplicateProperties(profileBytes);
        var profile = JsonSerializer.Deserialize<IndependentAuthorityReceiverInstallation>(profileBytes, CustodyBindingContract.Json);
        return await RunCapturedAsync(profile,recordingSource,ownerHandlers,holderCompletionWorker,startupBudget.Token,cancellationToken).ConfigureAwait(false);
        } catch(Exception error) when(error is IOException or JsonException or CryptographicException or OperationCanceledException or ArgumentException) { RecordStartupFailure(error,cancellationToken);return 2; }
    }
    internal static async Task<int> RunCapturedAsync(IndependentAuthorityReceiverInstallation? profile,IPrivateRecordingEventStore? recordingSource,IReadOnlyList<IIndependentOwnerOperationHandler>? ownerHandlers,Microsoft.Extensions.Hosting.IHostedService? holderCompletionWorker,CancellationToken startupToken,CancellationToken lifetimeToken)
    {
        try
        {
        if (profile is null || !SecretsProvisioning.IsReference(profile.Component) || !SecretsProvisioning.IsReference(profile.StateKey)) { IndependentReceivingDiagnostics.Record(IndependentReceivingFailure.EnrolledStateUnavailable);return 2; }
        using var signer=await LoadSignerAsync(profile.SigningKeyFile,startupToken).ConfigureAwait(false);
        using var verifier = ECDsa.Create(); verifier.ImportFromPem(profile.PublicKeyPem);
        if (signer.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7"
            || !signer.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(verifier.ExportSubjectPublicKeyInfo())) return 2;
        using var dapr = new DaprClientBuilder().Build();
        ICustodyRegistrationOriginalVerifier? registrationVerifier = profile.RegistrationVerifier is { } lookup
            ? new DaprCustodyRegistrationOriginalVerifier(new CustodyLifecycleClient(dapr,
                new Dictionary<string,string> { [CustodyLifecycleClient.LogicalRole] = lookup.BindingName }, lookup.OwnerPurposeScope,
                lookup.EnrollmentJson, lookup.PublicKeyPem, new DaprCustodyAuthorizerClient(dapr,
                    new Dictionary<string,string> { [DaprCustodyAuthorizerClient.LogicalRole] = lookup.AuthorizerAppId }, lookup.AuthorizerCredentialReference)))
            : null;
        var receiver = new IndependentAuthorityReceiver(dapr, profile, signer, TimeProvider.System, recordingSource, ownerHandlers, registrationVerifier:registrationVerifier);
        await using var app=BuildApplication(receiver,holderCompletionWorker);
        await RunServiceAsync(app,startupToken,lifetimeToken).ConfigureAwait(false); return 0;
        }
        catch(Exception error) when(error is IOException or JsonException or CryptographicException or OperationCanceledException or ArgumentException) { RecordStartupFailure(error,lifetimeToken);return 2; }
    }
    private static void RecordStartupFailure(Exception error,CancellationToken lifetime)
        =>IndependentReceivingDiagnostics.Record(error is OperationCanceledException?(lifetime.IsCancellationRequested?IndependentReceivingFailure.CallerCancelled:IndependentReceivingFailure.AcquisitionTimeout):error is IOException?IndependentReceivingFailure.StorageFailure:IndependentReceivingFailure.MalformedEnrolledState);
    internal static async Task RunServiceAsync(WebApplication app,CancellationToken startupToken,CancellationToken lifetimeToken)
    {
        await app.StartAsync(startupToken).ConfigureAwait(false);
        await app.WaitForShutdownAsync(lifetimeToken).ConfigureAwait(false);
    }
    internal static async Task<ECDsa> LoadSignerAsync(string exactPath,CancellationToken token)
    {
        byte[] keyBytes=await BoundedRegularFile.ReadAsync(exactPath,65536,token).ConfigureAwait(false);
        char[]? keyChars=null;var signer=ECDsa.Create();
        try { keyChars=System.Text.Encoding.UTF8.GetChars(keyBytes);signer.ImportFromPem(keyChars);return signer; }
        catch { signer.Dispose();throw; }
        finally { CryptographicOperations.ZeroMemory(keyBytes);if(keyChars is not null) Array.Clear(keyChars); }
    }
    /// <summary>Builds the actual owning service and its automatic exact holder recovery registration.</summary>
    public static WebApplication BuildApplication(IndependentAuthorityReceiver receiver,Microsoft.Extensions.Hosting.IHostedService? holderCompletionWorker=null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=CustodyBindingContract.MaximumIndependentAuthorityFrameBytes-1); builder.Logging.ClearProviders();
        if(holderCompletionWorker is not null) Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(builder.Services,holderCompletionWorker);
        var app = builder.Build();
        app.MapPost("/platform-independent/v1/{method}", (HttpContext context, string method, CancellationToken token) => receiver.HandleAsync(context, method, token));
        return app;
    }
}
