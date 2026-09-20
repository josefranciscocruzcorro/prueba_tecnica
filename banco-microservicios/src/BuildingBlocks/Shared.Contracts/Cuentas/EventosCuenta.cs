using Shared.Contracts.Clientes;

namespace Shared.Contracts.Cuentas;

/// <summary>
/// Contratos publicados por el microservicio de Cuentas. Cierran el circuito asíncrono: Clientes
/// los consume para mantener su bitácora de actividad sin consultar a Cuentas de forma síncrona.
/// </summary>
public sealed record MovimientoRegistrado(
    Guid EventoId,
    DateTime OcurridoEn,
    Guid MovimientoId,
    string NumeroCuenta,
    string ClienteId,
    string TipoMovimiento,
    decimal Valor,
    decimal SaldoResultante,
    DateTime Fecha) : IEventoIntegracion;

/// <summary>Apertura de una cuenta asociada a un cliente existente.</summary>
public sealed record CuentaAperturada(
    Guid EventoId,
    DateTime OcurridoEn,
    Guid CuentaId,
    string NumeroCuenta,
    string ClienteId,
    string TipoCuenta,
    decimal SaldoInicial) : IEventoIntegracion;
