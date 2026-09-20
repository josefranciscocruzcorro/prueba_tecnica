using Cuentas.Domain.Clientes;
using Cuentas.Infrastructure.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Clientes;

namespace Cuentas.Infrastructure.Mensajeria;

/// <summary>
/// Mantiene la réplica local de clientes a partir de los eventos del microservicio de Clientes.
/// Es la única escritura sobre <see cref="ClienteReferencia"/>: ningún caso de uso la modifica.
/// </summary>
/// <remarks>
/// Los tres consumidores son idempotentes y conmutativos frente a reentregas: crear un cliente
/// que ya existe equivale a actualizarlo, y actualizar uno que aún no llegó lo crea. Así el
/// resultado final es correcto aunque los mensajes lleguen repetidos o desordenados.
/// </remarks>
public sealed class ConsumidorClienteCreado(
    CuentasDbContext contexto,
    ILogger<ConsumidorClienteCreado> registro) : IConsumer<ClienteCreado>
{
    public async Task Consume(ConsumeContext<ClienteCreado> mensaje)
    {
        var evento = mensaje.Message;

        await ReplicaClientes.GuardarAsync(
            contexto, evento.ClienteId, evento.Nombre, evento.Identificacion, evento.Estado, mensaje.CancellationToken);

        registro.LogInformation("Réplica del cliente {ClienteId} creada o actualizada.", evento.ClienteId);
    }
}

public sealed class ConsumidorClienteActualizado(
    CuentasDbContext contexto,
    ILogger<ConsumidorClienteActualizado> registro) : IConsumer<ClienteActualizado>
{
    public async Task Consume(ConsumeContext<ClienteActualizado> mensaje)
    {
        var evento = mensaje.Message;

        await ReplicaClientes.GuardarAsync(
            contexto, evento.ClienteId, evento.Nombre, evento.Identificacion, evento.Estado, mensaje.CancellationToken);

        registro.LogInformation("Réplica del cliente {ClienteId} sincronizada.", evento.ClienteId);
    }
}

/// <summary>
/// La baja del cliente no borra nada: las cuentas y sus movimientos son un registro contable. La
/// réplica se marca inactiva, lo que bloquea aperturas y movimientos pero conserva el histórico.
/// </summary>
public sealed class ConsumidorClienteEliminado(
    CuentasDbContext contexto,
    ILogger<ConsumidorClienteEliminado> registro) : IConsumer<ClienteEliminado>
{
    public async Task Consume(ConsumeContext<ClienteEliminado> mensaje)
    {
        var clienteId = mensaje.Message.ClienteId.Trim().ToUpperInvariant();

        var replica = await contexto.ClientesReplicados
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId, mensaje.CancellationToken);

        if (replica is null)
        {
            registro.LogDebug("Baja de {ClienteId} recibida sin réplica previa; nada que hacer.", clienteId);
            return;
        }

        replica.MarcarComoBaja();
        await contexto.SaveChangesAsync(mensaje.CancellationToken);

        registro.LogInformation("Cliente {ClienteId} marcado como dado de baja en la réplica local.", clienteId);
    }
}

/// <summary>Lógica común de alta/actualización de la réplica, compartida por los consumidores.</summary>
internal static class ReplicaClientes
{
    public static async Task GuardarAsync(
        CuentasDbContext contexto,
        string clienteId,
        string nombre,
        string identificacion,
        bool estado,
        CancellationToken cancelacion)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();

        var existente = await contexto.ClientesReplicados
            .FirstOrDefaultAsync(c => c.ClienteId == normalizado, cancelacion);

        if (existente is null)
        {
            contexto.ClientesReplicados.Add(ClienteReferencia.Crear(normalizado, nombre, identificacion, estado));
        }
        else
        {
            existente.Sincronizar(nombre, identificacion, estado);
        }

        await contexto.SaveChangesAsync(cancelacion);
    }
}
