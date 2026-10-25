using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody.Dapr;

// Bounded fake HTTP transport only. These checks establish neither installed server capability nor physical closure.
internal static class OpenBaoNativeOperationsTests
{
    internal static async Task Run()
    {
        foreach(bool derived in new[]{true,false}) await Binding(derived);
    }
    private static async Task Binding(bool derived)
    {
        using var fixture=new Fixture();using var dapr=new DaprClientBuilder().Build();
        string credential=Path.GetTempFileName();await File.WriteAllTextAsync(credential,"fixture-native-token");
        try
        {
            var metadata=JsonSerializer.SerializeToElement(new { type="aes256-gcm96",exportable=false,allow_plaintext_backup=false,derived });
            var epochs=fixture.Profile.Epochs.Select(value=>value with { KeyArn="openbao-transit:"+value.OpaqueId }).ToArray();
            var profile=fixture.Profile with { Epochs=epochs };
            var installation=new NativeInstallation("OpenBaoTransit","fixture","head","/fixture/checkpoints","/fixture/originals",Fixture.Digest,"https://fixture.invalid/","transit",credential,
                epochs.ToDictionary(value=>value.KeyArn,_=>CustodyJson.Hash(CustodyJson.Bytes(metadata))),Fixture.Digest,TransitBindingProfiles:epochs.ToDictionary(value=>value.KeyArn,_=>new OpenBaoTransitBindingProfile("OpenBaoTransit.AeadAssociatedData.V1",derived)));
            using var handler=new NativeTransport(metadata,derived);using var operations=new OpenBaoNativeOperations(profile,installation,fixture.Store,dapr,handler);
            byte[] original=RandomNumberGenerator.GetBytes(32);
            try
            {
                byte[] encrypted=await operations.EncryptOuterKeyAsync("e1","tenant",original,default);
                var registration=await KeyEnvelopeBuilder.WrapAsync(fixture.Wrap("native-aad").WrapInput!,RandomNumberGenerator.GetBytes(32),"e1",fixture.Native,default);
                registration=registration with { EncryptedOuterKey=encrypted,Epoch="e1" };
                byte[] recovered=await operations.ReadOuterKeyAsync(registration,default);
                if(!recovered.AsSpan().SequenceEqual(original)) throw new Exception("OpenBao AEAD roundtrip changed key.");CryptographicOperations.ZeroMemory(recovered);
                int effects=handler.Effects;
                await Refused(()=>operations.EncryptOuterKeyAsync("e1","other",original,default));
                if(handler.Effects!=effects) throw new Exception("Foreign tenant reached native effect.");
                // Same encrypted bytes transplanted to an independently enrolled tenant resource must fail authenticated binding.
                await Refused(()=>operations.ReadOuterKeyAsync(registration with { Epoch="other-e1",Registration=registration.Registration with { Identity=registration.Registration.Identity with { TenantId="other" } } },default));
                handler.Substitute=true;effects=handler.Effects;
                await Refused(()=>operations.EncryptOuterKeyAsync("e1","tenant",original,default));
                if(handler.Effects!=effects) throw new Exception("Substituted metadata reached encryption.");
                handler.Substitute=false;handler.Malformed=true;
                await Refused(()=>operations.EncryptOuterKeyAsync("e1","tenant",original,default));
                if(handler.Effects!=effects) throw new Exception("Malformed metadata reached encryption.");handler.Malformed=false;
                using(var legacy=new OpenBaoNativeOperations(profile,installation with { TransitBindingProfiles=null },fixture.Store,dapr,new NativeTransport(metadata,derived)))
                    await Refused(()=>legacy.EncryptOuterKeyAsync("e1","tenant",original,default));
                using(var mismatched=new OpenBaoNativeOperations(profile,installation with { TransitBindingProfiles=epochs.ToDictionary(value=>value.KeyArn,_=>new OpenBaoTransitBindingProfile("OpenBaoTransit.AeadAssociatedData.V1",!derived)) },fixture.Store,dapr,new NativeTransport(metadata,derived)))
                    await Refused(()=>mismatched.EncryptOuterKeyAsync("e1","tenant",original,default));
                using(var badEndpoint=new OpenBaoNativeOperations(profile,installation with { OpenBaoHttpsUrl="https://fixture.invalid/substituted-path" },fixture.Store,dapr,new NativeTransport(metadata,derived)))
                    await Refused(()=>badEndpoint.EncryptOuterKeyAsync("e1","tenant",original,default));
                using(var stalled=new OpenBaoNativeOperations(profile,installation,fixture.Store,dapr,new StalledTransport(),TimeSpan.FromMilliseconds(100)))
                {
                    var elapsed=System.Diagnostics.Stopwatch.StartNew();
                    try { await stalled.EncryptOuterKeyAsync("e1","tenant",original,CancellationToken.None);throw new Exception("Stalled native response body escaped total deadline."); }
                    catch(OperationCanceledException) { }
                    if(elapsed.Elapsed>TimeSpan.FromSeconds(3)) throw new Exception("Native headers ended total deadline protection.");
                }
                effects=handler.Effects;
                await Refused(()=>operations.ReencryptOuterKeyAsync("e1","e2","tenant",encrypted,default));
                if(handler.Effects!=effects) throw new Exception("Missing independent holder/recovery protocol reached survivor plaintext operation.");
                handler.WrongAad=true;await Refused(()=>operations.ReadOuterKeyAsync(registration,default));
                if(handler.LastAad is null || !Encoding.UTF8.GetString(handler.LastAad).Contains("custody-outer-key-v1",StringComparison.Ordinal)) throw new Exception("Typed purpose/tenant/epoch AAD absent.");
            }
            finally { CryptographicOperations.ZeroMemory(original); }
        }
        finally { File.Delete(credential); }
    }
    private static async Task Refused(Func<Task> action)
    { try { await action(); } catch(Exception exception) when(exception is AuthorityUnavailableException or HttpRequestException or CryptographicException) { return; }throw new Exception("OpenBao substitution or unsupported mapping was accepted."); }
    private sealed class StalledTransport:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            =>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StreamContent(new StalledStream()) });
    }
    private sealed class StalledStream:Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position { get=>throw new NotSupportedException();set=>throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default) { await Task.Delay(Timeout.Infinite,token);return 0; }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    private sealed class NativeTransport(JsonElement metadata,bool derived):HttpMessageHandler
    {
        private readonly byte[] _key=RandomNumberGenerator.GetBytes(32);internal int Effects;internal bool Substitute,Malformed,WrongAad;internal byte[]? LastAad;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri!.Host!="fixture.invalid" || request.RequestUri.Scheme!="https" || request.Headers.GetValues("X-Vault-Token").Single()!="fixture-native-token") throw new Exception("Endpoint or scoped credential substituted.");
            string path=request.RequestUri.AbsolutePath;
            if(request.Method==HttpMethod.Get)
            {
                if(!path.StartsWith("/v1/transit/keys/",StringComparison.Ordinal)) throw new Exception("Wrong metadata endpoint.");
                return Json(new { data=Malformed?JsonSerializer.SerializeToElement(new { type="aes256-gcm96" }):Substitute?JsonSerializer.SerializeToElement(new { type="aes256-gcm96",exportable=true,allow_plaintext_backup=false,derived }):metadata });
            }
            if(request.Method!=HttpMethod.Post || !path.StartsWith("/v1/transit/",StringComparison.Ordinal)) throw new Exception("Unexpected native API.");
            var input=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            LastAad=Convert.FromBase64String(input.GetProperty("associated_data").GetString()!);
            var binding=JsonDocument.Parse(LastAad).RootElement;
            if(binding.GetProperty("schema").GetString()!="custody-native-aad-v1" || binding.GetProperty("authority").GetString()!="authority" || binding.GetProperty("incarnation").GetString()!="incarnation"
                || binding.GetProperty("purpose").GetString()!="custody-outer-key-v1") throw new Exception("Closed authority/purpose AAD missing.");
            if(derived!=input.TryGetProperty("context",out var context) || derived && context.GetString()!=input.GetProperty("associated_data").GetString()) throw new Exception("Derivation context mapping changed.");
            byte[] aad=WrongAad?Encoding.UTF8.GetBytes("wrong-tenant-context"):LastAad;
            if(path.Contains("/encrypt/",StringComparison.Ordinal))
            {
                Effects++;byte[] plain=Convert.FromBase64String(input.GetProperty("plaintext").GetString()!);byte[] nonce=RandomNumberGenerator.GetBytes(12),cipher=new byte[plain.Length],tag=new byte[16];
                using var aes=new AesGcm(_key,16);aes.Encrypt(nonce,plain,cipher,tag,aad);CryptographicOperations.ZeroMemory(plain);
                return Json(new { data=new { ciphertext="vault:v1:"+Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray()) } });
            }
            if(!path.Contains("/decrypt/",StringComparison.Ordinal)) throw new Exception("Unexpected native mutation endpoint.");
            byte[] encrypted=Convert.FromBase64String(input.GetProperty("ciphertext").GetString()![9..]),result=new byte[32];
            using(var aes=new AesGcm(_key,16)) aes.Decrypt(encrypted.AsSpan(0,12),encrypted.AsSpan(12,32),encrypted.AsSpan(44,16),result,aad);
            string encoded=Convert.ToBase64String(result);CryptographicOperations.ZeroMemory(result);return Json(new { data=new { plaintext=encoded } });
        }
        private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(value)) };
        protected override void Dispose(bool disposing) { CryptographicOperations.ZeroMemory(_key);base.Dispose(disposing); }
    }
}
