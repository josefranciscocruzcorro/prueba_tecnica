using Clientes.Domain.Clientes;

namespace Clientes.Application.Clientes.Dtos;

/// <summary>
/// Proyección de lectura del cliente. Deliberadamente omite el hash de la contraseña: el dato
/// sensible no puede filtrarse ni siquiera por error al serializar la entidad.
/// </summary>
public sealed record ClienteDto(
    Guid PersonaId,
    string ClienteId,
    string Nombre,
    string Genero,
    int Edad,
    string Identificacion,
    string Direccion,
    string Telefono,
    bool Estado,
    DateTime CreadoEn,
    DateTime ActualizadoEn)
{
    public static ClienteDto Desde(Cliente cliente) => new(
        cliente.Id,
        cliente.ClienteId,
        cliente.Nombre,
        cliente.Genero.ToString(),
        cliente.Edad,
        cliente.Identificacion,
        cliente.Direccion,
        cliente.Telefono,
        cliente.Estado,
        cliente.CreadoEn,
        cliente.ActualizadoEn);
}
