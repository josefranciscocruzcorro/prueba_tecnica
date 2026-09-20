namespace Shared.Kernel.Paginacion;

/// <summary>
/// Parámetros de paginación saneados: la página nunca baja de 1 y el tamaño se acota para
/// proteger a la base de datos de peticiones desmedidas (factor de rendimiento).
/// </summary>
public readonly record struct ConsultaPaginada
{
    public const int TamanoMaximo = 200;
    public const int TamanoPorDefecto = 20;

    public ConsultaPaginada(int? pagina, int? tamano)
    {
        Pagina = pagina is null or < 1 ? 1 : pagina.Value;
        Tamano = tamano switch
        {
            null or < 1 => TamanoPorDefecto,
            > TamanoMaximo => TamanoMaximo,
            _ => tamano.Value,
        };
    }

    public int Pagina { get; }

    public int Tamano { get; }

    public int Salto => (Pagina - 1) * Tamano;
}
