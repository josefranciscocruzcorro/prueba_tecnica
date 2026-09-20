namespace Shared.Infrastructure.Bandeja;

/// <summary>
/// Fila de la bandeja de salida (patrón Outbox). El evento de integración se guarda en la misma
/// transacción que el cambio de negocio que lo produjo, de modo que es imposible que uno se
/// confirme sin el otro. Un proceso en segundo plano lo publica después contra RabbitMQ.
/// </summary>
/// <remarks>
/// Esto es lo que hace fiable la comunicación asíncrona: si el broker está caído cuando se crea el
/// cliente, la operación HTTP termina bien igualmente y el mensaje se entrega cuando el broker
/// vuelve. La entrega es "al menos una vez", por lo que los consumidores son idempotentes.
/// </remarks>
public sealed class MensajeSalida
{
    private MensajeSalida()
    {
        // Constructor requerido por Entity Framework Core.
    }

    public MensajeSalida(string tipo, string contenido)
    {
        Id = Guid.NewGuid();
        Tipo = tipo;
        Contenido = contenido;
        CreadoEn = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Nombre completo del tipo del contrato, usado para rehidratarlo al publicar.</summary>
    public string Tipo { get; private set; } = string.Empty;

    /// <summary>Carga útil serializada como JSON.</summary>
    public string Contenido { get; private set; } = string.Empty;

    public DateTime CreadoEn { get; private set; }

    public DateTime? PublicadoEn { get; private set; }

    public int Intentos { get; private set; }

    public string? UltimoError { get; private set; }

    public void MarcarPublicado()
    {
        PublicadoEn = DateTime.UtcNow;
        UltimoError = null;
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;

        // Se acota el texto para que un error muy largo no haga crecer la tabla sin control.
        UltimoError = error.Length > 500 ? error[..500] : error;
    }
}
