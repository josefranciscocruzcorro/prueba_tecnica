namespace Shared.Kernel.Excepciones;

/// <summary>Regla de negocio incumplida con los datos recibidos. Se traduce a HTTP 400.</summary>
public class ExcepcionReglaNegocio(string codigo, string mensaje) : ExcepcionDominio(codigo, mensaje)
{
    public override int CodigoEstado => 400;
}
