using Clientes.Application.Clientes.Dtos;
using Shared.Kernel.Paginacion;

namespace Clientes.Application.Clientes;

/// <summary>Casos de uso del maestro de clientes (F1 y autenticación).</summary>
public interface IServicioClientes
{
    Task<PaginaDe<ClienteDto>> ListarAsync(
        string? termino, bool? estado, ConsultaPaginada paginacion, CancellationToken cancelacion = default);

    Task<ClienteDto> ObtenerAsync(string clienteId, CancellationToken cancelacion = default);

    Task<ClienteDto> CrearAsync(CrearClienteSolicitud solicitud, CancellationToken cancelacion = default);

    Task<ClienteDto> ActualizarAsync(
        string clienteId, ActualizarClienteSolicitud solicitud, CancellationToken cancelacion = default);

    Task<ClienteDto> ParchearAsync(
        string clienteId, ParchearClienteSolicitud solicitud, CancellationToken cancelacion = default);

    Task EliminarAsync(string clienteId, CancellationToken cancelacion = default);

    Task<SesionDto> AutenticarAsync(CredencialesSolicitud credenciales, CancellationToken cancelacion = default);
}
