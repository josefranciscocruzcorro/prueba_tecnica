using Clientes.Application.Clientes.Dtos;
using Clientes.Domain.Personas;
using FluentValidation;

namespace Clientes.Application.Clientes.Validadores;

/// <summary>
/// Validación de forma de la petición: comprueba lo que se puede saber sin tocar la base de datos
/// y devuelve todos los errores de una vez. Las invariantes de negocio siguen viviendo en la
/// entidad; esta capa solo evita viajes inútiles y produce mensajes accionables para el usuario.
/// </summary>
public sealed class ValidadorCrearCliente : AbstractValidator<CrearClienteSolicitud>
{
    public ValidadorCrearCliente()
    {
        RuleFor(x => x.Nombre).AplicarReglasNombre();
        RuleFor(x => x.Genero).AplicarReglasGenero();
        RuleFor(x => x.Edad).AplicarReglasEdad();
        RuleFor(x => x.Identificacion).AplicarReglasIdentificacion();
        RuleFor(x => x.Direccion).AplicarReglasDireccion();
        RuleFor(x => x.Telefono).AplicarReglasTelefono();
        RuleFor(x => x.Contrasena).AplicarReglasContrasena();

        RuleFor(x => x.ClienteId)
            .Matches("^[A-Za-z0-9-]{1,30}$")
            .WithMessage("El clienteId solo admite letras, números y guiones (máximo 30 caracteres).")
            .When(x => !string.IsNullOrWhiteSpace(x.ClienteId));
    }
}

public sealed class ValidadorActualizarCliente : AbstractValidator<ActualizarClienteSolicitud>
{
    public ValidadorActualizarCliente()
    {
        RuleFor(x => x.Nombre).AplicarReglasNombre();
        RuleFor(x => x.Genero).AplicarReglasGenero();
        RuleFor(x => x.Edad).AplicarReglasEdad();
        RuleFor(x => x.Identificacion).AplicarReglasIdentificacion();
        RuleFor(x => x.Direccion).AplicarReglasDireccion();
        RuleFor(x => x.Telefono).AplicarReglasTelefono();

        RuleFor(x => x.Contrasena!).AplicarReglasContrasena().When(x => x.Contrasena is not null);
    }
}

public sealed class ValidadorParchearCliente : AbstractValidator<ParchearClienteSolicitud>
{
    public ValidadorParchearCliente()
    {
        RuleFor(x => x.Nombre!).AplicarReglasNombre().When(x => x.Nombre is not null);
        RuleFor(x => x.Genero!.Value).AplicarReglasGenero().When(x => x.Genero is not null);
        RuleFor(x => x.Edad!.Value).AplicarReglasEdad().When(x => x.Edad is not null);
        RuleFor(x => x.Identificacion!).AplicarReglasIdentificacion().When(x => x.Identificacion is not null);
        RuleFor(x => x.Direccion!).AplicarReglasDireccion().When(x => x.Direccion is not null);
        RuleFor(x => x.Telefono!).AplicarReglasTelefono().When(x => x.Telefono is not null);
        RuleFor(x => x.Contrasena!).AplicarReglasContrasena().When(x => x.Contrasena is not null);

        RuleFor(x => x)
            .Must(TieneAlgunCampo)
            .WithMessage("Envíe al menos un campo para modificar.")
            .OverridePropertyName(string.Empty);
    }

    private static bool TieneAlgunCampo(ParchearClienteSolicitud s) =>
        s.Nombre is not null || s.Genero is not null || s.Edad is not null || s.Identificacion is not null
        || s.Direccion is not null || s.Telefono is not null || s.Estado is not null || s.Contrasena is not null;
}

public sealed class ValidadorCredenciales : AbstractValidator<CredencialesSolicitud>
{
    public ValidadorCredenciales()
    {
        RuleFor(x => x.ClienteId).NotEmpty().WithMessage("Indique su identificador de cliente.");
        RuleFor(x => x.Contrasena).NotEmpty().WithMessage("Indique su contraseña.");
    }
}

/// <summary>Reglas compartidas por los tres validadores, declaradas una sola vez.</summary>
internal static class ReglasComunesCliente
{
    private const string PatronTelefono = @"^[0-9+\- ]{7,20}$";
    private const string PatronIdentificacion = "^[A-Za-z0-9]{5,20}$";

    public static IRuleBuilderOptions<T, string> AplicarReglasNombre<T>(this IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("El nombre es obligatorio.")
        .MaximumLength(Persona.LargoMaximoNombre)
        .WithMessage($"El nombre no puede superar {Persona.LargoMaximoNombre} caracteres.");

    public static IRuleBuilderOptions<T, Genero> AplicarReglasGenero<T>(this IRuleBuilder<T, Genero> regla) => regla
        .IsInEnum().WithMessage("El género indicado no es válido.");

    public static IRuleBuilderOptions<T, int> AplicarReglasEdad<T>(this IRuleBuilder<T, int> regla) => regla
        .InclusiveBetween(Persona.EdadMinima, Persona.EdadMaxima)
        .WithMessage($"La edad debe estar entre {Persona.EdadMinima} y {Persona.EdadMaxima} años.");

    public static IRuleBuilderOptions<T, string> AplicarReglasIdentificacion<T>(this IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("La identificación es obligatoria.")
        .Matches(PatronIdentificacion)
        .WithMessage("La identificación debe tener entre 5 y 20 caracteres alfanuméricos.");

    public static IRuleBuilderOptions<T, string> AplicarReglasDireccion<T>(this IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("La dirección es obligatoria.")
        .MaximumLength(200).WithMessage("La dirección no puede superar 200 caracteres.");

    public static IRuleBuilderOptions<T, string> AplicarReglasTelefono<T>(this IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("El teléfono es obligatorio.")
        .Matches(PatronTelefono).WithMessage("El teléfono debe tener entre 7 y 20 dígitos.");

    public static IRuleBuilderOptions<T, string> AplicarReglasContrasena<T>(this IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("La contraseña es obligatoria.")
        .MinimumLength(Domain.Clientes.Cliente.LargoMinimoContrasena)
        .WithMessage($"La contraseña debe tener al menos {Domain.Clientes.Cliente.LargoMinimoContrasena} caracteres.")
        .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
}
