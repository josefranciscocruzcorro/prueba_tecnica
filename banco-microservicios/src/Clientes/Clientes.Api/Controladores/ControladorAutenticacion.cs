using Clientes.Application.Clientes;
using Clientes.Application.Clientes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errores;
using Shared.Api.Seguridad;

namespace Clientes.Api.Controladores;

/// <summary>
/// Emisión y comprobación de la sesión. Es el único punto anónimo de la API: el token que entrega
/// sirve para operar tanto contra Clientes como contra Cuentas, porque ambos validan la misma firma.
/// </summary>
[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public sealed class ControladorAutenticacion(IServicioClientes servicio) : ControllerBase
{
    /// <summary>Inicia sesión con el identificador de cliente y su contraseña.</summary>
    [HttpPost("autenticar")]
    [AllowAnonymous]
    [ProducesResponseType<SesionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SesionDto>> Autenticar(
        [FromBody] CredencialesSolicitud credenciales, CancellationToken cancelacion)
        => Ok(await servicio.AutenticarAsync(credenciales, cancelacion));

    /// <summary>Devuelve el cliente dueño del token presentado.</summary>
    [HttpGet("sesion/actual")]
    [Authorize(Policy = ExtensionesAutenticacion.PoliticaClienteActivo)]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RespuestaError>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClienteDto>> SesionActual(CancellationToken cancelacion)
    {
        var clienteId = User.FindFirst(ClaimsBanco.ClienteId)!.Value;

        return Ok(await servicio.ObtenerAsync(clienteId, cancelacion));
    }
}
