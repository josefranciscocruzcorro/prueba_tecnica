using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Excepciones;

namespace Shared.Api.Errores;

/// <summary>
/// Punto único de traducción de excepciones a respuestas HTTP. Al centralizarlo aquí los casos de
/// uso lanzan excepciones de dominio expresivas y ningún controlador necesita bloques try/catch.
/// Solo los fallos no previstos se registran como error; el resto son respuestas de negocio.
/// </summary>
public sealed class MiddlewareManejoExcepciones(
    RequestDelegate siguiente,
    ILogger<MiddlewareManejoExcepciones> registro)
{
    /// <summary>499 "Client Closed Request": no está en <see cref="StatusCodes"/> pero es el estándar de facto.</summary>
    private const int CodigoClienteCerroConexion = 499;

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception excepcion)
        {
            await EscribirRespuestaAsync(contexto, excepcion);
        }
    }

    private async Task EscribirRespuestaAsync(HttpContext contexto, Exception excepcion)
    {
        if (contexto.Response.HasStarted)
        {
            registro.LogError(excepcion, "La respuesta ya había comenzado; no se puede formatear el error.");
            return;
        }

        var traceId = Activity.Current?.Id ?? contexto.TraceIdentifier;
        var respuesta = Traducir(excepcion, contexto, traceId);

        if (respuesta.Estado >= StatusCodes.Status500InternalServerError)
        {
            registro.LogError(excepcion, "Fallo no controlado en {Metodo} {Ruta}. TraceId={TraceId}",
                contexto.Request.Method, contexto.Request.Path, traceId);
        }
        else
        {
            registro.LogWarning("Solicitud rechazada ({Codigo}) en {Metodo} {Ruta}: {Detalle}",
                respuesta.Codigo, contexto.Request.Method, contexto.Request.Path, respuesta.Detalle);
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = respuesta.Estado;
        contexto.Response.ContentType = "application/problem+json; charset=utf-8";
        await contexto.Response.WriteAsync(JsonSerializer.Serialize(respuesta, OpcionesJson));
    }

    private static RespuestaError Traducir(Exception excepcion, HttpContext contexto, string traceId) => excepcion switch
    {
        ValidationException validacion => new RespuestaError
        {
            Tipo = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Titulo = "Los datos enviados no son válidos",
            Estado = StatusCodes.Status400BadRequest,
            Detalle = "Revise los campos indicados en 'errores'.",
            Codigo = "VALIDACION_FALLIDA",
            Instancia = contexto.Request.Path,
            TraceId = traceId,
            Errores = validacion.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()),
        },
        ExcepcionDominio dominio => new RespuestaError
        {
            Tipo = $"https://httpstatuses.io/{dominio.CodigoEstado}",
            Titulo = dominio.Message,
            Estado = dominio.CodigoEstado,
            Detalle = dominio.Message,
            Codigo = dominio.Codigo,
            Instancia = contexto.Request.Path,
            TraceId = traceId,
        },
        OperationCanceledException => new RespuestaError
        {
            Titulo = "La solicitud fue cancelada por el cliente",
            Estado = CodigoClienteCerroConexion,
            Detalle = "La conexión se cerró antes de completar la operación.",
            Codigo = "SOLICITUD_CANCELADA",
            Instancia = contexto.Request.Path,
            TraceId = traceId,
        },
        _ => new RespuestaError
        {
            Tipo = "https://httpstatuses.io/500",
            Titulo = "Error interno del servidor",
            Estado = StatusCodes.Status500InternalServerError,
            Detalle = "Ocurrió un error inesperado. Use el traceId para consultar el detalle en los registros.",
            Codigo = "ERROR_INTERNO",
            Instancia = contexto.Request.Path,
            TraceId = traceId,
        },
    };
}
