using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Registro.Core.Numeracion;

/// <summary>
/// Registration number <c>{flow}-{year}-{sequence:000000}</c>, e.g. <c>E-2026-000001</c>.
/// </summary>
public readonly partial record struct NumeroRegistro
{
    public const int SecuenciaMaxima = 999_999;

    public NumeroRegistro(TipoFlujo flujo, int anio, long secuencia)
    {
        if (anio is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(anio), anio, "Year must be between 2000 and 9999.");
        }

        if (secuencia is < 1 or > SecuenciaMaxima)
        {
            throw new ArgumentOutOfRangeException(nameof(secuencia), secuencia, $"Sequence must be between 1 and {SecuenciaMaxima}.");
        }

        Flujo = flujo;
        Anio = anio;
        Secuencia = secuencia;
    }

    public TipoFlujo Flujo { get; }

    public int Anio { get; }

    public long Secuencia { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Flujo.Prefijo()}-{Anio:D4}-{Secuencia:D6}");

    public static NumeroRegistro Parse(string value) =>
        TryParse(value, out var numero)
            ? numero
            : throw new FormatException($"'{value}' is not a valid registration number (expected e.g. E-2026-000001).");

    public static bool TryParse([NotNullWhen(true)] string? value, out NumeroRegistro numero)
    {
        numero = default;
        if (value is null)
        {
            return false;
        }

        var match = Formato().Match(value);
        if (!match.Success || !TipoFlujoExtensions.TryParse(match.Groups["f"].Value, out var flujo))
        {
            return false;
        }

        var anio = int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
        var secuencia = long.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        if (anio < 2000 || secuencia < 1)
        {
            return false;
        }

        numero = new NumeroRegistro(flujo, anio, secuencia);
        return true;
    }

    [GeneratedRegex(@"^(?<f>[ESI])-(?<a>\d{4})-(?<s>\d{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
