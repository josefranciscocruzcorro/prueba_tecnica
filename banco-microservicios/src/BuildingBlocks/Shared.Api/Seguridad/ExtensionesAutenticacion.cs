using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Api.Seguridad;

/// <summary>Registro común de autenticación por portador (JWT) para ambos microservicios.</summary>
public static class ExtensionesAutenticacion
{
    public const string PoliticaClienteActivo = "ClienteActivo";

    public static IServiceCollection AgregarAutenticacionJwt(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddOptions<OpcionesJwt>()
            .Bind(configuracion.GetSection(OpcionesJwt.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opciones = configuracion.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()
            ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = opciones.Emisor,
                    ValidAudience = opciones.Audiencia,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Clave)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        servicios.AddAuthorizationBuilder()
            .AddPolicy(PoliticaClienteActivo, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimsBanco.ClienteId));

        return servicios;
    }
}

/// <summary>Nombres de las reclamaciones propias que viajan en el token.</summary>
public static class ClaimsBanco
{
    public const string ClienteId = "cliente_id";
    public const string Nombre = "nombre";
    public const string PersonaId = "persona_id";
}
