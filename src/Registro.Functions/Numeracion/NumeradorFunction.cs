using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Registro.Core.Numeracion;

namespace Registro.Functions.Numeracion;

/// <summary>
/// HTTP endpoint that reserves the next registration number.
/// <c>POST /api/numerador/{flujo}[?anio=2026]</c> → <c>{ "numero": "E-2026-000001", ... }</c>.
/// Called from the Power Automate flow <c>Registrar entrada</c> (HTTP action) instead of deriving
/// the number from the SharePoint list item ID.
/// </summary>
public sealed class NumeradorFunction(
    Numerador numerador,
    TimeProvider clock,
    IConfiguration configuration,
    ILogger<NumeradorFunction> logger)
{
    private const string DefaultTimeZone = "America/Montevideo";

    [Function("Numerador")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "numerador/{flujo}")] HttpRequest request,
        string flujo,
        CancellationToken cancellationToken)
    {
        if (!TipoFlujoExtensions.TryParse(flujo, out var tipo))
        {
            return new BadRequestObjectResult(new { error = "Unknown flow. Use E, S, I (or entrada, salida, interno)." });
        }

        int anio;
        if (request.Query.TryGetValue("anio", out var anioText) && !string.IsNullOrEmpty(anioText))
        {
            if (!int.TryParse(anioText, NumberStyles.None, CultureInfo.InvariantCulture, out anio) || anio is < 2000 or > 9999)
            {
                return new BadRequestObjectResult(new { error = "Parameter 'anio' must be a four-digit year." });
            }
        }
        else
        {
            anio = CurrentYear();
        }

        try
        {
            var numero = await numerador.SiguienteAsync(tipo, anio, cancellationToken);
            logger.LogInformation("Issued registration number {Numero}", numero.ToString());

            return new OkObjectResult(new
            {
                numero = numero.ToString(),
                flujo = numero.Flujo.Prefijo().ToString(),
                anio = numero.Anio,
                secuencia = numero.Secuencia,
            });
        }
        catch (NumeradorConcurrencyException ex)
        {
            logger.LogWarning(ex, "Counter contention for {Flujo}-{Anio}", tipo, anio);
            return new ObjectResult(new { error = "Register is busy, retry." }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }

    /// <summary>The registration year follows the organisation's calendar, not UTC (31 Dec 22:00 in Montevideo is still that year).</summary>
    private int CurrentYear()
    {
        var zoneId = configuration["Numerador:TimeZone"] ?? DefaultTimeZone;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).Year;
    }
}
