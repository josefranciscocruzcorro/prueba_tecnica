namespace Shared.Kernel.Excepciones;

/// <summary>El recurso solicitado no existe. Se traduce a HTTP 404.</summary>
public sealed class ExcepcionNoEncontrado(string recurso, object clave)
    : ExcepcionDominio("RECURSO_NO_ENCONTRADO", $"No se encontró {recurso} con identificador '{clave}'.")
{
    public override int CodigoEstado => 404;
}
