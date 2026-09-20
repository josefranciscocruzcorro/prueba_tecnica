using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Api.Filtros;

/// <summary>
/// Ejecuta el <c>IValidator&lt;T&gt;</c> registrado para cada argumento de la acción antes de que
/// el controlador se ejecute. Deja los controladores libres de comprobaciones repetitivas y
/// garantiza que todo error de forma se devuelva con el mismo contrato que el resto de errores.
/// </summary>
public sealed class FiltroValidacion : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate siguiente)
    {
        foreach (var argumento in contexto.ActionArguments.Values.Where(a => a is not null))
        {
            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento!.GetType());

            if (contexto.HttpContext.RequestServices.GetService(tipoValidador) is not IValidator validador)
            {
                continue;
            }

            var contextoValidacion = new ValidationContext<object>(argumento);
            var resultado = await validador.ValidateAsync(contextoValidacion, contexto.HttpContext.RequestAborted);

            if (!resultado.IsValid)
            {
                throw new ValidationException(resultado.Errors);
            }
        }

        await siguiente();
    }
}
