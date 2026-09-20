namespace Shared.Kernel.Paginacion;

/// <summary>Página de resultados con los metadatos mínimos para que el cliente navegue el listado.</summary>
public sealed record PaginaDe<T>(IReadOnlyList<T> Elementos, int Pagina, int Tamano, long Total)
{
    public int TotalPaginas => Tamano <= 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);

    public bool TieneSiguiente => Pagina < TotalPaginas;

    public bool TieneAnterior => Pagina > 1;

    public static PaginaDe<T> Vacia(int pagina, int tamano) => new([], pagina, tamano, 0);
}
