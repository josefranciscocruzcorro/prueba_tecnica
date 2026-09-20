using Clientes.Domain.Personas;

namespace Clientes.Application.Clientes.Dtos;

/// <summary>Alta de cliente. Si <paramref name="ClienteId"/> se omite, el sistema genera uno único.</summary>
public sealed record CrearClienteSolicitud(
    string Nombre,
    Genero Genero,
    int Edad,
    string Identificacion,
    string Direccion,
    string Telefono,
    string Contrasena,
    string? ClienteId = null,
    bool Estado = true);

/// <summary>Reemplazo completo de los datos del cliente (PUT). La contraseña es opcional.</summary>
public sealed record ActualizarClienteSolicitud(
    string Nombre,
    Genero Genero,
    int Edad,
    string Identificacion,
    string Direccion,
    string Telefono,
    bool Estado,
    string? Contrasena = null);

/// <summary>Modificación parcial (PATCH). Solo se aplican los campos presentes.</summary>
public sealed record ParchearClienteSolicitud(
    string? Nombre = null,
    Genero? Genero = null,
    int? Edad = null,
    string? Identificacion = null,
    string? Direccion = null,
    string? Telefono = null,
    bool? Estado = null,
    string? Contrasena = null);

/// <summary>Credenciales de inicio de sesión.</summary>
public sealed record CredencialesSolicitud(string ClienteId, string Contrasena);

/// <summary>Sesión emitida tras autenticar correctamente.</summary>
public sealed record SesionDto(string Token, DateTime ExpiraEnUtc, ClienteDto Cliente);
