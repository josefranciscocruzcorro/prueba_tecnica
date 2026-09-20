using Clientes.Infrastructure.Seguridad;

namespace Clientes.UnitTests.Seguridad;

/// <summary>
/// La derivación de contraseñas es la pieza de seguridad más sensible del servicio, así que se
/// prueba su contrato completo: irreversibilidad, sal por usuario y tolerancia a datos corruptos.
/// </summary>
public sealed class PruebasHashContrasena
{
    private readonly ServicioHashContrasenaPbkdf2 _servicio = new();

    [Fact]
    public void Derivar_NoDevuelveLaContrasenaEnClaro()
    {
        var hash = _servicio.Derivar("1234");

        hash.Should().NotContain("1234");
        hash.Should().StartWith("pbkdf2-sha256$");
    }

    [Fact]
    public void Derivar_DosVeces_ProduceHashesDistintosPorLaSalAleatoria()
    {
        var primero = _servicio.Derivar("1234");
        var segundo = _servicio.Derivar("1234");

        primero.Should().NotBe(segundo, "cada derivación usa una sal nueva");
        _servicio.Verificar("1234", primero).Should().BeTrue();
        _servicio.Verificar("1234", segundo).Should().BeTrue();
    }

    [Fact]
    public void Verificar_ConLaContrasenaCorrecta_DevuelveVerdadero() =>
        _servicio.Verificar("5678", _servicio.Derivar("5678")).Should().BeTrue();

    [Theory]
    [InlineData("incorrecta")]
    [InlineData("5677")]
    [InlineData("")]
    public void Verificar_ConLaContrasenaIncorrecta_DevuelveFalso(string intento) =>
        _servicio.Verificar(intento, _servicio.Derivar("5678")).Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano-sin-formato")]
    [InlineData("pbkdf2-sha256$abc$sal$hash")]
    [InlineData("pbkdf2-sha256$210000$no-es-base64$tampoco")]
    public void Verificar_ConUnHashCorrupto_DevuelveFalsoEnLugarDeFallar(string hashCorrupto)
    {
        var resultado = () => _servicio.Verificar("1234", hashCorrupto);

        resultado.Should().NotThrow("un hash ilegible es una credencial inválida, no un error del servidor");
        resultado().Should().BeFalse();
    }
}
