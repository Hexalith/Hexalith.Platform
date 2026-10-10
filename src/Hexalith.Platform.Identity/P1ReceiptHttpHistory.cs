namespace Hexalith.Platform.Identity;

/// <summary>Non-evicting local first-observation history, bounded by framed bytes and ID count.</summary>
internal sealed class P1ReceiptHttpHistory
{
    internal const int MaxFrameBytes = 16 * 1024 * 1024;
    internal const int MaxIds = 1024;
    private readonly Dictionary<string, byte[]> _frames = new(MaxIds, StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _frameBytes;

    internal P1ReceiptHttpFailure Compare(string id, byte[] frame)
    {
        lock (_gate)
        {
            return _frames.TryGetValue(id, out byte[]? previous) && !previous.AsSpan().SequenceEqual(frame)
                ? P1ReceiptHttpFailure.ImmutableIdIncident : P1ReceiptHttpFailure.None;
        }
    }

    internal P1ReceiptHttpFailure Commit(string id, byte[] frame, Func<bool> finalGuard)
    {
        lock (_gate)
        {
            if (!finalGuard())
            {
                return P1ReceiptHttpFailure.Deadline;
            }

            if (_frames.TryGetValue(id, out byte[]? previous))
            {
                bool same = previous.AsSpan().SequenceEqual(frame);
                if (!finalGuard())
                {
                    return P1ReceiptHttpFailure.Deadline;
                }

                return same ? P1ReceiptHttpFailure.None : P1ReceiptHttpFailure.ImmutableIdIncident;
            }

            if (_frames.Count >= MaxIds || _frameBytes + frame.Length > MaxFrameBytes)
            {
                return P1ReceiptHttpFailure.HistoryFull;
            }

            _frames.Add(id, frame);
            _frameBytes += frame.Length;
            if (!finalGuard())
            {
                _frames.Remove(id);
                _frameBytes -= frame.Length;
                return P1ReceiptHttpFailure.Deadline;
            }

            return P1ReceiptHttpFailure.None;
        }
    }
}
