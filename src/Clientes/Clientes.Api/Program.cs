using Clientes.Api.Arranque;
using Clientes.Application;
using Clientes.Infrastructure;
using Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Api.Configuracion;

const string Titulo = "Banco · API de Clientes";
const string Descripcion =
    "Microservicio del maestro de Clientes y Personas. Publica de forma asíncrona los cambios del " +
    "cliente para que el microservicio de Cuentas mantenga su réplica local.";

var constructor = WebApplication.CreateBuilder(args);

// Registro estructurado: la misma configuración en ambos servicios permite correlacionar por traceId.
constructor.Host.UseSerilog((contexto, registro) => registro
    .ReadFrom.Configuration(contexto.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("servicio", "clientes"));

constructor.Services
    .AgregarApiBase(constructor.Configuration, Titulo, Descripcion)
    .AgregarAplicacionClientes()
    .AgregarInfraestructuraClientes(constructor.Configuration);

constructor.Services
    .AddHealthChecks()
    .AddDbContextCheck<ClientesDbContext>("base-de-datos");

var app = constructor.Build();

app.UseSerilogRequestLogging(opciones => opciones.GetLevel = (contexto, _, excepcion) =>
    excepcion is not null || contexto.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Error
        : Serilog.Events.LogEventLevel.Information);

app.UsarApiBase(Titulo);

// Sonda de vida (¿el proceso responde?) y de disponibilidad (¿puede atender tráfico?). Docker usa
// la segunda para no enrutar peticiones hacia una réplica que aún no tiene base de datos.
app.MapHealthChecks("/salud/vivo", new() { Predicate = _ => false });
app.MapHealthChecks("/salud/listo");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.PrepararBaseDeDatosAsync();

await app.RunAsync();

/// <summary>Punto de entrada expuesto para que las pruebas de integración puedan hospedar la API.</summary>
public partial class Program;
