using Cuentas.Domain.Clientes;
using Cuentas.Domain.Movimientos;
using Shared.Kernel.Paginacion;
using Shared.Kernel.Persistencia;

namespace Cuentas.Domain.Cuentas;

/// <summary>Puerto de persistencia del agregado Cuenta (patrón Repository).</summary>
public interface IRepositorioCuentas : IRepositorio<Cuenta, Guid>
{
    /// <summary>Carga la cuenta sin sus movimientos. Suficiente para consultas y edición de datos.</summary>
    Task<Cuenta?> ObtenerPorNumeroAsync(string numeroCuenta, CancellationToken cancelacion = default);

    /// <summary>
    /// Carga la cuenta con toda su serie de movimientos. Es la forma obligatoria de obtenerla
    /// antes de asentar o corregir apuntes, porque el saldo se recalcula sobre el histórico.
    /// </summary>
    Task<Cuenta?> ObtenerConMovimientosAsync(string numeroCuenta, CancellationToken cancelacion = default);

    Task<bool> ExisteNumeroAsync(string numeroCuenta, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(string clienteId, CancellationToken cancelacion = default);

    Task<PaginaDe<Cuenta>> BuscarAsync(
        string? clienteId,
        string? termino,
        bool? estado,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default);
}

/// <summary>Consultas sobre movimientos que no requieren cargar el agregado completo.</summary>
public interface IRepositorioMovimientos
{
    Task<Movimiento?> ObtenerPorIdAsync(Guid movimientoId, CancellationToken cancelacion = default);

    Task<PaginaDe<(Movimiento Movimiento, Cuenta Cuenta)>> BuscarAsync(
        string? numeroCuenta,
        string? clienteId,
        DateTime? desde,
        DateTime? hasta,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default);

    /// <summary>Movimientos de las cuentas de un cliente dentro de un rango, para el reporte F4.</summary>
    Task<IReadOnlyList<Movimiento>> ListarParaReporteAsync(
        IReadOnlyCollection<Guid> cuentaIds,
        DateTime desde,
        DateTime hasta,
        CancellationToken cancelacion = default);
}

/// <summary>Puerto de acceso a la réplica local de clientes.</summary>
public interface IRepositorioClientesReplicados
{
    Task<ClienteReferencia?> ObtenerAsync(string clienteId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<ClienteReferencia>> ListarAsync(CancellationToken cancelacion = default);

    Task GuardarAsync(ClienteReferencia cliente, CancellationToken cancelacion = default);
}
