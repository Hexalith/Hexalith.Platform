using System.Security.Cryptography;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Ephemeral dedicated export fixture only; there is no installed production resolver or profile fallback.
internal sealed class ExportQualificationFixture:IDisposable
{
    private readonly byte[] _export=RandomNumberGenerator.GetBytes(32),_kek=RandomNumberGenerator.GetBytes(32);
    private readonly ECDsa _signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _foreign=ECDsa.Create(ECCurve.NamedCurves.nistP256);
    internal List<byte[]> ReleasedBuffers { get; }=[];
    internal ExportQualificationInput Input(string exportId)=>new("tenant",exportId,"export-kek-v1","export-signing-v1",1,"actual-fixture-manifest-canary"u8.ToArray(),_foreign.ExportSubjectPublicKeyInfo());
    internal ExportQualificationEngine Engine()=>new(new PrivateMaterial(this),new PublicTrust(_signer.ExportSubjectPublicKeyInfo()));
    public void Dispose() { CryptographicOperations.ZeroMemory(_export);CryptographicOperations.ZeroMemory(_kek);_signer.Dispose();_foreign.Dispose(); }
    private sealed class PrivateMaterial(ExportQualificationFixture fixture):IExportQualificationMaterialSource
    {
        public Task<IReadOnlyList<string>> PreflightAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<ExportQualificationMaterial?> ResolveAsync(ExportQualificationInput input,ExportQualificationMaterialUse use,CancellationToken token)
        {
            byte[] export=fixture._export.ToArray(),kek=fixture._kek.ToArray();fixture.ReleasedBuffers.Add(export);fixture.ReleasedBuffers.Add(kek);
            var signer=ECDsa.Create();byte[] encoded=fixture._signer.ExportECPrivateKey();try { signer.ImportECPrivateKey(encoded,out _); } finally { CryptographicOperations.ZeroMemory(encoded); }
            return Task.FromResult<ExportQualificationMaterial?>(new(export,kek,signer));
        }
    }
    private sealed class PublicTrust(byte[] spki):IExportQualificationTrustSource
    {
        public Task<ExportQualificationTrust?> ResolveAsync(ExportQualificationInput input,CancellationToken token)=>Task.FromResult<ExportQualificationTrust?>(new(input.TenantId,input.SigningKeyVersion,"fixture-independent-export-public-anchor","v1",spki));
    }
}
