using Cuentas.Application.Cuentas;
using Cuentas.Application.Cuentas.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errores;
using Shared.Kernel.Paginacion;

namespace Cuentas.Api.Controladores;

/// <summary>
/// F1 — Crear, leer y actualizar cuentas. No se expone borrado: una cuenta con historia contable
/// no se elimina, se desactiva con <c>PATCH { "estado": false }</c>, y así el registro se conserva.
/// </summary>
[ApiController]
[Authorize]
[Route("api/cuentas")]
[Produces("application/json")]
[ProducesResponseType<RespuestaError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<RespuestaError>(StatusCodes.Status500InternalServerError)]
public sealed class ControladorCuentas(IServicioCuentas servicio, IServicioMovimientos movimientos) : ControllerBase
{
    /// <summary>Lista las cuentas con filtros por cliente, texto y estado.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaDe<CuentaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDe<CuentaDto>>> Listar(
        [FromQuery] string? cliente,
        [FromQuery] string? buscar,
        [FromQuery] bool? estado,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancelacion)
        => Ok(await servicio.ListarAsync(cliente, buscar, estado, new ConsultaPaginada(pagina, tamano), cancelacion));

    /// <summary>Obtiene una cuenta por su número.</summary>
    [HttpGet("{numeroCuenta}")]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuentaDto>> Obtener(string numeroCuenta, CancellationToken cancelacion)
        => Ok(await servicio.ObtenerAsync(numeroCuenta, cancelacion));

    /// <summary>Apertura una cuenta para un cliente existente.</summary>
    [HttpPost]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CuentaDto>> Crear(
        [FromBody] CrearCuentaSolicitud solicitud, CancellationToken cancelacion)
    {
        var creada = await servicio.CrearAsync(solicitud, cancelacion);

        return CreatedAtAction(nameof(Obtener), new { numeroCuenta = creada.NumeroCuenta }, creada);
    }

    /// <summary>Reemplaza los datos editables de la cuenta.</summary>
    [HttpPut("{numeroCuenta}")]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuentaDto>> Actualizar(
        string numeroCuenta, [FromBody] ActualizarCuentaSolicitud solicitud, CancellationToken cancelacion)
        => Ok(await servicio.ActualizarAsync(numeroCuenta, solicitud, cancelacion));

    /// <summary>Modifica solo los campos enviados. Es la vía para activar o desactivar la cuenta.</summary>
    [HttpPatch("{numeroCuenta}")]
    [ProducesResponseType<CuentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CuentaDto>> Parchear(
        string numeroCuenta, [FromBody] ParchearCuentaSolicitud solicitud, CancellationToken cancelacion)
        => Ok(await servicio.ParchearAsync(numeroCuenta, solicitud, cancelacion));

    /// <summary>Movimientos de una cuenta concreta, del más reciente al más antiguo.</summary>
    [HttpGet("{numeroCuenta}/movimientos")]
    [ProducesResponseType<PaginaDe<MovimientoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaDe<MovimientoDto>>> MovimientosDeLaCuenta(
        string numeroCuenta,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancelacion)
    {
        // Se comprueba la existencia para distinguir "cuenta sin movimientos" de "cuenta inexistente".
        await servicio.ObtenerAsync(numeroCuenta, cancelacion);

        return Ok(await movimientos.ListarAsync(
            numeroCuenta, null, null, null, new ConsultaPaginada(pagina, tamano), cancelacion));
    }
}
