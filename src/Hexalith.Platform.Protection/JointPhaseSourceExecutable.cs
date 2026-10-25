using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Protection;

/// <summary>Independent executable native-source service. It holds only source signer/read grants, never phase-owner private signing material.</summary>
public static class JointPhaseSourceExecutable
{
    /// <summary>Builds actual native readers and serves the authenticated closed source protocol; no enrollment or native effect occurs at startup.</summary>
    public static async Task<int> RunAsync(string? installationPath)
    {
        if(installationPath is null || !Path.IsPathFullyQualified(installationPath)) return 2;
        byte[] bytes=await File.ReadAllBytesAsync(installationPath).ConfigureAwait(false);if(bytes.Length is 0 or >1024*1024) return 2;
        CustodyBindingContract.RejectDuplicateProperties(bytes);var installed=JsonSerializer.Deserialize<JointPhaseSourceInstallation>(bytes,CustodyBindingContract.Json);if(installed is null) return 2;
        using var signer=await JointPhaseCommandExecutable.OpenSignerAsync(installed.SourceSigningKeyFile,installed.SourcePublicKeyFile,CancellationToken.None).ConfigureAwait(false);
        using var dapr=new DaprClientBuilder().Build();var transport=new DaprSecretsInvoker(dapr);var clock=TimeProvider.System;
        var originals=new InstalledQualificationOriginalStore(new IndependentDaprAuthority(transport,installed.ReadOnlyOriginalAuthority,clock));
        var runtime=SecretsRuntimeConfiguration.Load(installed.RuntimeConfigurationFile);if(runtime is null) return 2;
        var ports=QualificationGuardProductionComposition.Create(dapr,runtime,installed.Protection,originals,clock);
        var profile=installed.Protection;if(profile is not { EventStoreEndpoint:{ IsAbsoluteUri:true,Scheme:"https" },GatewayCredentials:{ } credentials,ReadAuthority:{ } readAuthority }) return 2;
        ICustodyOwnerStateAuthority Owner(CustodyLifecycleInstallation value)=>new CustodyOwnerAuthority(dapr,value.BindingName,value.TenantId,value.OwnerScope,value.EnrollmentJson,value.PublicKeyPem,new DaprCustodyOwnerCallSource(new(transport,value.OwnerAuthority,clock)));
        var streams=installed.SourceScopes.Values.SelectMany(value=>value.NativeStreams).Distinct().ToArray();var proofs=new QualificationGatewayReadProofs(credentials,Owner(readAuthority),streams,clock);
        using var http=new HttpClient(new SocketsHttpHandler { AllowAutoRedirect=false,UseProxy=false }) { BaseAddress=profile.EventStoreEndpoint,Timeout=TimeSpan.FromSeconds(30) };
        var gateway=new EventStoreGatewayClient(http,Options.Create(new EventStoreGatewayClientOptions { BaseAddress=profile.EventStoreEndpoint,UseWorkloadTransport=true,MaxStreamReadResponseBytes=1024*1024 }),proofs,proofs);
        var publisher=new JointPhaseNativeSourcePublisher(new QualificationNativeSourceReader(new QualificationWorkloadReadGateway(gateway),clock),ports.Guard,ports.Protection,Owner(installed.SourceReadAuthority),installed.SourceScopes,installed.SourceArchiveDirectory,signer,new DaprFr34MeasuredOriginalReader(new(transport,installed.ReadOnlyOriginalAuthority,clock)),ports.Signing,ports.NativeOriginals,new QualificationDirectoryBoundaryOriginalReader());
        return await JointPhaseSourceApplication.RunAsync(installed.ReceivingInstallationPath,publisher,clock).ConfigureAwait(false);
    }
}
