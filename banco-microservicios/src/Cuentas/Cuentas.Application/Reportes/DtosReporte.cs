using System.Text.Json.Serialization;

namespace Cuentas.Application.Reportes;

/// <summary>Formato de salida del estado de cuenta (F4).</summary>
public enum FormatoReporte
{
    /// <summary>Estructura jerárquica: cliente → cuentas → movimientos, con totales. Es el predeterminado.</summary>
    Detallado = 1,

    /// <summary>Lista plana con las claves literales del enunciado, lista para volcar a una tabla.</summary>
    Plano = 2,
}

/// <summary>Rango de fechas efectivamente aplicado, devuelto para que el consumidor pueda mostrarlo.</summary>
public sealed record RangoFechas(DateOnly Desde, DateOnly Hasta);

/// <summary>Cabecera del cliente en el reporte.</summary>
public sealed record ClienteReporte(string ClienteId, string Nombre, string Identificacion, bool Estado);

/// <summary>Totales del periodo, para no obligar al consumidor a sumar.</summary>
public sealed record ResumenReporte(
    int TotalCuentas,
    int TotalMovimientos,
    decimal TotalDepositos,
    decimal TotalRetiros,
    decimal SaldoDisponibleTotal);

/// <summary>Movimiento dentro del estado de cuenta.</summary>
public sealed record MovimientoReporte(
    DateTime Fecha, string TipoMovimiento, decimal Valor, decimal SaldoDisponible);

/// <summary>Cuenta con su saldo y el detalle de movimientos del periodo.</summary>
public sealed record CuentaReporte(
    string NumeroCuenta,
    string TipoCuenta,
    decimal SaldoInicial,
    decimal SaldoDisponible,
    bool Estado,
    decimal TotalMovimientosPeriodo,
    IReadOnlyList<MovimientoReporte> Movimientos);

/// <summary>
/// Estado de cuenta completo (F4): cuentas asociadas con sus saldos y el detalle de movimientos
/// del rango solicitado.
/// </summary>
public sealed record EstadoDeCuentaDto(
    ClienteReporte Cliente,
    RangoFechas Rango,
    ResumenReporte Resumen,
    IReadOnlyList<CuentaReporte> Cuentas);

/// <summary>
/// Fila del formato plano. Reproduce literalmente las claves del ejemplo del enunciado, incluidos
/// los espacios, para que el resultado sea comparable carácter a carácter con lo pedido.
/// </summary>
public sealed record FilaEstadoDeCuenta(
    [property: JsonPropertyName("Fecha")] string Fecha,
    [property: JsonPropertyName("Cliente")] string Cliente,
    [property: JsonPropertyName("Numero Cuenta")] string NumeroCuenta,
    [property: JsonPropertyName("Tipo")] string Tipo,
    [property: JsonPropertyName("Saldo Inicial")] decimal SaldoInicial,
    [property: JsonPropertyName("Estado")] bool Estado,
    [property: JsonPropertyName("Movimiento")] decimal Movimiento,
    [property: JsonPropertyName("Saldo Disponible")] decimal SaldoDisponible);
