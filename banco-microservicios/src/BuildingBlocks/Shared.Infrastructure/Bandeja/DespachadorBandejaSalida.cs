using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Clientes;

namespace Shared.Infrastructure.Bandeja;

/// <summary>
/// Proceso en segundo plano que publica en el bus los mensajes pendientes de la bandeja de salida.
/// Es la mitad asíncrona del patrón Outbox: desacopla la petición HTTP del estado del broker, de
/// modo que una caída de RabbitMQ degrada la latencia de propagación pero nunca provoca un error
/// al usuario ni pierde eventos.
/// </summary>
/// <remarks>
/// El bloqueo pesimista (<c>FOR UPDATE SKIP LOCKED</c>) permite ejecutar varias réplicas del
/// servicio sin que dos publiquen el mismo mensaje: cada una toma un lote distinto. Como la
/// entrega es "al menos una vez", los consumidores son idempotentes por diseño.
/// </remarks>
public sealed class DespachadorBandejaSalida<TContexto>(
    IServiceScopeFactory fabricaAmbitos,
    ILogger<DespachadorBandejaSalida<TContexto>> registro) : BackgroundService
    where TContexto : ContextoConBandejaSalida
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EsperaTrasFallo = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
    private const int TamanoLote = 50;
    private const int MaximoIntentos = 10;

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        registro.LogInformation("Despachador de la bandeja de salida iniciado.");

        using var temporizador = new PeriodicTimer(Intervalo);

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                await ProcesarLoteAsync(cancelacion);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excepcion)
            {
                // Nunca dejamos morir el bucle: se registra el fallo, se espera y se reintenta.
                registro.LogError(excepcion, "Fallo al procesar la bandeja de salida. Se reintentará.");

                try
                {
                    await Task.Delay(EsperaTrasFallo, cancelacion);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                await temporizador.WaitForNextTickAsync(cancelacion);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        registro.LogInformation("Despachador de la bandeja de salida detenido.");
    }

    private async Task ProcesarLoteAsync(CancellationToken cancelacion)
    {
        using var ambito = fabricaAmbitos.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<TContexto>();
        var publicador = ambito.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // La estrategia de reintentos de EF Core exige que las transacciones explícitas se
        // ejecuten dentro de ella para poder repetirlas completas ante un fallo transitorio.
        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

            // El nombre del esquema procede de una constante del propio servicio, nunca de una
            // entrada externa, y aun así se valida antes de interpolarlo. Los demás valores son
            // constantes enteras, por lo que no hay superficie de inyección: por eso se silencia
            // EF1002 en este punto concreto en lugar de en todo el proyecto.
            var consulta =
                $"""
                 SELECT * FROM {IdentificadorSeguro(contexto.EsquemaBandeja)}.bandeja_salida
                 WHERE publicado_en IS NULL AND intentos < {MaximoIntentos}
                 ORDER BY creado_en
                 LIMIT {TamanoLote}
                 FOR UPDATE SKIP LOCKED
                 """;

#pragma warning disable EF1002 // SQL construido solo con identificadores validados y constantes.
            var pendientes = await contexto.BandejaSalida.FromSqlRaw(consulta).ToListAsync(cancelacion);
#pragma warning restore EF1002

            if (pendientes.Count == 0)
            {
                await transaccion.RollbackAsync(cancelacion);
                return;
            }

            foreach (var mensaje in pendientes)
            {
                await PublicarAsync(publicador, mensaje, cancelacion);
            }

            await contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        });
    }

    private async Task PublicarAsync(IPublishEndpoint publicador, MensajeSalida mensaje, CancellationToken cancelacion)
    {
        try
        {
            var tipo = ResolverTipo(mensaje.Tipo);
            var evento = JsonSerializer.Deserialize(mensaje.Contenido, tipo, OpcionesJson)
                ?? throw new InvalidOperationException("El contenido del mensaje quedó vacío al deserializar.");

            await publicador.Publish(evento, tipo, cancelacion);
            mensaje.MarcarPublicado();

            registro.LogInformation("Evento {Tipo} publicado (mensaje {MensajeId}).", tipo.Name, mensaje.Id);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            mensaje.RegistrarFallo(excepcion.Message);
            registro.LogWarning(
                excepcion, "No se pudo publicar el mensaje {MensajeId} (intento {Intentos}).",
                mensaje.Id, mensaje.Intentos);
        }
    }

    /// <summary>
    /// Comprueba que el esquema es un identificador SQL simple y lo entrecomilla. Si algún día
    /// alguien intentase inyectar algo por esta vía, el arranque fallaría de inmediato.
    /// </summary>
    private static string IdentificadorSeguro(string esquema)
    {
        var valido = esquema.Length is > 0 and <= 63
            && (char.IsLetter(esquema[0]) || esquema[0] == '_')
            && esquema.All(c => char.IsLetterOrDigit(c) || c == '_');

        return valido
            ? $"\"{esquema}\""
            : throw new InvalidOperationException($"El esquema '{esquema}' no es un identificador SQL válido.");
    }

    /// <summary>Todos los contratos viven en el mismo ensamblado compartido, así que basta con él.</summary>
    private static Type ResolverTipo(string nombreCompleto) =>
        typeof(ClienteCreado).Assembly.GetType(nombreCompleto, throwOnError: false)
        ?? throw new InvalidOperationException($"No se pudo resolver el contrato '{nombreCompleto}'.");
}
