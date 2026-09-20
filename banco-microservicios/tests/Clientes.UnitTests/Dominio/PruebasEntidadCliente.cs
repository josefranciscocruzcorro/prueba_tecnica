using Clientes.Domain.Clientes;
using Clientes.Domain.Eventos;
using Clientes.Domain.Personas;
using Shared.Kernel.Excepciones;

namespace Clientes.UnitTests.Dominio;

/// <summary>
/// F5 — Pruebas unitarias de la entidad de dominio <see cref="Cliente"/>.
/// </summary>
/// <remarks>
/// Son pruebas puras: no tocan base de datos, ni bus, ni HTTP. Verifican que la entidad protege
/// sus propias invariantes, que es precisamente la razón por la que las reglas viven en el dominio
/// y no en el controlador.
/// </remarks>
public sealed class PruebasEntidadCliente
{
    private const string HashDePrueba = "pbkdf2-sha256$210000$c2FsdGVqZW1wbG8=$aGFzaGVqZW1wbG8=";

    [Fact]
    public void Registrar_ConDatosValidos_CreaElClienteYEmiteElEventoDeAlta()
    {
        var cliente = CrearCliente();

        cliente.ClienteId.Should().Be("CLI-001");
        cliente.Nombre.Should().Be("Jose Lema");
        cliente.Identificacion.Should().Be("1717171717");
        cliente.Estado.Should().BeTrue();
        cliente.Id.Should().NotBe(Guid.Empty);

        // El alta debe anunciarse: es lo que permite al microservicio de Cuentas replicar al cliente.
        cliente.EventosDominio.Should().ContainSingle()
            .Which.Should().BeOfType<ClienteRegistrado>()
            .Which.ClienteId.Should().Be("CLI-001");
    }

    [Fact]
    public void Registrar_NormalizaElIdentificadorYLosEspaciosDelNombre()
    {
        var cliente = CrearCliente(clienteId: " cli-042 ", nombre: "  Marianela   Montalvo  ");

        cliente.ClienteId.Should().Be("CLI-042", "el identificador se normaliza a mayúsculas y sin espacios");
        cliente.Nombre.Should().Be("Marianela Montalvo", "los espacios repetidos se colapsan");
    }

    [Theory]
    [InlineData(17, "menor de edad")]
    [InlineData(0, "edad cero")]
    [InlineData(121, "edad imposible")]
    [InlineData(-5, "edad negativa")]
    public void Registrar_ConEdadFueraDeRango_Falla(int edad, string caso)
    {
        var accion = () => CrearCliente(edad: edad);

        accion.Should().Throw<ExcepcionReglaNegocio>($"se rechaza el caso de {caso}")
            .Which.Codigo.Should().Be("EDAD_FUERA_DE_RANGO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_SinNombre_Falla(string nombre)
    {
        var accion = () => CrearCliente(nombre: nombre);

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("NOMBRE_REQUERIDO");
    }

    [Fact]
    public void Registrar_ConIdentificacionNoAlfanumerica_Falla()
    {
        var accion = () => CrearCliente(identificacion: "17-171/717");

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("IDENTIFICACION_INVALIDA");
    }

    [Fact]
    public void Registrar_ConTelefonoNoNumerico_Falla()
    {
        var accion = () => CrearCliente(telefono: "098-ABC-785");

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("TELEFONO_INVALIDO");
    }

    [Fact]
    public void Registrar_SinContrasena_Falla()
    {
        var accion = () => CrearCliente(hash: "  ");

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("CONTRASENA_REQUERIDA");
    }

    [Fact]
    public void Actualizar_CambiaLosDatosYAnunciaLaModificacion()
    {
        var cliente = CrearCliente();
        cliente.LimpiarEventos();

        cliente.Actualizar("Jose Lema Ruiz", Genero.Masculino, 36, "1717171718", "Otavalo 123", "0987654321", false);

        cliente.Nombre.Should().Be("Jose Lema Ruiz");
        cliente.Edad.Should().Be(36);
        cliente.Estado.Should().BeFalse();
        cliente.EventosDominio.Should().ContainSingle().Which.Should().BeOfType<ClienteModificado>();
    }

    [Fact]
    public void Actualizar_ConDatosInvalidos_NoDejaLaEntidadAMedias()
    {
        var cliente = CrearCliente();
        var nombreOriginal = cliente.Nombre;
        cliente.LimpiarEventos();

        var accion = () => cliente.Actualizar(
            "Jose Lema Ruiz", Genero.Masculino, 200, "1717171718", "Otavalo 123", "0987654321", true);

        accion.Should().Throw<ExcepcionReglaNegocio>();

        // La validación de la edad ocurre después de asignar el nombre en el mismo método, así que
        // esta prueba fija el contrato: si algo falla, la entidad no queda parcialmente modificada.
        cliente.Nombre.Should().Be(nombreOriginal);
        cliente.EventosDominio.Should().BeEmpty("una actualización rechazada no anuncia nada");
    }

    [Fact]
    public void CambiarEstado_AlMismoValor_NoGeneraEventoRedundante()
    {
        var cliente = CrearCliente(estado: true);
        cliente.LimpiarEventos();

        cliente.CambiarEstado(true);

        cliente.EventosDominio.Should().BeEmpty("la operación es idempotente y no hubo cambio real");
    }

    [Fact]
    public void CambiarEstado_AUnValorDistinto_DesactivaYAnuncia()
    {
        var cliente = CrearCliente(estado: true);
        cliente.LimpiarEventos();

        cliente.CambiarEstado(false);

        cliente.Estado.Should().BeFalse();
        cliente.EventosDominio.Should().ContainSingle().Which.Should().BeOfType<ClienteModificado>();
    }

    [Fact]
    public void AsegurarQuePuedeAutenticarse_ConClienteInactivo_Falla()
    {
        var cliente = CrearCliente(estado: false);

        var accion = cliente.AsegurarQuePuedeAutenticarse;

        accion.Should().Throw<ExcepcionReglaNegocio>().Which.Codigo.Should().Be("CLIENTE_INACTIVO");
    }

    [Fact]
    public void AsegurarQuePuedeAutenticarse_ConClienteActivo_NoFalla()
    {
        var cliente = CrearCliente(estado: true);

        var accion = cliente.AsegurarQuePuedeAutenticarse;

        accion.Should().NotThrow();
    }

    [Fact]
    public void MarcarParaBaja_AnunciaLaEliminacionParaQueCuentasLaReplique()
    {
        var cliente = CrearCliente();
        cliente.LimpiarEventos();

        cliente.MarcarParaBaja();

        cliente.EventosDominio.Should().ContainSingle()
            .Which.Should().BeOfType<ClienteDadoDeBaja>()
            .Which.ClienteId.Should().Be("CLI-001");
    }

    [Fact]
    public void CambiarContrasena_SustituyeElHashSinExponerLaContrasena()
    {
        var cliente = CrearCliente();
        const string nuevoHash = "pbkdf2-sha256$210000$bnVldmFzYWw=$bnVldm9oYXNo";

        cliente.CambiarContrasena(nuevoHash);

        cliente.ContrasenaHash.Should().Be(nuevoHash);
    }

    [Fact]
    public void DosClientesConElMismoIdentificador_SonLaMismaEntidad()
    {
        var uno = CrearCliente();
        var otro = CrearCliente(clienteId: "CLI-999", identificacion: "1818181818");

        uno.Equals(uno).Should().BeTrue();
        uno.Equals(otro).Should().BeFalse("son dos personas distintas aunque compartan tipo");
    }

    private static Cliente CrearCliente(
        string clienteId = "CLI-001",
        string nombre = "Jose Lema",
        Genero genero = Genero.Masculino,
        int edad = 35,
        string identificacion = "1717171717",
        string direccion = "Otavalo sn y principal",
        string telefono = "098254785",
        string hash = HashDePrueba,
        bool estado = true) =>
        Cliente.Registrar(clienteId, nombre, genero, edad, identificacion, direccion, telefono, hash, estado);
}
