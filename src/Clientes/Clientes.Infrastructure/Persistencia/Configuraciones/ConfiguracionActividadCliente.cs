using Clientes.Domain.Actividad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clientes.Infrastructure.Persistencia.Configuraciones;

public sealed class ConfiguracionActividadCliente : IEntityTypeConfiguration<ActividadCliente>
{
    public void Configure(EntityTypeBuilder<ActividadCliente> constructor)
    {
        constructor.ToTable("actividad_cliente");
        constructor.HasKey(a => a.Id);

        // La clave primaria es el identificador del evento de integración: da idempotencia gratis.
        constructor.Property(a => a.Id).HasColumnName("evento_id").ValueGeneratedNever();
        constructor.Property(a => a.ClienteId).HasColumnName("cliente_id").HasMaxLength(30).IsRequired();
        constructor.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
        constructor.Property(a => a.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsRequired();
        constructor.Property(a => a.OcurridoEn).HasColumnName("ocurrido_en").IsRequired();

        constructor.HasIndex(a => new { a.ClienteId, a.OcurridoEn }).HasDatabaseName("ix_actividad_cliente_fecha");

        constructor.Ignore(a => a.EventosDominio);
    }
}
