using Clientes.Infrastructure.Datos;
using Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clientes.Api.Arranque;

/// <summary>
/// Aplica las migraciones y la carga inicial al arrancar. En un despliegue con contenedores la API
/// puede ganarle la carrera a PostgreSQL, así que se reintenta con espera creciente en lugar de
/// caer: el contenedor se vuelve saludable en cuanto la base está lista.
/// </summary>
public static class PreparacionBaseDeDatos
{
    private const int MaximoIntentos = 12;
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromSeconds(5);

    public static async Task PrepararBaseDeDatosAsync(this WebApplication app)
    {
        var registro = app.Services.GetRequiredService<ILogger<Program>>();

        for (var intento = 1; intento <= MaximoIntentos; intento++)
        {
            try
            {
                using var ambito = app.Services.CreateScope();
                var contexto = ambito.ServiceProvider.GetRequiredService<ClientesDbContext>();

                await AplicarEsquemaAsync(contexto);
                await ambito.ServiceProvider.GetRequiredService<SembradorDatos>().SembrarAsync();

                registro.LogInformation("Base de datos de clientes lista.");
                return;
            }
            catch (Exception excepcion) when (EsTransitoria(excepcion) && intento < MaximoIntentos)
            {
                registro.LogWarning(
                    "La base de datos aún no acepta conexiones (intento {Intento}/{Maximo}). Reintentando en {Espera}s.",
                    intento, MaximoIntentos, EsperaEntreIntentos.TotalSeconds);

                await Task.Delay(EsperaEntreIntentos);
            }
        }

        throw new InvalidOperationException(
            "No fue posible preparar la base de datos de clientes tras varios intentos. Revise la conexión.");
    }

    /// <summary>
    /// Con PostgreSQL se aplican las migraciones, que son la fuente de verdad del esquema. Con
    /// cualquier otro proveedor —el SQLite en memoria de las pruebas de integración— se crea el
    /// esquema a partir del modelo, porque las migraciones llevan SQL específico de PostgreSQL.
    /// </summary>
    private static async Task AplicarEsquemaAsync(DbContext contexto)
    {
        if (contexto.Database.IsNpgsql())
        {
            await contexto.Database.MigrateAsync();
            return;
        }

        await contexto.Database.EnsureCreatedAsync();
    }

    private static bool EsTransitoria(Exception excepcion) =>
        excepcion is NpgsqlException or TimeoutException
        || excepcion.InnerException is NpgsqlException or TimeoutException;
}
