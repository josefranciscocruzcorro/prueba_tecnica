using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Clientes.Application.Abstracciones;
using Clientes.Domain.Clientes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Api.Seguridad;

namespace Clientes.Infrastructure.Seguridad;

/// <summary>
/// Emite el JWT de sesión. El token lleva el <c>cliente_id</c> como reclamación, que es lo que el
/// microservicio de Cuentas usa para resolver a qué cliente pertenece cada petición sin tener que
/// preguntarle nada a Clientes: el token es la credencial y, a la vez, el dato de correlación.
/// </summary>
public sealed class GeneradorTokensJwt(IOptions<OpcionesJwt> opciones) : IGeneradorTokens
{
    private readonly OpcionesJwt _opciones = opciones.Value;

    public (string Token, DateTime ExpiraEnUtc) Emitir(Cliente cliente)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.MinutosVigencia);

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, cliente.ClienteId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimsBanco.ClienteId, cliente.ClienteId),
                new Claim(ClaimsBanco.PersonaId, cliente.Id.ToString()),
                new Claim(ClaimsBanco.Nombre, cliente.Nombre),
            ],
            notBefore: ahora,
            expires: expira,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
