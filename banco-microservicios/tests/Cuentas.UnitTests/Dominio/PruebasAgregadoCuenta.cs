using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Eventos;
using Cuentas.Domain.Movimientos;
using Shared.Kernel.Excepciones;

namespace Cuentas.UnitTests.Dominio;

/// <summary>
/// Pruebas del agregado contable. Cubren F2 (el movimiento actualiza el saldo y queda registrado)
/// y F3 (un retiro sin fondos se rechaza con "Saldo no disponible"), además de los casos de uso
/// concretos del enunciado.
/// </summary>
public sealed class PruebasAgregadoCuenta
{
    [Fact]
    public void Aperturar_DejaElSaldoDisponibleIgualAlInicialYAnunciaLaApertura()
    {
        var cuenta = Cuenta.Aperturar("478758", TipoCuenta.Ahorros, 2000m, "CLI-001");

        cuenta.NumeroCuenta.Should().Be("478758");
        cuenta.SaldoInicial.Should().Be(2000m);
        cuenta.SaldoDisponible.Should().Be(2000m);
        cuenta.Estado.Should().BeTrue();
        cuenta.EventosDominio.Should().ContainSingle().Which.Should().BeOfType<CuentaAperturadaEnDominio>();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012345678901")]
    [InlineData("47A758")]
    [InlineData("")]
    public void Aperturar_ConNumeroInvalido_Falla(string numero)
    {
        var accion = () => Cuenta.Aperturar(numero, TipoCuenta.Ahorros, 100m, "CLI-001");

        accion.Should().Throw<ExcepcionReglaNegocio>();
    }

    [Fact]
    public void Aperturar_ConSaldoInicialNegativo_Falla()
    {
        var accion = () => Cuenta.Aperturar("478758", TipoCuenta.Ahorros, -1m, "CLI-001");

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("SALDO_INICIAL_NEGATIVO");
    }

    // ── F2: el movimiento actualiza el saldo disponible y queda registrado ──────────────────────

    [Theory]
    [InlineData("478758", 2000, -575, 1425)]
    [InlineData("225487", 100, 600, 700)]
    [InlineData("495878", 0, 150, 150)]
    [InlineData("496825", 540, -540, 0)]
    public void RegistrarMovimiento_ActualizaElSaldoSegunLosCasosDelEnunciado(
        string numero, decimal saldoInicial, decimal valor, decimal saldoEsperado)
    {
        var cuenta = Cuenta.Aperturar(numero, TipoCuenta.Ahorros, saldoInicial, "CLI-001");

        var movimiento = cuenta.RegistrarMovimiento(valor);

        cuenta.SaldoDisponible.Should().Be(saldoEsperado);
        movimiento.SaldoDisponible.Should().Be(saldoEsperado);
        movimiento.Valor.Should().Be(valor);
        cuenta.Movimientos.Should().ContainSingle("se lleva el registro de las transacciones realizadas");
    }

    [Fact]
    public void RegistrarMovimiento_DerivaElTipoDelSignoDelValor()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 1000m, "CLI-001");

        cuenta.RegistrarMovimiento(600m).Tipo.Should().Be(TipoMovimiento.Deposito);
        cuenta.RegistrarMovimiento(-250m).Tipo.Should().Be(TipoMovimiento.Retiro);
    }

    [Fact]
    public void RegistrarMovimiento_AcumulaVariosApuntesEnOrden()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 100m, "CLI-002");

        cuenta.RegistrarMovimiento(600m, new DateTime(2022, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        cuenta.RegistrarMovimiento(-200m, new DateTime(2022, 2, 11, 0, 0, 0, DateTimeKind.Utc));
        cuenta.RegistrarMovimiento(50m, new DateTime(2022, 2, 12, 0, 0, 0, DateTimeKind.Utc));

        cuenta.SaldoDisponible.Should().Be(550m);
        cuenta.Movimientos.OrderBy(m => m.Fecha).Select(m => m.SaldoDisponible)
            .Should().Equal([700m, 500m, 550m], "cada apunte guarda el saldo resultante en su momento");
    }

    [Fact]
    public void RegistrarMovimiento_ConUnApunteRetroactivo_RecalculaLaSerieCompleta()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 100m, "CLI-002");
        cuenta.RegistrarMovimiento(600m, new DateTime(2022, 2, 10, 0, 0, 0, DateTimeKind.Utc));

        // Un apunte con fecha anterior debe reordenar la serie, no añadirse al final.
        cuenta.RegistrarMovimiento(-50m, new DateTime(2022, 2, 5, 0, 0, 0, DateTimeKind.Utc));

        cuenta.SaldoDisponible.Should().Be(650m);
        cuenta.Movimientos.OrderBy(m => m.Fecha).Select(m => m.SaldoDisponible).Should().Equal(50m, 650m);
    }

    // ── F3: "Saldo no disponible" ───────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarMovimiento_SinSaldoSuficiente_LanzaSaldoNoDisponible()
    {
        var cuenta = Cuenta.Aperturar("495878", TipoCuenta.Ahorros, 0m, "CLI-003");

        var accion = () => cuenta.RegistrarMovimiento(-150m);

        var excepcion = accion.Should().Throw<ExcepcionSaldoNoDisponible>().Which;
        excepcion.Message.Should().Be("Saldo no disponible", "es el mensaje literal que exige el enunciado");
        excepcion.Codigo.Should().Be("SALDO_NO_DISPONIBLE");
        excepcion.CodigoEstado.Should().Be(400);
        excepcion.SaldoActual.Should().Be(0m);
        excepcion.ValorSolicitado.Should().Be(-150m);
    }

    [Fact]
    public void RegistrarMovimiento_QueDejaElSaldoExactamenteEnCero_SePermite()
    {
        var cuenta = Cuenta.Aperturar("496825", TipoCuenta.Ahorros, 540m, "CLI-002");

        var accion = () => cuenta.RegistrarMovimiento(-540m);

        accion.Should().NotThrow("agotar el saldo es legítimo; lo que no se permite es el descubierto");
        cuenta.SaldoDisponible.Should().Be(0m);
    }

    [Fact]
    public void RegistrarMovimiento_RechazadoPorSaldo_NoDejaRastroEnLaCuenta()
    {
        var cuenta = Cuenta.Aperturar("495878", TipoCuenta.Ahorros, 100m, "CLI-003");

        var accion = () => cuenta.RegistrarMovimiento(-500m);

        accion.Should().Throw<ExcepcionSaldoNoDisponible>();
        cuenta.SaldoDisponible.Should().Be(100m, "el saldo no cambia si la operación se rechaza");
    }

    [Fact]
    public void RegistrarMovimiento_ConValorCero_Falla()
    {
        var cuenta = Cuenta.Aperturar("478758", TipoCuenta.Ahorros, 100m, "CLI-001");

        var accion = () => cuenta.RegistrarMovimiento(0m);

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("VALOR_INVALIDO");
    }

    [Fact]
    public void RegistrarMovimiento_SobreCuentaInactiva_Falla()
    {
        var cuenta = Cuenta.Aperturar("478758", TipoCuenta.Ahorros, 1000m, "CLI-001", estado: false);

        var accion = () => cuenta.RegistrarMovimiento(100m);

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("CUENTA_INACTIVA");
    }

    [Fact]
    public void RegistrarMovimiento_ConFechaFutura_Falla()
    {
        var cuenta = Cuenta.Aperturar("478758", TipoCuenta.Ahorros, 1000m, "CLI-001");

        var accion = () => cuenta.RegistrarMovimiento(100m, DateTime.UtcNow.AddDays(1));

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("FECHA_FUTURA");
    }

    // ── Corrección de movimientos ───────────────────────────────────────────────────────────────

    [Fact]
    public void CorregirMovimiento_RecalculaElSaldoDeTodaLaSerie()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 100m, "CLI-002");
        var primero = cuenta.RegistrarMovimiento(600m, new DateTime(2022, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        cuenta.RegistrarMovimiento(-200m, new DateTime(2022, 2, 11, 0, 0, 0, DateTimeKind.Utc));

        cuenta.CorregirMovimiento(primero.Id, 800m);

        cuenta.SaldoDisponible.Should().Be(700m);
        cuenta.Movimientos.OrderBy(m => m.Fecha).Select(m => m.SaldoDisponible).Should().Equal(900m, 700m);
    }

    [Fact]
    public void CorregirMovimiento_QueProvocariaDescubierto_SeRechazaYRevierte()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 100m, "CLI-002");
        var deposito = cuenta.RegistrarMovimiento(600m, new DateTime(2022, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        cuenta.RegistrarMovimiento(-650m, new DateTime(2022, 2, 11, 0, 0, 0, DateTimeKind.Utc));

        // Rebajar el depósito dejaría el retiro posterior en descubierto.
        var accion = () => cuenta.CorregirMovimiento(deposito.Id, 100m);

        accion.Should().Throw<ExcepcionSaldoNoDisponible>();
        cuenta.SaldoDisponible.Should().Be(50m, "la corrección inválida se deshace por completo");
        cuenta.Movimientos.Single(m => m.Id == deposito.Id).Valor.Should().Be(600m);
    }

    [Fact]
    public void CorregirMovimiento_Inexistente_Falla()
    {
        var cuenta = Cuenta.Aperturar("225487", TipoCuenta.Corriente, 100m, "CLI-002");

        var accion = () => cuenta.CorregirMovimiento(Guid.NewGuid(), 50m);

        accion.Should().Throw<ExcepcionNoEncontrado>();
    }

    // ── Saldo inicial ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CambiarSaldoInicial_SinMovimientos_SePermite()
    {
        var cuenta = Cuenta.Aperturar("585545", TipoCuenta.Corriente, 1000m, "CLI-001");

        cuenta.CambiarSaldoInicial(1500m);

        cuenta.SaldoInicial.Should().Be(1500m);
        cuenta.SaldoDisponible.Should().Be(1500m);
    }

    [Fact]
    public void CambiarSaldoInicial_ConMovimientos_SeBloquea()
    {
        var cuenta = Cuenta.Aperturar("585545", TipoCuenta.Corriente, 1000m, "CLI-001");
        cuenta.RegistrarMovimiento(-100m);

        var accion = () => cuenta.CambiarSaldoInicial(1500m);

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("SALDO_INICIAL_BLOQUEADO");
    }

    [Fact]
    public void CambiarSaldoInicial_AlMismoValor_NoSeBloqueaAunqueHayaMovimientos()
    {
        var cuenta = Cuenta.Aperturar("585545", TipoCuenta.Corriente, 1000m, "CLI-001");
        cuenta.RegistrarMovimiento(-100m);

        var accion = () => cuenta.CambiarSaldoInicial(1000m);

        accion.Should().NotThrow("no hay cambio real que bloquear");
    }
}
