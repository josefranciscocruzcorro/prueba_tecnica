using Clientes.Domain.Actividad;
using Clientes.Infrastructure.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Cuentas;

namespace Clientes.Infrastructure.Mensajeria;

/// <summary>
/// Consume los movimientos publicados por el microservicio de Cuentas y los anota en la bitácora
/// del cliente. Cierra el circuito asíncrono en sentido inverso.
/// </summary>
/// <remarks>
/// El consumidor es idempotente: la clave primaria de la bitácora es el identificador del evento,
/// así que una reentrega —inevitable en una entrega "al menos una vez"— no duplica nada.
/// </remarks>
public sealed class ConsumidorMovimientoRegistrado(
    ClientesDbContext contexto,
    ILogger<ConsumidorMovimientoRegistrado> registro) : IConsumer<MovimientoRegistrado>
{
    public async Task Consume(ConsumeContext<MovimientoRegistrado> contextoMensaje)
    {
        var evento = contextoMensaje.Message;
        var cancelacion = contextoMensaje.CancellationToken;

        var yaRegistrado = await contexto.Actividades
            .AsNoTracking()
            .AnyAsync(a => a.Id == evento.EventoId, cancelacion);

        if (yaRegistrado)
        {
            registro.LogDebug("Evento {EventoId} ya estaba en la bitácora; se descarta.", evento.EventoId);
            return;
        }

        var signo = evento.Valor >= 0 ? "+" : string.Empty;
        var descripcion =
            $"{evento.TipoMovimiento} de {signo}{evento.Valor:0.00} en la cuenta {evento.NumeroCuenta}. " +
            $"Saldo resultante: {evento.SaldoResultante:0.00}.";

        contexto.Actividades.Add(ActividadCliente.Desde(
            evento.EventoId, evento.ClienteId, "MOVIMIENTO", descripcion, evento.Fecha));

        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation(
            "Movimiento {MovimientoId} anotado en la bitácora de {ClienteId}.", evento.MovimientoId, evento.ClienteId);
    }
}

/// <summary>Anota la apertura de cuentas en la misma bitácora.</summary>
public sealed class ConsumidorCuentaAperturada(
    ClientesDbContext contexto,
    ILogger<ConsumidorCuentaAperturada> registro) : IConsumer<CuentaAperturada>
{
    public async Task Consume(ConsumeContext<CuentaAperturada> contextoMensaje)
    {
        var evento = contextoMensaje.Message;
        var cancelacion = contextoMensaje.CancellationToken;

        if (await contexto.Actividades.AsNoTracking().AnyAsync(a => a.Id == evento.EventoId, cancelacion))
        {
            return;
        }

        contexto.Actividades.Add(ActividadCliente.Desde(
            evento.EventoId,
            evento.ClienteId,
            "APERTURA",
            $"Apertura de cuenta {evento.TipoCuenta.ToLowerInvariant()} {evento.NumeroCuenta} " +
            $"con saldo inicial de {evento.SaldoInicial:0.00}.",
            evento.OcurridoEn));

        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation("Apertura de {NumeroCuenta} anotada para {ClienteId}.",
            evento.NumeroCuenta, evento.ClienteId);
    }
}
