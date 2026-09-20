using Clientes.Application.Clientes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Clientes.Application;

/// <summary>Registro de la capa de aplicación. Cada capa expone su propio módulo de composición.</summary>
public static class InyeccionDependenciasAplicacion
{
    public static IServiceCollection AgregarAplicacionClientes(this IServiceCollection servicios)
    {
        // Un campo vacío incumple a la vez «obligatorio» y «formato»; mostrar ambos mensajes solo
        // añade ruido. Con Stop, cada campo informa del primer problema y basta con corregir ese.
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        servicios.AddScoped<IServicioClientes, ServicioClientes>();
        servicios.AddValidatorsFromAssemblyContaining<IServicioClientes>(ServiceLifetime.Singleton);

        return servicios;
    }
}
