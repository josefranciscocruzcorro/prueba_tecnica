using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cuentas.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Cuentas.IntegrationTests.Infraestructura;

/// <summary>
/// Hospeda la API de Cuentas completa —canalización HTTP, autenticación, validación, casos de uso,
/// Entity Framework Core y manejo de excepciones— sobre una base SQLite en memoria.
/// </summary>
/// <remarks>
/// Se sustituyen únicamente las dos dependencias externas que no aportan nada a lo que se quiere
/// verificar: PostgreSQL, por una base en memoria equivalente, y RabbitMQ, retirando los servicios
/// en segundo plano. Todo lo demás es el código que se despliega en producción, de modo que
/// <c>dotnet test</c> funciona en cualquier máquina sin Docker ni infraestructura previa.
/// </remarks>
public sealed class FabricaApiCuentas : WebApplicationFactory<Program>
{
    /// <summary>Debe coincidir con la clave configurada; de lo contrario el token no valida.</summary>
    public const string ClaveFirma = "clave-de-pruebas-de-integracion-32-caracteres";

    public const string Emisor = "banco.clientes";
    public const string Audiencia = "banco.api";

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    /// <summary>
    /// La configuración se fija por variables de entorno y no con <c>ConfigureAppConfiguration</c>.
    /// Con el modelo de arranque mínimo, <c>WebApplication.CreateBuilder</c> ya ha leído su
    /// configuración cuando la fábrica de pruebas aplica sus propias fuentes, de modo que estas
    /// llegarían tarde para lo que se resuelve durante el registro de servicios —como la clave de
    /// firma del token—. Las variables de entorno sí están presentes desde el primer instante.
    /// </summary>
    static FabricaApiCuentas()
    {
        Environment.SetEnvironmentVariable("Jwt__Clave", ClaveFirma);
        Environment.SetEnvironmentVariable("Jwt__Emisor", Emisor);
        Environment.SetEnvironmentVariable("Jwt__Audiencia", Audiencia);
        Environment.SetEnvironmentVariable("Jwt__MinutosVigencia", "60");
        Environment.SetEnvironmentVariable("ConnectionStrings__CuentasDb", "Host=sustituido-por-sqlite");
    }

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Testing");

        constructor.ConfigureTestServices(servicios =>
        {
            SustituirBaseDeDatos(servicios);
            RetirarServiciosEnSegundoPlano(servicios);
        });
    }

    /// <summary>Cambia PostgreSQL por SQLite en memoria manteniendo el mismo modelo y el mismo código.</summary>
    /// <remarks>
    /// No basta con quitar <c>DbContextOptions&lt;T&gt;</c>: desde EF Core 9 cada
    /// <c>AddDbContext</c> deja además un <c>IDbContextOptionsConfiguration&lt;T&gt;</c>, y si se
    /// conserva el de PostgreSQL acaban registrados dos proveedores sobre el mismo contexto. Por eso
    /// se retira todo registro que mencione el contexto antes de volver a declararlo.
    /// </remarks>
    private void SustituirBaseDeDatos(IServiceCollection servicios)
    {
        var registros = servicios
            .Where(s => s.ServiceType == typeof(CuentasDbContext)
                        || s.ServiceType == typeof(DbContextOptions)
                        || (s.ServiceType.IsGenericType
                            && s.ServiceType.GetGenericArguments().Contains(typeof(CuentasDbContext))))
            .ToList();

        foreach (var registro in registros)
        {
            servicios.Remove(registro);
        }

        _conexion.Open();

        servicios.AddDbContext<CuentasDbContext>(opciones => opciones
            .UseSqlite(_conexion)
            .EnableSensitiveDataLogging());
    }

    /// <summary>
    /// Retira el bus y el despachador de la bandeja de salida: la prueba verifica el contrato HTTP
    /// y el estado persistido, no la entrega de mensajes, que se comprueba en el entorno real.
    /// </summary>
    private static void RetirarServiciosEnSegundoPlano(IServiceCollection servicios)
    {
        foreach (var registro in servicios.Where(s => s.ServiceType == typeof(IHostedService)).ToList())
        {
            servicios.Remove(registro);
        }
    }

    /// <summary>Emite un token válido, como haría el microservicio de Clientes al iniciar sesión.</summary>
    public static string EmitirToken(string clienteId = "CLI-002", string nombre = "Marianela Montalvo")
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveFirma)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Emisor,
            audience: Audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, clienteId),
                new Claim("cliente_id", clienteId),
                new Claim("nombre", nombre),
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Cliente HTTP con la cabecera de autorización ya puesta.</summary>
    public HttpClient CrearClienteAutenticado(string clienteId = "CLI-002")
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", EmitirToken(clienteId));

        return cliente;
    }

    /// <summary>Ejecuta una acción con un contexto propio, como haría cualquier caso de uso.</summary>
    public async Task<T> ConContextoAsync<T>(Func<CuentasDbContext, Task<T>> accion)
    {
        using var ambito = Services.CreateScope();

        return await accion(ambito.ServiceProvider.GetRequiredService<CuentasDbContext>());
    }

    public async Task EnContextoAsync(Func<CuentasDbContext, Task> accion)
    {
        using var ambito = Services.CreateScope();
        await accion(ambito.ServiceProvider.GetRequiredService<CuentasDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await _conexion.DisposeAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
