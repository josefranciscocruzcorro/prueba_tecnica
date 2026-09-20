using Cuentas.Domain.Eventos;
using Shared.Contracts.Clientes;
using Shared.Contracts.Cuentas;
using Shared.Kernel.Dominio;

namespace Cuentas.Infrastructure.Mensajeria;

/// <summary>Traduce los hechos del dominio contable a los contratos publicados en el bus.</summary>
public static class TraductorEventos
{
    public static IEventoIntegracion? ATipoIntegracion(IEventoDominio evento) => evento switch
    {
        CuentaAperturadaEnDominio e => new CuentaAperturada(
            Guid.NewGuid(), DateTime.UtcNow, e.CuentaId, e.NumeroCuenta, e.ClienteId, e.TipoCuenta, e.SaldoInicial),

        MovimientoAsentado e => new MovimientoRegistrado(
            Guid.NewGuid(),
            DateTime.UtcNow,
            e.MovimientoId,
            e.NumeroCuenta,
            e.ClienteId,
            e.TipoMovimiento,
            e.Valor,
            e.SaldoResultante,
            e.Fecha),

        _ => null,
    };
}
