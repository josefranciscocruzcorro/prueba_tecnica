namespace Shared.Kernel.Dominio;

/// <summary>
/// Hecho relevante ocurrido dentro del dominio. Se acumula en la entidad y se traduce a un
/// evento de integración (bandeja de salida) cuando la unidad de trabajo confirma.
/// </summary>
public interface IEventoDominio
{
    DateTime OcurridoEn => DateTime.UtcNow;
}
