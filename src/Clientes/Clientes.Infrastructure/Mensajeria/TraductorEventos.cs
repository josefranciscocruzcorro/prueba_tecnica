using Clientes.Domain.Eventos;
using Shared.Contracts.Clientes;
using Shared.Kernel.Dominio;

namespace Clientes.Infrastructure.Mensajeria;

/// <summary>
/// Frontera entre el lenguaje interno del dominio y el contrato público que viaja por el bus.
/// Gracias a esta traducción el dominio puede evolucionar sin romper a los suscriptores: solo se
/// rompe el contrato si se cambia deliberadamente aquí.
/// </summary>
public static class TraductorEventos
{
    /// <summary>Devuelve el evento de integración equivalente, o <c>null</c> si el hecho es interno.</summary>
    public static IEventoIntegracion? ATipoIntegracion(IEventoDominio evento) => evento switch
    {
        ClienteRegistrado e => new ClienteCreado(
            Guid.NewGuid(), DateTime.UtcNow, e.PersonaId, e.ClienteId, e.Nombre, e.Identificacion, e.Estado),

        ClienteModificado e => new ClienteActualizado(
            Guid.NewGuid(), DateTime.UtcNow, e.PersonaId, e.ClienteId, e.Nombre, e.Identificacion, e.Estado),

        ClienteDadoDeBaja e => new ClienteEliminado(Guid.NewGuid(), DateTime.UtcNow, e.ClienteId),

        _ => null,
    };
}
