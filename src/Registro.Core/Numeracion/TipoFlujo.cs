namespace Registro.Core.Numeracion;

/// <summary>
/// Document flow (register) a registration number belongs to.
/// Each flow keeps its own counter per year: E-2026-000001, S-2026-000001, I-2026-000001.
/// </summary>
public enum TipoFlujo
{
    /// <summary>Inbound correspondence — library <c>Entrada</c>, prefix <c>E</c>.</summary>
    Entrada,

    /// <summary>Outbound correspondence — library <c>Salida</c>, prefix <c>S</c>.</summary>
    Salida,

    /// <summary>Internal documents — library <c>Interno</c>, prefix <c>I</c>.</summary>
    Interno,
}

public static class TipoFlujoExtensions
{
    public static char Prefijo(this TipoFlujo flujo) => flujo switch
    {
        TipoFlujo.Entrada => 'E',
        TipoFlujo.Salida => 'S',
        TipoFlujo.Interno => 'I',
        _ => throw new ArgumentOutOfRangeException(nameof(flujo), flujo, null),
    };

    /// <summary>
    /// Accepts the prefix (<c>E</c>/<c>S</c>/<c>I</c>) or the Spanish name
    /// (<c>entrada</c>/<c>salida</c>/<c>interno</c>), case-insensitive.
    /// </summary>
    public static bool TryParse(string? value, out TipoFlujo flujo)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "E" or "ENTRADA":
                flujo = TipoFlujo.Entrada;
                return true;
            case "S" or "SALIDA":
                flujo = TipoFlujo.Salida;
                return true;
            case "I" or "INTERNO":
                flujo = TipoFlujo.Interno;
                return true;
            default:
                flujo = default;
                return false;
        }
    }
}
