using Cuentas.Domain.Eventos;
using Cuentas.Domain.Movimientos;
using Shared.Kernel.Dominio;
using Shared.Kernel.Excepciones;

namespace Cuentas.Domain.Cuentas;

/// <summary>Modalidad de la cuenta.</summary>
public enum TipoCuenta
{
    Ahorros = 1,
    Corriente = 2,
}

/// <summary>
/// Raíz del agregado contable. La cuenta es la dueña de sus movimientos: ningún apunte se crea ni
/// se corrige fuera de ella, porque el saldo es una propiedad del conjunto y no de cada apunte.
/// </summary>
/// <remarks>
/// El saldo disponible se guarda desnormalizado para que consultarlo no exija sumar el histórico,
/// pero jamás se asigna a mano: siempre es el resultado de <see cref="RecalcularSaldos"/> sobre la
/// serie completa. Así el dato materializado no puede divergir de los movimientos que lo sustentan.
/// </remarks>
public sealed class Cuenta : Entidad<Guid>
{
    private readonly List<Movimiento> _movimientos = [];

    private Cuenta()
    {
        // Constructor requerido por Entity Framework Core.
    }

    private Cuenta(
        Guid id, string numeroCuenta, TipoCuenta tipo, decimal saldoInicial, string clienteId, bool estado)
    {
        Id = id;
        NumeroCuenta = ValidarNumero(numeroCuenta);
        Tipo = ValidarTipo(tipo);
        SaldoInicial = ValidarSaldoInicial(saldoInicial);
        SaldoDisponible = SaldoInicial;
        ClienteId = ValidarClienteId(clienteId);
        Estado = estado;
        CreadaEn = DateTime.UtcNow;
    }

    /// <summary>Número de cuenta visible para el cliente. Es la clave única de negocio.</summary>
    public string NumeroCuenta { get; private set; } = string.Empty;

    public TipoCuenta Tipo { get; private set; }

    /// <summary>Saldo de apertura. Inmutable en cuanto la cuenta tiene movimientos.</summary>
    public decimal SaldoInicial { get; private set; }

    /// <summary>Saldo tras aplicar todos los movimientos (F2).</summary>
    public decimal SaldoDisponible { get; private set; }

    public bool Estado { get; private set; }

    /// <summary>Referencia al cliente en el otro microservicio; no es una clave foránea física.</summary>
    public string ClienteId { get; private set; } = string.Empty;

    public DateTime CreadaEn { get; private set; }

    public IReadOnlyList<Movimiento> Movimientos => _movimientos.AsReadOnly();

    public static Cuenta Aperturar(
        string numeroCuenta, TipoCuenta tipo, decimal saldoInicial, string clienteId, bool estado = true)
    {
        var cuenta = new Cuenta(Guid.NewGuid(), numeroCuenta, tipo, saldoInicial, clienteId, estado);

        cuenta.RegistrarEvento(new CuentaAperturadaEnDominio(
            cuenta.Id, cuenta.NumeroCuenta, cuenta.ClienteId, cuenta.Tipo.ToString(), cuenta.SaldoInicial));

        return cuenta;
    }

    /// <summary>
    /// F2 — Asienta un movimiento y actualiza el saldo disponible. F3 — Si la operación dejaría la
    /// cuenta en descubierto se rechaza con el mensaje "Saldo no disponible".
    /// </summary>
    public Movimiento RegistrarMovimiento(decimal valor, DateTime? fecha = null)
    {
        AsegurarOperable();

        var momento = fecha ?? DateTime.UtcNow;
        var movimiento = Movimiento.Crear(Id, momento, valor, SiguienteSecuencia());

        _movimientos.Add(movimiento);
        RecalcularSaldos();

        RegistrarEvento(new MovimientoAsentado(
            movimiento.Id,
            NumeroCuenta,
            ClienteId,
            movimiento.Tipo.ToString(),
            movimiento.Valor,
            movimiento.SaldoDisponible,
            movimiento.Fecha));

        return movimiento;
    }

    /// <summary>
    /// Corrige un movimiento ya asentado. Al cambiar un importe intermedio cambia toda la serie
    /// posterior, así que se recalcula el histórico completo y se rechaza la corrección si en
    /// algún punto dejase la cuenta en descubierto.
    /// </summary>
    public Movimiento CorregirMovimiento(Guid movimientoId, decimal nuevoValor, DateTime? nuevaFecha = null)
    {
        AsegurarOperable();

        var movimiento = _movimientos.FirstOrDefault(m => m.Id == movimientoId)
            ?? throw new ExcepcionNoEncontrado("el movimiento", movimientoId);

        var fechaOriginal = movimiento.Fecha;
        var valorOriginal = movimiento.Valor;

        movimiento.Reexpresar(nuevaFecha ?? movimiento.Fecha, nuevoValor);

        try
        {
            RecalcularSaldos();
        }
        catch (ExcepcionSaldoNoDisponible)
        {
            // La corrección es inválida: se deshace en memoria para que el agregado no quede en un
            // estado incoherente si quien llama decide ignorar la excepción.
            movimiento.Reexpresar(fechaOriginal, valorOriginal);
            RecalcularSaldos();
            throw;
        }

        RegistrarEvento(new MovimientoAsentado(
            movimiento.Id,
            NumeroCuenta,
            ClienteId,
            movimiento.Tipo.ToString(),
            movimiento.Valor,
            movimiento.SaldoDisponible,
            movimiento.Fecha));

        return movimiento;
    }

    /// <summary>Actualiza los datos editables de la cuenta (PUT).</summary>
    public void Actualizar(TipoCuenta tipo, decimal saldoInicial, bool estado)
    {
        Tipo = ValidarTipo(tipo);
        CambiarSaldoInicial(saldoInicial);
        Estado = estado;
    }

    public void CambiarEstado(bool activa) => Estado = activa;

    /// <summary>
    /// El saldo inicial solo puede corregirse mientras la cuenta no tenga historia: después sería
    /// reescribir la contabilidad. Una vez hay movimientos, el ajuste se hace con un apunte.
    /// </summary>
    public void CambiarSaldoInicial(decimal saldoInicial)
    {
        var nuevo = ValidarSaldoInicial(saldoInicial);

        if (nuevo == SaldoInicial)
        {
            return;
        }

        if (_movimientos.Count > 0)
        {
            throw new ExcepcionReglaNegocio(
                "SALDO_INICIAL_BLOQUEADO",
                "No se puede cambiar el saldo inicial de una cuenta con movimientos. Registre un movimiento de ajuste.");
        }

        SaldoInicial = nuevo;
        SaldoDisponible = nuevo;
    }

    /// <summary>Permite a la infraestructura reconstruir el agregado tras cargarlo de la base.</summary>
    public void AdjuntarMovimientos(IEnumerable<Movimiento> movimientos)
    {
        _movimientos.Clear();
        _movimientos.AddRange(movimientos);
    }

    private void AsegurarOperable()
    {
        if (!Estado)
        {
            throw new ExcepcionReglaNegocio(
                "CUENTA_INACTIVA", $"La cuenta {NumeroCuenta} está inactiva y no admite movimientos.");
        }
    }

    private int SiguienteSecuencia() => _movimientos.Count == 0 ? 1 : _movimientos.Max(m => m.Secuencia) + 1;

    /// <summary>
    /// Recorre la serie en orden cronológico acumulando el saldo. Es la única vía por la que
    /// <see cref="SaldoDisponible"/> cambia, y donde se aplica la regla F3.
    /// </summary>
    private void RecalcularSaldos()
    {
        var saldo = SaldoInicial;

        foreach (var movimiento in _movimientos.OrderBy(m => m.Fecha).ThenBy(m => m.Secuencia))
        {
            var saldoPrevio = saldo;
            saldo += movimiento.Valor;

            if (saldo < 0)
            {
                throw new ExcepcionSaldoNoDisponible(saldoPrevio, movimiento.Valor);
            }

            movimiento.EstablecerSaldo(saldo);
        }

        SaldoDisponible = saldo;
    }

    private static string ValidarNumero(string numeroCuenta)
    {
        if (string.IsNullOrWhiteSpace(numeroCuenta))
        {
            throw new ExcepcionReglaNegocio("NUMERO_CUENTA_REQUERIDO", "El número de cuenta es obligatorio.");
        }

        var limpio = numeroCuenta.Trim();

        return limpio.Length is >= 4 and <= 20 && limpio.All(char.IsDigit)
            ? limpio
            : throw new ExcepcionReglaNegocio(
                "NUMERO_CUENTA_INVALIDO", "El número de cuenta debe tener entre 4 y 20 dígitos.");
    }

    private static TipoCuenta ValidarTipo(TipoCuenta tipo) => Enum.IsDefined(tipo)
        ? tipo
        : throw new ExcepcionReglaNegocio("TIPO_CUENTA_INVALIDO", "El tipo de cuenta indicado no es válido.");

    private static decimal ValidarSaldoInicial(decimal saldoInicial) => saldoInicial >= 0
        ? decimal.Round(saldoInicial, 2)
        : throw new ExcepcionReglaNegocio("SALDO_INICIAL_NEGATIVO", "El saldo inicial no puede ser negativo.");

    private static string ValidarClienteId(string clienteId) => string.IsNullOrWhiteSpace(clienteId)
        ? throw new ExcepcionReglaNegocio("CLIENTEID_REQUERIDO", "La cuenta debe pertenecer a un cliente.")
        : clienteId.Trim().ToUpperInvariant();
}
