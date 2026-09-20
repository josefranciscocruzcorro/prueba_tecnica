using Cuentas.Application.Cuentas.Dtos;
using Shared.Kernel.Paginacion;

namespace Cuentas.Application.Cuentas;

/// <summary>F1 — Casos de uso de cuentas (crear, leer, actualizar).</summary>
public interface IServicioCuentas
{
    Task<PaginaDe<CuentaDto>> ListarAsync(
        string? clienteId, string? buscar, bool? estado, ConsultaPaginada paginacion,
        CancellationToken cancelacion = default);

    Task<CuentaDto> ObtenerAsync(string numeroCuenta, CancellationToken cancelacion = default);

    Task<CuentaDto> CrearAsync(CrearCuentaSolicitud solicitud, CancellationToken cancelacion = default);

    Task<CuentaDto> ActualizarAsync(
        string numeroCuenta, ActualizarCuentaSolicitud solicitud, CancellationToken cancelacion = default);

    Task<CuentaDto> ParchearAsync(
        string numeroCuenta, ParchearCuentaSolicitud solicitud, CancellationToken cancelacion = default);
}

/// <summary>F2 y F3 — Casos de uso de movimientos.</summary>
public interface IServicioMovimientos
{
    Task<PaginaDe<MovimientoDto>> ListarAsync(
        string? numeroCuenta, string? clienteId, DateTime? desde, DateTime? hasta,
        ConsultaPaginada paginacion, CancellationToken cancelacion = default);

    Task<MovimientoDto> ObtenerAsync(Guid movimientoId, CancellationToken cancelacion = default);

    Task<MovimientoDto> RegistrarAsync(CrearMovimientoSolicitud solicitud, CancellationToken cancelacion = default);

    Task<MovimientoDto> ActualizarAsync(
        Guid movimientoId, ActualizarMovimientoSolicitud solicitud, CancellationToken cancelacion = default);
}
