using Shared.Kernel.Dominio;

namespace Cuentas.Domain.Eventos;

/// <summary>Se abrió una cuenta para un cliente.</summary>
public sealed record CuentaAperturadaEnDominio(
    Guid CuentaId, string NumeroCuenta, string ClienteId, string TipoCuenta, decimal SaldoInicial) : IEventoDominio;

/// <summary>Se asentó un movimiento sobre una cuenta.</summary>
public sealed record MovimientoAsentado(
    Guid MovimientoId,
    string NumeroCuenta,
    string ClienteId,
    string TipoMovimiento,
    decimal Valor,
    decimal SaldoResultante,
    DateTime Fecha) : IEventoDominio;
