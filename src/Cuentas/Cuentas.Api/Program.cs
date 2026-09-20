using Cuentas.Api.Arranque;
using Cuentas.Application;
using Cuentas.Infrastructure;
using Cuentas.Infrastructure.Datos;
using Cuentas.Infrastructure.Persistencia;
using Serilog;
using Shared.Api.Configuracion;

const string Titulo = "Banco · API de Cuentas y Movimientos";
const string Descripcion =
    "Microservicio contable: cuentas, movimientos y estado de cuenta. Mantiene una réplica local " +
    "de clientes alimentada por eventos, de modo que opera sin llamar al microservicio de Clientes.";

var constructor = WebApplication.CreateBuilder(args);

constructor.Host.UseSerilog((contexto, registro) => registro
    .ReadFrom.Configuration(contexto.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("servicio", "cuentas"));

constructor.Services
    .AgregarApiBase(constructor.Configuration, Titulo, Descripcion)
    .AgregarAplicacionCuentas()
    .AgregarInfraestructuraCuentas(constructor.Configuration);

constructor.Services.AddHostedService<SembradorEnSegundoPlano>();

constructor.Services
    .AddHealthChecks()
    .AddDbContextCheck<CuentasDbContext>("base-de-datos");

var app = constructor.Build();

app.UseSerilogRequestLogging(opciones => opciones.GetLevel = (contexto, _, excepcion) =>
    excepcion is not null || contexto.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Error
        : Serilog.Events.LogEventLevel.Information);

app.UsarApiBase(Titulo);

app.MapHealthChecks("/salud/vivo", new() { Predicate = _ => false });
app.MapHealthChecks("/salud/listo");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.PrepararBaseDeDatosAsync();

await app.RunAsync();

/// <summary>Punto de entrada expuesto para que las pruebas de integración puedan hospedar la API.</summary>
public partial class Program;
