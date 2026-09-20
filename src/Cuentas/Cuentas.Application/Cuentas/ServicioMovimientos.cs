using Cuentas.Application.Cuentas.Dtos;
using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Excepciones;
using Shared.Kernel.Paginacion;
using Shared.Kernel.Persistencia;

namespace Cuentas.Application.Cuentas;

/// <summary>
/// Casos de uso de movimientos (F2 y F3). Toda la aritmética de saldos vive en el agregado
/// <see cref="Cuenta"/>; aquí solo se resuelve la cuenta, se normaliza el signo del importe y se
/// confirma la transacción.
/// </summary>
public sealed class ServicioMovimientos(
    IRepositorioCuentas cuentas,
    IRepositorioMovimientos movimientos,
    IRepositorioClientesReplicados clientes,
    IUnidadDeTrabajo unidadDeTrabajo,
    ILogger<ServicioMovimientos> registro) : IServicioMovimientos
{
    public async Task<PaginaDe<MovimientoDto>> ListarAsync(
        string? numeroCuenta, string? clienteId, DateTime? desde, DateTime? hasta,
        ConsultaPaginada paginacion, CancellationToken cancelacion = default)
    {
        var pagina = await movimientos.BuscarAsync(
            numeroCuenta, clienteId, desde, hasta, paginacion, cancelacion);

        return new PaginaDe<MovimientoDto>(
            [.. pagina.Elementos.Select(par => MovimientoDto.Desde(par.Movimiento, par.Cuenta))],
            pagina.Pagina,
            pagina.Tamano,
            pagina.Total);
    }

    public async Task<MovimientoDto> ObtenerAsync(Guid movimientoId, CancellationToken cancelacion = default)
    {
        var movimiento = await movimientos.ObtenerPorIdAsync(movimientoId, cancelacion)
            ?? throw new ExcepcionNoEncontrado("el movimiento", movimientoId);

        var cuenta = await cuentas.ObtenerPorIdAsync(movimiento.CuentaId, cancelacion)
            ?? throw new ExcepcionNoEncontrado("la cuenta del movimiento", movimiento.CuentaId);

        return MovimientoDto.Desde(movimiento, cuenta);
    }

    /// <summary>F2 — Asienta el movimiento y actualiza el saldo disponible de la cuenta.</summary>
    public async Task<MovimientoDto> RegistrarAsync(
        CrearMovimientoSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var cuenta = await ObtenerCuentaOperableAsync(solicitud.NumeroCuenta, cancelacion);
        var valor = NormalizarSigno(solicitud.Valor, solicitud.TipoMovimiento);

        // Si el saldo no alcanza, el agregado lanza ExcepcionSaldoNoDisponible y el middleware la
        // traduce al mensaje "Saldo no disponible" exigido por F3.
        var movimiento = cuenta.RegistrarMovimiento(valor, solicitud.Fecha?.ToUniversalTime());

        cuentas.Actualizar(cuenta);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        registro.LogInformation(
            "Movimiento de {Valor} asentado en {NumeroCuenta}. Saldo disponible: {Saldo}.",
            valor, cuenta.NumeroCuenta, cuenta.SaldoDisponible);

        return MovimientoDto.Desde(movimiento, cuenta);
    }

    public async Task<MovimientoDto> ActualizarAsync(
        Guid movimientoId, ActualizarMovimientoSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var referencia = await movimientos.ObtenerPorIdAsync(movimientoId, cancelacion)
            ?? throw new ExcepcionNoEncontrado("el movimiento", movimientoId);

        var cuenta = await cuentas.ObtenerPorIdAsync(referencia.CuentaId, cancelacion)
            ?? throw new ExcepcionNoEncontrado("la cuenta del movimiento", referencia.CuentaId);

        var completa = await ObtenerCuentaOperableAsync(cuenta.NumeroCuenta, cancelacion);
        var valor = NormalizarSigno(solicitud.Valor, solicitud.TipoMovimiento);

        var movimiento = completa.CorregirMovimiento(movimientoId, valor, solicitud.Fecha?.ToUniversalTime());

        cuentas.Actualizar(completa);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        registro.LogInformation(
            "Movimiento {MovimientoId} corregido a {Valor}. Nuevo saldo: {Saldo}.",
            movimientoId, valor, completa.SaldoDisponible);

        return MovimientoDto.Desde(movimiento, completa);
    }

    /// <summary>
    /// Unifica las dos formas de expresar un importe: con signo, o en positivo acompañado del
    /// tipo. Que el cliente elija la que le resulte natural no debe complicar el dominio.
    /// </summary>
    private static decimal NormalizarSigno(decimal valor, TipoMovimiento? tipo) => tipo switch
    {
        Domain.Movimientos.TipoMovimiento.Deposito => Math.Abs(valor),
        Domain.Movimientos.TipoMovimiento.Retiro => -Math.Abs(valor),
        _ => valor,
    };

    private async Task<Cuenta> ObtenerCuentaOperableAsync(string numeroCuenta, CancellationToken cancelacion)
    {
        var cuenta = await cuentas.ObtenerConMovimientosAsync(numeroCuenta, cancelacion)
            ?? throw new ExcepcionNoEncontrado("la cuenta", numeroCuenta);

        var cliente = await clientes.ObtenerAsync(cuenta.ClienteId, cancelacion);

        if (cliente is { Estado: false })
        {
            throw new ExcepcionReglaNegocio(
                "CLIENTE_INACTIVO",
                $"El cliente {cuenta.ClienteId} está inactivo; sus cuentas no admiten movimientos.");
        }

        return cuenta;
    }
}
