namespace Clientes.Application.Abstracciones;

/// <summary>
/// Deriva y verifica contraseñas. Es un puerto: el algoritmo concreto (PBKDF2, Argon2, bcrypt…)
/// vive en la infraestructura y puede sustituirse sin tocar dominio ni casos de uso.
/// </summary>
public interface IServicioHashContrasena
{
    string Derivar(string contrasenaEnClaro);

    bool Verificar(string contrasenaEnClaro, string hashAlmacenado);
}
