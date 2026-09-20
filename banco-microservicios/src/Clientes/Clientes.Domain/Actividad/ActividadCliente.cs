using Shared.Kernel.Dominio;

namespace Clientes.Domain.Actividad;

/// <summary>
/// Bitácora de lo que le ocurre a un cliente en otros contextos. La alimenta el consumidor de
/// eventos de Cuentas, y es la prueba visible de que la comunicación asíncrona funciona en ambos
/// sentidos: Clientes conoce la actividad transaccional sin consultar nunca a Cuentas.
/// </summary>
public sealed class ActividadCliente : Entidad<Guid>
{
    private ActividadCliente()
    {
        // Constructor requerido por Entity Framework Core.
    }

    private ActividadCliente(Guid id, string clienteId, string tipo, string descripcion, DateTime ocurridoEn)
    {
        Id = id;
        ClienteId = clienteId;
        Tipo = tipo;
        Descripcion = descripcion;
        OcurridoEn = ocurridoEn;
    }

    public string ClienteId { get; private set; } = string.Empty;

    public string Tipo { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public DateTime OcurridoEn { get; private set; }

    /// <summary>
    /// Crea la entrada usando el identificador del evento de integración como clave primaria. Al
    /// ser determinista, reprocesar el mismo evento no duplica la bitácora (idempotencia).
    /// </summary>
    public static ActividadCliente Desde(
        Guid eventoId, string clienteId, string tipo, string descripcion, DateTime ocurridoEn) =>
        new(eventoId, clienteId, tipo, descripcion, ocurridoEn);
}
