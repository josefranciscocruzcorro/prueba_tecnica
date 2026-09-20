using Cuentas.Domain.Cuentas;
using Cuentas.Infrastructure.Datos;
using Cuentas.Infrastructure.Mensajeria;
using Cuentas.Infrastructure.Persistencia;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Bandeja;
using Shared.Kernel.Persistencia;

namespace Cuentas.Infrastructure;

/// <summary>Composición de la infraestructura del microservicio de Cuentas.</summary>
public static class InyeccionDependenciasInfraestructura
{
    public static IServiceCollection AgregarInfraestructuraCuentas(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios
            .AgregarPersistencia(configuracion)
            .AgregarMensajeria(configuracion);

        return servicios;
    }

    private static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("CuentasDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'CuentasDb'.");

        servicios.AddDbContext<CuentasDbContext>(opciones => opciones
            .UseNpgsql(cadena, npgsql => npgsql
                .MigrationsHistoryTable("__historial_migraciones", CuentasDbContext.Esquema)
                .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)));

        servicios.AddScoped<IRepositorioCuentas, RepositorioCuentas>();
        servicios.AddScoped<IRepositorioMovimientos, RepositorioMovimientos>();
        servicios.AddScoped<IRepositorioClientesReplicados, RepositorioClientesReplicados>();
        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        servicios.AddScoped<SembradorDatos>();

        return servicios;
    }

    private static IServiceCollection AgregarMensajeria(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection(OpcionesBus.Seccion).Get<OpcionesBus>() ?? new OpcionesBus();

        servicios.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.AddConsumer<ConsumidorClienteCreado>();
            bus.AddConsumer<ConsumidorClienteActualizado>();
            bus.AddConsumer<ConsumidorClienteEliminado>();

            bus.UsingRabbitMq((contexto, configurador) =>
            {
                configurador.Host(opciones.Host, opciones.PuertoVirtual, anfitrion =>
                {
                    anfitrion.Username(opciones.Usuario);
                    anfitrion.Password(opciones.Contrasena);
                });

                configurador.UseMessageRetry(reintento => reintento.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                configurador.ConfigureEndpoints(contexto);
            });
        });

        servicios.AddHostedService<DespachadorBandejaSalida<CuentasDbContext>>();

        return servicios;
    }
}

/// <summary>Conexión al broker de mensajería.</summary>
public sealed class OpcionesBus
{
    public const string Seccion = "Bus";

    public string Host { get; init; } = "localhost";

    public string PuertoVirtual { get; init; } = "/";

    public string Usuario { get; init; } = "guest";

    public string Contrasena { get; init; } = "guest";
}
