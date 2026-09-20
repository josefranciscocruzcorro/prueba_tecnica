namespace Clientes.Application.Actividad;

/// <summary>Entrada de la bitácora tal como la consume la interfaz.</summary>
public sealed record ActividadDto(Guid EventoId, string Tipo, string Descripcion, DateTime OcurridoEn);

/// <summary>
/// Lado de lectura de la bitácora. Se separa del repositorio del agregado porque no necesita
/// rastreo de cambios ni reglas de negocio: es una proyección de solo lectura y puede optimizarse
/// (o moverse a otra base) sin afectar al dominio.
/// </summary>
public interface IConsultaActividad
{
    Task<IReadOnlyList<ActividadDto>> ObtenerUltimasAsync(
        string clienteId, int limite, CancellationToken cancelacion = default);
}
