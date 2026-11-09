using Registro.Core.Numeracion;

namespace Registro.Core.Tests;

public class NumeroRegistroTests
{
    [Theory]
    [InlineData(TipoFlujo.Entrada, 2026, 1, "E-2026-000001")]
    [InlineData(TipoFlujo.Salida, 2026, 42, "S-2026-000042")]
    [InlineData(TipoFlujo.Interno, 2027, 999_999, "I-2027-999999")]
    public void Formats_as_flow_year_six_digit_sequence(TipoFlujo flujo, int anio, long secuencia, string expected)
    {
        Assert.Equal(expected, new NumeroRegistro(flujo, anio, secuencia).ToString());
    }

    [Fact]
    public void Parse_round_trips()
    {
        var numero = NumeroRegistro.Parse("S-2026-000042");

        Assert.Equal(TipoFlujo.Salida, numero.Flujo);
        Assert.Equal(2026, numero.Anio);
        Assert.Equal(42, numero.Secuencia);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("E-2026-1")]
    [InlineData("X-2026-000001")]
    [InlineData("E-2026-000000")]
    [InlineData("e-2026-000001")]
    public void Rejects_malformed_numbers(string? value)
    {
        Assert.False(NumeroRegistro.TryParse(value, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void Sequence_must_fit_six_digits(long secuencia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumeroRegistro(TipoFlujo.Entrada, 2026, secuencia));
    }

    [Theory]
    [InlineData("E", TipoFlujo.Entrada)]
    [InlineData("entrada", TipoFlujo.Entrada)]
    [InlineData("SALIDA", TipoFlujo.Salida)]
    [InlineData("i", TipoFlujo.Interno)]
    public void Flow_accepts_prefix_or_name(string value, TipoFlujo expected)
    {
        Assert.True(TipoFlujoExtensions.TryParse(value, out var flujo));
        Assert.Equal(expected, flujo);
    }
}
