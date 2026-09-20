using Shared.Kernel.Paginacion;
using Shared.Kernel.Persistencia;

namespace Clientes.Domain.Clientes;

/// <summary>
/// Puerto de persistencia del agregado Cliente. Se declara en el dominio y se implementa en la
/// infraestructura (inversión de dependencias), lo que permite probar los casos de uso con dobles.
/// </summary>
public interface IRepositorioClientes : IRepositorio<Cliente, Guid>
{
    Task<Cliente?> ObtenerPorClienteIdAsync(string clienteId, CancellationToken cancelacion = default);

    Task<bool> ExisteClienteIdAsync(string clienteId, Guid? excluyendoId = null, CancellationToken cancelacion = default);

    Task<bool> ExisteIdentificacionAsync(string identificacion, Guid? excluyendoId = null, CancellationToken cancelacion = default);

    Task<PaginaDe<Cliente>> BuscarAsync(
        string? termino,
        bool? estado,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default);
}
