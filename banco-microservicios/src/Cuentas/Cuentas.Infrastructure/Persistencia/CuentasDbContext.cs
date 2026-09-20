using Cuentas.Domain.Clientes;
using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;
using Cuentas.Infrastructure.Mensajeria;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Clientes;
using Shared.Infrastructure.Bandeja;
using Shared.Kernel.Dominio;

namespace Cuentas.Infrastructure.Persistencia;

/// <summary>
/// Contexto de persistencia del microservicio de Cuentas. Incluye la réplica local de clientes,
/// que se alimenta por eventos y nunca se escribe desde los casos de uso.
/// </summary>
public sealed class CuentasDbContext(DbContextOptions<CuentasDbContext> opciones)
    : ContextoConBandejaSalida(opciones)
{
    public const string Esquema = "cuentas";

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    public DbSet<ClienteReferencia> ClientesReplicados => Set<ClienteReferencia>();

    public override string EsquemaBandeja => Esquema;

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        constructor.HasDefaultSchema(Esquema);
        constructor.ApplyConfigurationsFromAssembly(typeof(CuentasDbContext).Assembly);
        constructor.ApplyConfiguration(new ConfiguracionMensajeSalida());
    }

    protected override IEventoIntegracion? TraducirAIntegracion(IEventoDominio evento) =>
        TraductorEventos.ATipoIntegracion(evento);
}
