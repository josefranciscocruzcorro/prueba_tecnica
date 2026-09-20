using Cuentas.Domain.Clientes;
using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cuentas.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo del agregado Cuenta. Los movimientos se configuran como colección de respaldo privada:
/// EF Core escribe directamente en el campo <c>_movimientos</c>, de modo que la entidad puede
/// exponer una lista de solo lectura y conservar el control sobre cómo se añaden los apuntes.
/// </summary>
public sealed class ConfiguracionCuenta : IEntityTypeConfiguration<Cuenta>
{
    public void Configure(EntityTypeBuilder<Cuenta> constructor)
    {
        constructor.ToTable("cuentas");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Id).HasColumnName("cuenta_id").ValueGeneratedNever();
        constructor.Property(c => c.NumeroCuenta).HasColumnName("numero_cuenta").HasMaxLength(20).IsRequired();
        constructor.Property(c => c.Tipo).HasColumnName("tipo_cuenta").HasConversion<string>().HasMaxLength(20).IsRequired();
        constructor.Property(c => c.ClienteId).HasColumnName("cliente_id").HasMaxLength(30).IsRequired();
        constructor.Property(c => c.Estado).HasColumnName("estado").IsRequired();
        constructor.Property(c => c.CreadaEn).HasColumnName("creada_en").IsRequired();

        // numeric(18,2): el dinero jamás se guarda en coma flotante binaria.
        constructor.Property(c => c.SaldoInicial).HasColumnName("saldo_inicial").HasPrecision(18, 2).IsRequired();
        constructor.Property(c => c.SaldoDisponible).HasColumnName("saldo_disponible").HasPrecision(18, 2).IsRequired();

        constructor.HasIndex(c => c.NumeroCuenta).IsUnique().HasDatabaseName("ux_cuentas_numero");
        constructor.HasIndex(c => c.ClienteId).HasDatabaseName("ix_cuentas_cliente");

        constructor.HasMany(c => c.Movimientos)
            .WithOne()
            .HasForeignKey(m => m.CuentaId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.Navigation(c => c.Movimientos)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_movimientos");

        constructor.Ignore(c => c.EventosDominio);
    }
}

public sealed class ConfiguracionMovimiento : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> constructor)
    {
        constructor.ToTable("movimientos");
        constructor.HasKey(m => m.Id);

        constructor.Property(m => m.Id).HasColumnName("movimiento_id").ValueGeneratedNever();
        constructor.Property(m => m.CuentaId).HasColumnName("cuenta_id").IsRequired();
        constructor.Property(m => m.Fecha).HasColumnName("fecha").IsRequired();
        constructor.Property(m => m.Tipo).HasColumnName("tipo_movimiento").HasConversion<string>().HasMaxLength(20).IsRequired();
        constructor.Property(m => m.Valor).HasColumnName("valor").HasPrecision(18, 2).IsRequired();
        constructor.Property(m => m.SaldoDisponible).HasColumnName("saldo_disponible").HasPrecision(18, 2).IsRequired();
        constructor.Property(m => m.Secuencia).HasColumnName("secuencia").IsRequired();

        // Índice compuesto alineado con el orden en que se leen los movimientos: el reporte y el
        // recálculo de saldos recorren (cuenta, fecha) y lo aprovechan sin ordenar en memoria.
        constructor.HasIndex(m => new { m.CuentaId, m.Fecha, m.Secuencia }).HasDatabaseName("ix_movimientos_cuenta_fecha");
        constructor.HasIndex(m => m.Fecha).HasDatabaseName("ix_movimientos_fecha");

        constructor.Ignore(m => m.EventosDominio);
    }
}

public sealed class ConfiguracionClienteReferencia : IEntityTypeConfiguration<ClienteReferencia>
{
    public void Configure(EntityTypeBuilder<ClienteReferencia> constructor)
    {
        constructor.ToTable("clientes_replicados");
        constructor.HasKey(c => c.ClienteId);

        constructor.Property(c => c.ClienteId).HasColumnName("cliente_id").HasMaxLength(30).ValueGeneratedNever();
        constructor.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        constructor.Property(c => c.Identificacion).HasColumnName("identificacion").HasMaxLength(20).IsRequired();
        constructor.Property(c => c.Estado).HasColumnName("estado").IsRequired();
        constructor.Property(c => c.SincronizadoEn).HasColumnName("sincronizado_en").IsRequired();
    }
}
