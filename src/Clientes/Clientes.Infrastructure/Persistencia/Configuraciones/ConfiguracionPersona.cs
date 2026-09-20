using Clientes.Domain.Clientes;
using Clientes.Domain.Personas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clientes.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapea la jerarquía Persona → Cliente con la estrategia tabla-por-tipo (TPT): una tabla
/// <c>personas</c> con los atributos comunes y una tabla <c>clientes</c> con los propios, unidas
/// por la clave primaria. Es el reflejo relacional fiel de la herencia que pide el enunciado y
/// evita las columnas nulas de la estrategia tabla-única.
/// </summary>
public sealed class ConfiguracionPersona : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> constructor)
    {
        constructor.ToTable("personas");
        constructor.HasKey(p => p.Id);

        constructor.Property(p => p.Id).HasColumnName("persona_id").ValueGeneratedNever();
        constructor.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(Persona.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.Genero).HasColumnName("genero").HasConversion<string>().HasMaxLength(20).IsRequired();
        constructor.Property(p => p.Edad).HasColumnName("edad").IsRequired();
        constructor.Property(p => p.Identificacion).HasColumnName("identificacion").HasMaxLength(20).IsRequired();
        constructor.Property(p => p.Direccion).HasColumnName("direccion").HasMaxLength(200).IsRequired();
        constructor.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired();

        constructor.HasIndex(p => p.Identificacion).IsUnique().HasDatabaseName("ux_personas_identificacion");
        constructor.HasIndex(p => p.Nombre).HasDatabaseName("ix_personas_nombre");

        constructor.Ignore(p => p.EventosDominio);
    }
}

public sealed class ConfiguracionCliente : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> constructor)
    {
        constructor.ToTable("clientes");

        constructor.Property(c => c.ClienteId).HasColumnName("cliente_id").HasMaxLength(30).IsRequired();
        constructor.Property(c => c.ContrasenaHash).HasColumnName("contrasena_hash").HasMaxLength(256).IsRequired();
        constructor.Property(c => c.Estado).HasColumnName("estado").IsRequired();
        constructor.Property(c => c.CreadoEn).HasColumnName("creado_en").IsRequired();
        constructor.Property(c => c.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

        constructor.HasIndex(c => c.ClienteId).IsUnique().HasDatabaseName("ux_clientes_cliente_id");
        constructor.HasIndex(c => c.Estado).HasDatabaseName("ix_clientes_estado");
    }
}
