using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api.Errores;
using Shared.Api.Filtros;
using Shared.Api.Seguridad;

namespace Shared.Api.Configuracion;

/// <summary>
/// Arranque común de los dos microservicios. Centralizarlo garantiza que ambos hablen exactamente
/// el mismo dialecto HTTP: mismo formato de error, mismo JSON, misma autenticación y misma
/// documentación. Añadir un tercer servicio costaría tres líneas.
/// </summary>
public static class ExtensionesApiBase
{
    public const string PoliticaCors = "InterfazWeb";

    public static IServiceCollection AgregarApiBase(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        string titulo,
        string descripcion)
    {
        servicios
            .AddControllers(opciones =>
            {
                opciones.Filters.Add<FiltroValidacion>();
                opciones.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(json =>
            {
                json.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Los errores de enlace de modelo (JSON mal formado, tipo incorrecto) se devuelven con el
        // mismo contrato que el resto de errores; el consumidor solo necesita entender un formato.
        servicios.Configure<ApiBehaviorOptions>(opciones =>
            opciones.InvalidModelStateResponseFactory = contexto => new ObjectResult(new RespuestaError
            {
                Tipo = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Titulo = "Los datos enviados no son válidos",
                Estado = StatusCodes.Status400BadRequest,
                Detalle = "Revise los campos indicados en 'errores'.",
                Codigo = "VALIDACION_FALLIDA",
                Instancia = contexto.HttpContext.Request.Path,
                TraceId = contexto.HttpContext.TraceIdentifier,
                Errores = contexto.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        e => e.Key,
                        e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()),
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            });

        servicios.AgregarDocumentacionApi(titulo, descripcion);
        servicios.AgregarAutenticacionJwt(configuracion);

        var origenes = configuracion.GetSection("Cors:Origenes").Get<string[]>() ?? [];

        servicios.AddCors(cors => cors.AddPolicy(PoliticaCors, politica =>
        {
            if (origenes.Length == 0)
            {
                // Sin lista blanca configurada solo se sirve el mismo origen (la interfaz va tras
                // el proxy inverso), que es la postura más restrictiva y la de producción.
                politica.WithOrigins("https://localhost").AllowAnyHeader().AllowAnyMethod();
                return;
            }

            politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));

        return servicios;
    }

    /// <summary>Canalización HTTP común, en el orden en que debe ejecutarse.</summary>
    public static WebApplication UsarApiBase(this WebApplication app, string titulo)
    {
        // Primero de todo: cualquier excepción aguas abajo se convierte aquí en una respuesta limpia.
        app.UseMiddleware<MiddlewareManejoExcepciones>();

        app.UseSwagger();
        app.UseSwaggerUI(opciones =>
        {
            opciones.SwaggerEndpoint("/swagger/v1/swagger.json", $"{titulo} v1");
            opciones.DocumentTitle = titulo;
            opciones.RoutePrefix = "swagger";
            opciones.DisplayRequestDuration();
        });

        app.UseCors(PoliticaCors);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
