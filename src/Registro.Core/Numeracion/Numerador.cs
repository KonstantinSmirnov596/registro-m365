using System.Globalization;

namespace Registro.Core.Numeracion;

public sealed class NumeradorOptions
{
    /// <summary>How many compare-and-swap rounds to try before giving up.</summary>
    public int MaxAttempts { get; set; } = 50;

    /// <summary>Upper bound of the random back-off after a lost round grows with this step (0 disables waiting).</summary>
    public TimeSpan BackoffStep { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>Back-off never exceeds this value.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>Thrown when the counter stays contended for all configured attempts.</summary>
public sealed class NumeradorConcurrencyException(TipoFlujo flujo, int anio, int attempts)
    : Exception($"Could not reserve a number for {flujo.Prefijo()}-{anio} after {attempts} attempts.")
{
    public TipoFlujo Flujo { get; } = flujo;

    public int Anio { get; } = anio;
}

/// <summary>
/// Issues gap-free, never-repeating registration numbers per flow and per year.
/// </summary>
/// <remarks>
/// Algorithm (optimistic concurrency): read the counter with its ETag → write value + 1 only if
/// the ETag is unchanged → on conflict re-read and retry. The first number of a year is created
/// with an insert that fails if another caller created it first. No locks are held, so a crashed
/// caller can never block the register; the cost is a retry under contention.
/// </remarks>
public sealed class Numerador(ICounterStore store, NumeradorOptions? options = null)
{
    private readonly NumeradorOptions _options = options ?? new NumeradorOptions();

    public async Task<NumeroRegistro> SiguienteAsync(TipoFlujo flujo, int anio, CancellationToken cancellationToken = default)
    {
        var partition = flujo.Prefijo().ToString();
        var key = anio.ToString("D4", CultureInfo.InvariantCulture);

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = await store.ReadAsync(partition, key, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                if (await store.TryCreateAsync(partition, key, 1, cancellationToken).ConfigureAwait(false))
                {
                    return new NumeroRegistro(flujo, anio, 1);
                }
            }
            else
            {
                var next = current.Value + 1;
                if (next > NumeroRegistro.SecuenciaMaxima)
                {
                    throw new InvalidOperationException(
                        $"Register {partition}-{key} is full ({NumeroRegistro.SecuenciaMaxima} numbers issued).");
                }

                if (await store.TryUpdateAsync(partition, key, next, current.ETag, cancellationToken).ConfigureAwait(false))
                {
                    return new NumeroRegistro(flujo, anio, next);
                }
            }

            await BackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
        }

        throw new NumeradorConcurrencyException(flujo, anio, _options.MaxAttempts);
    }

    private async Task BackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        if (_options.BackoffStep <= TimeSpan.Zero)
        {
            await Task.Yield();
            return;
        }

        // Random jitter so that callers who lost the same round do not collide again in lockstep.
        var ceiling = Math.Min(_options.BackoffStep.TotalMilliseconds * attempt, _options.MaxBackoff.TotalMilliseconds);
        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * ceiling), cancellationToken).ConfigureAwait(false);
    }
}
