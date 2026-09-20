using System.Security.Cryptography;
using Clientes.Application.Abstracciones;

namespace Clientes.Infrastructure.Seguridad;

/// <summary>
/// Derivación de contraseñas con PBKDF2-HMAC-SHA256, sal aleatoria por usuario y comparación en
/// tiempo constante. Se eligió una primitiva del propio framework para no añadir dependencias
/// criptográficas de terceros a un servicio que ya maneja datos sensibles.
/// </summary>
/// <remarks>
/// El hash se guarda autodescrito (<c>pbkdf2-sha256$iteraciones$sal$hash</c>). Así, cuando haya
/// que subir el coste o cambiar de algoritmo, los hashes antiguos se siguen verificando y pueden
/// re-derivarse de forma transparente en el siguiente inicio de sesión.
/// </remarks>
public sealed class ServicioHashContrasenaPbkdf2 : IServicioHashContrasena
{
    private const string Etiqueta = "pbkdf2-sha256";
    private const int Iteraciones = 210_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;
    private const char Separador = '$';

    public string Derivar(string contrasenaEnClaro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasenaEnClaro);

        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasenaEnClaro, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);

        return string.Join(Separador, Etiqueta, Iteraciones, Convert.ToBase64String(sal), Convert.ToBase64String(hash));
    }

    public bool Verificar(string contrasenaEnClaro, string hashAlmacenado)
    {
        if (string.IsNullOrWhiteSpace(contrasenaEnClaro) || string.IsNullOrWhiteSpace(hashAlmacenado))
        {
            return false;
        }

        var partes = hashAlmacenado.Split(Separador);

        if (partes.Length != 4 || partes[0] != Etiqueta || !int.TryParse(partes[1], out var iteraciones))
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(
                contrasenaEnClaro, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);

            // Comparación en tiempo constante: no filtra información por el tiempo de respuesta.
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            // Hash corrupto o con formato ajeno: se trata como credencial inválida, no como error.
            return false;
        }
    }
}
