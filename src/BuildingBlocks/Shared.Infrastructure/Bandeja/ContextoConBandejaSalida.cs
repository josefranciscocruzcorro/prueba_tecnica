using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Contracts.Clientes;
using Shared.Kernel.Dominio;

namespace Shared.Infrastructure.Bandeja;

/// <summary>
/// Contexto de persistencia que implementa el patrón Outbox. Antes de cada <c>SaveChanges</c>
/// recoge los eventos de dominio acumulados en las entidades rastreadas, los traduce al contrato
/// público y los inserta como filas pendientes — todo dentro de la misma transacción.
/// </summary>
/// <remarks>
/// Esta es la pieza que hace fiable la integración asíncrona. Sin ella habría que elegir entre
/// publicar antes de guardar (riesgo de anunciar algo que luego se deshace) o después (riesgo de
/// guardar sin anunciar). Con la bandeja de salida ambos hechos comparten destino atómico.
/// </remarks>
public abstract class ContextoConBandejaSalida(DbContextOptions opciones) : DbContext(opciones)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public DbSet<MensajeSalida> BandejaSalida => Set<MensajeSalida>();

    /// <summary>Esquema donde vive la tabla de la bandeja, necesario para el SQL del despachador.</summary>
    public abstract string EsquemaBandeja { get; }

    /// <summary>
    /// Traduce un hecho del dominio al contrato público equivalente, o devuelve <c>null</c> si el
    /// evento es puramente interno y no debe salir del servicio.
    /// </summary>
    protected abstract IEventoIntegracion? TraducirAIntegracion(IEventoDominio evento);

    public override Task<int> SaveChangesAsync(CancellationToken cancelacion = default)
    {
        VolcarEventosABandejaDeSalida();

        return base.SaveChangesAsync(cancelacion);
    }

    public override int SaveChanges()
    {
        VolcarEventosABandejaDeSalida();

        return base.SaveChanges();
    }

    private void VolcarEventosABandejaDeSalida()
    {
        var conEventos = ChangeTracker
            .Entries()
            .Select(entrada => entrada.Entity)
            .OfType<IPortadorEventos>()
            .Where(entidad => entidad.EventosDominio.Count > 0)
            .ToList();

        foreach (var entidad in conEventos)
        {
            foreach (var integracion in entidad.EventosDominio.Select(TraducirAIntegracion).OfType<IEventoIntegracion>())
            {
                BandejaSalida.Add(new MensajeSalida(
                    integracion.GetType().FullName!,
                    JsonSerializer.Serialize(integracion, integracion.GetType(), OpcionesJson)));
            }

            entidad.LimpiarEventos();
        }
    }
}

/// <summary>Mapeo de la tabla de la bandeja de salida, idéntico en todos los microservicios.</summary>
public sealed class ConfiguracionMensajeSalida : IEntityTypeConfiguration<MensajeSalida>
{
    public void Configure(EntityTypeBuilder<MensajeSalida> constructor)
    {
        constructor.ToTable("bandeja_salida");
        constructor.HasKey(m => m.Id);

        constructor.Property(m => m.Id).HasColumnName("mensaje_id").ValueGeneratedNever();
        constructor.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(250).IsRequired();
        constructor.Property(m => m.Contenido).HasColumnName("contenido").IsRequired();
        constructor.Property(m => m.CreadoEn).HasColumnName("creado_en").IsRequired();
        constructor.Property(m => m.PublicadoEn).HasColumnName("publicado_en");
        constructor.Property(m => m.Intentos).HasColumnName("intentos").IsRequired();
        constructor.Property(m => m.UltimoError).HasColumnName("ultimo_error").HasMaxLength(500);

        // Índice parcial: el despachador solo consulta lo pendiente, así que el índice solo lo cubre.
        constructor.HasIndex(m => m.CreadoEn)
            .HasDatabaseName("ix_bandeja_salida_pendientes")
            .HasFilter("publicado_en IS NULL");
    }
}
