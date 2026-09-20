using Shared.Kernel.Dominio;

namespace Clientes.Domain.Eventos;

/// <summary>Un cliente fue dado de alta.</summary>
public sealed record ClienteRegistrado(Guid PersonaId, string ClienteId, string Nombre, string Identificacion, bool Estado)
    : IEventoDominio;

/// <summary>Cambiaron datos del cliente que otros contextos replican.</summary>
public sealed record ClienteModificado(Guid PersonaId, string ClienteId, string Nombre, string Identificacion, bool Estado)
    : IEventoDominio;

/// <summary>El cliente fue eliminado del maestro.</summary>
public sealed record ClienteDadoDeBaja(string ClienteId) : IEventoDominio;
