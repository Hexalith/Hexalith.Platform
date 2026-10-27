using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Custody.Dapr;

// Bounded fake HTTP transport only. These checks establish neither installed server capability nor physical closure.
internal static class OpenBaoNativeOperationsTests
{
    internal static async Task Run()
    {
        foreach(bool derived in new[]{true,false}) { await Binding(derived);await TenantKekExchange(derived);await SurvivorAndSourceCut(derived); }
    }
    private static async Task SurvivorAndSourceCut(bool derived)
    {
        using var fixture=new Fixture();using var dapr=new DaprClientBuilder().Build();string credential=Path.GetTempFileName();File.WriteAllText(credential,"fixture-native-token");
        byte[] kek=RandomNumberGenerator.GetBytes(32);
        try
        {
            var metadata=JsonSerializer.SerializeToElement(new { type="aes256-gcm96",exportable=false,allow_plaintext_backup=false,derived });using var transport=new NativeTransport(metadata,derived);
            var epochs=fixture.Profile.Epochs.Select(value=>value with { KeyArn="openbao-transit:"+value.OpaqueId }).ToArray();fixture.Profile=fixture.Profile with { Epochs=epochs };
            using var authority=new ReceivingHolderOriginals();
            var installation=new NativeInstallation("OpenBaoTransit","fixture","head","/fixture/checkpoints","/fixture/originals",Fixture.Digest,"https://fixture.invalid/","transit",credential,
                epochs.ToDictionary(value=>value.KeyArn,_=>CustodyJson.Hash(CustodyJson.Bytes(metadata))),Fixture.Digest,"producer","transport",authority.Binding,
                epochs.ToDictionary(value=>value.KeyArn,_=>new OpenBaoTransitBindingProfile("OpenBaoTransit.AeadAssociatedData.V1",derived)));
            using var native=new OpenBaoNativeOperations(fixture.Profile,installation,fixture.Store,dapr,new NonOwningTransport(transport),originalTransport:authority);
            var registration=await KeyEnvelopeBuilder.WrapAsync(fixture.Wrap("survivor-source").WrapInput!,kek,"e1",native,default);
            var admission=new NativeReencryptionAdmission("tenant","e1","e2",CustodyJson.Hash(registration.EncryptedOuterKey),"producer","transport",fixture.Profile.ProfileDigest,"","");
            string subject=CustodyJson.Hash(CustodyJson.Bytes(admission));
            void Enroll(NativeReencryptionAdmission actual)
            {
                var statement=fixture.Sign("independent-observer",subject,actual);
                authority.Enroll(subject,fixture.Store.State.Head.Revision,fixture.Store.State.Head.Digest,statement);
            }
            Enroll(admission with { ProducerHolderOriginalSha256=Fixture.Digest,NativeHolderOriginalSha256=Fixture.Digest });
            byte[] copied=await native.ReencryptOuterKeyAsync("e1","e2","tenant",registration.EncryptedOuterKey,default);
            Enroll(admission with { ProducerHolderOriginalSha256=Fixture.Digest,NativeHolderOriginalSha256=Fixture.Digest,DestinationEpoch="other-e2" });
            int before=transport.Effects;await Refused(()=>native.ReencryptOuterKeyAsync("e1","e2","tenant",registration.EncryptedOuterKey,default));
            if(transport.Effects!=before) throw new Exception("Substituted signed native-holder admission reached encryption.");
            // The real adapter deletion path must affect only the selected enrolled source.
            var manifest=fixture.Manifest([registration.Registration]);
            var checkpoint=SemanticProvider.Clone(fixture.Store.State) with { Cycle=new(manifest.Envelope.OperationId,manifest,"e1","e2",CyclePhase.Armed,[],[],Fixture.Digest,null),CurrentEpoch="e2" };
            await fixture.Store.CommitAsync(fixture.Store.State,checkpoint,default);fixture.Qualify();
            var worker=new RecoverableLifecycle(fixture.Profile,fixture.Store,native,new StatementVerifier(fixture.Profile,TimeProvider.System));
            var operation=new WorkerOperation("schedule-cut",manifest.Envelope.OperationId,"real-openbao-cut","e1","e2","","producer");
            await worker.ScheduleCutAsync("tenant",operation,fixture.Sign("private-operation",CustodyJson.Hash(CustodyJson.Bytes(operation)),operation),default);
            fixture.Qualify();await worker.ScheduleCutAsync("tenant",operation,fixture.Sign("private-operation",CustodyJson.Hash(CustodyJson.Bytes(operation)),operation),default);
            if(fixture.Store.State.Checkpoint.Cycle!.Phase!=CyclePhase.AwaitingFinalEvidence) throw new Exception("Native cut scheduling was not retained as awaiting independent final evidence.");
            await Refused(()=>native.ReadOuterKeyAsync(registration,default));
            var survivor=registration with { Epoch="e2",EncryptedOuterKey=copied };byte[] outer=await native.ReadOuterKeyAsync(survivor,default);
            try { byte[] root=KeyEnvelopeBuilder.Unwrap(survivor,outer,kek);try { if(!root.AsSpan().SequenceEqual(Fixture.Root)) throw new Exception("Admitted native survivor changed the original root."); }finally { CryptographicOperations.ZeroMemory(root); } }
            finally { CryptographicOperations.ZeroMemory(outer); }
            if(transport.Deletes!=1) throw new Exception("Actual source-cut DELETE was missing or repeated.");
            await Refused(()=>native.ScheduleSourceCutAsync("e1","other",default));
        }
        finally { CryptographicOperations.ZeroMemory(kek);File.Delete(credential); }
    }
    private sealed class ReceivingHolderOriginals:ISecretsCredentialBoundDaprInvoker,IDisposable
    {
        private readonly ECDsa _signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);private readonly string _channel=Path.GetTempFileName();
        private readonly DaprClient _dapr;private readonly IndependentAuthorityReceiver _receiver;private readonly HeadTransport _head;
        internal IndependentDaprAuthorityBinding Binding=>new("fixture-holder-originals","file:/fixture-holder-caller",_signer.ExportSubjectPublicKeyInfoPem(),"holder-enrollment","holder-originals","holder-trust");
        internal ReceivingHolderOriginals()
        {
            File.WriteAllText(_channel,"fixture-holder-channel");
            var state=new IndependentAuthorityState("holder-enrollment","holder-originals","holder-trust",1,DateTimeOffset.UtcNow.AddHours(1),
                [new(CustodyJson.Hash(Encoding.UTF8.GetBytes("fixture-holder-caller")),"holder-reader","tenant|native-reencryption","native-observation-reader",["custody-native-observation-original"],DateTimeOffset.UtcNow.AddHours(1),false)],null,0,Fixture.Digest,[],[],[],[],[],[]);
            _head=new(state);_dapr=new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler=_head }).Build();
            _receiver=new(_dapr,new("holder-enrollment","holder-originals","holder-trust","fixture","head","unused",_signer.ExportSubjectPublicKeyInfoPem(),"file:"+_channel),_signer,TimeProvider.System);
        }
        internal void Enroll(string operationId,long revision,string digest,SignedStatement statement)
        {
            string id=CustodyJson.Hash(CustodyJson.Bytes(new { tenant="tenant",scope="native-reencryption",operationId,revision,digest }));
            _head.State.Originals["native-observation-original:"+id]=new(JsonSerializer.SerializeToElement(statement,CustodyBindingContract.Json),null);
        }
        public Task<string?> InvokeAsync(string appId,string method,string body,CancellationToken token)=>throw new Exception("Uncredentialed holder source.");
        public async Task<string?> InvokeCredentialBoundAsync(string appId,string credential,string method,string body,CancellationToken token)
        {
            if(appId!=Binding.AppId || credential!=Binding.CredentialReference) throw new Exception("Holder original credential substitution.");
            var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
            context.Request.Headers["dapr-api-token"]="fixture-holder-channel";context.Request.Headers.Authorization="Bearer fixture-holder-caller";
            context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body));context.Response.Body=new MemoryStream();
            await(await _receiver.HandleAsync(context,method[(method.LastIndexOf('/')+1)..],token)).ExecuteAsync(context);
            return context.Response.StatusCode==200?Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()):null;
        }
        public void Dispose() { _dapr.Dispose();_signer.Dispose();File.Delete(_channel); }
        private sealed class HeadTransport(IndependentAuthorityState state):HttpMessageHandler
        {
            internal IndependentAuthorityState State=state;private int _revision=1;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            {
                byte[] incoming=await request.Content!.ReadAsByteArrayAsync(token);bool read=request.RequestUri!.AbsolutePath.EndsWith("/GetState",StringComparison.Ordinal);
                if(!read)
                {
                    if(!request.RequestUri.AbsolutePath.EndsWith("/SaveState",StringComparison.Ordinal)) throw new Exception("Unexpected holder source operation.");
                    string store="",key="",etag="";byte[]? value=null;int entries=0;
                    using var input=new CodedInputStream(incoming,5,incoming.Length-5);uint tag;
                    while((tag=input.ReadTag())!=0)
                    {
                        if(WireFormat.GetTagFieldNumber(tag)==1) store=input.ReadString();
                        else if(WireFormat.GetTagFieldNumber(tag)==2)
                        {
                            entries++;using var item=new CodedInputStream(input.ReadBytes().ToByteArray());uint itemTag;
                            while((itemTag=item.ReadTag())!=0)
                            {
                                int field=WireFormat.GetTagFieldNumber(itemTag);
                                if(field==1) key=item.ReadString();else if(field==2)value=item.ReadBytes().ToByteArray();
                                else if(field==3) { using var version=new CodedInputStream(item.ReadBytes().ToByteArray());if(version.ReadTag()!=10)throw new Exception("Exact holder etag required.");etag=version.ReadString();if(version.ReadTag()!=0)throw new Exception("Closed holder etag required."); }
                                else item.SkipLastField();
                            }
                        }
                        else input.SkipLastField();
                    }
                    var next=value is null?null:JsonSerializer.Deserialize<IndependentAuthorityState>(value,new JsonSerializerOptions(CustodyBindingContract.Json) { PropertyNameCaseInsensitive=true });
                    if(entries!=1 || store!="fixture" || key!="head" || etag!=_revision.ToString() || next is null || next.Revision!=State.Revision+1
                        || !CustodyJson.Bytes(State with { Revision=next.Revision }).AsSpan().SequenceEqual(CustodyJson.Bytes(next))) throw new Exception("Holder result ordering cannot change any original or permission.");
                    State=next;_revision++;
                }
                using var wire=new MemoryStream();if(read) using(var proto=new CodedOutputStream(wire,true)) { proto.WriteTag(1,WireFormat.WireType.LengthDelimited);proto.WriteBytes(ByteString.CopyFrom(CustodyJson.Bytes(State)));proto.WriteTag(2,WireFormat.WireType.LengthDelimited);proto.WriteString(_revision.ToString());proto.Flush(); }
                byte[] payload=wire.ToArray(),framed=new byte[payload.Length+5];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(framed.AsSpan(1,4),payload.Length);payload.CopyTo(framed,5);
                var response=new HttpResponseMessage(HttpStatusCode.OK) { Version=HttpVersion.Version20,Content=new ByteArrayContent(framed) };response.Content.Headers.ContentType=new("application/grpc");response.TrailingHeaders.Add("grpc-status","0");return response;
            }
        }
    }
    private static async Task TenantKekExchange(bool derived)
    {
        using var fixture=new Fixture();using var dapr=new DaprClientBuilder().Build();string credential=Path.GetTempFileName();File.WriteAllText(credential,"fixture-native-token");
        byte[] kek=RandomNumberGenerator.GetBytes(32);
        try
        {
            var metadata=JsonSerializer.SerializeToElement(new { type="aes256-gcm96",exportable=false,allow_plaintext_backup=false,derived });using var transport=new NativeTransport(metadata,derived);
            var epochs=fixture.Profile.Epochs.Select(value=>value with { KeyArn="openbao-transit:"+value.OpaqueId }).ToArray();
            var keks=fixture.Profile.Keks.Select(value=>value with { ProtectorArn="openbao-transit:"+value.ProtectorKeyId,EncryptedKey=transport.PrepareTenantKek(value.ProtectorKeyId,value.TenantId,value.Version,kek) }).ToArray();
            string[] resources=epochs.Select(value=>value.KeyArn).Concat(keks.Select(value=>value.ProtectorArn)).ToArray();
            var installation=new NativeInstallation("OpenBaoTransit","fixture","head","/fixture/checkpoints","/fixture/originals",Fixture.Digest,"https://fixture.invalid/","transit",credential,
                resources.ToDictionary(value=>value,_=>CustodyJson.Hash(CustodyJson.Bytes(metadata))),Fixture.Digest,TransitBindingProfiles:resources.ToDictionary(value=>value,_=>new OpenBaoTransitBindingProfile("OpenBaoTransit.AeadAssociatedData.V1",derived)));
            var profile=fixture.Profile with { Epochs=epochs,Keks=keks };fixture.Profile=profile;fixture.Verifier=new(profile,TimeProvider.System);fixture.Qualify();
            using var native=new OpenBaoNativeOperations(profile,installation,fixture.Store,dapr,new NonOwningTransport(transport));var exchange=new KeyExchange(profile,fixture.Store,native,native,fixture.Verifier);
            SignedStatement Admission(Hexalith.Platform.Secrets.Runtime.CustodyBindingRequestV1 request)
            {
                var use=request.WrapInput?.KeyUse??request.KeyUse!;
                var claim=new KeyAdmission(use,request.Envelope.RequestDigest,profile.ProfileDigest,new[]{"caller","sdk","sidecar","uds","grpc","tls","component","native"}.Select(role=>new KeyHolder("fixture-"+use.KeyUseLeaseId+role,role,use.RecipientProcessIncarnation,use.TransportIncarnation,"independent-owner",Fixture.Digest)).ToArray());
                return fixture.Sign("independent-observer",CustodyJson.Hash(CustodyJson.Bytes(claim)),claim);
            }
            var wrap=fixture.Wrap("openbao-actual-kek");var wrapped=await exchange.ExecuteAsync(wrap,fixture.Store.State,Admission(wrap),default);
            var registration=wrapped.WrappedRegistration??throw new Exception("Real OpenBao KEK key exchange did not wrap.");
            fixture.Qualify();var unwrap=fixture.Request("unwrap",registration,keyUse:new("read-lease","read-delivery","recipient","transport",registration.OpaqueWrappedKeyReference));
            var opened=await exchange.ExecuteAsync(unwrap,fixture.Store.State,Admission(unwrap),default);
            if(opened.KeyDelivery is not { } delivered || !delivered.RootKey.AsSpan().SequenceEqual(Fixture.Root)) throw new Exception("Real OpenBao tenant KEK failed KeyExchange unwrap.");
            CryptographicOperations.ZeroMemory(delivered.RootKey);
            byte[] independentlyOpened=await native.ReadTenantKekAsync("tenant","kek",default);if(!independentlyOpened.AsSpan().SequenceEqual(kek)) throw new Exception("Independent tenant KEK AAD mismatch.");CryptographicOperations.ZeroMemory(independentlyOpened);
            foreach(string substitution in new[]{"tenant","version","resource"})
            {
                var changed=profile with { Keks=keks.Select(value=>value.TenantId!="tenant"?value:substitution switch { "tenant"=>value with { TenantId="foreign" },"version"=>value with { Version="changed" },_=>value with { ProtectorArn=epochs[0].KeyArn } }).ToArray() };
                using var refused=new OpenBaoNativeOperations(changed,installation,fixture.Store,dapr,new NonOwningTransport(transport));
                await Refused(()=>refused.ReadTenantKekAsync(substitution=="tenant"?"foreign":"tenant",substitution=="version"?"changed":"kek",default));
            }
            transport.WrongAad=true;await Refused(()=>native.ReadTenantKekAsync("tenant","kek",default));
        }
        finally { CryptographicOperations.ZeroMemory(kek);File.Delete(credential); }
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
    private sealed class NonOwningTransport(HttpMessageHandler inner):HttpMessageHandler
    {
        private readonly HttpMessageInvoker _invoke=new(inner,false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>_invoke.SendAsync(request,token);
        protected override void Dispose(bool disposing) { _invoke.Dispose();base.Dispose(disposing); }
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
        private readonly Dictionary<string,byte[]> _keys=[];private readonly HashSet<string> _deleted=[];internal int Deletes;
        internal int Effects;internal bool Substitute,Malformed,WrongAad;internal byte[]? LastAad;
        private byte[] Key(string resource) { if(!_keys.TryGetValue(resource,out var key)) _keys.Add(resource,key=RandomNumberGenerator.GetBytes(32));return key; }
        internal byte[] PrepareTenantKek(string resource,string tenant,string version,byte[] kek)
        {
            // Prepared independently of OpenBaoNativeOperations, with the actual physical resource's distinct key and the exact tenant/version/purpose AAD.
            byte[] aad=JsonSerializer.SerializeToUtf8Bytes(new { schema="custody-native-aad-v1",authority="authority",incarnation="incarnation",tenant,purpose="custody-tenant-kek-v1",identity=version });
            byte[] nonce=RandomNumberGenerator.GetBytes(12),cipher=new byte[32],tag=new byte[16];using(var aes=new AesGcm(Key(resource),16)) aes.Encrypt(nonce,kek,cipher,tag,aad);
            return Encoding.UTF8.GetBytes("vault:v1:"+Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray()));
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri!.Host!="fixture.invalid" || request.RequestUri.Scheme!="https" || request.Headers.GetValues("X-Vault-Token").Single()!="fixture-native-token") throw new Exception("Endpoint or scoped credential substituted.");
            string path=request.RequestUri.AbsolutePath;
            string resource=path[(path.LastIndexOf('/')+1)..];
            if(_deleted.Contains(resource)) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if(request.Method==HttpMethod.Delete)
            {
                if(!path.StartsWith("/v1/transit/keys/",StringComparison.Ordinal)) throw new Exception("Changed source-cut endpoint.");
                Deletes++;_deleted.Add(resource);if(_keys.Remove(resource,out var removed)) CryptographicOperations.ZeroMemory(removed);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
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
                || binding.GetProperty("purpose").GetString() is not ("custody-outer-key-v1" or "custody-tenant-kek-v1")) throw new Exception("Closed authority/purpose AAD missing.");
            if(derived!=input.TryGetProperty("context",out var context) || derived && context.GetString()!=input.GetProperty("associated_data").GetString()) throw new Exception("Derivation context mapping changed.");
            byte[] aad=WrongAad?Encoding.UTF8.GetBytes("wrong-tenant-context"):LastAad;
            if(path.Contains("/encrypt/",StringComparison.Ordinal))
            {
                Effects++;byte[] plain=Convert.FromBase64String(input.GetProperty("plaintext").GetString()!);byte[] nonce=RandomNumberGenerator.GetBytes(12),cipher=new byte[plain.Length],tag=new byte[16];
                using var aes=new AesGcm(Key(path[(path.LastIndexOf('/')+1)..]),16);aes.Encrypt(nonce,plain,cipher,tag,aad);CryptographicOperations.ZeroMemory(plain);
                return Json(new { data=new { ciphertext="vault:v1:"+Convert.ToBase64String(nonce.Concat(cipher).Concat(tag).ToArray()) } });
            }
            if(!path.Contains("/decrypt/",StringComparison.Ordinal)) throw new Exception("Unexpected native mutation endpoint.");
            byte[] encrypted=Convert.FromBase64String(input.GetProperty("ciphertext").GetString()![9..]),result=new byte[32];
            using(var aes=new AesGcm(Key(path[(path.LastIndexOf('/')+1)..]),16)) aes.Decrypt(encrypted.AsSpan(0,12),encrypted.AsSpan(12,32),encrypted.AsSpan(44,16),result,aad);
            string encoded=Convert.ToBase64String(result);CryptographicOperations.ZeroMemory(result);return Json(new { data=new { plaintext=encoded } });
        }
        private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(value)) };
        protected override void Dispose(bool disposing) { foreach(var key in _keys.Values) CryptographicOperations.ZeroMemory(key);base.Dispose(disposing); }
    }
}
