using System.Globalization;
using Cuentas.Application.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errores;
using Shared.Kernel.Excepciones;

namespace Cuentas.Api.Controladores;

/// <summary>
/// F4 — Estado de cuenta por cliente y rango de fechas.
/// </summary>
/// <remarks>
/// El enunciado pide la forma <c>/reportes?fecha={rango}&amp;cliente={cliente}</c>, que se soporta
/// tal cual (<c>fecha=2022-02-01,2022-02-28</c>). Como esa sintaxis es incómoda de construir desde
/// un formulario, se admite además el par <c>desde</c>/<c>hasta</c>; ambas vías producen el mismo
/// resultado. El parámetro <c>formato</c> elige entre la respuesta estructurada (predeterminada) y
/// la lista plana con las claves literales del ejemplo del enunciado.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/reportes")]
[Produces("application/json")]
[ProducesResponseType<RespuestaError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<RespuestaError>(StatusCodes.Status500InternalServerError)]
public sealed class ControladorReportes(IServicioReportes servicio) : ControllerBase
{
    /// <summary>Genera el estado de cuenta en formato JSON.</summary>
    /// <param name="cliente">Identificador del cliente, por ejemplo <c>CLI-002</c>.</param>
    /// <param name="fecha">Rango como <c>aaaa-MM-dd,aaaa-MM-dd</c>. Alternativa a desde/hasta.</param>
    /// <param name="desde">Inicio del rango, si no se usa <c>fecha</c>.</param>
    /// <param name="hasta">Fin del rango, si no se usa <c>fecha</c>.</param>
    /// <param name="formato"><c>detallado</c> (predeterminado) o <c>plano</c>.</param>
    [HttpGet]
    [ProducesResponseType<EstadoDeCuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generar(
        [FromQuery] string cliente,
        [FromQuery] string? fecha,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] FormatoReporte formato = FormatoReporte.Detallado,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(cliente))
        {
            throw new ExcepcionReglaNegocio("CLIENTE_REQUERIDO", "Indique el cliente del que desea el reporte.");
        }

        var (inicio, fin) = ResolverRango(fecha, desde, hasta);

        return formato == FormatoReporte.Plano
            ? Ok(await servicio.GenerarPlanoAsync(cliente, inicio, fin, cancelacion))
            : Ok(await servicio.GenerarAsync(cliente, inicio, fin, cancelacion));
    }

    /// <summary>
    /// Acepta las dos formas de expresar el rango y las reduce a una. Si no se indica ninguna, se
    /// asume el mes en curso, que es lo que espera quien abre el reporte sin configurar nada.
    /// </summary>
    private static (DateOnly Desde, DateOnly Hasta) ResolverRango(string? fecha, DateOnly? desde, DateOnly? hasta)
    {
        if (!string.IsNullOrWhiteSpace(fecha))
        {
            var partes = fecha.Split([',', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (partes.Length != 2)
            {
                throw new ExcepcionReglaNegocio(
                    "RANGO_MAL_FORMADO",
                    "El parámetro 'fecha' debe ser un rango con el formato aaaa-MM-dd,aaaa-MM-dd.");
            }

            return (Interpretar(partes[0]), Interpretar(partes[1]));
        }

        if (desde is not null && hasta is not null)
        {
            return (desde.Value, hasta.Value);
        }

        if (desde is not null || hasta is not null)
        {
            throw new ExcepcionReglaNegocio(
                "RANGO_INCOMPLETO", "Indique 'desde' y 'hasta', o bien el parámetro 'fecha' con el rango completo.");
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        return (new DateOnly(hoy.Year, hoy.Month, 1), hoy);
    }

    private static DateOnly Interpretar(string valor) =>
        DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var resultado)
        || DateOnly.TryParse(valor, new CultureInfo("es-EC"), out resultado)
            ? resultado
            : throw new ExcepcionReglaNegocio(
                "FECHA_INVALIDA", $"La fecha '{valor}' no es válida. Use el formato aaaa-MM-dd.");
}
