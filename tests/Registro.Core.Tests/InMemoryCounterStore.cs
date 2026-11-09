using Registro.Core.Numeracion;

namespace Registro.Core.Tests;

/// <summary>
/// Test double with the same contract as Azure Table Storage:
/// insert fails if the entity exists, update fails if the ETag changed.
/// </summary>
internal sealed class InMemoryCounterStore : ICounterStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string, string), CounterSnapshot> _rows = [];

    public int Conflicts { get; private set; }

    public Task<CounterSnapshot?> ReadAsync(string partition, string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_rows.TryGetValue((partition, key), out var row) ? row : null);
        }
    }

    public Task<bool> TryCreateAsync(string partition, string key, long value, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_rows.ContainsKey((partition, key)))
            {
                Conflicts++;
                return Task.FromResult(false);
            }

            _rows[(partition, key)] = new CounterSnapshot(value, NewETag());
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryUpdateAsync(string partition, string key, long value, string etag, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_rows.TryGetValue((partition, key), out var row) || row.ETag != etag)
            {
                Conflicts++;
                return Task.FromResult(false);
            }

            _rows[(partition, key)] = new CounterSnapshot(value, NewETag());
            return Task.FromResult(true);
        }
    }

    private static string NewETag() => Guid.NewGuid().ToString("N");
}

/// <summary>Store whose writes always lose the race — to test the give-up path.</summary>
internal sealed class AlwaysConflictingStore : ICounterStore
{
    public int Attempts { get; private set; }

    public Task<CounterSnapshot?> ReadAsync(string partition, string key, CancellationToken cancellationToken) =>
        Task.FromResult<CounterSnapshot?>(new CounterSnapshot(41, "stale"));

    public Task<bool> TryCreateAsync(string partition, string key, long value, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> TryUpdateAsync(string partition, string key, long value, string etag, CancellationToken cancellationToken)
    {
        Attempts++;
        return Task.FromResult(false);
    }
}
