using Clientes.Domain.Clientes;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Paginacion;

namespace Clientes.Infrastructure.Persistencia;

/// <summary>
/// Implementación del patrón Repository sobre Entity Framework Core. Concentra aquí todo el
/// conocimiento de LINQ-to-SQL: ninguna otra capa construye consultas, de modo que el modelo de
/// acceso a datos puede cambiar sin tocar los casos de uso.
/// </summary>
public sealed class RepositorioClientes(ClientesDbContext contexto) : IRepositorioClientes
{
    public async Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default) =>
        await contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancelacion);

    public async Task<Cliente?> ObtenerPorClienteIdAsync(string clienteId, CancellationToken cancelacion = default)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();

        return await contexto.Clientes.FirstOrDefaultAsync(c => c.ClienteId == normalizado, cancelacion);
    }

    public async Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken cancelacion = default) =>
        await contexto.Clientes.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(cancelacion);

    public async Task<bool> ExisteClienteIdAsync(
        string clienteId, Guid? excluyendoId = null, CancellationToken cancelacion = default)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();

        return await contexto.Clientes
            .AsNoTracking()
            .AnyAsync(c => c.ClienteId == normalizado && (excluyendoId == null || c.Id != excluyendoId), cancelacion);
    }

    public async Task<bool> ExisteIdentificacionAsync(
        string identificacion, Guid? excluyendoId = null, CancellationToken cancelacion = default)
    {
        var normalizada = identificacion.Trim();

        return await contexto.Personas
            .AsNoTracking()
            .AnyAsync(p => p.Identificacion == normalizada && (excluyendoId == null || p.Id != excluyendoId), cancelacion);
    }

    public async Task<PaginaDe<Cliente>> BuscarAsync(
        string? termino,
        bool? estado,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var patron = $"%{termino.Trim()}%";

            // ILIKE se traduce al operador nativo de PostgreSQL: búsqueda sin distinguir mayúsculas
            // que aprovecha el índice de texto sin traer las filas a memoria.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Nombre, patron)
                || EF.Functions.ILike(c.Identificacion, patron)
                || EF.Functions.ILike(c.ClienteId, patron));
        }

        if (estado is not null)
        {
            consulta = consulta.Where(c => c.Estado == estado.Value);
        }

        var total = await consulta.LongCountAsync(cancelacion);

        if (total == 0)
        {
            return PaginaDe<Cliente>.Vacia(paginacion.Pagina, paginacion.Tamano);
        }

        var elementos = await consulta
            .OrderBy(c => c.Nombre)
            .ThenBy(c => c.ClienteId)
            .Skip(paginacion.Salto)
            .Take(paginacion.Tamano)
            .ToListAsync(cancelacion);

        return new PaginaDe<Cliente>(elementos, paginacion.Pagina, paginacion.Tamano, total);
    }

    public async Task AgregarAsync(Cliente entidad, CancellationToken cancelacion = default) =>
        await contexto.Clientes.AddAsync(entidad, cancelacion);

    public void Actualizar(Cliente entidad) => contexto.Clientes.Update(entidad);

    public void Eliminar(Cliente entidad) => contexto.Clientes.Remove(entidad);
}
