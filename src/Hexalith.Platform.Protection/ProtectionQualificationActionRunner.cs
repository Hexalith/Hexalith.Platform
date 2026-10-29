using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Source-owned native action sequencing and race dispatch; results come exclusively from actual driver calls, with durable admission before each effect.</summary>
public sealed class ProtectionQualificationActionRunner(ProtectionQualificationNativeDriver driver)
{
    /// <summary>Reads only the exact prior admitted reservation identity; there is no fresh reservation path.</summary>
    public Task<ProtectionQualificationNativeCall?> PendingRecoveryAsync(string tenant, string fixtureId, CancellationToken token)
        => driver.PendingRecoveryAsync(tenant, fixtureId, token);
    /// <summary>Completes an already admitted exact native operation with the same durable callbacks as every fixture action.</summary>
    public async Task PrepareAsync(ProtectionQualificationNativeCall original, JsonObject request,
        Func<ProtectionQualificationNativeCall, CancellationToken, Task<bool>> admit, Func<ProtectionQualificationNativeCall, JsonNode?, CancellationToken, Task> retain, CancellationToken token)
    {
        if (original.Kind != ProtectionQualificationNativeCallKind.LookupProvider || !await admit(original, token).ConfigureAwait(false))
        { throw new InvalidOperationException("Exact prior reservation preparation admission required."); }
        var result = await driver.ExecuteAsync(original, request, token).ConfigureAwait(false);
        await retain(original, result, token).ConfigureAwait(false);
        if (result?["State"]?.GetValue<int>() != (int)Hexalith.EventStore.Contracts.Security.DeletionManifestProviderState.Consumed)
        { throw new InvalidOperationException("Prior original reservation remains unresolved."); }
    }
    /// <summary>Reads an exact already admitted native original. This member has no dispatch path.</summary>
    public Task<JsonNode?> RecoverAsync(ProtectionQualificationNativeCall call, CancellationToken token)
        => driver.RecoverAsync(call, token);
    /// <summary>Executes an installed exact native recipe. Durable callbacks belong to the independently surviving operation journal, not the executor response stream.</summary>
    public async Task<JsonNode?> ExecuteAsync(ProtectionQualificationActionRecipe recipe, JsonObject request,
        IReadOnlyDictionary<string, JsonNode?> previous,
        Func<ProtectionQualificationNativeCall, CancellationToken, Task<bool>> admit,
        Func<ProtectionQualificationNativeCall, JsonNode?, CancellationToken, Task> retain,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(admit);
        ArgumentNullException.ThrowIfNull(retain);
        var originals = new Dictionary<string, JsonNode?>(previous, StringComparer.Ordinal);
        async Task<JsonNode?> Run(ProtectionQualificationNativeCall template)
        {
            var bound = ProtectionQualificationRecipeBinding.Bind(template, request, originals);
            var call = await driver.PrepareCallAsync(bound, token).ConfigureAwait(false);
            if (!await admit(call, token).ConfigureAwait(false)) { throw new InvalidOperationException("Native call already admitted: exact original lookup required."); }
            JsonNode? result = await driver.ExecuteAsync(call, request, token).ConfigureAwait(false);
            await retain(call, result, token).ConfigureAwait(false);
            return result;
        }
        foreach (var before in recipe.Before) { originals.Add(before.Id, await Run(before).ConfigureAwait(false)); }
        JsonNode? effect;
        if (recipe.Race is { } race)
        {
            Task<JsonNode?> primary = Run(recipe.Effect), competing = Run(race);
            JsonNode?[] outcomes = await Task.WhenAll(primary, competing).ConfigureAwait(false);
            effect = outcomes[0]; originals.Add(race.Id, outcomes[1]);
        }
        else { effect = await Run(recipe.Effect).ConfigureAwait(false); }
        originals.Add(recipe.Effect.Id, effect);
        foreach (var after in recipe.After) { originals.Add(after.Id, await Run(after).ConfigureAwait(false)); }
        return effect;
    }
}
