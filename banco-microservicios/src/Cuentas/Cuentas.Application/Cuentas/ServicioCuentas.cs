using Cuentas.Application.Cuentas.Dtos;
using Cuentas.Domain.Clientes;
using Cuentas.Domain.Cuentas;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Excepciones;
using Shared.Kernel.Paginacion;
using Shared.Kernel.Persistencia;

namespace Cuentas.Application.Cuentas;

/// <summary>
/// Casos de uso de cuentas. Valida contra la réplica local de clientes —nunca llamando al otro
/// microservicio— y delega toda la regla contable en el agregado <see cref="Cuenta"/>.
/// </summary>
public sealed class ServicioCuentas(
    IRepositorioCuentas repositorio,
    IRepositorioClientesReplicados clientes,
    IUnidadDeTrabajo unidadDeTrabajo,
    ILogger<ServicioCuentas> registro) : IServicioCuentas
{
    private const int IntentosGeneracionNumero = 10;

    public async Task<PaginaDe<CuentaDto>> ListarAsync(
        string? clienteId, string? buscar, bool? estado, ConsultaPaginada paginacion,
        CancellationToken cancelacion = default)
    {
        var pagina = await repositorio.BuscarAsync(clienteId, buscar, estado, paginacion, cancelacion);

        // Una sola lectura de la réplica resuelve el nombre de todas las cuentas de la página:
        // así se evita el clásico problema de N+1 consultas.
        var nombres = await ResolverNombresAsync(pagina.Elementos.Select(c => c.ClienteId), cancelacion);

        return new PaginaDe<CuentaDto>(
            [.. pagina.Elementos.Select(c => CuentaDto.Desde(c, nombres.GetValueOrDefault(c.ClienteId)))],
            pagina.Pagina,
            pagina.Tamano,
            pagina.Total);
    }

    public async Task<CuentaDto> ObtenerAsync(string numeroCuenta, CancellationToken cancelacion = default)
    {
        var cuenta = await ObtenerObligatoriaAsync(numeroCuenta, cancelacion);
        var cliente = await clientes.ObtenerAsync(cuenta.ClienteId, cancelacion);

        return CuentaDto.Desde(cuenta, cliente?.Nombre);
    }

    public async Task<CuentaDto> CrearAsync(CrearCuentaSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var cliente = await AsegurarClienteOperableAsync(solicitud.ClienteId, cancelacion);

        var numeroCuenta = string.IsNullOrWhiteSpace(solicitud.NumeroCuenta)
            ? await GenerarNumeroLibreAsync(cancelacion)
            : solicitud.NumeroCuenta.Trim();

        if (await repositorio.ExisteNumeroAsync(numeroCuenta, cancelacion))
        {
            throw new ExcepcionConflicto("NUMERO_CUENTA_DUPLICADO", $"Ya existe la cuenta {numeroCuenta}.");
        }

        var cuenta = Cuenta.Aperturar(
            numeroCuenta, solicitud.TipoCuenta, solicitud.SaldoInicial, cliente.ClienteId, solicitud.Estado);

        await repositorio.AgregarAsync(cuenta, cancelacion);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        registro.LogInformation("Cuenta {NumeroCuenta} aperturada para {ClienteId}.", numeroCuenta, cliente.ClienteId);

        return CuentaDto.Desde(cuenta, cliente.Nombre);
    }

    public async Task<CuentaDto> ActualizarAsync(
        string numeroCuenta, ActualizarCuentaSolicitud solicitud, CancellationToken cancelacion = default)
    {
        // Se carga con movimientos porque cambiar el saldo inicial depende de si existen.
        var cuenta = await ObtenerConMovimientosAsync(numeroCuenta, cancelacion);

        cuenta.Actualizar(solicitud.TipoCuenta, solicitud.SaldoInicial, solicitud.Estado);

        repositorio.Actualizar(cuenta);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        var cliente = await clientes.ObtenerAsync(cuenta.ClienteId, cancelacion);

        return CuentaDto.Desde(cuenta, cliente?.Nombre);
    }

    public async Task<CuentaDto> ParchearAsync(
        string numeroCuenta, ParchearCuentaSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var cuenta = await ObtenerConMovimientosAsync(numeroCuenta, cancelacion);

        cuenta.Actualizar(
            solicitud.TipoCuenta ?? cuenta.Tipo,
            solicitud.SaldoInicial ?? cuenta.SaldoInicial,
            solicitud.Estado ?? cuenta.Estado);

        repositorio.Actualizar(cuenta);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        var cliente = await clientes.ObtenerAsync(cuenta.ClienteId, cancelacion);

        return CuentaDto.Desde(cuenta, cliente?.Nombre);
    }

    private async Task<Cuenta> ObtenerObligatoriaAsync(string numeroCuenta, CancellationToken cancelacion) =>
        await repositorio.ObtenerPorNumeroAsync(numeroCuenta, cancelacion)
        ?? throw new ExcepcionNoEncontrado("la cuenta", numeroCuenta);

    private async Task<Cuenta> ObtenerConMovimientosAsync(string numeroCuenta, CancellationToken cancelacion) =>
        await repositorio.ObtenerConMovimientosAsync(numeroCuenta, cancelacion)
        ?? throw new ExcepcionNoEncontrado("la cuenta", numeroCuenta);

    /// <summary>
    /// Comprueba contra la réplica local que el cliente existe y está activo. Si el evento de alta
    /// todavía no ha llegado, se devuelve un mensaje que explica la situación en lugar del genérico
    /// "no encontrado": el consumidor sabe así que basta con reintentar.
    /// </summary>
    private async Task<ClienteReferencia> AsegurarClienteOperableAsync(
        string clienteId, CancellationToken cancelacion)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, cancelacion)
            ?? throw new ExcepcionConflicto(
                "CLIENTE_NO_SINCRONIZADO",
                $"El cliente {clienteId} no consta en este servicio. Verifique que existe o reintente " +
                "en unos segundos: la réplica se actualiza de forma asíncrona.");

        return cliente.Estado
            ? cliente
            : throw new ExcepcionReglaNegocio(
                "CLIENTE_INACTIVO", $"El cliente {clienteId} está inactivo y no puede abrir cuentas.");
    }

    private async Task<Dictionary<string, string>> ResolverNombresAsync(
        IEnumerable<string> clienteIds, CancellationToken cancelacion)
    {
        var buscados = clienteIds.Distinct().ToHashSet();

        if (buscados.Count == 0)
        {
            return [];
        }

        var replicados = await clientes.ListarAsync(cancelacion);

        return replicados
            .Where(c => buscados.Contains(c.ClienteId))
            .ToDictionary(c => c.ClienteId, c => c.Nombre);
    }

    /// <summary>Genera un número de cuenta de seis dígitos que no esté en uso.</summary>
    private async Task<string> GenerarNumeroLibreAsync(CancellationToken cancelacion)
    {
        for (var intento = 0; intento < IntentosGeneracionNumero; intento++)
        {
            var candidato = Random.Shared.Next(100_000, 1_000_000).ToString();

            if (!await repositorio.ExisteNumeroAsync(candidato, cancelacion))
            {
                return candidato;
            }
        }

        throw new ExcepcionConflicto(
            "NUMERO_CUENTA_NO_GENERADO", "No fue posible generar un número de cuenta libre. Indíquelo manualmente.");
    }
}
