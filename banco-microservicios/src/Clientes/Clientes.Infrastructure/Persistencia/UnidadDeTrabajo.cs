using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Kernel.Excepciones;
using Shared.Kernel.Persistencia;

namespace Clientes.Infrastructure.Persistencia;

/// <summary>
/// Confirma la unidad de trabajo y traduce los fallos de integridad de PostgreSQL a excepciones
/// de dominio. Sin esta traducción, una condición de carrera entre dos altas simultáneas con la
/// misma identificación acabaría como un 500; aquí se convierte en un 409 correcto.
/// </summary>
public sealed class UnidadDeTrabajo(ClientesDbContext contexto) : IUnidadDeTrabajo
{
    /// <summary>Código SQLSTATE de PostgreSQL para violación de restricción única.</summary>
    private const string ViolacionUnicidad = "23505";

    public async Task<int> ConfirmarAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException pg
                                                  && pg.SqlState == ViolacionUnicidad)
        {
            throw new ExcepcionConflicto("REGISTRO_DUPLICADO", DescribirDuplicado(pg.ConstraintName));
        }
    }

    private static string DescribirDuplicado(string? restriccion) => restriccion switch
    {
        "ux_personas_identificacion" => "Ya existe una persona registrada con esa identificación.",
        "ux_clientes_cliente_id" => "Ya existe un cliente con ese identificador.",
        _ => "El registro que intenta guardar ya existe.",
    };
}
