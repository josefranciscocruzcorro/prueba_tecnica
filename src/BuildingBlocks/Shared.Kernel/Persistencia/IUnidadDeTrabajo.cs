namespace Shared.Kernel.Persistencia;

/// <summary>
/// Frontera transaccional explícita. Los casos de uso deciden cuándo confirmar; los repositorios
/// solo acumulan cambios. Confirmar también vuelca los eventos de dominio a la bandeja de salida.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task<int> ConfirmarAsync(CancellationToken cancelacion = default);
}
