namespace Shared.Kernel.Dominio;

/// <summary>
/// Expone el buzón de eventos de una entidad sin necesidad de conocer el tipo de su clave. Lo usa
/// la infraestructura para recorrer las entidades rastreadas de forma uniforme.
/// </summary>
public interface IPortadorEventos
{
    IReadOnlyCollection<IEventoDominio> EventosDominio { get; }

    void LimpiarEventos();
}

/// <summary>
/// Raíz común de todas las entidades del dominio. Aporta identidad por valor de clave
/// (dos entidades son la misma si comparten tipo e identificador) y el buzón de eventos
/// de dominio que la capa de infraestructura publica al confirmar la transacción.
/// </summary>
public abstract class Entidad<TId> : IPortadorEventos
    where TId : notnull
{
    private readonly List<IEventoDominio> _eventos = [];

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<IEventoDominio> EventosDominio => _eventos.AsReadOnly();

    protected void RegistrarEvento(IEventoDominio evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();

    public override bool Equals(object? obj) =>
        obj is Entidad<TId> otra && otra.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(otra.Id, Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
