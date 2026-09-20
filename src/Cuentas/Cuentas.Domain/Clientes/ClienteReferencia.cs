namespace Cuentas.Domain.Clientes;

/// <summary>
/// Réplica local y de solo lectura del cliente, mantenida por los eventos que publica el
/// microservicio de Clientes. Es la clave de la independencia entre servicios: para abrir una
/// cuenta o emitir un reporte, Cuentas no llama a nadie —consulta su propia copia—, de modo que
/// sigue operando aunque Clientes esté caído.
/// </summary>
/// <remarks>
/// El precio es la consistencia eventual: durante los milisegundos que tarda el evento en llegar,
/// un cliente recién creado todavía no existe aquí. El caso de uso lo contempla y devuelve un
/// error explicativo en lugar de un fallo genérico.
/// </remarks>
public sealed class ClienteReferencia
{
    private ClienteReferencia()
    {
        // Constructor requerido por Entity Framework Core.
    }

    private ClienteReferencia(string clienteId, string nombre, string identificacion, bool estado, DateTime sincronizado)
    {
        ClienteId = clienteId;
        Nombre = nombre;
        Identificacion = identificacion;
        Estado = estado;
        SincronizadoEn = sincronizado;
    }

    public string ClienteId { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string Identificacion { get; private set; } = string.Empty;

    public bool Estado { get; private set; }

    /// <summary>Marca de la última réplica recibida. Útil para diagnosticar retrasos del bus.</summary>
    public DateTime SincronizadoEn { get; private set; }

    public static ClienteReferencia Crear(string clienteId, string nombre, string identificacion, bool estado) =>
        new(clienteId.Trim().ToUpperInvariant(), nombre, identificacion, estado, DateTime.UtcNow);

    public void Sincronizar(string nombre, string identificacion, bool estado)
    {
        Nombre = nombre;
        Identificacion = identificacion;
        Estado = estado;
        SincronizadoEn = DateTime.UtcNow;
    }

    /// <summary>
    /// La baja del cliente no borra la réplica: las cuentas y sus movimientos son un registro
    /// contable que debe conservarse. Se marca como inactiva para bloquear nuevas operaciones.
    /// </summary>
    public void MarcarComoBaja()
    {
        Estado = false;
        SincronizadoEn = DateTime.UtcNow;
    }
}
