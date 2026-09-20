namespace Clientes.Domain.Personas;

/// <summary>Género declarado por la persona. Se persiste como texto para que la base sea legible.</summary>
public enum Genero
{
    Masculino = 1,
    Femenino = 2,
    Otro = 3,
    NoDeclarado = 4,
}
