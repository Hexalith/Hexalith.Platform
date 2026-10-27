using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Complete signed stage chains use disposable independent keys; they establish no physical qualification.
public sealed class QualificationCopyCensusTests
{
    [Theory]
    [InlineData("complete",true)][InlineData("earlier-revival",true)][InlineData("empty",false)][InlineData("omitted",false)][InlineData("duplicate",false)]
    [InlineData("foreign",false)][InlineData("negative-scan",false)][InlineData("duplicate-scan",false)][InlineData("missing-scan",false)][InlineData("extra-scan",false)]
    [InlineData("nonobject-mutation",false)][InlineData("missing-classification",false)][InlineData("mistyped-classification",false)][InlineData("unknown",false)][InlineData("missing-destruction",false)][InlineData("foreign-destruction",false)]
    [InlineData("malformed-destruction",false)][InlineData("duplicate-destruction",false)]
    public void SignedCensusRequiresEveryUniqueEnrolledCopyAndItsCorrelatedOriginalDestructionAndReadProof(string fault,bool expected)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);string requestId=new('1',64);const string caseId="S1.reject.missing-config";string receipt=new('a',64);
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
            case "malformed-destruction":destructions.Add(3);break;case "duplicate-destruction":destructions.Add(destructions[0]!.DeepClone());break;
        }
        var receipts=new JsonArray();string[] stages=["AfterEffect","AfterRestart","AfterRestore","AfterLookup"],kinds=["OriginalPersisted","Restart","Restore","Lookup"];
        for(int index=0;index<4;index++) receipts.Add(new JsonObject { ["position"]=index+1,["receiptId"]=receipt,["kind"]=kinds[index],
            ["keyVersion"]=null,["guardRevision"]=0,["blockSetRevision"]=0,["occurredAt"]=1,["requestId"]=requestId,["tenantId"]="tenant",["originalReceiptId"]=receipt });
        var controls=new JsonArray();for(int index=0;index<2;index++) controls.Add(JsonSerializer.SerializeToNode(new QualificationRecoveryControlOriginal(requestId,"operation-"+kinds[index+1].ToLowerInvariant(),kinds[index+1].ToLowerInvariant(),new string('b',40),"fixture-installation","inc-"+index,"inc-"+(index+1),receipt,QualificationProtocolSchema.Digest(receipts[index+1]!)),CustodyBindingContract.Json));
        var scans=new JsonArray();foreach(string surface in new[]{"Events","Projections","Responses","Logs","Traces","Evidence"}) scans.Add(new JsonObject { ["surface"]=surface,["matchCount"]=0 });
        var source=new JsonObject { ["state"]=state,["copyCensus"]=copies,["destructionOriginals"]=destructions,["surfaceScans"]=scans,["targetMutations"]=new JsonArray() };
        switch(fault)
        {
            case "negative-scan":scans[0]!["matchCount"]=1;scans[1]!["matchCount"]=-1;break;
            case "duplicate-scan":scans[1]=scans[0]!.DeepClone();break;case "missing-scan":scans.RemoveAt(1);break;
            case "extra-scan":scans.Add(new JsonObject { ["surface"]="Extra",["matchCount"]=0 });break;
            case "nonobject-mutation":source["targetMutations"]!.AsArray().Add("malformed");break;
            case "missing-classification":source["targetMutations"]!.AsArray().Add(new JsonObject { ["targetTenantId"]="tenant",["authorizationReceiptId"]=null });break;
            case "mistyped-classification":source["targetMutations"]!.AsArray().Add(new JsonObject { ["targetTenantId"]="tenant",["authorizationReceiptId"]=3,["keyVersion"]="v1",["keyState"]="Active",["epochId"]="epoch",["productDisposition"]="Open" });break;
        }
        var snapshots=new JsonArray();long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();string predecessor=QualificationProtocolSchema.Digest(original);
        for(int index=0;index<4;index++)
        {
            var measured=(JsonObject)source.DeepClone();measured["receipts"]=new JsonArray(receipts.Take(index+1).Select(value=>value!.DeepClone()).ToArray());
            if(index==0 && fault=="earlier-revival") measured["copyCensus"]![0]!["readability"]="Readable";
            var control=index is 1 or 2?controls[index-1]!.Deserialize<QualificationRecoveryControlOriginal>(CustodyBindingContract.Json):null;
            var context=new QualificationObservationStageRequest("EXT-SECRETS-1.native-observation.v2",QualificationProtocolSchema.Digest(original),stages[index],predecessor,
                index==3?requestId+":lookup-stage":control?.OperationId,control is null?null:QualificationProtocolSchema.Digest(controls[index-1]!));
            var native=new QualificationNativeStageBoundary(stages[index],"inc-"+Math.Min(index,2),"revision-"+(index+1),context.ControlOperationId,index==3?QualificationProtocolSchema.Digest(receipts[3]!):context.ControlOriginalSha256);
            measured["nativeStageBoundary"]=JsonSerializer.SerializeToNode(native,CustodyBindingContract.Json);
            var raw=(JsonObject)measured.DeepClone();var positions=new JsonObject();foreach(var entry in measured["receipts"]!.AsArray()) positions[QualificationProtocolSchema.Digest(entry!)]=entry!["position"]!.GetValue<int>()*10L;
            measured["independentReceiptPositions"]=positions;
            if(index==3) measured["readonlyLookupWitness"]=new JsonObject { ["retainedRevision"]=100,["body"]=new JsonObject {
                ["protocol"]="EXT-SECRETS-1.observer-native-lookup.v1",["requestId"]=requestId,["caseRequestSha256"]=QualificationProtocolSchema.Digest(original),["lookupOperationId"]=requestId+":lookup-stage",["stage"]="AfterLookup",["originalReceiptSha256"]=receipt,
                ["returnedSourceSha256"]=QualificationProtocolSchema.Digest(raw),["authorityRevision"]="authority-1",["sourceIncarnation"]=native.SourceIncarnation,["sourceRevision"]=native.SourceRevision,
                ["precedingObservationSha256"]=context.PredecessorSha256,["precedingControlOperationId"]="operation-restore",["precedingControlSha256"]=QualificationProtocolSchema.Digest(controls[1]!),["occurredAt"]=now*1000 } };
            var proof=new QualificationObservedOriginal(requestId,receipt,"authority-1",measured,"observer","v1",now,now+30,[],new(context,native));
            proof=proof with { Signature=key.SignData(QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(proof,CustodyBindingContract.Json)!),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation) };
            var body=JsonSerializer.SerializeToNode(proof,CustodyBindingContract.Json)!;predecessor=QualificationProtocolSchema.Digest(body);
            snapshots.Add(new JsonObject { ["stage"]=stages[index],["originalRequestId"]=requestId,["originalResultSha256"]=receipt,["stateSha256"]=QualificationProtocolSchema.Digest(measured),["authorityRevision"]="authority-1",["source"]=measured.DeepClone(),["sourceProof"]=body });
        }
        var verifier=Substitute.For<IQualificationRecoveryController>();verifier.Verify(Arg.Any<QualificationRecoveryControlOriginal>(),Arg.Any<JsonObject>()).Returns(true);
        byte[] anchor=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());var projector=new QualificationMeasuredCaseProjector(new("observer","v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor))),verifier,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}});
        var projected=projector.Project("observer",original,new JsonObject { ["requestId"]=requestId,["originalResultSha256"]=receipt,["snapshots"]=snapshots,["controls"]=controls });
        if(expected) projected.ShouldNotBeNull()["metrics"]!["allCopyRevival"]!.GetValue<long>().ShouldBe(fault=="earlier-revival"?1:0);
        else { projected.ShouldBeNull();if(fault.Contains("scan",StringComparison.Ordinal) || fault.Contains("mutation",StringComparison.Ordinal) || fault.Contains("classification",StringComparison.Ordinal)) projector.MissingReadInputs.ShouldContain("CompleteExactSurfaceAndTargetMutationCensusSchema");else projector.MissingReadInputs.ShouldContain("IndependentNativeMeasurement."+caseId+".allCopyRevival"); }
    }
}
