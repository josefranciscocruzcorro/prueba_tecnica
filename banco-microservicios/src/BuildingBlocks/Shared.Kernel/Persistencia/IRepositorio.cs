using Shared.Kernel.Dominio;

namespace Shared.Kernel.Persistencia;

/// <summary>
/// Contrato genérico del patrón Repository. Vive en el núcleo compartido para que las capas de
/// dominio y aplicación puedan depender de él sin conocer Entity Framework Core.
/// </summary>
public interface IRepositorio<TEntidad, TId>
    where TEntidad : Entidad<TId>
    where TId : notnull
{
    Task<TEntidad?> ObtenerPorIdAsync(TId id, CancellationToken cancelacion = default);

    Task<IReadOnlyList<TEntidad>> ListarAsync(CancellationToken cancelacion = default);

    Task AgregarAsync(TEntidad entidad, CancellationToken cancelacion = default);

    void Actualizar(TEntidad entidad);

    void Eliminar(TEntidad entidad);
}
