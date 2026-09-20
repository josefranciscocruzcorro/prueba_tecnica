using Clientes.Domain.Clientes;

namespace Clientes.Application.Abstracciones;

/// <summary>Emite el token de sesión con el que el cliente opera contra ambos microservicios.</summary>
public interface IGeneradorTokens
{
    (string Token, DateTime ExpiraEnUtc) Emitir(Cliente cliente);
}
