using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Protection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// The shipped executor process, real retained Python attempt admission and actual C# original receiver.
// Disposable signatures/evidence establish source dispatch only; no native case or cohort phase executes.
public sealed class JointPhaseFinalizerEntrypointTests
{
    [Theory]
    [InlineData("finalize")][InlineData("phase-success")][InlineData("phase-timeout")][InlineData("call-timeout")][InlineData("startup-timeout")]
    public async Task AcyclicPinnedInstallationsAdmitRealMetadataOnlyEntrypointWithoutNormalRuntimeOrSecondEffect(string mode)
    {
        string directory=Path.Combine(Path.GetTempPath(),"story54-fixture-current-cohort-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));var token=timeout.Token;
        using var cohortKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var owner=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var independent=new IndependentAuthorityReceiverTests.Fixture();
        using var listener=new HttpListener();Task? serving=null;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory,"fixture-key.pem"),cohortKey.ExportECPrivateKeyPem(),token);
            await File.WriteAllTextAsync(Path.Combine(directory,"owner-private.pem"),owner.ExportECPrivateKeyPem(),token);await File.WriteAllTextAsync(Path.Combine(directory,"owner-public.pem"),owner.ExportSubjectPublicKeyInfoPem(),token);
            // This valid JSON lacks all normal runtime role/native inputs. Ordinary composition cannot run any selected secret.
            await File.WriteAllTextAsync(Path.Combine(directory,"runtime.json"),"{}",token);
            foreach(string name in new[]{"executor","secrets","command","cohort"}) await File.WriteAllTextAsync(Path.Combine(directory,name+"-credential"),"isolated-caller",token);
            var partitions=(await Python(directory,"describe",new(),token))["partitions"]!.AsObject();var installations=new JsonObject();string target=new('a',40);
            IndependentDaprAuthorityBinding Binding(string purpose)=>independent.PublicBinding with { CredentialReference="file:"+Path.Combine(directory,purpose+"-credential") };
            byte[] publicKey=Encoding.ASCII.GetBytes(cohortKey.ExportSubjectPublicKeyInfoPem());
            for(int phase=0;phase<4;phase++)
            {
                var installed=new JointPhaseCommandInstallation(new("",target,phase,partitions[phase.ToString()]!.AsArray().Select(value=>value!.GetValue<string>()).ToArray()),Path.Combine(directory,"runtime.json"),null,
                    Binding("executor"),[],Path.Combine(directory,"evidence"),Path.Combine(directory,"owner-private.pem"),Path.Combine(directory,"owner-public.pem"),null,
                    JointSecretsOriginals:Binding("secrets"),JointCommandOriginals:Binding("command"),
                    CohortAttempt:new("fixture-cohort","file:"+Path.Combine(directory,"cohort-credential"),Encoding.ASCII.GetString(publicKey),Convert.ToHexStringLower(SHA256.HashData(publicKey)),""),BindingSchema:"Platform.JointPhaseInstallation.v2",CompletionTimeoutSeconds:mode=="phase-timeout"?1:30);
                string path=Path.Combine(directory,"installation-"+phase+".json");await File.WriteAllBytesAsync(path,JsonSerializer.SerializeToUtf8Bytes(installed,CustodyBindingContract.Json),token);installations[phase.ToString()]=path;
            }
            // Finite order: exact installation bytes -> artifact hashes -> profile pins -> final binding -> authenticated retained request.
            var constructed=await Python(directory,"construct-v2",new() { ["platformCommit"]=target,["installations"]=installations.DeepClone() },token);var binding=constructed["binding"]!.AsObject();
            var before=Directory.GetFiles(directory,"installation-*.json").ToDictionary(path=>path,path=>SHA256.HashData(File.ReadAllBytes(path)));
            await Python(directory,"init",new() { ["binding"]=binding.DeepClone() },token);
            var reserved=await Python(directory,"reserve",new(),token);var request=reserved["phaseRequest"]!.AsObject();string id=request["requestId"]!.GetValue<string>();
            independent.State=independent.State with { Callers=independent.State.Callers.Select(value=>value with { Role="joint-phase-command",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
            var originals=new InstalledQualificationOriginalStore(independent.Client());
            if(mode!="finalize")
            {
                var slow=new SlowReceivingRead();using var callSource=new IndependentAuthorityReceiverTests.Fixture(handlers:[slow]);callSource.State=callSource.State with { Callers=callSource.State.Callers.Select(value=>value with { Methods=["fixture-bounded-read"] }).ToArray() };
                var secrets=new BudgetSecrets(mode,callSource.Client());
                var transport=new PhaseTransport(independent.PrivateTransport(),(value,ct)=>Python(directory,"rpc",value,ct));
                Stream Input()=>mode=="startup-timeout"?new StalledInput():new MemoryStream(Encoding.UTF8.GetBytes(request.ToJsonString()));
                Task<int> Run(bool final=false)
                {
                    Stream input=Input();
                    return JointPhaseCommandExecutable.RunAsync(installations["1"]!.GetValue<string>(),input,token,final,TimeSpan.FromMilliseconds(800),transport,
                        (installed,stores,signer,admission)=>new(installed.Scope,stores.Command,new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>>(),null,installed.EvidenceDirectory,signer,secrets,stores.Executor,admission));
                }
                if(mode=="phase-success") { (await Run()).ShouldBe(0);secrets.Effects.ShouldBe(request["cases"]!.AsArray().Count); }
                else { await Should.ThrowAsync<OperationCanceledException>(()=>Run());secrets.Effects.ShouldBe(mode=="startup-timeout"?0:1); }
                if(mode!="startup-timeout")
                {
                    int effects=secrets.Effects;(await Run(true)).ShouldBe(mode=="phase-success"?0:3);secrets.Effects.ShouldBe(effects);
                }
                foreach(var pair in before) SHA256.HashData(await File.ReadAllBytesAsync(pair.Key,token)).ShouldBe(pair.Value);
                return;
            }
            await originals.BeginAsync("joint-phase-command",id,request,token);
            string evidence=Path.Combine(directory,"evidence",id);Directory.CreateDirectory(evidence);
            foreach(var caseNode in request["cases"]!.AsArray())
            {
                string caseId=caseNode!.GetValue<string>();var value=new JsonObject { ["cohortRequestId"]=id,["request"]=new JsonObject { ["runId"]=reserved["attemptId"]!.DeepClone(),["targetCommit"]=target,["caseId"]=caseId[8..] } };
                await ImmutableFileArchive.PublishAsync(Path.Combine(evidence,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(caseId)))+".json"),QualificationProofVerifier.CanonicalBytes(value),token);
            }
            var methods=new List<string>();using var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
            listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
            serving=Task.Run(async()=>
            {
                while(!token.IsCancellationRequested && listener.IsListening)
                {
                    HttpListenerContext context;try { context=await listener.GetContextAsync().WaitAsync(token); }catch(Exception) { return; }
                    try
                    {
                        using var input=new StreamReader(context.Request.InputStream);string body=await input.ReadToEndAsync(token);string path=context.Request.Url!.AbsolutePath;
                        methods.Add(path);string? response;
                        if(path.Contains("/joint/v1/phase-admission",StringComparison.Ordinal)) response=(await Python(directory,"rpc",JsonNode.Parse(body)!.AsObject(),token)).ToJsonString();
                        else response=await independent.PrivateTransport().InvokeCredentialBoundAsync("isolated-owner","file:/isolated-bearer",path,body,token);
                        if(response is null) { context.Response.StatusCode=503;context.Response.Close();continue; }
                        byte[] bytes=Encoding.UTF8.GetBytes(response);context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes,token);context.Response.Close();
                    }
                    catch(Exception) { context.Response.StatusCode=503;context.Response.Close(); }
                }
            },token);
            async Task<int> Launch(string flag)
            {
                var start=new ProcessStartInfo("dotnet") { RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
                foreach(string arg in new[]{"exec","--runtimeconfig",Path.ChangeExtension(typeof(JointPhaseFinalizerEntrypointTests).Assembly.Location,"runtimeconfig.json"),"--depsfile",Path.ChangeExtension(typeof(JointPhaseFinalizerEntrypointTests).Assembly.Location,"deps.json"),Path.Combine(AppContext.BaseDirectory,"Hexalith.Platform.Secrets.Qualification.Executor.dll"),flag}) start.ArgumentList.Add(arg);
                start.Environment["PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION"]=installations["1"]!.GetValue<string>();start.Environment["DAPR_HTTP_ENDPOINT"]="http://127.0.0.1:"+port;start.Environment["DAPR_HTTP_PORT"]=port.ToString();start.Environment.Remove("PLATFORM_QUALIFICATION_INSTALLATION");
                using var process=Process.Start(start)!;await process.StandardInput.WriteAsync(request.ToJsonString());process.StandardInput.Close();string error=await process.StandardError.ReadToEndAsync(token);await process.StandardOutput.ReadToEndAsync(token);await process.WaitForExitAsync(token);if(process.ExitCode is not (0 or 3)) throw new InvalidOperationException(error);return process.ExitCode;
            }
            (await Launch("--phase-finalize")).ShouldBe(0);
            var exact=await originals.LookupResultAsync("joint-phase-command",id,token);exact.ShouldNotBeNull();byte[] index=await File.ReadAllBytesAsync(Path.Combine(evidence,"owner-original.json"),token);
            JsonNode.DeepEquals(JsonNode.Parse(index),exact).ShouldBeTrue();owner.VerifyData(QualificationProofVerifier.CanonicalBytes(exact["payload"]!),Convert.FromBase64String(exact["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence).ShouldBeTrue();
            (await Launch("--phase-finalize")).ShouldBe(0);(await File.ReadAllBytesAsync(Path.Combine(evidence,"owner-original.json"),token)).ShouldBe(index);
            // This also proves deleting Program's flag forwarding switches to failing normal composition.
            (await Launch("--phase-command")).ShouldBe(3);
            methods.ShouldAllBe(path=>path.EndsWith("phase-admission",StringComparison.Ordinal) || path.EndsWith("qualification-lookup-original",StringComparison.Ordinal) || path.EndsWith("qualification-retain-result",StringComparison.Ordinal));
            foreach(var pair in before) SHA256.HashData(await File.ReadAllBytesAsync(pair.Key,token)).ShouldBe(pair.Value);
        }
        finally { timeout.Cancel();listener.Stop();if(serving is not null) await serving;Directory.Delete(directory,true); }
    }
    private sealed class PhaseTransport(ISecretsCredentialBoundDaprInvoker originals,Func<JsonObject,CancellationToken,Task<JsonObject>> cohort):ISecretsCredentialBoundDaprInvoker
    {
        public Task<string?> InvokeAsync(string app,string method,string json,CancellationToken token)=>Task.FromResult<string?>(null);
        public async Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string json,CancellationToken token)
            =>method.StartsWith("joint/v1/",StringComparison.Ordinal)?(await cohort(JsonNode.Parse(json)!.AsObject(),token)).ToJsonString():await originals.InvokeCredentialBoundAsync(app,credential,method,json,token);
    }
    private sealed class BudgetSecrets(string mode,IndependentDaprAuthority source):IJointSecretsCaseExecutor
    {
        internal int Effects;
        public Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject request,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public async Task<JsonObject?> ExecuteAsync(string caseId,JsonObject request,CancellationToken token)
        {
            if(++Effects==1)
            {
                if(mode=="call-timeout") { using var call=new CancellationTokenSource(TimeSpan.FromMilliseconds(60));await source.InvokeAsync<JsonElement>("fixture-bounded-read",new { requestId=request["requestId"] },call.Token); }
                else await Task.Delay(mode=="phase-timeout"?TimeSpan.FromSeconds(2):TimeSpan.FromMilliseconds(1100),token);
            }
            return new JsonObject { ["cohortRequestId"]=request["requestId"]!.DeepClone(),["request"]=new JsonObject { ["runId"]=request["attemptId"]!.DeepClone(),["targetCommit"]=new string('a',40),["caseId"]=caseId[8..] } };
        }
    }
    private sealed class SlowReceivingRead:IIndependentOwnerOperationHandler
    {
        public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fixture-bounded-read"]);
        public bool IsReadOnly(string method)=>true;
        public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState state,CancellationToken token)
        { await Task.Delay(Timeout.InfiniteTimeSpan,token);return null; }
    }
    private sealed class StalledInput:MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        { await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);return 0; }
    }
    private static async Task<JsonObject> Python(string directory,string mode,JsonObject value,CancellationToken token,[System.Runtime.CompilerServices.CallerFilePath]string source="")
    {
        string repository=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"../.."));var start=new ProcessStartInfo("python3") { RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        foreach(string arg in new[]{Path.Combine(repository,"tests/Hexalith.Platform.Secrets.Runtime.Tests/Fixtures/current_cohort_protocol.py"),directory,repository,mode}) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;await process.StandardInput.WriteAsync(value.ToJsonString());process.StandardInput.Close();string output=await process.StandardOutput.ReadToEndAsync(token),error=await process.StandardError.ReadToEndAsync(token);await process.WaitForExitAsync(token);process.ExitCode.ShouldBe(0,error);return JsonNode.Parse(output)!.AsObject();
    }
}
