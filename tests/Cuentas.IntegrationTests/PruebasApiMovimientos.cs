using System.Net;
using System.Net.Http.Json;
using Cuentas.Application.Cuentas.Dtos;
using Cuentas.Application.Reportes;
using Cuentas.Domain.Clientes;
using Cuentas.Domain.Cuentas;
using Cuentas.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Shared.Api.Errores;

namespace Cuentas.IntegrationTests;

/// <summary>
/// F6 — Pruebas de integración de la API de Cuentas. Recorren el camino completo de una petición
/// real: enrutado, autenticación por token, validación, caso de uso, dominio, persistencia y
/// traducción de errores.
/// </summary>
[Collection(nameof(ColeccionApiCuentas))]
public sealed class PruebasApiMovimientos(FabricaApiCuentas fabrica) : IAsyncLifetime
{
    private const string CuentaAhorros = "478758";
    private const string CuentaCorriente = "225487";
    private const string CuentaSinFondos = "495878";
    private const string ClienteId = "CLI-002";

    /// <summary>Deja la base en un estado conocido antes de cada prueba.</summary>
    public async Task InitializeAsync() => await fabrica.EnContextoAsync(async contexto =>
    {
        contexto.Movimientos.RemoveRange(contexto.Movimientos);
        contexto.Cuentas.RemoveRange(contexto.Cuentas);
        contexto.ClientesReplicados.RemoveRange(contexto.ClientesReplicados);
        contexto.BandejaSalida.RemoveRange(contexto.BandejaSalida);
        await contexto.SaveChangesAsync();

        // La réplica simula lo que habría dejado el consumidor del evento ClienteCreado.
        contexto.ClientesReplicados.Add(ClienteReferencia.Crear(ClienteId, "Marianela Montalvo", "1818181818", true));
        contexto.ClientesReplicados.Add(ClienteReferencia.Crear("CLI-009", "Cliente Inactivo", "1616161616", false));

        contexto.Cuentas.Add(Cuenta.Aperturar(CuentaAhorros, TipoCuenta.Ahorros, 2000m, ClienteId));
        contexto.Cuentas.Add(Cuenta.Aperturar(CuentaCorriente, TipoCuenta.Corriente, 100m, ClienteId));
        contexto.Cuentas.Add(Cuenta.Aperturar(CuentaSinFondos, TipoCuenta.Ahorros, 0m, ClienteId));

        await contexto.SaveChangesAsync();
    });

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Autenticación ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SinToken_LaApiRechazaLaPeticion()
    {
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/cuentas");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConTokenValido_LaApiDevuelveLasCuentas()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync($"/api/cuentas?cliente={ClienteId}");
        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaCuentas>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        pagina!.Total.Should().Be(3);
        pagina.Elementos.Should().OnlyContain(c => c.NombreCliente == "Marianela Montalvo");
    }

    // ── F2: el movimiento actualiza el saldo y queda registrado ─────────────────────────────────

    [Fact]
    public async Task RegistrarDeposito_ActualizaElSaldoYPersisteElMovimiento()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaCorriente,
            valor = 600m,
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var movimiento = await respuesta.Content.ReadFromJsonAsync<MovimientoDto>(FabricaApiCuentas.Json);
        movimiento!.SaldoDisponible.Should().Be(700m);
        movimiento.TipoMovimiento.Should().Be("Deposito");

        // El saldo debe verse igual desde la API...
        var cuenta = await cliente.GetFromJsonAsync<CuentaDto>(
            $"/api/cuentas/{CuentaCorriente}", FabricaApiCuentas.Json);
        cuenta!.SaldoDisponible.Should().Be(700m);

        // ...y estar realmente escrito en la base, no solo en la respuesta.
        var persistido = await fabrica.ConContextoAsync(async ctx => await ctx.Movimientos
            .AsNoTracking()
            .CountAsync(m => m.Valor == 600m));
        persistido.Should().Be(1);
    }

    [Fact]
    public async Task RegistrarRetiro_ConValorNegativo_DescuentaDelSaldo()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaAhorros,
            valor = -575m,
        });

        var movimiento = await respuesta.Content.ReadFromJsonAsync<MovimientoDto>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        movimiento!.SaldoDisponible.Should().Be(1425m);
        movimiento.TipoMovimiento.Should().Be("Retiro");
    }

    [Fact]
    public async Task RegistrarRetiro_ConTipoExplicitoYValorPositivo_EquivaleAlValorNegativo()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaAhorros,
            valor = 575m,
            tipoMovimiento = "Retiro",
        });

        var movimiento = await respuesta.Content.ReadFromJsonAsync<MovimientoDto>(FabricaApiCuentas.Json);

        movimiento!.Valor.Should().Be(-575m, "el servicio normaliza el signo a partir del tipo");
        movimiento.SaldoDisponible.Should().Be(1425m);
    }

    [Fact]
    public async Task VariosMovimientosSeguidos_AcumulanElSaldoCorrectamente()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await cliente.PostAsJsonAsync("/api/movimientos", new { numeroCuenta = CuentaCorriente, valor = 600m });
        await cliente.PostAsJsonAsync("/api/movimientos", new { numeroCuenta = CuentaCorriente, valor = -200m });
        await cliente.PostAsJsonAsync("/api/movimientos", new { numeroCuenta = CuentaCorriente, valor = 50m });

        var cuenta = await cliente.GetFromJsonAsync<CuentaDto>(
            $"/api/cuentas/{CuentaCorriente}", FabricaApiCuentas.Json);

        cuenta!.SaldoDisponible.Should().Be(550m);
    }

    // ── F3: "Saldo no disponible" ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarRetiro_SinSaldo_DevuelveSaldoNoDisponible()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaSinFondos,
            valor = -150m,
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);
        error!.Detalle.Should().Be("Saldo no disponible");
        error.Titulo.Should().Be("Saldo no disponible");
        error.Codigo.Should().Be("SALDO_NO_DISPONIBLE");
        error.TraceId.Should().NotBeNullOrWhiteSpace("el error debe poder correlacionarse con los registros");
    }

    [Fact]
    public async Task RegistrarRetiro_SinSaldo_NoDejaRastroEnLaBase()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await cliente.PostAsJsonAsync("/api/movimientos", new { numeroCuenta = CuentaSinFondos, valor = -150m });

        var cuenta = await cliente.GetFromJsonAsync<CuentaDto>(
            $"/api/cuentas/{CuentaSinFondos}", FabricaApiCuentas.Json);
        var movimientos = await fabrica.ConContextoAsync(async ctx => await ctx.Movimientos.CountAsync());

        cuenta!.SaldoDisponible.Should().Be(0m);
        movimientos.Should().Be(0, "una operación rechazada no persiste nada");
    }

    // ── Validación y errores ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarMovimiento_ConValorCero_DevuelveErrorDeValidacion()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaCorriente,
            valor = 0m,
        });

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Codigo.Should().Be("VALIDACION_FALLIDA");
        error.Errores.Should().ContainKey("Valor");
    }

    [Fact]
    public async Task RegistrarMovimiento_SobreCuentaInexistente_DevuelveNoEncontrado()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = "999999",
            valor = 100m,
        });

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error!.Codigo.Should().Be("RECURSO_NO_ENCONTRADO");
    }

    // ── Apertura de cuentas y consistencia eventual ─────────────────────────────────────────────

    [Fact]
    public async Task CrearCuenta_ParaClienteReplicado_DevuelveCreada()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/cuentas", new
        {
            clienteId = ClienteId,
            tipoCuenta = "Corriente",
            saldoInicial = 1000m,
            numeroCuenta = "585545",
        });

        var creada = await respuesta.Content.ReadFromJsonAsync<CuentaDto>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        respuesta.Headers.Location!.ToString().Should().EndWith("/api/cuentas/585545");
        creada!.SaldoDisponible.Should().Be(1000m);
    }

    [Fact]
    public async Task CrearCuenta_ParaUnClienteQueAunNoSeReplico_ExplicaLaConsistenciaEventual()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/cuentas", new
        {
            clienteId = "CLI-INEXISTENTE",
            tipoCuenta = "Ahorros",
            saldoInicial = 10m,
        });

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        error!.Codigo.Should().Be("CLIENTE_NO_SINCRONIZADO");
    }

    [Fact]
    public async Task CrearCuenta_ParaUnClienteInactivo_SeRechaza()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.PostAsJsonAsync("/api/cuentas", new
        {
            clienteId = "CLI-009",
            tipoCuenta = "Ahorros",
            saldoInicial = 10m,
        });

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Codigo.Should().Be("CLIENTE_INACTIVO");
    }

    // ── F4: estado de cuenta ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reporte_DevuelveCuentasConSusSaldosYElDetalleDeMovimientos()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var hoy = DateTime.UtcNow.Date;

        await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaCorriente,
            valor = 600m,
            fecha = hoy.AddHours(9),
        });

        var respuesta = await cliente.GetAsync(
            $"/api/reportes?cliente={ClienteId}&fecha={hoy:yyyy-MM-dd},{hoy:yyyy-MM-dd}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var reporte = await respuesta.Content.ReadFromJsonAsync<EstadoDeCuentaDto>(FabricaApiCuentas.Json);

        reporte!.Cliente.Nombre.Should().Be("Marianela Montalvo");
        reporte.Cuentas.Should().HaveCount(3, "el reporte incluye todas las cuentas del cliente");
        reporte.Resumen.TotalDepositos.Should().Be(600m);
        reporte.Resumen.SaldoDisponibleTotal.Should().Be(2700m, "2000 + 700 + 0");

        var corriente = reporte.Cuentas.Single(c => c.NumeroCuenta == CuentaCorriente);
        corriente.Movimientos.Should().ContainSingle()
            .Which.SaldoDisponible.Should().Be(700m);
    }

    [Fact]
    public async Task Reporte_EnFormatoPlano_UsaLasClavesLiteralesDelEnunciado()
    {
        var cliente = fabrica.CrearClienteAutenticado();
        var hoy = DateTime.UtcNow.Date;

        await cliente.PostAsJsonAsync("/api/movimientos", new
        {
            numeroCuenta = CuentaCorriente,
            valor = 600m,
            fecha = hoy.AddHours(9),
        });

        var respuesta = await cliente.GetAsync(
            $"/api/reportes?cliente={ClienteId}&fecha={hoy:yyyy-MM-dd},{hoy:yyyy-MM-dd}&formato=plano");
        var json = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("\"Numero Cuenta\"").And.Contain("\"Saldo Inicial\"").And.Contain("\"Saldo Disponible\"");
        json.Should().Contain("\"Cliente\":\"Marianela Montalvo\"");
    }

    [Fact]
    public async Task Reporte_ConRangoInvertido_SeRechaza()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync(
            $"/api/reportes?cliente={ClienteId}&fecha=2022-03-01,2022-02-01");
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Codigo.Should().Be("RANGO_INVALIDO");
    }

    [Fact]
    public async Task Reporte_ConRangoMalFormado_ExplicaElFormatoEsperado()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        var respuesta = await cliente.GetAsync($"/api/reportes?cliente={ClienteId}&fecha=2022-03-01");
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>(FabricaApiCuentas.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Codigo.Should().Be("RANGO_MAL_FORMADO");
    }

    // ── Bandeja de salida: la integración asíncrona queda encolada de forma atómica ─────────────

    [Fact]
    public async Task RegistrarMovimiento_DejaElEventoEnLaBandejaDeSalida()
    {
        var cliente = fabrica.CrearClienteAutenticado();

        await cliente.PostAsJsonAsync("/api/movimientos", new { numeroCuenta = CuentaCorriente, valor = 600m });

        var pendientes = await fabrica.ConContextoAsync(async ctx => await ctx.BandejaSalida
            .AsNoTracking()
            .Where(m => m.PublicadoEn == null)
            .ToListAsync());

        pendientes.Should().Contain(m => m.Tipo.EndsWith("MovimientoRegistrado", StringComparison.Ordinal),
            "el evento se escribe en la misma transacción que el movimiento");
    }

    /// <summary>Forma concreta de la página que devuelve la API, para deserializarla sin genéricos.</summary>
    private sealed record PaginaCuentas(IReadOnlyList<CuentaDto> Elementos, int Pagina, int Tamano, long Total);
}

/// <summary>
/// Comparte una única instancia de la API entre todas las pruebas: arrancar el host es lo caro,
/// y cada prueba deja la base en un estado conocido en su inicialización.
/// </summary>
[CollectionDefinition(nameof(ColeccionApiCuentas))]
public sealed class ColeccionApiCuentas : ICollectionFixture<FabricaApiCuentas>;
