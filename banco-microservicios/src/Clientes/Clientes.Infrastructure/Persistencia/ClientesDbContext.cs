using Clientes.Domain.Actividad;
using Clientes.Domain.Clientes;
using Clientes.Domain.Personas;
using Clientes.Infrastructure.Mensajeria;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Clientes;
using Shared.Infrastructure.Bandeja;
using Shared.Kernel.Dominio;

namespace Clientes.Infrastructure.Persistencia;

/// <summary>
/// Contexto de persistencia del microservicio de Clientes. Hereda de
/// <see cref="ContextoConBandejaSalida"/>, de modo que cada confirmación deja los eventos de
/// integración listos para publicarse en la misma transacción que los datos.
/// </summary>
public sealed class ClientesDbContext(DbContextOptions<ClientesDbContext> opciones)
    : ContextoConBandejaSalida(opciones)
{
    public const string Esquema = "clientes";

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<ActividadCliente> Actividades => Set<ActividadCliente>();

    public override string EsquemaBandeja => Esquema;

    protected override void OnModelCreating(ModelBuilder constructor)
    {
        constructor.HasDefaultSchema(Esquema);
        constructor.ApplyConfigurationsFromAssembly(typeof(ClientesDbContext).Assembly);
        constructor.ApplyConfiguration(new ConfiguracionMensajeSalida());
    }

    protected override IEventoIntegracion? TraducirAIntegracion(IEventoDominio evento) =>
        TraductorEventos.ATipoIntegracion(evento);
}
