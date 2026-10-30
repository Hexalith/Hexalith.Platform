using System.Reflection;
using System.Runtime.Loader;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Net;
var runtime="/home/administrator/hexalith-protection-sessions/ext-protection-loop7-recovered-cd78452ebe/platform/artifacts/ext-protection-loop7-qualification-runtime-20";
AssemblyLoadContext.Default.Resolving+=(context,name)=>File.Exists(Path.Combine(runtime,name.Name+".dll"))?context.LoadFromAssemblyPath(Path.Combine(runtime,name.Name+".dll")):null;
var tests=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"Hexalith.Platform.Protection.Tests.dll"));
var protection=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"Hexalith.Platform.Protection.dll"));
var custodyAssembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"Hexalith.Platform.Custody.dll"));
var contracts=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"Hexalith.EventStore.Contracts.dll"));
const BindingFlags all=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
object Create(Type t,params object?[] a)=>Activator.CreateInstance(t,all,null,a,null)!;
object? Get(object o,string n)=>o.GetType().GetProperty(n,all)!.GetValue(o);
async Task<object?> Call(object o,string n,params object?[] a){var m=o.GetType().GetMethods(all).Single(x=>x.Name==n&&x.GetParameters().Length==a.Length);var v=m.Invoke(o,a);if(v is Task t){await t;return t.GetType().GetProperty("Result")?.GetValue(t);}return v;}
object Engine()=>Create(tests.GetType("Hexalith.Platform.Protection.Tests.ErasureRootEngineFixture")!,new object?[]{null});
var results=new Dictionary<string,object>();
foreach(var mode in new[]{"missing-transit","denied-encryption"})
using(var engine=(IDisposable)Engine())
using(var independent=(IDisposable)Create(tests.GetType("Hexalith.Platform.Protection.Tests.ErasureRootIndependentSourceFixture")!))
{
 var store=Get(engine,"Store")!;var custody=Get(engine,"Custody")!;var permissions=Get(engine,"Permissions")!;
 var enrollment=await Call(engine,"Enroll","directory","01ARZ3NDEKTSV4RRFFQ69G5FAV",(ulong)1);
 var cellType=protection.GetType("Hexalith.Platform.Protection.ProtectionQualificationNativeCell")!;
 var cell=Create(cellType,"independent","root",new Dictionary<string,string>(),null);
 enrollment!.GetType().GetProperty("Writer")!.SetValue(enrollment,Create(protection.GetType("Hexalith.Platform.Protection.ErasureRootWriterReference")!,"independent","writer",new Dictionary<string,string>()));
 var dictionary=(IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string),cellType))!;dictionary.Add("tenant-a",cell);
 var source=Create(protection.GetType("Hexalith.Platform.Protection.ErasureRootNativeOccurrenceSource")!,custody,permissions,Get(independent,"Native"),Get(engine,"Digests"),dictionary);
 if(mode=="missing-transit") { ((IDictionary)store.GetType().GetField("_epochs",all)!.GetValue(store)!).Remove("tenant-a"); }
 else { var storeInterface=protection.GetType("Hexalith.Platform.Protection.IErasureRootStore")!;var denied=DispatchProxy.Create(storeInterface,typeof(StoreSpy));((StoreSpy)denied).Inner=store;custody=Create(protection.GetType("Hexalith.Platform.Protection.ErasureRootCustody")!,denied,Get(custody,"Installation"),permissions);source=Create(protection.GetType("Hexalith.Platform.Protection.ErasureRootNativeOccurrenceSource")!,custody,permissions,Get(independent,"Native"),Get(engine,"Digests"),dictionary); }
 var preflight=await Call(custody,"PreflightWrapAsync",Get(enrollment,"RootIdentity"),Get(enrollment,"RootOperationId"),Get(enrollment,"ExpectedLifecycleRevision"),CancellationToken.None);
 var registered=await Call(source,"RegisterOriginalAsync",enrollment,CancellationToken.None);
 var wrapped=await Call(custody,"WrapAsync",Get(enrollment,"RootIdentity"),Get(enrollment,"RootOperationId"),Get(enrollment,"ExpectedLifecycleRevision"),CancellationToken.None);
 results[mode]=new {NativeEpochMetadataAvailable=mode=="denied-encryption",Preflight=preflight,IndependentSourceRegistration=registered,IndependentSourceWrites=Get(independent,"Revision"),ErcCasWrites=Get(store,"CasCount"),RootRefused=wrapped is null};
}
using(var engine=(IDisposable)Engine())
{
 var store=Get(engine,"Store")!;var custody=Get(engine,"Custody")!;var permissions=Get(engine,"Permissions")!;
 var enrollment=await Call(engine,"Enroll","directory","01ARZ3NDEKTSV4RRFFQ69G5FAV",(ulong)1);
 var head=await Call(store,"HeadAsync","tenant-a");var anchors=(IDictionary)Get(head!,"Anchors")!;var cursor=anchors.GetEnumerator();cursor.MoveNext();var scope=(string)cursor.Key;var digest=(string)Get(cursor.Value!,"Digest")!;anchors.Clear();await Call(store,"ReplaceHead","tenant-a",head);
 var preflight=await Call(custody,"PreflightWrapAsync",Get(enrollment!,"RootIdentity"),Get(enrollment!,"RootOperationId"),Get(enrollment!,"ExpectedLifecycleRevision"),CancellationToken.None);
 var wrapped=await Call(custody,"WrapAsync",Get(enrollment!,"RootIdentity"),Get(enrollment!,"RootOperationId"),Get(enrollment!,"ExpectedLifecycleRevision"),CancellationToken.None);
 var authority=Create(protection.GetType("Hexalith.Platform.Protection.ErasureRootTransitionAuthority")!,custody,permissions,"tenant-a",scope);
 var valid=await Call(authority,"ValidateStateAsync",(long)0,digest,CancellationToken.None);
 results["missingGenesis"]=new {Preflight=preflight,RootCreated=wrapped is not null,MissingAnchors=anchors.Count,ActualOwnerGenesisValidation=valid};
}
{
 var iface=contracts.GetType("Hexalith.EventStore.Contracts.Security.IAnchoredStateTransitionAuthority")!;var read=iface.GetMethod("ReadAdmittedOriginalAsync")!;var transitionType=read.ReturnType.GenericTypeArguments[0];
 var transition=Create(transitionType,"scope",(long)0,(long)1,new string('A',64),new string('B',64),Encoding.UTF8.GetBytes("{}"));
 var inner=DispatchProxy.Create(iface,typeof(AuthoritySpy));((AuthoritySpy)inner).Original=transition;
 var deadline=Create(custodyAssembly.GetType("Hexalith.Platform.Custody.PrivateOwnerOperationDeadline")!,TimeProvider.System,CancellationToken.None);
 var wrapper=Create(custodyAssembly.GetType("Hexalith.Platform.Custody.DeadlineAnchoredStateAuthority")!,inner,deadline);
 var pending=(Task)read.Invoke(wrapper,new object[]{"scope",CancellationToken.None})!;await pending;
 results["deadlineWrapper"]=new {HasIndependentOriginal=true,ReturnedNull=pending.GetType().GetProperty("Result")!.GetValue(pending) is null,UnderlyingReadCalls=((AuthoritySpy)inner).Calls};
}
{
 var folder=Path.Combine(Path.GetTempPath(),"protection-r8-client-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
 try
 {
  using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var anchor=Path.Combine(folder,"public.pem");File.WriteAllText(anchor,key.ExportSubjectPublicKeyInfoPem());
  var credentialName="EXT_PROTECTION_ROLE_PARENT_R8";Environment.SetEnvironmentVariable(credentialName,"owned-component-test-bearer");
  var document=new Dictionary<string,string>{{"Schema","ext-protection-1-role-program/v1"},{"Role","enrollment"},{"Identity","parent-r8"},{"Endpoint","https://accepted.invalid/role"},{"CredentialReference","env:"+credentialName},{"PublicAnchorFile",anchor},{"PublicAnchorSha256",Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(anchor)))}};
  var accepted=JsonSerializer.SerializeToUtf8Bytes(document);document["Endpoint"]="https://substitute.invalid/role";var replacement=JsonSerializer.SerializeToUtf8Bytes(document);
  var installation=JsonSerializer.Deserialize(replacement,protection.GetType("Hexalith.Platform.Protection.ProtectionQualificationRoleInstallation")!)!;
  using var handler=new CapturingHandler();using var http=new HttpClient(handler);var input=JsonSerializer.SerializeToUtf8Bytes(new {operationId="owned-probe",action="InspectInstallation",bindingDigest=new string('a',64)});
  try {await (Task)protection.GetType("Hexalith.Platform.Protection.ProtectionQualificationRoleProgram")!.GetMethod("InvokeAsync")!.Invoke(null,new object[]{http,installation,input,CancellationToken.None})!;}catch(InvalidOperationException){}
  results["roleClientConfiguration"]=new {AcceptedConfigSha256=Convert.ToHexStringLower(SHA256.HashData(accepted)),ReplacementSha256=Convert.ToHexStringLower(SHA256.HashData(replacement)),handler.Endpoint,handler.SyntheticBearerSent,ResponseFailedAfterSend=true,ExternalNetworkUsed=false};Environment.SetEnvironmentVariable(credentialName,null);
 }
 finally {Directory.Delete(folder,true);}
}
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
public class AuthoritySpy:DispatchProxy
{
 public object? Original;public int Calls;
 protected override object? Invoke(MethodInfo? method,object?[]? args)
 {
  if(method!.Name=="ReadAdmittedOriginalAsync"){Calls++;return typeof(Task).GetMethods().Single(x=>x.Name=="FromResult").MakeGenericMethod(method.ReturnType.GenericTypeArguments[0]).Invoke(null,new[]{Original});}
  return Task.FromResult(true);
 }
}
public sealed class CapturingHandler:HttpMessageHandler
{
 public string? Endpoint;public bool SyntheticBearerSent;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
 {Endpoint=request.RequestUri!.AbsoluteUri;SyntheticBearerSent=request.Headers.Authorization?.Parameter=="owned-component-test-bearer";return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));}
}

public class StoreSpy:DispatchProxy
{
 public object? Inner;
 protected override object? Invoke(MethodInfo? method,object?[]? args)
 {
  if(method!.Name=="EncryptAsync")return Task.FromException<string>(new IOException("Owned fixture denied encrypt ACL"));
  return method.Invoke(Inner,args);
 }
}
