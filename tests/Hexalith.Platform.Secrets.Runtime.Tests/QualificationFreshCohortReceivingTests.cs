using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Protection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// A fresh disposable fixture joins the real Python retained authority protocol and actual C# admission/signature handlers. No owner command or physical phase runs.
public sealed class QualificationFreshCohortReceivingTests
{
    [Fact]
    public async Task RecipesPinnedBeforeFreshUuidReserveBindEnrollExecuteAndRecoverWithoutAnyPerRunInstallationUpdate()
    {
        string directory=Path.Combine(Path.GetTempPath(),"story54-fixture-current-cohort-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            using var cohortKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var enrollmentKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var executorKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
            await File.WriteAllTextAsync(Path.Combine(directory,"fixture-key.pem"),cohortKey.ExportECPrivateKeyPem(),TestContext.Current.CancellationToken);
            var complete=QualificationHostTests.Request();var recipe=(JsonObject)complete.DeepClone();recipe.Remove("runId");recipe.Remove("requestId");string pinnedRecipe=recipe.ToJsonString();
            var binding=new JsonObject { ["platformCommit"]=recipe["targetCommit"]!.DeepClone(),["phaseExecutionProfileSha256"]=new JsonObject() };
            for(int phase=0;phase<4;phase++) binding["phaseExecutionProfileSha256"]![phase.ToString()]=new string((char)('a'+phase),64);
            var initialized=await PythonAsync(directory,"init",new JsonObject { ["binding"]=binding });string bindingDigest=initialized["bindingDigest"]!.GetValue<string>();
            byte[] publicKey=Encoding.ASCII.GetBytes(cohortKey.ExportSubjectPublicKeyInfoPem());
            var transport=new FixtureCohortTransport(directory);var attempts=new DaprQualificationAttemptAuthority(transport,new("fixture-cohort","file:/fixture-cohort",Encoding.ASCII.GetString(publicKey),Convert.ToHexStringLower(SHA256.HashData(publicKey)),bindingDigest),TimeProvider.System);
            using var originalAuthority=new IndependentAuthorityReceiverTests.Fixture();
            var cases=new Dictionary<string,JsonElement>();var allDrivers=new Dictionary<string,IQualificationOwnerCaseDriver>();
            foreach(string id in QualificationProtocolSchema.CaseIds)
            {
                var scope=(JsonObject)complete["enrolledScope"]!.DeepClone();string target=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
                if(id==recipe["caseId"]!.GetValue<string>()) target=recipe["caseTargetId"]!.GetValue<string>();
                scope["logicalId"]=target;scope["targetIds"]=new JsonArray(target);
                if(id==recipe["caseId"]!.GetValue<string>()) scope=(JsonObject)recipe["enrolledScope"]!.DeepClone();
                cases[id]=JsonSerializer.SerializeToElement(new { caseTargetId=target,enrolledScope=scope },CustodyBindingContract.Json);allDrivers[id]=new FixtureCaseDriver(id);
            }
            var staticBindings=new Dictionary<string,string>(new[]{"targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,recipe[name]!.ToJsonString())));
            foreach(var pair in new[]{("executorIdentity","fixture-executor"),("executorVersion","v1"),("observerIdentity","fixture-observer"),("observerVersion","v1"),("qualifiedReferencesSha256",new string('b',64)),("currentProfileSha256",new string('c',64))}) staticBindings[pair.Item1]=JsonSerializer.Serialize(pair.Item2);
            originalAuthority.State=originalAuthority.State with { QualificationBindings=staticBindings,QualificationCases=cases,Callers=originalAuthority.State.Callers.Select(value=>value with { Role="enrollment",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
            string unchangedInstallation=JsonSerializer.Serialize(staticBindings);var store=new InstalledQualificationOriginalStore(originalAuthority.Client());
            // Recipes and all static receiving/trust inputs already exist before the retained authority chooses its unpredictable attempt.
            var retained=await PythonAsync(directory,"reserve",new JsonObject());string attempt=retained["attemptId"]!.GetValue<string>();Guid.TryParse(attempt,out _).ShouldBeTrue();
            var phaseRequest=new JsonObject { ["attemptId"]=attempt,["bindingDigest"]=bindingDigest,["phase"]=1 };
            var scopeBinding=new JointPhaseCommandScope(bindingDigest,recipe["targetCommit"]!.GetValue<string>(),1,["secrets:"+recipe["caseId"]!.GetValue<string>()]);
            var full=JointPhaseRequestRecipe.Bind(recipe,phaseRequest,scopeBinding)!;var owner=(JsonObject)full.DeepClone();owner.Remove("scenario");
            var enroll=new JsonObject();foreach(string field in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256"}) enroll[field]=owner[field]!.DeepClone();enroll["operation"]="enroll";enroll["requestId"]=QualificationProtocolSchema.Digest(enroll);
            var enroller=new QualificationOwnerOperationHandler(new("enrollment","fixture-enrollment","v1",enrollmentKey.ExportSubjectPublicKeyInfoPem()),enrollmentKey,store,allDrivers,TimeProvider.System,attemptAuthority:attempts);
            var enrolled=(JsonObject)(await enroller.HandleAsync("qualification-enrollment-enroll",JsonSerializer.SerializeToElement(enroll),originalAuthority.State.Callers[0],originalAuthority.State,TestContext.Current.CancellationToken))!;
            enrolled["payload"]!["runId"]!.GetValue<string>().ShouldBe(attempt);
            originalAuthority.State=originalAuthority.State with { Callers=originalAuthority.State.Callers.Select(value=>value with { Role="executor" }).ToArray() };
            var executor=new QualificationOwnerOperationHandler(new("executor","fixture-executor","v1",executorKey.ExportSubjectPublicKeyInfoPem()),executorKey,store,allDrivers,TimeProvider.System,attemptAuthority:attempts);
            var first=(JsonObject)(await executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(owner),originalAuthority.State.Callers[0],originalAuthority.State,TestContext.Current.CancellationToken))!;
            originalAuthority.Restart();var exact=JointPhaseRequestRecipe.Bind(recipe,phaseRequest,scopeBinding)!;JsonNode.DeepEquals(full,exact).ShouldBeTrue();
            var recovered=(JsonObject)(await executor.HandleAsync("qualification-executor-lookup",JsonSerializer.SerializeToElement(owner),originalAuthority.State.Callers[0],originalAuthority.State,TestContext.Current.CancellationToken))!;
            JsonNode.DeepEquals(first,recovered).ShouldBeTrue();((FixtureCaseDriver)allDrivers[owner["caseId"]!.GetValue<string>()]).Effects.ShouldBe(1);
            JsonSerializer.Serialize(originalAuthority.State.QualificationBindings).ShouldBe(unchangedInstallation);originalAuthority.State.QualificationBindings!.ContainsKey("runId").ShouldBeFalse();recipe.ToJsonString().ShouldBe(pinnedRecipe);
        }
        finally { Directory.Delete(directory,true); }
    }
    private sealed class FixtureCaseDriver(string id):IQualificationOwnerCaseDriver
    {
        internal int Effects;public string CaseId=>id;
        public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<JsonObject?> ExecuteAsync(JsonObject request,CancellationToken token) { Effects++;return LookupAsync(request,token); }
        public Task<JsonObject?> LookupAsync(JsonObject request,CancellationToken token)=>Task.FromResult<JsonObject?>(new() { ["outcome"]="Denied",["resultSha256"]=new string('d',64) });
        public Task<JsonObject?> ObserveAsync(JsonObject request,CancellationToken token)=>throw new InvalidOperationException("No physical observer in this authority/admission fixture.");
    }
    private sealed class FixtureCohortTransport(string directory):ISecretsCredentialBoundDaprInvoker
    {
        public Task<string?> InvokeAsync(string app,string method,string body,CancellationToken token)=>throw new InvalidOperationException("Uncredentialed fixture call.");
        public async Task<string?> InvokeCredentialBoundAsync(string app,string credential,string method,string body,CancellationToken token)
        { app.ShouldBe("fixture-cohort");credential.ShouldBe("file:/fixture-cohort");method.ShouldBe("joint/v1/qualification-attempt");return (await PythonAsync(directory,"rpc",JsonNode.Parse(body)!.AsObject())).ToJsonString(); }
    }
    private static async Task<JsonObject> PythonAsync(string directory,string mode,JsonObject request,[System.Runtime.CompilerServices.CallerFilePath]string source="")
    {
        string repository=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"../.."));
        var start=new ProcessStartInfo("python3") { RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        start.ArgumentList.Add(Path.Combine(repository,"tests/Hexalith.Platform.Secrets.Runtime.Tests/Fixtures/current_cohort_protocol.py"));start.ArgumentList.Add(directory);start.ArgumentList.Add(repository);start.ArgumentList.Add(mode);
        using var process=Process.Start(start)!;await process.StandardInput.WriteAsync(request.ToJsonString());process.StandardInput.Close();
        string output=await process.StandardOutput.ReadToEndAsync(),error=await process.StandardError.ReadToEndAsync();await process.WaitForExitAsync(TestContext.Current.CancellationToken);process.ExitCode.ShouldBe(0,error);
        return JsonNode.Parse(output)!.AsObject();
    }
}
