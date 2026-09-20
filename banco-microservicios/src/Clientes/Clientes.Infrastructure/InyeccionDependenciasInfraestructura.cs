using Clientes.Application.Abstracciones;
using Clientes.Application.Actividad;
using Clientes.Domain.Clientes;
using Clientes.Infrastructure.Datos;
using Clientes.Infrastructure.Mensajeria;
using Clientes.Infrastructure.Persistencia;
using Clientes.Infrastructure.Seguridad;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Bandeja;
using Shared.Kernel.Persistencia;

namespace Clientes.Infrastructure;

/// <summary>
/// Composición de la capa de infraestructura: base de datos, mensajería y servicios técnicos.
/// Es el único punto del microservicio que conoce PostgreSQL y RabbitMQ.
/// </summary>
public static class InyeccionDependenciasInfraestructura
{
    public static IServiceCollection AgregarInfraestructuraClientes(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios
            .AgregarPersistencia(configuracion)
            .AgregarMensajeria(configuracion)
            .AgregarServiciosTecnicos();

        return servicios;
    }

    private static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("ClientesDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ClientesDb'.");

        servicios.AddDbContext<ClientesDbContext>(opciones => opciones
            .UseNpgsql(cadena, npgsql => npgsql
                .MigrationsHistoryTable("__historial_migraciones", ClientesDbContext.Esquema)
                // Resiliencia: reintenta los fallos transitorios de red o los arranques en frío
                // de PostgreSQL sin propagar el error al usuario.
                .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)));

        servicios.AddScoped<IRepositorioClientes, RepositorioClientes>();
        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        servicios.AddScoped<IConsultaActividad, ConsultaActividad>();
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
            bus.AddConsumer<ConsumidorMovimientoRegistrado>();
            bus.AddConsumer<ConsumidorCuentaAperturada>();

            bus.UsingRabbitMq((contexto, configurador) =>
            {
                configurador.Host(opciones.Host, opciones.PuertoVirtual, anfitrion =>
                {
                    anfitrion.Username(opciones.Usuario);
                    anfitrion.Password(opciones.Contrasena);
                });

                // Reintentos escalonados y cola de errores: un consumidor que falla de forma
                // transitoria se recupera solo; el que falla siempre acaba en _error para su análisis.
                configurador.UseMessageRetry(reintento => reintento.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                configurador.ConfigureEndpoints(contexto);
            });
        });

        servicios.AddHostedService<DespachadorBandejaSalida<ClientesDbContext>>();

        return servicios;
    }

    private static IServiceCollection AgregarServiciosTecnicos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<IServicioHashContrasena, ServicioHashContrasenaPbkdf2>();
        servicios.AddSingleton<IGeneradorTokens, GeneradorTokensJwt>();

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
