using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Disposable loopback sidecar and public HTTP request. Concrete installed providers, verifier,
// authenticator, durable store and receiving owner are never replaced by test providers.
internal static class InstalledQualificationRoleServingFixture
{
    internal static async Task VerifyAsync(string role,JsonObject caseRequest,IndependentAuthorityReceiverTests.Fixture originalsAuthority,
        IQualificationOriginalStore originals,IReadOnlyDictionary<string,IQualificationOwnerCaseDriver> drivers,ECDsa executorKey,ECDsa observerKey,
        QualificationOwnerOperationHandler executor,QualificationOwnerOperationHandler observer,CancellationToken token)
    {
        string directory=Path.Combine(Path.GetTempPath(),"installed-role-source-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        using var enrollmentKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var foreignKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var scopes=new IndependentAuthorityReceiverTests.Fixture();using var listener=new HttpListener();using var stop=new CancellationTokenSource();
        var priorBindings=originalsAuthority.State.QualificationBindings;var priorCases=originalsAuthority.State.QualificationCases;var priorCallers=originalsAuthority.State.Callers;
        var request=(JsonObject)caseRequest.DeepClone();var owner=role=="executor"?executor:observer;
        if(role=="enrollment")
        {
            foreach(string field in new[]{"caseId","caseTargetId","enrolledScope","scenario","requestId"}) request.Remove(field);
            request["operation"]="enroll";request["requestId"]=QualificationProtocolSchema.Digest(request);
            var enrolledCases=new Dictionary<string,JsonElement>();var preflightDrivers=new Dictionary<string,IQualificationOwnerCaseDriver>();
            foreach(string id in QualificationProtocolSchema.CaseIds)
            {
                string target=Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(id)));var scope=(JsonObject)caseRequest["enrolledScope"]!.DeepClone();scope["logicalId"]=target;scope["targetIds"]=new JsonArray(target);
                enrolledCases[id]=JsonSerializer.SerializeToElement(new { caseTargetId=target,enrolledScope=scope },CustodyBindingContract.Json);preflightDrivers[id]=new EnrollmentOnlyFixtureRecipe(id);
            }
            var bindings=new Dictionary<string,string>(originalsAuthority.State.QualificationBindings!);
            foreach(var pair in new[]{("executorIdentity","executor"),("executorVersion","fixture-v1"),("observerIdentity","observer"),("observerVersion","fixture-v1"),("qualifiedReferencesSha256",new string('b',64)),("currentProfileSha256",new string('c',64))}) bindings[pair.Item1]=JsonSerializer.Serialize(pair.Item2);
            originalsAuthority.State=originalsAuthority.State with { QualificationBindings=bindings,QualificationCases=enrolledCases };
            owner=new(new("enrollment","enrollment","fixture-v1",enrollmentKey.ExportSubjectPublicKeyInfoPem()),enrollmentKey,originals,preflightDrivers,TimeProvider.System);
        }
        originalsAuthority.State=originalsAuthority.State with { Callers=originalsAuthority.State.Callers.Select(value=>value with { Role=role }).ToArray() };
        using var ownerAuthority=new IndependentAuthorityReceiverTests.Fixture(handlers:[owner]);
        string operation=role=="executor"?"execute":role=="observer"?"observe":"enroll";
        ownerAuthority.State=ownerAuthority.State with { QualificationBindings=originalsAuthority.State.QualificationBindings,QualificationCases=originalsAuthority.State.QualificationCases,
            Callers=ownerAuthority.State.Callers.Select(value=>value with { Role=role,Methods=["qualification-"+role+"-"+operation] }).ToArray() };
        var workload=scopes.State.Callers[0] with { BearerSha256=Convert.ToHexString(SHA256.HashData("public-fixture-workload"u8)),Identity="public-fixture-caller",ScopeReference="public-fixture-scope",Role=role,Methods=[] };
        scopes.State=scopes.State with { QualificationBindings=originalsAuthority.State.QualificationBindings,QualificationCases=originalsAuthority.State.QualificationCases,
            Callers=[scopes.State.Callers[0] with { Methods=["qualification-authenticate","qualification-authorize"] },workload] };
        scopes.State=scopes.State with { QualificationCases=scopes.State.QualificationCases!.ToDictionary(pair=>pair.Key,pair=>
            { var enrolled=JsonNode.Parse(pair.Value.GetRawText())!.AsObject();enrolled["caseId"]=pair.Key;return JsonSerializer.SerializeToElement(enrolled,CustodyBindingContract.Json); }) };
        string credential=Path.Combine(directory,"credential");await File.WriteAllTextAsync(credential,"isolated-caller",token);
        IndependentDaprAuthorityBinding Binding(IndependentAuthorityReceiverTests.Fixture fixture,string name)=>fixture.PublicBinding with { AppId=name,CredentialReference="file:"+credential };
        QualificationInstalledAnchor Anchor(string identity,ECDsa key) { string pem=key.ExportSubjectPublicKeyInfoPem();return new(identity,"fixture-v1",pem,Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(pem)))); }
        var anchors=new Dictionary<string,QualificationInstalledAnchor>{{"executor",Anchor("executor",executorKey)},{"observer",Anchor("observer",observerKey)},{"enrollment",Anchor("enrollment",enrollmentKey)}};
        var installation=new QualificationInstallation(role,Binding(scopes,"fixture-scope-service"),Binding(originalsAuthority,"fixture-original-service"),Binding(ownerAuthority,"fixture-owner-service"),anchors);
        string path=Path.Combine(directory,"installation.json");await File.WriteAllBytesAsync(path,JsonSerializer.SerializeToUtf8Bytes(installation,CustodyBindingContract.Json),token);
        using var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();string url="http://127.0.0.1:"+port+"/";
        listener.Prefixes.Add(url);listener.Start();using var dapr=Substitute.For<DaprClient>();
        dapr.CreateInvokeMethodRequest(Arg.Any<HttpMethod>(),Arg.Any<string>(),Arg.Any<string>()).Returns(call=>new HttpRequestMessage(call.ArgAt<HttpMethod>(0),url+call.ArgAt<string>(1)+"/"+call.ArgAt<string>(2)));
        dapr.CreateInvokableHttpClient(Arg.Any<string>()).Returns(_=>new HttpClient());
        ServiceProvider Services()
        {
            var services=new ServiceCollection().AddLogging();services.AddSingleton(dapr);services.AddSingleton(TimeProvider.System);services.AddInstalledQualificationRole(role,path,token).ShouldBeNull();
            var result=services.BuildServiceProvider();result.GetRequiredService<IQualificationPublicProofVerifier>().ShouldBeOfType<QualificationProofVerifier>();result.GetRequiredService<IQualificationCallerAuthenticator>().ShouldBeOfType<InstalledQualificationCallerAuthenticator>();result.GetRequiredService<IQualificationOriginalStore>().ShouldBeOfType<InstalledQualificationOriginalStore>();return result;
        }
        var publicReceiver=new QualificationHttpReceiver();ServiceProvider active=Services();var requests=new List<Task>();var trace=new List<string>();
        var serving=Task.Run(async()=>
        {
            while(!stop.IsCancellationRequested)
            {
                HttpListenerContext context;try { context=await listener.GetContextAsync().WaitAsync(stop.Token); }catch(Exception) { break; }
                requests.Add(Handle(context));
            }
        },token);
        async Task Handle(HttpListenerContext context)
        {
            try
            {
                string route=context.Request.Url!.AbsolutePath;trace.Add(route);string? body;
                if(route=="/public")
                {
                    var http=new DefaultHttpContext { RequestServices=active };http.Response.Body=new MemoryStream();http.Request.Body=context.Request.InputStream;http.Request.Headers["dapr-api-token"]=context.Request.Headers["dapr-api-token"];http.Request.Headers.Authorization=context.Request.Headers["Authorization"];
                    IQualificationRoleProvider provider=role switch { "executor"=>active.GetRequiredService<IQualificationExecutorProvider>(),"observer"=>active.GetRequiredService<IQualificationObserverProvider>(),_=>active.GetRequiredService<IQualificationEnrollmentProvider>() };
                    provider.GetType().Name.ShouldBe("InstalledQualification"+char.ToUpperInvariant(role[0])+role[1..]);
                    var result=await publicReceiver.HandleAsync(role,operation,QualificationHostTests.Configuration(),http,"public-channel",active.GetRequiredService<IQualificationCallerAuthenticator>(),provider,active.GetRequiredService<IQualificationOriginalStore>(),active.GetRequiredService<IQualificationPublicProofVerifier>(),token);
                    await result.ExecuteAsync(http);context.Response.StatusCode=http.Response.StatusCode;body=Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
                }
                else
                {
                    context.Request.Headers["Authorization"].ShouldBe("Bearer isolated-caller");
                    var fixture=route.StartsWith("/fixture-scope-service/",StringComparison.Ordinal)?scopes:route.StartsWith("/fixture-owner-service/",StringComparison.Ordinal)?ownerAuthority:originalsAuthority;
                    using var input=new StreamReader(context.Request.InputStream);string frame=await input.ReadToEndAsync(token);
                    body=await fixture.PrivateTransport().InvokeCredentialBoundAsync("isolated-owner","file:/isolated-bearer",route,frame,token);if(body is null) context.Response.StatusCode=503;
                }
                byte[] bytes=Encoding.UTF8.GetBytes(body??"");context.Response.ContentLength64=bytes.Length;context.Response.ContentType="application/json";await context.Response.OutputStream.WriteAsync(bytes,token);context.Response.Close();
            }
            catch(Exception error) { trace.Add(error.GetType().Name+":"+error.Message);context.Response.StatusCode=503;context.Response.Close(); }
        }
        using var client=new HttpClient();
        async Task<HttpResponseMessage> Send(JsonObject input,string bearer="public-fixture-workload")
        {
            using var message=new HttpRequestMessage(HttpMethod.Post,url+"public") { Content=new StringContent(input.ToJsonString(),Encoding.UTF8,"application/json") };message.Headers.Add("dapr-api-token","public-channel");message.Headers.Add("Authorization","Bearer "+bearer);return await client.SendAsync(message,token);
        }
        try
        {
            using(var success=await Send(request))
            { string body=await success.Content.ReadAsStringAsync(token);success.StatusCode.ShouldBe(HttpStatusCode.OK,body+" "+string.Join(",",trace));var result=JsonNode.Parse(body)!.AsObject();active.GetRequiredService<IQualificationPublicProofVerifier>().Verify(role,request,result,token).ShouldBeTrue(); }
            using(var wrong=await Send(request,"wrong-workload")) wrong.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            scopes.State=scopes.State with { Callers=scopes.State.Callers.Select(value=>value.Identity==workload.Identity?value with { Role="foreign-role" }:value).ToArray() };
            using(var wrong=await Send(request)) wrong.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            scopes.State=scopes.State with { Callers=scopes.State.Callers.Select(value=>value.Identity==workload.Identity?workload:value).ToArray() };
            var foreign=(JsonObject)request.DeepClone();foreign["isolatedTargetId"]="foreign-scope";foreign.Remove("requestId");foreign["requestId"]=QualificationProtocolSchema.Digest(foreign);
            using(var wrong=await Send(foreign)) wrong.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            anchors[role]=Anchor(role,foreignKey);await File.WriteAllBytesAsync(path,JsonSerializer.SerializeToUtf8Bytes(installation,CustodyBindingContract.Json),token);active.Dispose();active=Services();
            using(var wrong=await Send(request)) wrong.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }
        finally { stop.Cancel();listener.Stop();await serving;await Task.WhenAll(requests);active.Dispose();Directory.Delete(directory,true);originalsAuthority.State=originalsAuthority.State with { QualificationBindings=priorBindings,QualificationCases=priorCases,Callers=priorCallers }; }
    }
    // All recipes are explicitly disposable enrollment-only scope fixtures; none can execute or qualify a native case.
    private sealed class EnrollmentOnlyFixtureRecipe(string id):IQualificationOwnerCaseDriver
    {
        public string CaseId=>id;
        public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment fixture cannot execute cases.");
        public Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment fixture cannot recover cases.");
        public Task<JsonObject?> ObserveAsync(JsonObject request,CancellationToken token)=>throw new InvalidOperationException("Enrollment fixture cannot observe cases.");
    }
}
