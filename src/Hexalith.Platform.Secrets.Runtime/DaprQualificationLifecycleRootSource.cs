using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Narrow private Dapr GetSecret root resolver. Exact native version/field/canonical AES-256 bytes and independent current permission are mandatory; all owned buffers and response dictionaries are cleared.</summary>
public sealed class DaprQualificationLifecycleRootSource(DaprClient dapr,ICustodyOwnerStateAuthority authority,
    IReadOnlyDictionary<string,QualificationLifecycleRootReference> enrolledReferences):IQualificationLifecycleRootSource
{
    /// <inheritdoc />
    public async Task<byte[]?> ReadAsync(CustodyKeyRegistration registration,CustodyKeyUse use,CancellationToken token)
    {
        if(!enrolledReferences.TryGetValue(registration.OperationId,out var reference) || reference is not { Component.Length:>0,Key.Length:>0,Version.Length:>0,Field.Length:>0 }
            || use.OriginalRootReference!=registration.OpaqueWrappedKeyReference) return null;
        string identity=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(new { registration,use,reference }));
        if(!await authority.AuthorizeOperationAsync(identity,"ReadQualificationLifecycleRoot",token).ConfigureAwait(false)) return null;
        var pending=dapr.GetSecretAsync(reference.Component,reference.Key,new Dictionary<string,string> { ["version_id"]=reference.Version },token);
        Dictionary<string,string>? response=null;byte[]? material=null;bool transferred=false;
        try
        {
            response=await pending.WaitAsync(token).ConfigureAwait(false);
            if(response.Count!=1 || !response.TryGetValue(reference.Field,out var encoded)) return null;
            material=Convert.FromBase64String(encoded);
            if(material.Length!=32 || Convert.ToBase64String(material)!=encoded || !await authority.AuthorizeOperationAsync(identity,"ReadQualificationLifecycleRoot",token).ConfigureAwait(false)) return null;
            token.ThrowIfCancellationRequested();transferred=true;return material;
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { _=ClearLate(pending);throw; }
        catch(Exception) { token.ThrowIfCancellationRequested();return null; }
        finally { response?.Clear();if(!transferred && material is not null) CryptographicOperations.ZeroMemory(material); }
    }
    private static async Task ClearLate(Task<Dictionary<string,string>> pending) { try { (await pending.ConfigureAwait(false)).Clear(); } catch(Exception) { } }
}
