using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

// Legacy uncertainty is immutable history. A separately admitted versioned terminal locator can settle it without replacing that body.
internal static class QualificationTerminalResult
{
    internal static async Task<JsonObject?> ReadAsync(IQualificationOriginalStore store,string requestId,Func<JsonObject,bool> terminal,CancellationToken token)
    {
        var settled=await store.LookupResultAsync("executor",requestId+":case-terminal-v2",token).ConfigureAwait(false);
        if(settled is not null && terminal(settled)) return settled;
        var legacy=await store.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        return legacy is not null && terminal(legacy)?legacy:null;
    }
    internal static async Task RetainAsync(IQualificationOriginalStore store,JsonObject request,JsonObject result,CancellationToken token)
    {
        string id=request["requestId"]!.GetValue<string>();
        string locator=id+":case-result";
        if(await store.LookupResultAsync("executor",locator,token).ConfigureAwait(false) is { } historical)
        {
            if(JsonNode.DeepEquals(historical,result)) return;
            bool uncertain=historical["actual"]?["Status"]?.GetValue<string>() is "Unknown" or "Unavailable"
                || historical["actual"]?["Result"]?["State"]?.GetValue<string>() is "Unknown" or "NotStarted"
                || historical["actual"]?["State"]?.GetValue<string>() is "Unknown" or "Unavailable" or "SendCommitted" or "ResultDurable"
                || historical["delivery"]?["State"]?.GetValue<string>() is "Unknown" or "Unavailable";
            if(!uncertain) throw new IOException("Immutable terminal case result conflict.");
            locator=id+":case-terminal-v2";await store.BeginAsync("executor",locator,request,token).ConfigureAwait(false);
        }
        await store.RetainAsync("executor",locator,result,token).ConfigureAwait(false);
    }
}
