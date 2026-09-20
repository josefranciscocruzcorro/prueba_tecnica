using Shared.Kernel.Dominio;
using Shared.Kernel.Excepciones;

namespace Clientes.Domain.Personas;

/// <summary>
/// Datos de una persona física. Es la clase base de la que hereda <c>Cliente</c> tal como exige el
/// enunciado; se mapea con herencia tabla-por-tipo para que el esquema relacional refleje la
/// jerarquía sin columnas nulas ni duplicación de atributos.
/// </summary>
/// <remarks>
/// La clase es responsable de sus propias invariantes: no existe forma de construir una persona
/// en estado inválido, ni desde la API ni desde las pruebas.
/// </remarks>
public abstract class Persona : Entidad<Guid>
{
    public const int EdadMinima = 18;
    public const int EdadMaxima = 120;
    public const int LargoMaximoNombre = 120;

    protected Persona()
    {
        // Constructor requerido por Entity Framework Core para materializar la entidad.
    }

    protected Persona(
        Guid id,
        string nombre,
        Genero genero,
        int edad,
        string identificacion,
        string direccion,
        string telefono)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        EstablecerDatosPersonales(nombre, genero, edad, identificacion, direccion, telefono);
    }

    public string Nombre { get; private set; } = string.Empty;

    public Genero Genero { get; private set; }

    public int Edad { get; private set; }

    /// <summary>Documento de identidad. Es único en todo el microservicio.</summary>
    public string Identificacion { get; private set; } = string.Empty;

    public string Direccion { get; private set; } = string.Empty;

    public string Telefono { get; private set; } = string.Empty;

    /// <summary>
    /// Actualiza los datos personales respetando las mismas invariantes del alta. Primero valida
    /// todo y solo después asigna: si algún campo es inválido la entidad no queda a medias, que es
    /// justo el estado inconsistente que una actualización campo a campo produciría.
    /// </summary>
    protected void EstablecerDatosPersonales(
        string nombre,
        Genero genero,
        int edad,
        string identificacion,
        string direccion,
        string telefono)
    {
        var nombreValidado = NormalizarNombre(nombre);
        var edadValidada = ValidarEdad(edad);
        var identificacionValidada = ValidarIdentificacion(identificacion);
        var direccionValidada = ValidarTexto(direccion, nameof(Direccion), 200);
        var telefonoValidado = ValidarTelefono(telefono);

        Nombre = nombreValidado;
        Genero = genero;
        Edad = edadValidada;
        Identificacion = identificacionValidada;
        Direccion = direccionValidada;
        Telefono = telefonoValidado;
    }

    private static string NormalizarNombre(string nombre)
    {
        var limpio = ValidarTexto(nombre, nameof(Nombre), LargoMaximoNombre);

        // Colapsa espacios repetidos: "Jose   Lema" y "Jose Lema" son la misma persona.
        return string.Join(' ', limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int ValidarEdad(int edad) => edad is >= EdadMinima and <= EdadMaxima
        ? edad
        : throw new ExcepcionReglaNegocio(
            "EDAD_FUERA_DE_RANGO",
            $"La edad debe estar entre {EdadMinima} y {EdadMaxima} años.");

    private static string ValidarIdentificacion(string identificacion)
    {
        var limpia = ValidarTexto(identificacion, nameof(Identificacion), 20);

        return limpia.All(char.IsLetterOrDigit)
            ? limpia
            : throw new ExcepcionReglaNegocio(
                "IDENTIFICACION_INVALIDA",
                "La identificación solo admite letras y números.");
    }

    private static string ValidarTelefono(string telefono)
    {
        var limpio = ValidarTexto(telefono, nameof(Telefono), 20);

        return limpio.All(c => char.IsDigit(c) || c is '+' or '-' or ' ')
            ? limpio
            : throw new ExcepcionReglaNegocio(
                "TELEFONO_INVALIDO",
                "El teléfono solo admite dígitos y los símbolos '+' y '-'.");
    }

    private static string ValidarTexto(string valor, string campo, int largoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ExcepcionReglaNegocio($"{campo.ToUpperInvariant()}_REQUERIDO", $"El campo {campo} es obligatorio.");
        }

        var limpio = valor.Trim();

        return limpio.Length <= largoMaximo
            ? limpio
            : throw new ExcepcionReglaNegocio(
                $"{campo.ToUpperInvariant()}_MUY_LARGO",
                $"El campo {campo} no puede superar {largoMaximo} caracteres.");
    }
}
