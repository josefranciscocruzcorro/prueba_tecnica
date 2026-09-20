using Cuentas.Domain.Cuentas;
using Cuentas.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cuentas.Infrastructure.Datos;

/// <summary>
/// Carga las cuentas y movimientos de los casos de uso del enunciado. Las fechas son las del
/// propio enunciado, de modo que el reporte F4 sobre febrero de 2022 reproduce exactamente el
/// ejemplo publicado y puede compararse línea a línea.
/// </summary>
public sealed class SembradorDatos(
    CuentasDbContext contexto,
    ILogger<SembradorDatos> registro)
{
    /// <summary>Cuentas de los casos 2 y 3 del enunciado.</summary>
    private static readonly (string Numero, TipoCuenta Tipo, decimal SaldoInicial, string ClienteId)[] CuentasSemilla =
    [
        ("478758", TipoCuenta.Ahorros, 2000m, "CLI-001"),
        ("225487", TipoCuenta.Corriente, 100m, "CLI-002"),
        ("495878", TipoCuenta.Ahorros, 0m, "CLI-003"),
        ("496825", TipoCuenta.Ahorros, 540m, "CLI-002"),
        ("585545", TipoCuenta.Corriente, 1000m, "CLI-001"),
    ];

    /// <summary>Movimientos del caso 4, fechados según el listado del caso 5.</summary>
    private static readonly (string Numero, decimal Valor, DateTime Fecha)[] MovimientosSemilla =
    [
        ("496825", -540m, new DateTime(2022, 2, 8, 10, 0, 0, DateTimeKind.Utc)),
        ("478758", -575m, new DateTime(2022, 2, 9, 11, 30, 0, DateTimeKind.Utc)),
        ("225487", 600m, new DateTime(2022, 2, 10, 9, 15, 0, DateTimeKind.Utc)),
        ("495878", 150m, new DateTime(2022, 2, 11, 16, 45, 0, DateTimeKind.Utc)),
    ];

    /// <summary>Identificadores de los clientes que deben estar replicados antes de sembrar.</summary>
    public static IReadOnlyList<string> ClientesRequeridos { get; } = ["CLI-001", "CLI-002", "CLI-003"];

    /// <summary>Indica si la réplica ya recibió todos los clientes que la semilla necesita.</summary>
    public async Task<bool> ReplicaListaAsync(CancellationToken cancelacion = default)
    {
        var presentes = await contexto.ClientesReplicados
            .AsNoTracking()
            .CountAsync(c => ClientesRequeridos.Contains(c.ClienteId), cancelacion);

        return presentes == ClientesRequeridos.Count;
    }

    public async Task<bool> YaSembradoAsync(CancellationToken cancelacion = default) =>
        await contexto.Cuentas.AnyAsync(cancelacion);

    public async Task SembrarAsync(CancellationToken cancelacion = default)
    {
        if (await YaSembradoAsync(cancelacion))
        {
            registro.LogInformation("La base ya contiene cuentas; se omite la carga inicial.");
            return;
        }

        var cuentas = new Dictionary<string, Cuenta>(StringComparer.Ordinal);

        foreach (var fila in CuentasSemilla)
        {
            var cuenta = Cuenta.Aperturar(fila.Numero, fila.Tipo, fila.SaldoInicial, fila.ClienteId);
            cuentas[fila.Numero] = cuenta;
            contexto.Cuentas.Add(cuenta);
        }

        foreach (var fila in MovimientosSemilla)
        {
            cuentas[fila.Numero].RegistrarMovimiento(fila.Valor, fila.Fecha);
        }

        // Una sola confirmación: cuentas, movimientos y los eventos que anuncian ambos hechos
        // viajan juntos a la bandeja de salida.
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation(
            "Carga inicial completada: {Cuentas} cuentas y {Movimientos} movimientos.",
            CuentasSemilla.Length, MovimientosSemilla.Length);
    }
}

/// <summary>
/// Espera a que lleguen por el bus los clientes replicados y entonces siembra las cuentas. No se
/// hace en el arranque porque la réplica es asíncrona: bloquear el inicio acoplaría este servicio
/// al de Clientes, que es justo lo que la arquitectura pretende evitar.
/// </summary>
public sealed class SembradorEnSegundoPlano(
    IServiceScopeFactory fabricaAmbitos,
    ILogger<SembradorEnSegundoPlano> registro) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        var expira = DateTime.UtcNow + Limite;
        using var temporizador = new PeriodicTimer(Intervalo);

        while (!cancelacion.IsCancellationRequested && DateTime.UtcNow < expira)
        {
            try
            {
                using var ambito = fabricaAmbitos.CreateScope();
                var sembrador = ambito.ServiceProvider.GetRequiredService<SembradorDatos>();

                if (await sembrador.YaSembradoAsync(cancelacion))
                {
                    return;
                }

                if (await sembrador.ReplicaListaAsync(cancelacion))
                {
                    await sembrador.SembrarAsync(cancelacion);
                    return;
                }

                registro.LogDebug("Aún faltan clientes replicados para la carga inicial; se reintenta.");
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excepcion)
            {
                registro.LogWarning(excepcion, "No se pudo completar la carga inicial. Se reintentará.");
            }

            try
            {
                await temporizador.WaitForNextTickAsync(cancelacion);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        registro.LogInformation("Se agotó la espera de la carga inicial de cuentas. El servicio opera con normalidad.");
    }
}
