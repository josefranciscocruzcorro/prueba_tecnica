using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Kernel.Excepciones;
using Shared.Kernel.Persistencia;

namespace Cuentas.Infrastructure.Persistencia;

/// <summary>
/// Confirma la unidad de trabajo del microservicio de Cuentas y traduce los fallos de integridad
/// y de concurrencia a excepciones de dominio con un mensaje accionable.
/// </summary>
public sealed class UnidadDeTrabajo(CuentasDbContext contexto) : IUnidadDeTrabajo
{
    private const string ViolacionUnicidad = "23505";

    public async Task<int> ConfirmarAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Dos movimientos simultáneos sobre la misma cuenta: uno gana y el otro debe reintentar
            // con el saldo ya actualizado, nunca sobrescribir a ciegas.
            throw new ExcepcionConflicto(
                "CONFLICTO_CONCURRENCIA",
                "La cuenta fue modificada por otra operación al mismo tiempo. Vuelva a intentarlo.");
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException pg
                                                  && pg.SqlState == ViolacionUnicidad)
        {
            throw new ExcepcionConflicto("REGISTRO_DUPLICADO", pg.ConstraintName switch
            {
                "ux_cuentas_numero" => "Ya existe una cuenta con ese número.",
                _ => "El registro que intenta guardar ya existe.",
            });
        }
    }
}
