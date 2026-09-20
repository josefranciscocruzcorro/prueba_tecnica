using System.Text.Json.Serialization;

namespace Shared.Api.Errores;

/// <summary>
/// Cuerpo único de error de toda la plataforma, compatible con <c>application/problem+json</c>
/// (RFC 7807) y enriquecido con un <see cref="Codigo"/> estable que el cliente puede evaluar sin
/// depender del texto, y un <see cref="TraceId"/> para correlacionar con los registros.
/// </summary>
public sealed class RespuestaError
{
    [JsonPropertyName("type")]
    public string Tipo { get; init; } = "about:blank";

    [JsonPropertyName("title")]
    public string Titulo { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public int Estado { get; init; }

    [JsonPropertyName("detail")]
    public string Detalle { get; init; } = string.Empty;

    [JsonPropertyName("instance")]
    public string? Instancia { get; init; }

    [JsonPropertyName("codigo")]
    public string Codigo { get; init; } = "ERROR_INTERNO";

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("errores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errores { get; init; }
}
