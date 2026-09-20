using Clientes.Application.Actividad;
using Microsoft.EntityFrameworkCore;

namespace Clientes.Infrastructure.Persistencia;

/// <summary>Proyección directa a DTO: no materializa entidades ni activa el rastreo de cambios.</summary>
public sealed class ConsultaActividad(ClientesDbContext contexto) : IConsultaActividad
{
    private const int LimiteMaximo = 100;

    public async Task<IReadOnlyList<ActividadDto>> ObtenerUltimasAsync(
        string clienteId, int limite, CancellationToken cancelacion = default)
    {
        var normalizado = clienteId.Trim().ToUpperInvariant();
        var tope = Math.Clamp(limite, 1, LimiteMaximo);

        return await contexto.Actividades
            .AsNoTracking()
            .Where(a => a.ClienteId == normalizado)
            .OrderByDescending(a => a.OcurridoEn)
            .Take(tope)
            .Select(a => new ActividadDto(a.Id, a.Tipo, a.Descripcion, a.OcurridoEn))
            .ToListAsync(cancelacion);
    }
}
