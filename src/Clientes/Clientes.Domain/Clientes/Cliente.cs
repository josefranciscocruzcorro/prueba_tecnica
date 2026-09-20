using Clientes.Domain.Eventos;
using Clientes.Domain.Personas;
using Shared.Kernel.Excepciones;

namespace Clientes.Domain.Clientes;

/// <summary>
/// Cliente del banco: una <see cref="Persona"/> con credenciales y estado operativo. Es la raíz de
/// agregado del microservicio y la única puerta de entrada para modificar sus datos.
/// </summary>
/// <remarks>
/// La entidad nunca conoce la contraseña en claro: recibe siempre un hash ya calculado por la capa
/// de aplicación. Así el dominio permanece libre de dependencias criptográficas y comprobable.
/// </remarks>
public sealed class Cliente : Persona
{
    public const int LargoMinimoContrasena = 4;

    private Cliente()
    {
        // Constructor requerido por Entity Framework Core.
    }

    private Cliente(
        Guid id,
        string clienteId,
        string nombre,
        Genero genero,
        int edad,
        string identificacion,
        string direccion,
        string telefono,
        string contrasenaHash,
        bool estado)
        : base(id, nombre, genero, edad, identificacion, direccion, telefono)
    {
        ClienteId = ValidarClienteId(clienteId);
        ContrasenaHash = ValidarHash(contrasenaHash);
        Estado = estado;
    }

    /// <summary>
    /// Clave única de negocio del cliente. Es el identificador que viaja a otros microservicios,
    /// de modo que Cuentas nunca depende de la clave primaria física de este contexto.
    /// </summary>
    public string ClienteId { get; private set; } = string.Empty;

    /// <summary>Contraseña derivada (PBKDF2). Nunca se expone en ningún DTO.</summary>
    public string ContrasenaHash { get; private set; } = string.Empty;

    /// <summary>Indica si el cliente puede operar. Un cliente inactivo no abre cuentas ni transacciona.</summary>
    public bool Estado { get; private set; }

    public DateTime CreadoEn { get; private set; } = DateTime.UtcNow;

    public DateTime ActualizadoEn { get; private set; } = DateTime.UtcNow;

    /// <summary>Única forma de crear un cliente: garantiza invariantes y emite el evento de alta.</summary>
    public static Cliente Registrar(
        string clienteId,
        string nombre,
        Genero genero,
        int edad,
        string identificacion,
        string direccion,
        string telefono,
        string contrasenaHash,
        bool estado = true)
    {
        var cliente = new Cliente(
            Guid.NewGuid(), clienteId, nombre, genero, edad, identificacion, direccion, telefono, contrasenaHash, estado);

        cliente.RegistrarEvento(new ClienteRegistrado(
            cliente.Id, cliente.ClienteId, cliente.Nombre, cliente.Identificacion, cliente.Estado));

        return cliente;
    }

    /// <summary>Reemplaza los datos editables del cliente (semántica PUT).</summary>
    public void Actualizar(
        string nombre,
        Genero genero,
        int edad,
        string identificacion,
        string direccion,
        string telefono,
        bool estado)
    {
        EstablecerDatosPersonales(nombre, genero, edad, identificacion, direccion, telefono);
        Estado = estado;
        MarcarModificado();
    }

    /// <summary>Cambia el estado operativo. No hace nada si ya está en el estado pedido (idempotente).</summary>
    public void CambiarEstado(bool activo)
    {
        if (Estado == activo)
        {
            return;
        }

        Estado = activo;
        MarcarModificado();
    }

    /// <summary>Sustituye la credencial por un nuevo hash.</summary>
    public void CambiarContrasena(string nuevoHash)
    {
        ContrasenaHash = ValidarHash(nuevoHash);
        ActualizadoEn = DateTime.UtcNow;
    }

    /// <summary>
    /// Regla de acceso: solo un cliente activo puede autenticarse. Se comprueba en el dominio para
    /// que ninguna vía alternativa (API, pruebas, scripts) pueda saltársela.
    /// </summary>
    public void AsegurarQuePuedeAutenticarse()
    {
        if (!Estado)
        {
            throw new ExcepcionReglaNegocio("CLIENTE_INACTIVO", "El cliente se encuentra inactivo.");
        }
    }

    /// <summary>Emite el evento de baja para que los contextos suscritos actualicen su réplica.</summary>
    public void MarcarParaBaja() => RegistrarEvento(new ClienteDadoDeBaja(ClienteId));

    private void MarcarModificado()
    {
        ActualizadoEn = DateTime.UtcNow;
        RegistrarEvento(new ClienteModificado(Id, ClienteId, Nombre, Identificacion, Estado));
    }

    private static string ValidarClienteId(string clienteId)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ExcepcionReglaNegocio("CLIENTEID_REQUERIDO", "El campo clienteId es obligatorio.");
        }

        var limpio = clienteId.Trim().ToUpperInvariant();

        if (limpio.Length > 30)
        {
            throw new ExcepcionReglaNegocio("CLIENTEID_MUY_LARGO", "El campo clienteId no puede superar 30 caracteres.");
        }

        return limpio.All(c => char.IsLetterOrDigit(c) || c == '-')
            ? limpio
            : throw new ExcepcionReglaNegocio(
                "CLIENTEID_INVALIDO",
                "El campo clienteId solo admite letras, números y guiones.");
    }

    private static string ValidarHash(string hash) => string.IsNullOrWhiteSpace(hash)
        ? throw new ExcepcionReglaNegocio("CONTRASENA_REQUERIDA", "La contraseña es obligatoria.")
        : hash;
}
