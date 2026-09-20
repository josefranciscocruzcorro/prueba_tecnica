using Cuentas.Domain.Clientes;
using Cuentas.Domain.Cuentas;
using Cuentas.Domain.Movimientos;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Paginacion;

namespace Cuentas.Infrastructure.Persistencia;

/// <summary>Repositorio del agregado Cuenta sobre Entity Framework Core.</summary>
public sealed class RepositorioCuentas(CuentasDbContext contexto) : IRepositorioCuentas
{
    public async Task<Cuenta?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default) =>
        await contexto.Cuentas.FirstOrDefaultAsync(c => c.Id == id, cancelacion);

    public async Task<Cuenta?> ObtenerPorNumeroAsync(string numeroCuenta, CancellationToken cancelacion = default) =>
        await contexto.Cuentas.FirstOrDefaultAsync(c => c.NumeroCuenta == numeroCuenta.Trim(), cancelacion);

    /// <summary>
    /// Carga la cuenta junto a toda su serie de movimientos. Es obligatoria antes de asentar o
    /// corregir apuntes porque el agregado recalcula el saldo sobre el histórico completo.
    /// </summary>
    public async Task<Cuenta?> ObtenerConMovimientosAsync(
        string numeroCuenta, CancellationToken cancelacion = default) =>
        await contexto.Cuentas
            .Include(c => c.Movimientos)
            .FirstOrDefaultAsync(c => c.NumeroCuenta == numeroCuenta.Trim(), cancelacion);

    public async Task<bool> ExisteNumeroAsync(string numeroCuenta, CancellationToken cancelacion = default) =>
        await contexto.Cuentas.AsNoTracking().AnyAsync(c => c.NumeroCuenta == numeroCuenta.Trim(), cancelacion);

    public async Task<IReadOnlyList<Cuenta>> ListarAsync(CancellationToken cancelacion = default) =>
        await contexto.Cuentas.AsNoTracking().OrderBy(c => c.NumeroCuenta).ToListAsync(cancelacion);

    public async Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(
        string clienteId, CancellationToken cancelacion = default)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();

        return await contexto.Cuentas
            .AsNoTracking()
            .Where(c => c.ClienteId == normalizado)
            .OrderBy(c => c.NumeroCuenta)
            .ToListAsync(cancelacion);
    }

    public async Task<PaginaDe<Cuenta>> BuscarAsync(
        string? clienteId,
        string? termino,
        bool? estado,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Cuentas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(clienteId))
        {
            var normalizado = clienteId.Trim().ToUpperInvariant();
            consulta = consulta.Where(c => c.ClienteId == normalizado);
        }

        if (!string.IsNullOrWhiteSpace(termino))
        {
            var patron = $"%{termino.Trim()}%";
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.NumeroCuenta, patron) || EF.Functions.ILike(c.ClienteId, patron));
        }

        if (estado is not null)
        {
            consulta = consulta.Where(c => c.Estado == estado.Value);
        }

        var total = await consulta.LongCountAsync(cancelacion);

        if (total == 0)
        {
            return PaginaDe<Cuenta>.Vacia(paginacion.Pagina, paginacion.Tamano);
        }

        var elementos = await consulta
            .OrderBy(c => c.NumeroCuenta)
            .Skip(paginacion.Salto)
            .Take(paginacion.Tamano)
            .ToListAsync(cancelacion);

        return new PaginaDe<Cuenta>(elementos, paginacion.Pagina, paginacion.Tamano, total);
    }

    public async Task AgregarAsync(Cuenta entidad, CancellationToken cancelacion = default) =>
        await contexto.Cuentas.AddAsync(entidad, cancelacion);

    public void Actualizar(Cuenta entidad) => contexto.Cuentas.Update(entidad);

    public void Eliminar(Cuenta entidad) => contexto.Cuentas.Remove(entidad);
}

/// <summary>Consultas de movimientos que no necesitan cargar el agregado completo.</summary>
public sealed class RepositorioMovimientos(CuentasDbContext contexto) : IRepositorioMovimientos
{
    public async Task<Movimiento?> ObtenerPorIdAsync(Guid movimientoId, CancellationToken cancelacion = default) =>
        await contexto.Movimientos.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movimientoId, cancelacion);

    public async Task<PaginaDe<(Movimiento Movimiento, Cuenta Cuenta)>> BuscarAsync(
        string? numeroCuenta,
        string? clienteId,
        DateTime? desde,
        DateTime? hasta,
        ConsultaPaginada paginacion,
        CancellationToken cancelacion = default)
    {
        // Se compone una única consulta con join: una sola ida a la base devuelve el movimiento y
        // los datos de su cuenta, sin la cascada de consultas que produciría recorrer y resolver.
        var consulta =
            from movimiento in contexto.Movimientos.AsNoTracking()
            join cuenta in contexto.Cuentas.AsNoTracking() on movimiento.CuentaId equals cuenta.Id
            select new { movimiento, cuenta };

        if (!string.IsNullOrWhiteSpace(numeroCuenta))
        {
            var numero = numeroCuenta.Trim();
            consulta = consulta.Where(x => x.cuenta.NumeroCuenta == numero);
        }

        if (!string.IsNullOrWhiteSpace(clienteId))
        {
            var normalizado = clienteId.Trim().ToUpperInvariant();
            consulta = consulta.Where(x => x.cuenta.ClienteId == normalizado);
        }

        if (desde is not null)
        {
            var inicio = ComoUtc(desde.Value);
            consulta = consulta.Where(x => x.movimiento.Fecha >= inicio);
        }

        if (hasta is not null)
        {
            var fin = ComoUtc(hasta.Value);
            consulta = consulta.Where(x => x.movimiento.Fecha <= fin);
        }

        var total = await consulta.LongCountAsync(cancelacion);

        if (total == 0)
        {
            return PaginaDe<(Movimiento, Cuenta)>.Vacia(paginacion.Pagina, paginacion.Tamano);
        }

        var filas = await consulta
            .OrderByDescending(x => x.movimiento.Fecha)
            .ThenByDescending(x => x.movimiento.Secuencia)
            .Skip(paginacion.Salto)
            .Take(paginacion.Tamano)
            .ToListAsync(cancelacion);

        return new PaginaDe<(Movimiento, Cuenta)>(
            [.. filas.Select(f => (f.movimiento, f.cuenta))], paginacion.Pagina, paginacion.Tamano, total);
    }

    public async Task<IReadOnlyList<Movimiento>> ListarParaReporteAsync(
        IReadOnlyCollection<Guid> cuentaIds,
        DateTime desde,
        DateTime hasta,
        CancellationToken cancelacion = default)
    {
        if (cuentaIds.Count == 0)
        {
            return [];
        }

        return await contexto.Movimientos
            .AsNoTracking()
            .Where(m => cuentaIds.Contains(m.CuentaId) && m.Fecha >= desde && m.Fecha <= hasta)
            .OrderBy(m => m.CuentaId)
            .ThenBy(m => m.Fecha)
            .ThenBy(m => m.Secuencia)
            .ToListAsync(cancelacion);
    }

    /// <summary>PostgreSQL rechaza los <c>timestamptz</c> sin zona: se normaliza a UTC.</summary>
    private static DateTime ComoUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };
}

/// <summary>Acceso a la réplica local de clientes.</summary>
public sealed class RepositorioClientesReplicados(CuentasDbContext contexto) : IRepositorioClientesReplicados
{
    public async Task<ClienteReferencia?> ObtenerAsync(string clienteId, CancellationToken cancelacion = default)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();

        return await contexto.ClientesReplicados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClienteId == normalizado, cancelacion);
    }

    public async Task<IReadOnlyList<ClienteReferencia>> ListarAsync(CancellationToken cancelacion = default) =>
        await contexto.ClientesReplicados.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(cancelacion);

    public async Task GuardarAsync(ClienteReferencia cliente, CancellationToken cancelacion = default)
    {
        var existente = await contexto.ClientesReplicados
            .FirstOrDefaultAsync(c => c.ClienteId == cliente.ClienteId, cancelacion);

        if (existente is null)
        {
            await contexto.ClientesReplicados.AddAsync(cliente, cancelacion);
            return;
        }

        existente.Sincronizar(cliente.Nombre, cliente.Identificacion, cliente.Estado);
    }
}
