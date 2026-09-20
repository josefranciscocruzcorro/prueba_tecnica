using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;

namespace Cuentas.Application.Cuentas.Dtos;

/// <summary>Proyección de lectura de una cuenta.</summary>
public sealed record CuentaDto(
    Guid CuentaId,
    string NumeroCuenta,
    string TipoCuenta,
    decimal SaldoInicial,
    decimal SaldoDisponible,
    bool Estado,
    string ClienteId,
    string? NombreCliente,
    DateTime CreadaEn)
{
    public static CuentaDto Desde(Cuenta cuenta, string? nombreCliente = null) => new(
        cuenta.Id,
        cuenta.NumeroCuenta,
        cuenta.Tipo.ToString(),
        cuenta.SaldoInicial,
        cuenta.SaldoDisponible,
        cuenta.Estado,
        cuenta.ClienteId,
        nombreCliente,
        cuenta.CreadaEn);
}

/// <summary>Proyección de lectura de un movimiento.</summary>
public sealed record MovimientoDto(
    Guid MovimientoId,
    string NumeroCuenta,
    string TipoCuenta,
    DateTime Fecha,
    string TipoMovimiento,
    decimal Valor,
    decimal SaldoDisponible)
{
    public static MovimientoDto Desde(Movimiento movimiento, Cuenta cuenta) => new(
        movimiento.Id,
        cuenta.NumeroCuenta,
        cuenta.Tipo.ToString(),
        movimiento.Fecha,
        movimiento.Tipo.ToString(),
        movimiento.Valor,
        movimiento.SaldoDisponible);
}

/// <summary>Apertura de cuenta. El número puede omitirse: el sistema genera uno libre.</summary>
public sealed record CrearCuentaSolicitud(
    string ClienteId,
    TipoCuenta TipoCuenta,
    decimal SaldoInicial,
    string? NumeroCuenta = null,
    bool Estado = true);

/// <summary>Reemplazo de los datos editables de la cuenta (PUT).</summary>
public sealed record ActualizarCuentaSolicitud(TipoCuenta TipoCuenta, decimal SaldoInicial, bool Estado);

/// <summary>Modificación parcial de la cuenta (PATCH).</summary>
public sealed record ParchearCuentaSolicitud(
    TipoCuenta? TipoCuenta = null, decimal? SaldoInicial = null, bool? Estado = null);

/// <summary>
/// Alta de movimiento (F2). El valor lleva el signo: positivo deposita y negativo retira. Como
/// alternativa se admite <paramref name="TipoMovimiento"/> con un importe en positivo, que resulta
/// más natural desde un formulario; el servicio normaliza ambas formas al mismo signo.
/// </summary>
public sealed record CrearMovimientoSolicitud(
    string NumeroCuenta,
    decimal Valor,
    TipoMovimiento? TipoMovimiento = null,
    DateTime? Fecha = null);

/// <summary>Corrección de un movimiento ya asentado.</summary>
public sealed record ActualizarMovimientoSolicitud(
    decimal Valor, TipoMovimiento? TipoMovimiento = null, DateTime? Fecha = null);
