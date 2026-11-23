namespace Hexalith.Platform.AttemptStore.Tests;

internal sealed class InMemoryAttemptObjectStore : IAttemptObjectStore
{
    private readonly Dictionary<string, AttemptObject> _objects = new(StringComparer.Ordinal);
    private readonly List<(string Key, AttemptObject Object)> _history = [];

    internal Func<string, CancellationToken, Task>? BeforeReplace { get; set; }
    internal Func<string, CancellationToken, Task>? AfterReplace { get; set; }
    internal Func<string, CancellationToken, Task>? BeforeCreate { get; set; }
    internal Func<string, Exception?>? BeforeRead { get; set; }

    internal int Writes
    {
        get
        {
            lock (_objects)
            {
                return _history.Count;
            }
        }
    }

    internal IReadOnlyList<AttemptObject> History(string key)
    {
        lock (_objects)
        {
            return [.. _history.Where(entry => entry.Key == key).Select(entry => entry.Object)];
        }
    }

    public Task<AttemptObject?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeRead?.Invoke(key) is { } exception)
        {
            return Task.FromException<AttemptObject?>(exception);
        }

        lock (_objects)
        {
            if (!_objects.TryGetValue(key, out AttemptObject? value))
            {
                return Task.FromResult<AttemptObject?>(null);
            }

            Assert.True(value.Bytes.Length <= maximumBytes);
            return Task.FromResult<AttemptObject?>(value with { Bytes = value.Bytes.ToArray() });
        }
    }

    public async Task<bool> CreateAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeCreate is { } before)
        {
            await before(key, cancellationToken);
        }

        lock (_objects)
        {
            if (_objects.ContainsKey(key))
            {
                return false;
            }

            Store(key, bytes);
            return true;
        }
    }

    public async Task<bool> ReplaceAsync(string key, string etag, byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeReplace is { } before)
        {
            await before(key, cancellationToken);
        }

        lock (_objects)
        {
            if (!_objects.TryGetValue(key, out AttemptObject? value) || value.ETag != etag)
            {
                return false;
            }

            Store(key, bytes);
        }

        if (AfterReplace is { } after)
        {
            await after(key, cancellationToken);
        }

        return true;
    }

    private void Store(string key, byte[] bytes)
    {
        AttemptObject value = new(bytes.ToArray(), (_history.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        _objects[key] = value;
        _history.Add((key, value));
    }
}

internal sealed class WaitForTwoWriters
{
    private readonly string _key;
    private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    internal WaitForTwoWriters(string key) => _key = key;

    internal async Task WaitAsync(string key, CancellationToken cancellationToken)
    {
        if (key != _key)
        {
            return;
        }

        if (Interlocked.Increment(ref _arrivals) >= 2)
        {
            _both.TrySetResult();
        }

        await _both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }
}
