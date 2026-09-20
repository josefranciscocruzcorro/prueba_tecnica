using Clientes.Application.Actividad;
using Clientes.Application.Clientes;
using Clientes.Application.Clientes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errores;
using Shared.Kernel.Paginacion;

namespace Clientes.Api.Controladores;

/// <summary>
/// F1 — CRUD completo del maestro de clientes. El controlador es deliberadamente delgado: valida
/// la ruta, delega en el caso de uso y elige el código HTTP. Ni una sola regla de negocio vive aquí.
/// </summary>
[ApiController]
[Authorize]
[Route("api/clientes")]
[Produces("application/json")]
[ProducesResponseType<RespuestaError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<RespuestaError>(StatusCodes.Status500InternalServerError)]
public sealed class ControladorClientes(IServicioClientes servicio, IConsultaActividad actividad) : ControllerBase
{
    /// <summary>Lista los clientes con búsqueda por texto, filtro de estado y paginación.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaDe<ClienteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDe<ClienteDto>>> Listar(
        [FromQuery] string? buscar,
        [FromQuery] bool? estado,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancelacion)
        => Ok(await servicio.ListarAsync(buscar, estado, new ConsultaPaginada(pagina, tamano), cancelacion));

    /// <summary>Obtiene un cliente por su identificador de negocio.</summary>
    [HttpGet("{clienteId}")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> Obtener(string clienteId, CancellationToken cancelacion)
        => Ok(await servicio.ObtenerAsync(clienteId, cancelacion));

    /// <summary>Registra un cliente nuevo.</summary>
    [HttpPost]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteDto>> Crear(
        [FromBody] CrearClienteSolicitud solicitud, CancellationToken cancelacion)
    {
        var creado = await servicio.CrearAsync(solicitud, cancelacion);

        return CreatedAtAction(nameof(Obtener), new { clienteId = creado.ClienteId }, creado);
    }

    /// <summary>Reemplaza todos los datos editables del cliente.</summary>
    [HttpPut("{clienteId}")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> Actualizar(
        string clienteId, [FromBody] ActualizarClienteSolicitud solicitud, CancellationToken cancelacion)
        => Ok(await servicio.ActualizarAsync(clienteId, solicitud, cancelacion));

    /// <summary>Modifica solo los campos enviados.</summary>
    [HttpPatch("{clienteId}")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> Parchear(
        string clienteId, [FromBody] ParchearClienteSolicitud solicitud, CancellationToken cancelacion)
        => Ok(await servicio.ParchearAsync(clienteId, solicitud, cancelacion));

    /// <summary>Elimina el cliente y notifica la baja al resto de microservicios.</summary>
    [HttpDelete("{clienteId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(string clienteId, CancellationToken cancelacion)
    {
        await servicio.EliminarAsync(clienteId, cancelacion);

        return NoContent();
    }

    /// <summary>
    /// Bitácora de actividad del cliente, alimentada por los eventos que publica el microservicio
    /// de Cuentas. Es la evidencia observable de la comunicación asíncrona entre ambos servicios.
    /// </summary>
    [HttpGet("{clienteId}/actividad")]
    [ProducesResponseType<IReadOnlyList<ActividadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ActividadDto>>> Actividad(
        string clienteId, [FromQuery] int limite = 20, CancellationToken cancelacion = default)
    {
        // Comprobamos que el cliente existe para distinguir "sin actividad" de "no existe".
        await servicio.ObtenerAsync(clienteId, cancelacion);

        return Ok(await actividad.ObtenerUltimasAsync(clienteId, limite, cancelacion));
    }
}
