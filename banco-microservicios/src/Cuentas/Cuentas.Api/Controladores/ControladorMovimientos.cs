using Cuentas.Application.Cuentas;
using Cuentas.Application.Cuentas.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errores;
using Shared.Kernel.Paginacion;

namespace Cuentas.Api.Controladores;

/// <summary>
/// F2 y F3 — Registro y consulta de movimientos. Un intento de retiro sin fondos devuelve HTTP 400
/// con el código <c>SALDO_NO_DISPONIBLE</c> y el mensaje "Saldo no disponible".
/// </summary>
[ApiController]
[Authorize]
[Route("api/movimientos")]
[Produces("application/json")]
[ProducesResponseType<RespuestaError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<RespuestaError>(StatusCodes.Status500InternalServerError)]
public sealed class ControladorMovimientos(IServicioMovimientos servicio) : ControllerBase
{
    /// <summary>Lista movimientos con filtros por cuenta, cliente y rango de fechas.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaDe<MovimientoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDe<MovimientoDto>>> Listar(
        [FromQuery] string? cuenta,
        [FromQuery] string? cliente,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancelacion)
        => Ok(await servicio.ListarAsync(
            cuenta, cliente, desde, hasta, new ConsultaPaginada(pagina, tamano), cancelacion));

    /// <summary>Obtiene un movimiento por su identificador.</summary>
    [HttpGet("{movimientoId:guid}")]
    [ProducesResponseType<MovimientoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoDto>> Obtener(Guid movimientoId, CancellationToken cancelacion)
        => Ok(await servicio.ObtenerAsync(movimientoId, cancelacion));

    /// <summary>
    /// F2 — Registra un movimiento y actualiza el saldo disponible. El valor puede enviarse con
    /// signo (600 deposita, -575 retira) o en positivo acompañado de <c>tipoMovimiento</c>.
    /// </summary>
    /// <response code="400">
    /// Datos inválidos o, con el código <c>SALDO_NO_DISPONIBLE</c>, fondos insuficientes (F3).
    /// </response>
    [HttpPost]
    [ProducesResponseType<MovimientoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoDto>> Registrar(
        [FromBody] CrearMovimientoSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await servicio.RegistrarAsync(solicitud, cancelacion);

        return CreatedAtAction(nameof(Obtener), new { movimientoId = creado.MovimientoId }, creado);
    }

    /// <summary>
    /// Corrige un movimiento ya asentado. El saldo de la cuenta y el de los movimientos posteriores
    /// se recalculan; si la corrección dejase algún punto en descubierto, se rechaza.
    /// </summary>
    [HttpPut("{movimientoId:guid}")]
    [ProducesResponseType<MovimientoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoDto>> Actualizar(
        Guid movimientoId, [FromBody] ActualizarMovimientoSolicitud solicitud, CancellationToken cancelacion)
        => Ok(await servicio.ActualizarAsync(movimientoId, solicitud, cancelacion));
}
