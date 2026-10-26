using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AgentsAcceptedDeploymentContextTests
{
    [Fact]
    public void RealDateMustAgreeAcrossAcceptedRegisterPacketAndExactProfileBytes()
    {
        var (json, register, packet) = Inputs();
        var model = AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet);
        model.IntegrationDate.ShouldBe("2026-11-03"); model.AcceptedContextValidated.ShouldBeTrue();
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json.Replace("recorder-ref", "changed-recorder", StringComparison.Ordinal), register, packet));
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet.Replace("2026-11-03", "2026-11-04", StringComparison.Ordinal)));
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.ValidateAcceptedContext(json, register, packet.Replace(AgentsDeploymentProfile.ExpectedCohortPlanSha256, new string('c', 64), StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-1-03")]
    [InlineData("2026-11-03T00:00:00")]
    public void InvalidCalendarDateCannotBeComposed(string date)
    {
        var (json, _, _) = Inputs();
        Should.Throw<InvalidDataException>(() => AgentsDeploymentProfile.Validate(json.Replace("2026-11-03", date, StringComparison.Ordinal)));
    }

    [Fact]
    public void ParsingReferencesAloneDoesNotGrantProductionCompositionAuthority()
    {
        var (json, _, _) = Inputs();
        AgentsDeploymentProfile.Validate(json).AcceptedContextValidated.ShouldBeFalse();
    }
    [Theory]
    [InlineData("integrationDate","2026-02-30")][InlineData("integrationDate","2026-11-06")]
    [InlineData("targetCommit","cccccccccccccccccccccccccccccccccccccccc")]
    [InlineData("profileSha256","cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
    [InlineData("configurationSha256","cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
    [InlineData("planSha256","cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc")]
    public void ProtectionCommitmentAndEveryRawPinMustMatchTheExactAcceptedSection(string field,string value)
    {
        var (json,register,packet)=Inputs();var changed=JsonNode.Parse(packet)!;changed["protectionBinding"]![field]=value;
        Should.Throw<InvalidDataException>(()=>AgentsDeploymentProfile.ValidateAcceptedContext(json,register,changed.ToJsonString()));
        changed=JsonNode.Parse(packet)!;changed.AsObject().Remove("protectionBinding");
        Should.Throw<InvalidDataException>(()=>AgentsDeploymentProfile.ValidateAcceptedContext(json,register,changed.ToJsonString()));
    }
    [Fact]
    public async Task ActualPythonPreparedCoordinatedPacketIsConsumedWithItsCompleteAcceptedProtectionAndPlatformSections()
    {
        string root=SourceRoot();
        string output=Path.Combine(Path.GetTempPath(),"story54-python-csharp-packet-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        try
        {
            var start=new System.Diagnostics.ProcessStartInfo("python3") { WorkingDirectory=root,RedirectStandardError=true,RedirectStandardOutput=true };
            start.ArgumentList.Add("-c");start.ArgumentList.Add("import sys,pathlib\nsys.path.insert(0,'eng/ext-secrets-1')\nfrom test_coordinated_packets import CoordinatedPacketTests,host,q\nx=CoordinatedPacketTests();x.setUp()\ntry:\n r=host.prepare(x.fixture.root,x.host_output,x.profile_path,x.fixture.register,x.context_path)\n assert r['packetPrepared'],r\n p=q.read_json(x.host_output/'replacement-acceptance-packet.json');text=x.fixture.register.read_text()\n for record,section in p['coordinatedRecords'].items(): text=text.replace(section['acceptedVerbatimSection'],section['proposedVerbatimSection'])\n o=pathlib.Path(sys.argv[1]);(o/'profile.json').write_bytes((x.host_output/'agents-deployment-profile.json').read_bytes());(o/'register.md').write_text(text);(o/'packet.json').write_bytes(q.canonical(p))\nfinally: x.doCleanups()\n");start.ArgumentList.Add(output);
            using var process=System.Diagnostics.Process.Start(start)!;string errors=await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);await process.WaitForExitAsync(TestContext.Current.CancellationToken);process.ExitCode.ShouldBe(0,errors);
            var model=AgentsDeploymentProfile.ValidateAcceptedContext(await File.ReadAllTextAsync(Path.Combine(output,"profile.json"),TestContext.Current.CancellationToken),await File.ReadAllTextAsync(Path.Combine(output,"register.md"),TestContext.Current.CancellationToken),await File.ReadAllTextAsync(Path.Combine(output,"packet.json"),TestContext.Current.CancellationToken));model.AcceptedContextValidated.ShouldBeTrue();
        }
        finally { Directory.Delete(output,true); }
    }
    private static string SourceRoot([System.Runtime.CompilerServices.CallerFilePath] string source="")
    {
        string root=Path.GetDirectoryName(source)!;while(!File.Exists(Path.Combine(root,"eng/ext-secrets-1/test_coordinated_packets.py"))) root=Directory.GetParent(root)?.FullName??throw new IOException("Repository source fixture required.");return root;
    }

    private static (string Json, string Register, string Packet) Inputs()
    {
        var profile = new Dictionary<string, object?>
        {
            ["host"] = "192.168.1.30", ["integrationDate"] = "2026-11-03", ["partiesBranch"] = "B",
            ["timing"] = new { L = 300, S = 30, O = 600, H = 86400, R = 604800 },
            ["services"] = new { eventStore = "../eventstore", tenants = "../tenants", parties = "../parties", conversations = "../conversations", agents = "../agents" },
            ["replicaCount"] = 3,
        };
        foreach (string name in new[] { "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence", "replay", "recorder", "worker", "spoolBackend", "failureModel", "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding" }) profile[name] = name + "-ref";
        string json = JsonSerializer.Serialize(profile);
        string profileHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        string planHash = AgentsDeploymentProfile.ExpectedCohortPlanSha256; string target = new('a', 40);
        string config=new('d',64),protectionProfile=new('e',64),custodyRaw=new('f',64),semantic=new('b',64);
        var joint=new SortedDictionary<string,object> { ["platformCommit"]=target,["hostCommit"]=target,["secretsCommit"]=target,["protectionCommit"]=new string('b',40),
            ["platformIntegrationDate"]="2026-11-03",["protectionIntegrationDate"]="2026-11-05",["configurationSha256"]=semantic,["profileSha256"]=profileHash,["protectionProfileSha256"]=protectionProfile,["planSha256"]=planHash };
        string fields = "\n| `TargetVersionOrCommit` | `" + target + "` |\n| `TargetIntegrationDate` | `2026-11-03` |\n| `AcceptedStatus` | `Committed` |\n";
        string pin = JsonSerializer.Serialize(new { profileSha256 = profileHash, cohortPlanSha256 = planHash,configurationRawSha256=custodyRaw,configurationSha256=semantic });
        var protection=new Dictionary<string,object> { ["targetCommit"]=new string('b',40),["integrationDate"]="2026-11-05",["profileSha256"]=protectionProfile,["configurationSha256"]=config,["planSha256"]=planHash };
        string protectionSection="### EXT-PROTECTION-1 — Protection\n| `TargetVersionOrCommit` | `"+new string('b',40)+"` |\n| `TargetIntegrationDate` | `2026-11-05` |\n| `AcceptedStatus` | `Committed` |\n| `ProtectionQualificationBinding` | `"+JsonSerializer.Serialize(protection)+"` |\n";
        string jointPin=JsonSerializer.Serialize(new { configurationSha256=config,bindingSha256=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(joint))),planSha256=planHash });
        string jointRow="| `JointQualificationBinding` | `"+jointPin+"` |\n";
        string register = "### EXT-HOST-1 — Host\n" + fields + "| `HostQualificationBinding` | `" + pin + "` |\n"+jointRow+"### EXT-SECRETS-1 — Secrets\n" + fields+"| `RuntimeQualificationBinding` | `"+pin+"` |\n"+jointRow+protectionSection;
        protection["acceptedSectionSha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(protectionSection)));
        string packet=JsonSerializer.Serialize(new { candidateCommit=target,platformIntegrationDate="2026-11-03",protectionIntegrationDate="2026-11-05",profileSha256=profileHash,cohortPlanSha256=planHash,
            jointBinding=joint,jointConfigurationSha256=config,protectionBinding=protection,installedHashes=new { hostProfileSha256=profileHash,protectionProfileSha256=protectionProfile,jointConfigurationSha256=config,custodyConfigurationSha256=custodyRaw } });
        return (json, register, packet);
    }
}
