namespace Shared.Contracts.Clientes;

/// <summary>
/// Contratos de integración publicados por el microservicio de Clientes y consumidos por el de
/// Cuentas. Son inmutables y deliberadamente anémicos: solo transportan los datos que el otro
/// contexto necesita replicar, nunca el modelo de dominio completo.
/// </summary>
public interface IEventoIntegracion
{
    Guid EventoId { get; }

    DateTime OcurridoEn { get; }
}

/// <summary>Alta de un cliente. Cuentas crea con él su réplica local de solo lectura.</summary>
public sealed record ClienteCreado(
    Guid EventoId,
    DateTime OcurridoEn,
    Guid PersonaId,
    string ClienteId,
    string Nombre,
    string Identificacion,
    bool Estado) : IEventoIntegracion;

/// <summary>Cambio en los datos visibles del cliente (nombre, identificación o estado).</summary>
public sealed record ClienteActualizado(
    Guid EventoId,
    DateTime OcurridoEn,
    Guid PersonaId,
    string ClienteId,
    string Nombre,
    string Identificacion,
    bool Estado) : IEventoIntegracion;

/// <summary>
/// Baja del cliente. Cuentas no borra sus cuentas (son un registro contable histórico): marca la
/// réplica como inactiva y bloquea nuevas aperturas y movimientos.
/// </summary>
public sealed record ClienteEliminado(
    Guid EventoId,
    DateTime OcurridoEn,
    string ClienteId) : IEventoIntegracion;
