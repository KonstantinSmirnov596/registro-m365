using Registro.Core.Numeracion;

namespace Registro.Core.Tests;

public class NumeradorTests
{
    private static readonly NumeradorOptions NoWait = new() { BackoffStep = TimeSpan.Zero, MaxAttempts = 10_000 };

    [Fact]
    public async Task First_number_of_the_year_is_one_then_increments()
    {
        var numerador = new Numerador(new InMemoryCounterStore(), NoWait);

        var first = await numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, TestContext.Current.CancellationToken);
        var second = await numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, TestContext.Current.CancellationToken);

        Assert.Equal("E-2026-000001", first.ToString());
        Assert.Equal("E-2026-000002", second.ToString());
    }

    [Fact]
    public async Task Each_flow_and_year_has_its_own_counter()
    {
        var numerador = new Numerador(new InMemoryCounterStore(), NoWait);
        var ct = TestContext.Current.CancellationToken;

        await numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, ct);
        await numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, ct);

        Assert.Equal("S-2026-000001", (await numerador.SiguienteAsync(TipoFlujo.Salida, 2026, ct)).ToString());
        Assert.Equal("I-2026-000001", (await numerador.SiguienteAsync(TipoFlujo.Interno, 2026, ct)).ToString());
        Assert.Equal("E-2027-000001", (await numerador.SiguienteAsync(TipoFlujo.Entrada, 2027, ct)).ToString());
        Assert.Equal("E-2026-000003", (await numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, ct)).ToString());
    }

    [Fact]
    public async Task Concurrent_callers_get_unique_gap_free_numbers()
    {
        const int callers = 300;
        var store = new InMemoryCounterStore();
        var numerador = new Numerador(store, NoWait);
        var ct = TestContext.Current.CancellationToken;

        var issued = await Task.WhenAll(
            Enumerable.Range(0, callers).Select(_ => Task.Run(() => numerador.SiguienteAsync(TipoFlujo.Entrada, 2026, ct), ct)));

        var sequences = issued.Select(n => n.Secuencia).Order().ToArray();
        Assert.Equal(Enumerable.Range(1, callers).Select(i => (long)i), sequences);
    }

    [Fact]
    public async Task Gives_up_after_max_attempts_without_issuing_a_number()
    {
        var store = new AlwaysConflictingStore();
        var numerador = new Numerador(store, new NumeradorOptions { BackoffStep = TimeSpan.Zero, MaxAttempts = 5 });

        var ex = await Assert.ThrowsAsync<NumeradorConcurrencyException>(
            () => numerador.SiguienteAsync(TipoFlujo.Salida, 2026, TestContext.Current.CancellationToken));

        Assert.Equal(5, store.Attempts);
        Assert.Equal(TipoFlujo.Salida, ex.Flujo);
    }
}
