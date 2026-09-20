using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Shared.Api.Configuracion;

/// <summary>Documentación OpenAPI homogénea, con el esquema de portador ya cableado.</summary>
public static class ExtensionesSwagger
{
    public static IServiceCollection AgregarDocumentacionApi(
        this IServiceCollection servicios,
        string titulo,
        string descripcion)
    {
        servicios.AddEndpointsApiExplorer();
        servicios.AddSwaggerGen(opciones =>
        {
            opciones.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = titulo,
                Version = "v1",
                Description = descripcion,
                Contact = new OpenApiContact { Name = "Jose Francisco Cruz Corro" },
            });

            var esquema = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token obtenido en POST /api/clientes/autenticar. Pegue solo el valor del token.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };

            opciones.AddSecurityDefinition("Bearer", esquema);
            opciones.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = [] });
            opciones.SupportNonNullableReferenceTypes();
            opciones.CustomSchemaIds(tipo => tipo.FullName?.Replace('+', '.').Split('.').Last());
        });

        return servicios;
    }
}
