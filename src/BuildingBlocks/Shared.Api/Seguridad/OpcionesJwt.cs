using System.ComponentModel.DataAnnotations;

namespace Shared.Api.Seguridad;

/// <summary>
/// Parámetros del token de sesión. Ambos microservicios comparten emisor y clave para que un
/// cliente autenticado en Clientes pueda operar contra Cuentas sin un segundo inicio de sesión.
/// </summary>
public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    [Required]
    [MinLength(32, ErrorMessage = "La clave de firma debe tener al menos 32 caracteres.")]
    public string Clave { get; init; } = string.Empty;

    [Required]
    public string Emisor { get; init; } = "banco.clientes";

    [Required]
    public string Audiencia { get; init; } = "banco.api";

    [Range(1, 1440)]
    public int MinutosVigencia { get; init; } = 60;
}
