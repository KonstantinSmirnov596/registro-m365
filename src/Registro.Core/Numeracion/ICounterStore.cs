namespace Registro.Core.Numeracion;

/// <summary>Current counter value together with its concurrency token.</summary>
public sealed record CounterSnapshot(long Value, string ETag);

/// <summary>
/// Storage for counters with optimistic concurrency (compare-and-swap on an ETag).
/// Implementations must make <see cref="TryCreateAsync"/> fail when the counter already exists
/// and <see cref="TryUpdateAsync"/> fail when the ETag no longer matches — the numbering
/// algorithm relies on exactly these two guarantees to be atomic.
/// </summary>
public interface ICounterStore
{
    /// <summary>Returns the counter, or <c>null</c> if it has never been created.</summary>
    Task<CounterSnapshot?> ReadAsync(string partition, string key, CancellationToken cancellationToken);

    /// <summary>Creates the counter with <paramref name="value"/>; <c>false</c> if someone created it first.</summary>
    Task<bool> TryCreateAsync(string partition, string key, long value, CancellationToken cancellationToken);

    /// <summary>Sets the counter to <paramref name="value"/> only if its ETag is still <paramref name="etag"/>.</summary>
    Task<bool> TryUpdateAsync(string partition, string key, long value, string etag, CancellationToken cancellationToken);
}
