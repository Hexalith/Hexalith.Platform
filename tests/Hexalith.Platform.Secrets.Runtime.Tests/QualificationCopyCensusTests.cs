using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Every misleading source remains genuinely signed by the independently enrolled disposable fixture observer.
public sealed class QualificationCopyCensusTests
{
    [Theory]
    [InlineData("complete",true)][InlineData("empty",false)][InlineData("omitted",false)][InlineData("duplicate",false)]
    [InlineData("foreign",false)][InlineData("unknown",false)][InlineData("missing-destruction",false)][InlineData("foreign-destruction",false)]
    public void SignedCensusRequiresEveryUniqueEnrolledCopyAndItsCorrelatedOriginalDestructionAndReadProof(string fault,bool expected)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);const string requestId="exact-request",caseId="S1.reject.missing-config";string receipt=new('a',64);
        var original=new JsonObject { ["requestId"]=requestId,["caseId"]=caseId,["caseTargetId"]="logical",["targetCommit"]=new string('b',40),["isolatedTargetId"]="fixture-installation",
            ["enrolledScope"]=new JsonObject { ["tenantId"]="tenant",["targetIds"]=new JsonArray("target"),["providerCopyIds"]=new JsonArray("copy-a","copy-b") } };
        var state=new JsonObject { ["requestId"]=requestId,["receiptId"]=receipt,["tenantId"]="tenant",["logicalId"]="logical",["targetIds"]=new JsonArray("target"),["providerCopyIds"]=new JsonArray("copy-a","copy-b"),["outcome"]="Denied" };
        var copies=new JsonArray();var destructions=new JsonArray();
        foreach(string copy in new[]{"copy-a","copy-b"})
        {
            string destruction=copy=="copy-a"?new string('c',64):new string('d',64);
            copies.Add(new JsonObject { ["copyId"]=copy,["tenantId"]="tenant",["requestId"]=requestId,["priorDestructionReceiptId"]=destruction,["readability"]="Erased",["nativeReadProofSha256"]=new string('e',64) });
            destructions.Add(new JsonObject { ["copyId"]=copy,["tenantId"]="tenant",["requestId"]=requestId,["receiptId"]=destruction,["outcome"]="Destroyed" });
        }
        switch(fault)
        {
            case "empty":copies.Clear();break;case "omitted":copies.RemoveAt(1);break;
            case "duplicate":copies[1]=copies[0]!.DeepClone();break;case "foreign":copies[1]!["copyId"]="foreign";break;
            case "unknown":copies[1]!["readability"]="Unknown";break;case "missing-destruction":destructions.Clear();break;
            case "foreign-destruction":destructions[1]!["tenantId"]="foreign";break;
        }
        var receipts=new JsonArray();string[] stages=["AfterEffect","AfterRestart","AfterRestore","AfterLookup"],kinds=["OriginalPersisted","Restart","Restore","Lookup"];
        for(int index=0;index<4;index++) receipts.Add(new JsonObject { ["position"]=index+1,["receiptId"]=receipt,["kind"]=kinds[index] });
        var scans=new JsonArray();foreach(string surface in new[]{"Events","Projections","Responses","Logs","Traces","Evidence"}) scans.Add(new JsonObject { ["surface"]=surface,["matchCount"]=0 });
        var source=new JsonObject { ["state"]=state,["receipts"]=receipts,["copyCensus"]=copies,["destructionOriginals"]=destructions,["surfaceScans"]=scans,["targetMutations"]=new JsonArray() };
        var snapshots=new JsonArray();long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for(int index=0;index<4;index++)
        {
            var proof=new QualificationObservedOriginal(requestId,receipt,"authority-1",(JsonObject)source.DeepClone(),"observer","v1",now,now+30,[]);
            proof=proof with { Signature=key.SignData(QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(proof,CustodyBindingContract.Json)!),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation) };
            snapshots.Add(new JsonObject { ["stage"]=stages[index],["authorityRevision"]="authority-1",["source"]=source.DeepClone(),["sourceProof"]=JsonSerializer.SerializeToNode(proof,CustodyBindingContract.Json) });
        }
        var controls=new JsonArray();foreach(string action in new[]{"restart","restore"}) controls.Add(JsonSerializer.SerializeToNode(new QualificationRecoveryControlOriginal(requestId,"operation-"+action,action,new string('b',40),"fixture-installation","before-"+action,"after-"+action,receipt,new string('f',64)),CustodyBindingContract.Json));
        var verifier=Substitute.For<IQualificationRecoveryController>();verifier.Verify(Arg.Any<QualificationRecoveryControlOriginal>(),Arg.Any<JsonObject>()).Returns(true);
        byte[] anchor=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());var projector=new QualificationMeasuredCaseProjector(new("observer","v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor))),verifier,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}});
        var projected=projector.Project("observer",original,new JsonObject { ["requestId"]=requestId,["originalResultSha256"]=receipt,["snapshots"]=snapshots,["controls"]=controls });
        if(expected) projected.ShouldNotBeNull()["metrics"]!["allCopyRevival"]!.GetValue<long>().ShouldBe(0);
        else { projected.ShouldBeNull();projector.MissingReadInputs.ShouldContain("IndependentNativeMeasurement."+caseId+".allCopyRevival"); }
    }
}
