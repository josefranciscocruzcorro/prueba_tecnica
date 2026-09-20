using Shared.Kernel.Dominio;
using Shared.Kernel.Excepciones;

namespace Cuentas.Domain.Movimientos;

/// <summary>Naturaleza contable del movimiento, derivada del signo del valor.</summary>
public enum TipoMovimiento
{
    Deposito = 1,
    Retiro = 2,
}

/// <summary>
/// Apunte contable sobre una cuenta. Es una entidad interna del agregado Cuenta: nunca se crea ni
/// se modifica por su cuenta, siempre a través de la cuenta que lo contiene, porque su validez
/// depende del saldo acumulado de toda la serie.
/// </summary>
public sealed class Movimiento : Entidad<Guid>
{
    private Movimiento()
    {
        // Constructor requerido por Entity Framework Core.
    }

    private Movimiento(Guid id, Guid cuentaId, DateTime fecha, decimal valor, int secuencia)
    {
        Id = id;
        CuentaId = cuentaId;
        Fecha = fecha;
        Valor = valor;
        Secuencia = secuencia;
        Tipo = valor >= 0 ? TipoMovimiento.Deposito : TipoMovimiento.Retiro;
    }

    public Guid CuentaId { get; private set; }

    public DateTime Fecha { get; private set; }

    /// <summary>
    /// Importe con signo: positivo para depósitos y negativo para retiros (F2). Guardar el signo
    /// en el propio valor evita tener que interpretar el tipo para calcular: el saldo es una suma.
    /// </summary>
    public decimal Valor { get; private set; }

    public TipoMovimiento Tipo { get; private set; }

    /// <summary>Saldo de la cuenta después de aplicar este movimiento.</summary>
    public decimal SaldoDisponible { get; private set; }

    /// <summary>
    /// Desempata movimientos de la misma fecha. Sin él, dos apuntes del mismo día tendrían un
    /// orden indeterminado y el saldo acumulado variaría entre consultas.
    /// </summary>
    public int Secuencia { get; private set; }

    internal static Movimiento Crear(Guid cuentaId, DateTime fecha, decimal valor, int secuencia)
    {
        if (valor == 0)
        {
            throw new ExcepcionReglaNegocio("VALOR_INVALIDO", "El valor del movimiento no puede ser cero.");
        }

        if (fecha > DateTime.UtcNow.AddMinutes(5))
        {
            throw new ExcepcionReglaNegocio("FECHA_FUTURA", "No se pueden registrar movimientos con fecha futura.");
        }

        return new Movimiento(Guid.NewGuid(), cuentaId, fecha, decimal.Round(valor, 2), secuencia);
    }

    internal void Reexpresar(DateTime fecha, decimal valor)
    {
        if (valor == 0)
        {
            throw new ExcepcionReglaNegocio("VALOR_INVALIDO", "El valor del movimiento no puede ser cero.");
        }

        Fecha = fecha;
        Valor = decimal.Round(valor, 2);
        Tipo = Valor >= 0 ? TipoMovimiento.Deposito : TipoMovimiento.Retiro;
    }

    internal void EstablecerSaldo(decimal saldo) => SaldoDisponible = saldo;
}
