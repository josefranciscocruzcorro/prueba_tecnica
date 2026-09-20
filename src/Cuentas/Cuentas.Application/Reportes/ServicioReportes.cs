using System.Globalization;
using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;
using Shared.Kernel.Excepciones;

namespace Cuentas.Application.Reportes;

/// <summary>F4 — Generación del estado de cuenta por cliente y rango de fechas.</summary>
public interface IServicioReportes
{
    Task<EstadoDeCuentaDto> GenerarAsync(
        string clienteId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);

    /// <summary>Misma información, aplanada con las claves literales del enunciado.</summary>
    Task<IReadOnlyList<FilaEstadoDeCuenta>> GenerarPlanoAsync(
        string clienteId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default);
}

/// <summary>
/// Construye el estado de cuenta. Todo el trabajo pesado se hace en dos consultas —las cuentas del
/// cliente y sus movimientos del periodo— y el resto es composición en memoria, de modo que el
/// coste no crece con el número de cuentas.
/// </summary>
public sealed class ServicioReportes(
    IRepositorioCuentas cuentas,
    IRepositorioMovimientos movimientos,
    IRepositorioClientesReplicados clientes) : IServicioReportes
{
    /// <summary>Tope de amplitud del rango: protege la base de peticiones que barran años enteros.</summary>
    public const int MaximoDiasRango = 366;

    public async Task<EstadoDeCuentaDto> GenerarAsync(
        string clienteId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default)
    {
        ValidarRango(desde, hasta);

        var cliente = await clientes.ObtenerAsync(clienteId, cancelacion)
            ?? throw new ExcepcionNoEncontrado("el cliente", clienteId);

        var cuentasCliente = await cuentas.ListarPorClienteAsync(cliente.ClienteId, cancelacion);

        if (cuentasCliente.Count == 0)
        {
            return new EstadoDeCuentaDto(
                new ClienteReporte(cliente.ClienteId, cliente.Nombre, cliente.Identificacion, cliente.Estado),
                new RangoFechas(desde, hasta),
                new ResumenReporte(0, 0, 0, 0, 0),
                []);
        }

        var (inicio, fin) = AInstantesUtc(desde, hasta);
        var apuntes = await movimientos.ListarParaReporteAsync(
            [.. cuentasCliente.Select(c => c.Id)], inicio, fin, cancelacion);

        var porCuenta = apuntes.ToLookup(m => m.CuentaId);

        var detalle = cuentasCliente
            .OrderBy(c => c.NumeroCuenta, StringComparer.Ordinal)
            .Select(cuenta => ConstruirCuenta(cuenta, porCuenta[cuenta.Id]))
            .ToList();

        var resumen = new ResumenReporte(
            TotalCuentas: detalle.Count,
            TotalMovimientos: detalle.Sum(c => c.Movimientos.Count),
            TotalDepositos: apuntes.Where(m => m.Valor > 0).Sum(m => m.Valor),
            TotalRetiros: apuntes.Where(m => m.Valor < 0).Sum(m => m.Valor),
            SaldoDisponibleTotal: cuentasCliente.Sum(c => c.SaldoDisponible));

        return new EstadoDeCuentaDto(
            new ClienteReporte(cliente.ClienteId, cliente.Nombre, cliente.Identificacion, cliente.Estado),
            new RangoFechas(desde, hasta),
            resumen,
            detalle);
    }

    public async Task<IReadOnlyList<FilaEstadoDeCuenta>> GenerarPlanoAsync(
        string clienteId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default)
    {
        var reporte = await GenerarAsync(clienteId, desde, hasta, cancelacion);

        // Se ordena por la fecha real y solo después se formatea: ordenar por el texto "8/2/2022"
        // lo colocaría detrás de "10/2/2022" por comparación alfabética.
        return
        [
            .. reporte.Cuentas
                .SelectMany(cuenta => cuenta.Movimientos.Select(movimiento => (Cuenta: cuenta, Movimiento: movimiento)))
                .OrderByDescending(par => par.Movimiento.Fecha)
                .Select(par => new FilaEstadoDeCuenta(
                    Fecha: par.Movimiento.Fecha.ToString("d/M/yyyy", CultureInfo.InvariantCulture),
                    Cliente: reporte.Cliente.Nombre,
                    NumeroCuenta: par.Cuenta.NumeroCuenta,
                    Tipo: par.Cuenta.TipoCuenta,
                    SaldoInicial: par.Cuenta.SaldoInicial,
                    Estado: par.Cuenta.Estado,
                    Movimiento: par.Movimiento.Valor,
                    SaldoDisponible: par.Movimiento.SaldoDisponible)),
        ];
    }

    private static CuentaReporte ConstruirCuenta(Cuenta cuenta, IEnumerable<Movimiento> apuntes)
    {
        var ordenados = apuntes.OrderBy(m => m.Fecha).ThenBy(m => m.Secuencia).ToList();

        return new CuentaReporte(
            cuenta.NumeroCuenta,
            cuenta.Tipo.ToString(),
            cuenta.SaldoInicial,
            cuenta.SaldoDisponible,
            cuenta.Estado,
            ordenados.Sum(m => m.Valor),
            [.. ordenados.Select(m => new MovimientoReporte(
                m.Fecha, m.Tipo.ToString(), m.Valor, m.SaldoDisponible))]);
    }

    private static void ValidarRango(DateOnly desde, DateOnly hasta)
    {
        if (desde > hasta)
        {
            throw new ExcepcionReglaNegocio(
                "RANGO_INVALIDO", "La fecha inicial del rango no puede ser posterior a la final.");
        }

        if (hasta.DayNumber - desde.DayNumber > MaximoDiasRango)
        {
            throw new ExcepcionReglaNegocio(
                "RANGO_DEMASIADO_AMPLIO", $"El rango de fechas no puede superar {MaximoDiasRango} días.");
        }
    }

    /// <summary>
    /// Convierte el rango de días a instantes UTC. El día final se incluye completo: quien pide
    /// "hasta el 28" espera ver lo ocurrido ese día, no hasta su medianoche inicial.
    /// </summary>
    private static (DateTime Inicio, DateTime Fin) AInstantesUtc(DateOnly desde, DateOnly hasta) =>
        (DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
         DateTime.SpecifyKind(hasta.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc));
}
