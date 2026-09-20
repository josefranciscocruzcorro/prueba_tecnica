namespace Shared.Kernel.Excepciones;

/// <summary>
/// F3: intento de débito sobre una cuenta sin fondos suficientes. El mensaje es exactamente el
/// exigido por el enunciado ("Saldo no disponible") para que el consumidor lo muestre tal cual.
/// </summary>
public sealed class ExcepcionSaldoNoDisponible(decimal saldoActual, decimal valorSolicitado)
    : ExcepcionDominio("SALDO_NO_DISPONIBLE", MensajeCanonico)
{
    public const string MensajeCanonico = "Saldo no disponible";

    public override int CodigoEstado => 400;

    public decimal SaldoActual { get; } = saldoActual;

    public decimal ValorSolicitado { get; } = valorSolicitado;
}
