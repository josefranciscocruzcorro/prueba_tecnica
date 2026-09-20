namespace Shared.Kernel.Excepciones;

/// <summary>El estado actual del recurso impide la operación (duplicados, invariantes). HTTP 409.</summary>
public sealed class ExcepcionConflicto(string codigo, string mensaje) : ExcepcionDominio(codigo, mensaje)
{
    public override int CodigoEstado => 409;
}
