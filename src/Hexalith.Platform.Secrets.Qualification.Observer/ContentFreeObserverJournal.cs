using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Qualification.Observer;

/// <summary>Independent content-free observer journal. Append and read share one lock.</summary>
public sealed class ContentFreeObserverJournal
{
    private readonly Lock _gate = new();
    private readonly List<string> _entries = [];

    /// <summary>Appends one content-free identifier.</summary>
    public void Append(string contentFreeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFreeId);
        lock (_gate) { _entries.Add(contentFreeId); }
    }

    /// <summary>Returns a snapshot of the identifiers recorded so far.</summary>
    public IReadOnlyList<string> Read()
    {
        lock (_gate) { return _entries.ToArray(); }
    }
}

/// <summary>Observe denies before any journal append while required references are missing.</summary>
public static class ObserverEndpoint
{
    /// <summary>Returns 503 without appending when qualification is unavailable.</summary>
    public static int Observe(SecretsRuntimeConfiguration configuration, ContentFreeObserverJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        QualificationDenial denial = QualificationDenial.Evaluate(configuration);
        return denial.StatusCode == 200 ? StatusCodes.Status503ServiceUnavailable : denial.StatusCode;
    }
}
