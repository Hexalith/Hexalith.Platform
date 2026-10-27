using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Closed independent decision/actor-role verification workflows. No approval creation or decision signing operation is available.</summary>
public sealed class QualificationDecisionCaseWorkflow(IIndependentDecisionAuthority authority,TimeProvider clock,IReadOnlyDictionary<string,QualificationDecisionCaseInput> enrolledInputs)
{
    /// <summary>Registers actual read-only source operations with exact retained-original recovery.</summary>
    public IReadOnlyList<QualificationSourceWorkflow> Registrations(IQualificationOriginalStore originals)=>enrolledInputs.Keys.Where(Supported).Select(id=>QualificationRuntimeWorkflowCatalog.Retained(id,originals,VerifyAsync) with { Preflight=PreflightAsync }).ToArray();
    /// <summary>Requires actual pre-issued expiry/staleness/revocation/foreign-anchor conditions. A valid current artifact cannot stand in for a negative scenario.</summary>
    public async Task<IReadOnlyList<string>> PreflightAsync(JsonObject original,CancellationToken token)
    {
        string? id=SecretsProof.Text(original,"caseId");
        if(id is null || !enrolledInputs.TryGetValue(id,out var input)) return ["IndependentlyIssuedExactDecisionCaseOriginal"];
        string point=id[(id.LastIndexOf('.')+1)..];var manifest=input.OriginalManifest;
        if(Semantic(point))
        {
            if(input.SignedOriginalBasis is not { } signedBasis || !IntendedMismatch(point,signedBasis,input.ExpectedBasis))
                return ["External.IndependentlySignedDecisionSemanticOriginal."+point];
            // Authenticate the genuine original and actual approvers, while independently validating the current expected policy.
            // Only the expected semantic basis changes; the manifest and detached signature remain exact.
            return await new IndependentDecisionManifestVerifier(clock,authority).VerifySignedOriginalAsync(manifest,input.DetachedJws,input.ExpectedBasis,signedBasis,token).ConfigureAwait(false)
                ?[]:["CryptographicallyValidCurrentDecisionSemanticOriginal."+point];
        }
        if(point is not ("stale" or "expired" or "revoked" or "foreign-anchor")) return [];
        var now=clock.GetUtcNow();
        var profile=await authority.ResolveAsync(input.ExpectedBasis,manifest.SigningKeyVersion,token).ConfigureAwait(false);
        if(profile is null) return ["IndependentCurrentDecisionConditionProfile."+point];
        if(point!="foreign-anchor" && !DecisionAuthorityManifestCodec.Verify(manifest,profile,input.DetachedJws)) return ["CryptographicallyValidPreissuedDecisionOriginal."+point];
        if(point=="expired") return manifest.ExclusiveExpiry<=now?[ ]:["PreissuedActuallyExpiredDecisionArtifact"];
        bool condition=point switch {
            "stale"=>manifest.IssuedAt<profile.ValidFrom || manifest.ExclusiveExpiry-manifest.IssuedAt>profile.MaximumManifestLifetime,
            "revoked"=>profile.IsRevoked,
            "foreign-anchor"=>input.OriginalForeignAnchor is { } foreign && DecisionAuthorityManifestCodec.Verify(manifest,foreign,input.DetachedJws)
                && (foreign.PublicAnchorId!=profile.PublicAnchorId || foreign.PublicAnchorVersion!=profile.PublicAnchorVersion || !foreign.SubjectPublicKeyInfo.AsSpan().SequenceEqual(profile.SubjectPublicKeyInfo)),
            _=>false };
        return condition?[]:["PreissuedActuallyObservedDecisionCondition."+point];
    }
    /// <summary>Independently re-verifies original bytes, exact basis and currently applicable role authority.</summary>
    public async Task<JsonObject?> VerifyAsync(JsonObject original,CancellationToken token)
    {
        string? id=SecretsProof.Text(original,"caseId"),requestId=SecretsProof.Text(original,"requestId");
        if(id is null || requestId is null || !Supported(id) || !enrolledInputs.TryGetValue(id,out var input) || original["enrolledScope"] is not JsonObject scope
            || SecretsProof.Text(scope,"tenantId")!=input.TenantId) return null;
        if((await PreflightAsync(original,token).ConfigureAwait(false)).Count!=0) return null;
        var manifest=input.OriginalManifest;var basis=input.ExpectedBasis;
        string point=id[(id.LastIndexOf('.')+1)..];
        bool valid;int exactRoleReads=0;long? profileRevision=null;
        if(id.StartsWith("S1.independent-role.",StringComparison.Ordinal))
        {
            bool complete=await new IndependentDecisionManifestVerifier(clock,authority).VerifyAsync(manifest,input.DetachedJws,basis,token).ConfigureAwait(false);
            var roles=complete && point=="Maintainer"?basis.RequiredRoles.Where(value=>value=="Maintainer" || value.StartsWith("Maintainer:",StringComparison.Ordinal)).ToArray():[];
            string? exactRole=point=="Maintainer"?roles.Length==1?roles[0]:null:point;
            var approver=complete?manifest.Approvers.SingleOrDefault(value=>value.Role==exactRole):null;
            var profile=complete?await authority.ResolveAsync(basis,manifest.SigningKeyVersion,token).ConfigureAwait(false):null;
            valid=complete && approver is not null && profile is { IsRevoked:false,IsIndependentlyGoverned:true } && profile.ValidFrom<=clock.GetUtcNow() && profile.ValidUntil>clock.GetUtcNow()
                && await authority.VerifyExpectedBasisAsync(basis,profile,token).ConfigureAwait(false)
                && await authority.VerifyApproverAsync(basis,approver,manifest.IssuedAt,token).ConfigureAwait(false)
                && await authority.VerifyAuthorityBoundaryAsync(basis,manifest,profile,token).ConfigureAwait(false);
            if(valid) valid=await new IndependentDecisionManifestVerifier(clock,authority).VerifyAsync(manifest,input.DetachedJws,basis,token).ConfigureAwait(false);
            if(valid) exactRoleReads++;profileRevision=profile?.ProfileRevision;
        }
        else valid=await new IndependentDecisionManifestVerifier(clock,authority).VerifyAsync(manifest,input.DetachedJws,basis,token).ConfigureAwait(false);
        return new JsonObject { ["caseId"]=id,["requestId"]=requestId,["verified"]=valid,["currentRoleVerificationCount"]=exactRoleReads,["profileRevision"]=profileRevision,
            ["originalManifest"]=JsonSerializer.SerializeToNode(input.OriginalManifest,CustodyBindingContract.Json),["detachedJws"]=input.DetachedJws,["expectedBasis"]=JsonSerializer.SerializeToNode(basis,CustodyBindingContract.Json),
            ["manifestSha256"]=Hash(JsonSerializer.SerializeToUtf8Bytes(manifest,CustodyBindingContract.Json)),["basisSha256"]=Hash(JsonSerializer.SerializeToUtf8Bytes(basis,CustodyBindingContract.Json)),
            ["signatureVerifiedBeforeSemanticCheck"]=Semantic(point)?true:null,["signedOriginalBasis"]=JsonSerializer.SerializeToNode(input.SignedOriginalBasis,CustodyBindingContract.Json),
            ["artifactSha256"]=Hash(Encoding.UTF8.GetBytes(input.DetachedJws??string.Empty)) };
    }
    private static bool Semantic(string point)=>point is "missing-approvers" or "predecessor" or "contract" or "version" or "affected-evaluations" or "actor-roles";
    private static bool IntendedMismatch(string point,DecisionAuthorityExpectedBasis signed,DecisionAuthorityExpectedBasis expected)
    {
        var adjusted=point switch {
            "missing-approvers" or "actor-roles"=>signed with { RequiredRoles=expected.RequiredRoles },
            "predecessor"=>signed with { EffectivePredecessorVersion=expected.EffectivePredecessorVersion,RootPolicyId=expected.RootPolicyId },
            "contract"=>signed with { ContractId=expected.ContractId },
            "version"=>signed with { ContractVersion=expected.ContractVersion },
            "affected-evaluations"=>signed with { AffectedEvaluations=expected.AffectedEvaluations },_=>signed };
        bool distinct=!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(signed,CustodyBindingContract.Json),JsonSerializer.SerializeToNode(expected,CustodyBindingContract.Json));
        bool onlyIntended=JsonNode.DeepEquals(JsonSerializer.SerializeToNode(adjusted,CustodyBindingContract.Json),JsonSerializer.SerializeToNode(expected,CustodyBindingContract.Json));
        return distinct && onlyIntended && (point!="missing-approvers" || signed.RequiredRoles.All(expected.RequiredRoles.Contains) && expected.RequiredRoles.Count>signed.RequiredRoles.Count);
    }
    private static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
    private static bool Supported(string id)=>id=="S3.exact-independent-decision" || id.StartsWith("S1.independent-role.",StringComparison.Ordinal) || id.StartsWith("S3.decision-reject.",StringComparison.Ordinal);
}
