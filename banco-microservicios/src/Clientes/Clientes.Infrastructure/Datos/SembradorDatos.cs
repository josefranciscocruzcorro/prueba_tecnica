using Clientes.Application.Abstracciones;
using Clientes.Domain.Clientes;
using Clientes.Domain.Personas;
using Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Clientes.Infrastructure.Datos;

/// <summary>
/// Carga los clientes del caso de uso del enunciado. Es idempotente: si ya existe alguno no hace
/// nada, de modo que reiniciar los contenedores no duplica datos ni pisa cambios del evaluador.
/// </summary>
public sealed class SembradorDatos(
    ClientesDbContext contexto,
    IServicioHashContrasena hash,
    ILogger<SembradorDatos> registro)
{
    /// <summary>Clientes del caso 1 del enunciado, con la contraseña que allí se indica.</summary>
    private static readonly (string ClienteId, string Nombre, Genero Genero, int Edad, string Identificacion,
        string Direccion, string Telefono, string Contrasena)[] Semilla =
    [
        ("CLI-001", "Jose Lema", Genero.Masculino, 35, "1717171717", "Otavalo sn y principal", "098254785", "1234"),
        ("CLI-002", "Marianela Montalvo", Genero.Femenino, 29, "1818181818", "Amazonas y NNUU", "097548965", "5678"),
        ("CLI-003", "Juan Osorio", Genero.Masculino, 42, "1919191919", "13 junio y Equinoccial", "098874587", "1245"),
    ];

    public async Task SembrarAsync(CancellationToken cancelacion = default)
    {
        if (await contexto.Clientes.AnyAsync(cancelacion))
        {
            registro.LogInformation("La base ya contiene clientes; se omite la carga inicial.");
            return;
        }

        foreach (var fila in Semilla)
        {
            var cliente = Cliente.Registrar(
                fila.ClienteId,
                fila.Nombre,
                fila.Genero,
                fila.Edad,
                fila.Identificacion,
                fila.Direccion,
                fila.Telefono,
                hash.Derivar(fila.Contrasena));

            contexto.Clientes.Add(cliente);
        }

        // Guardar también emite los eventos de alta a la bandeja de salida, así que el
        // microservicio de Cuentas recibe la réplica de estos clientes en cuanto arranque.
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation("Carga inicial completada: {Total} clientes.", Semilla.Length);
    }
}
