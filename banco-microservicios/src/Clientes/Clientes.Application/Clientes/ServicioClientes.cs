using Clientes.Application.Abstracciones;
using Clientes.Application.Clientes.Dtos;
using Clientes.Domain.Clientes;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Excepciones;
using Shared.Kernel.Paginacion;
using Shared.Kernel.Persistencia;

namespace Clientes.Application.Clientes;

/// <summary>
/// Orquesta los casos de uso del maestro de clientes. No contiene reglas de negocio —esas viven en
/// la entidad <see cref="Cliente"/>—: su trabajo es coordinar repositorio, unidad de trabajo y
/// servicios de apoyo, y traducir el resultado a DTOs.
/// </summary>
public sealed class ServicioClientes(
    IRepositorioClientes repositorio,
    IUnidadDeTrabajo unidadDeTrabajo,
    IServicioHashContrasena hash,
    IGeneradorTokens generadorTokens,
    ILogger<ServicioClientes> registro) : IServicioClientes
{
    private const int IntentosGeneracionCodigo = 5;

    public async Task<PaginaDe<ClienteDto>> ListarAsync(
        string? termino, bool? estado, ConsultaPaginada paginacion, CancellationToken cancelacion = default)
    {
        var pagina = await repositorio.BuscarAsync(termino, estado, paginacion, cancelacion);

        return new PaginaDe<ClienteDto>(
            [.. pagina.Elementos.Select(ClienteDto.Desde)], pagina.Pagina, pagina.Tamano, pagina.Total);
    }

    public async Task<ClienteDto> ObtenerAsync(string clienteId, CancellationToken cancelacion = default) =>
        ClienteDto.Desde(await ObtenerObligatorioAsync(clienteId, cancelacion));

    public async Task<ClienteDto> CrearAsync(CrearClienteSolicitud solicitud, CancellationToken cancelacion = default)
    {
        await AsegurarIdentificacionLibreAsync(solicitud.Identificacion, null, cancelacion);

        var clienteId = string.IsNullOrWhiteSpace(solicitud.ClienteId)
            ? await GenerarClienteIdUnicoAsync(cancelacion)
            : solicitud.ClienteId.Trim().ToUpperInvariant();

        if (await repositorio.ExisteClienteIdAsync(clienteId, null, cancelacion))
        {
            throw new ExcepcionConflicto(
                "CLIENTEID_DUPLICADO", $"Ya existe un cliente con el identificador {clienteId}.");
        }

        var cliente = Cliente.Registrar(
            clienteId,
            solicitud.Nombre,
            solicitud.Genero,
            solicitud.Edad,
            solicitud.Identificacion,
            solicitud.Direccion,
            solicitud.Telefono,
            hash.Derivar(solicitud.Contrasena),
            solicitud.Estado);

        await repositorio.AgregarAsync(cliente, cancelacion);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        registro.LogInformation("Cliente {ClienteId} registrado.", cliente.ClienteId);

        return ClienteDto.Desde(cliente);
    }

    public async Task<ClienteDto> ActualizarAsync(
        string clienteId, ActualizarClienteSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var cliente = await ObtenerObligatorioAsync(clienteId, cancelacion);
        await AsegurarIdentificacionLibreAsync(solicitud.Identificacion, cliente.Id, cancelacion);

        cliente.Actualizar(
            solicitud.Nombre,
            solicitud.Genero,
            solicitud.Edad,
            solicitud.Identificacion,
            solicitud.Direccion,
            solicitud.Telefono,
            solicitud.Estado);

        if (!string.IsNullOrWhiteSpace(solicitud.Contrasena))
        {
            cliente.CambiarContrasena(hash.Derivar(solicitud.Contrasena));
        }

        repositorio.Actualizar(cliente);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        return ClienteDto.Desde(cliente);
    }

    public async Task<ClienteDto> ParchearAsync(
        string clienteId, ParchearClienteSolicitud solicitud, CancellationToken cancelacion = default)
    {
        var cliente = await ObtenerObligatorioAsync(clienteId, cancelacion);
        var identificacion = solicitud.Identificacion ?? cliente.Identificacion;

        if (!string.Equals(identificacion, cliente.Identificacion, StringComparison.OrdinalIgnoreCase))
        {
            await AsegurarIdentificacionLibreAsync(identificacion, cliente.Id, cancelacion);
        }

        // PATCH conserva todo lo no enviado; delegamos en la entidad para revalidar el conjunto.
        cliente.Actualizar(
            solicitud.Nombre ?? cliente.Nombre,
            solicitud.Genero ?? cliente.Genero,
            solicitud.Edad ?? cliente.Edad,
            identificacion,
            solicitud.Direccion ?? cliente.Direccion,
            solicitud.Telefono ?? cliente.Telefono,
            solicitud.Estado ?? cliente.Estado);

        if (!string.IsNullOrWhiteSpace(solicitud.Contrasena))
        {
            cliente.CambiarContrasena(hash.Derivar(solicitud.Contrasena));
        }

        repositorio.Actualizar(cliente);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        return ClienteDto.Desde(cliente);
    }

    public async Task EliminarAsync(string clienteId, CancellationToken cancelacion = default)
    {
        var cliente = await ObtenerObligatorioAsync(clienteId, cancelacion);

        // El evento se emite antes de borrar para que la bandeja de salida lo capture dentro de la
        // misma transacción que elimina la fila: o se van ambos, o no se va ninguno.
        cliente.MarcarParaBaja();
        repositorio.Eliminar(cliente);
        await unidadDeTrabajo.ConfirmarAsync(cancelacion);

        registro.LogInformation("Cliente {ClienteId} eliminado.", clienteId);
    }

    public async Task<SesionDto> AutenticarAsync(
        CredencialesSolicitud credenciales, CancellationToken cancelacion = default)
    {
        var cliente = await repositorio.ObtenerPorClienteIdAsync(credenciales.ClienteId, cancelacion);

        // Mismo mensaje para usuario inexistente y contraseña incorrecta: no revelamos cuál falló.
        if (cliente is null || !hash.Verificar(credenciales.Contrasena, cliente.ContrasenaHash))
        {
            registro.LogWarning("Intento de acceso fallido para {ClienteId}.", credenciales.ClienteId);
            throw new ExcepcionReglaNegocio("CREDENCIALES_INVALIDAS", "Usuario o contraseña incorrectos.");
        }

        cliente.AsegurarQuePuedeAutenticarse();

        var (token, expira) = generadorTokens.Emitir(cliente);

        return new SesionDto(token, expira, ClienteDto.Desde(cliente));
    }

    private async Task<Cliente> ObtenerObligatorioAsync(string clienteId, CancellationToken cancelacion) =>
        await repositorio.ObtenerPorClienteIdAsync(clienteId, cancelacion)
        ?? throw new ExcepcionNoEncontrado("el cliente", clienteId);

    private async Task AsegurarIdentificacionLibreAsync(
        string identificacion, Guid? excluyendoId, CancellationToken cancelacion)
    {
        if (await repositorio.ExisteIdentificacionAsync(identificacion.Trim(), excluyendoId, cancelacion))
        {
            throw new ExcepcionConflicto(
                "IDENTIFICACION_DUPLICADA", $"Ya existe una persona con la identificación {identificacion}.");
        }
    }

    /// <summary>
    /// Genera un código legible y único cuando el consumidor no aporta uno. Se reintenta un número
    /// acotado de veces para descartar la colisión —altamente improbable— con un código existente.
    /// </summary>
    private async Task<string> GenerarClienteIdUnicoAsync(CancellationToken cancelacion)
    {
        for (var intento = 0; intento < IntentosGeneracionCodigo; intento++)
        {
            var candidato = $"CLI-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

            if (!await repositorio.ExisteClienteIdAsync(candidato, null, cancelacion))
            {
                return candidato;
            }
        }

        throw new ExcepcionConflicto(
            "CLIENTEID_NO_GENERADO", "No fue posible generar un identificador de cliente único. Inténtelo de nuevo.");
    }
}
