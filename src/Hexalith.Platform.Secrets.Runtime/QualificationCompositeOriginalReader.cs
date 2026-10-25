using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact closed source-reader routing; no stage, target or expected-result fallback is accepted.</summary>
public sealed class QualificationCompositeOriginalReader(IReadOnlyDictionary<string,IQualificationObservedOriginalReader> readers):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken cancellationToken)=>SecretsProof.Text(original,"caseId") is { } id && readers.TryGetValue(id,out var reader)
        ?reader.ReadAsync(original,cancellationToken):Task.FromResult<QualificationObservedOriginal?>(null);
}
