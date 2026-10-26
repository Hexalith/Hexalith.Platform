using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual private receiving authorizer for exact retained dynamic claims and the original undelivered canary command. It enrolls nothing.</summary>
public sealed class Fr34CommandAuthorizationHandler(Fr34OccurrenceRecipe recipe,Fr34OccurrenceProducer claims,
    IndependentDaprAuthority commandOriginals,TimeProvider clock):IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public bool IsReadOnly(string method)=>method is "fr34-command-authorize";
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
        =>HandleAsync(method,payload,caller,current,token);
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; }=new HashSet<string>(["fr34-command-authorize"],StringComparer.Ordinal);
    /// <inheritdoc />
    public async Task<object?> HandleAsync(string method,JsonElement payload,IndependentCallerGrant caller,IndependentAuthorityState current,CancellationToken token)
    {
        var request=payload.Deserialize<Request>(CustodyBindingContract.Json);
        if(method!="fr34-command-authorize" || request is null || caller.Role!="fr34-engine" || caller.Revoked || caller.ValidUntil<=clock.GetUtcNow()
            || current.ValidUntil<=clock.GetUtcNow() || current.Fr34Target!=recipe.Target || string.IsNullOrWhiteSpace(current.Fr34AuthorizationRevision)
            || current.Fr34RecipeSha256!=QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(recipe,CustodyBindingContract.Json)!)
            || !current.Callers.Any(value=>value.Identity==request.caller && value.ScopeReference==request.principal && !value.Revoked
                && value.ValidUntil>clock.GetUtcNow() && value.Methods.Contains("recording-source:fr34-canary",StringComparer.Ordinal))) return new { Allowed=false };
        // The original-store port supports both immutable indexed originals and enrolled legacy originals. Missing/changed original bytes deny.
        var original=await commandOriginals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=request.command.MessageId },token).ConfigureAwait(false);
        bool exact=original is { SubmissionState:"Unsent" } && JsonNode.DeepEquals(JsonNode.Parse(original.Request.GetRawText()),JsonNode.Parse(request.command.Payload.GetRawText()));
        return new { Allowed=exact && await claims.VerifyCommandAsync(request.command,token).ConfigureAwait(false)
            && caller.ScopeReference==recipe.Target.TenantId+"|fr34" };
    }
    private sealed record Request(string caller,string principal,SubmitCommandRequest command);
}
