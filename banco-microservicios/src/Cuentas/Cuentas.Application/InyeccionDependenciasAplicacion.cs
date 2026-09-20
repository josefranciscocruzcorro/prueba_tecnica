using Cuentas.Application.Cuentas;
using Cuentas.Application.Reportes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cuentas.Application;

/// <summary>Registro de la capa de aplicación del microservicio de Cuentas.</summary>
public static class InyeccionDependenciasAplicacion
{
    public static IServiceCollection AgregarAplicacionCuentas(this IServiceCollection servicios)
    {
        // Un campo vacío incumple a la vez «obligatorio» y «formato»; mostrar ambos mensajes solo
        // añade ruido. Con Stop, cada campo informa del primer problema y basta con corregir ese.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        servicios.AddScoped<IServicioCuentas, ServicioCuentas>();
        servicios.AddScoped<IServicioMovimientos, ServicioMovimientos>();
        servicios.AddScoped<IServicioReportes, ServicioReportes>();
        servicios.AddValidatorsFromAssemblyContaining<IServicioCuentas>(ServiceLifetime.Singleton);

        return servicios;
    }
}
