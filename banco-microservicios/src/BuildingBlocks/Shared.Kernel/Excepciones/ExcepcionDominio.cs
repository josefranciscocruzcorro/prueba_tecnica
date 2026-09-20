namespace Shared.Kernel.Excepciones;

/// <summary>
/// Excepción base para toda violación de una regla de negocio. La capa de API la traduce a una
/// respuesta <c>ProblemDetails</c> con el código HTTP que declara cada especialización.
/// </summary>
public abstract class ExcepcionDominio : Exception
{
    protected ExcepcionDominio(string codigo, string mensaje)
        : base(mensaje) => Codigo = codigo;

    /// <summary>Código estable y legible por máquina, p. ej. <c>SALDO_NO_DISPONIBLE</c>.</summary>
    public string Codigo { get; }

    /// <summary>Código HTTP con el que la API responde ante esta excepción.</summary>
    public abstract int CodigoEstado { get; }
}
