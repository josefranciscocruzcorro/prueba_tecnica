using Cuentas.Application.Cuentas.Dtos;
using FluentValidation;

namespace Cuentas.Application.Cuentas.Validadores;

/// <summary>Validación de forma de las peticiones de cuenta y movimiento.</summary>
public sealed class ValidadorCrearCuenta : AbstractValidator<CrearCuentaSolicitud>
{
    public ValidadorCrearCuenta()
    {
        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("Indique el cliente titular de la cuenta.")
            .MaximumLength(30).WithMessage("El identificador de cliente no puede superar 30 caracteres.");

        RuleFor(x => x.TipoCuenta).IsInEnum().WithMessage("El tipo de cuenta debe ser Ahorros o Corriente.");

        RuleFor(x => x.SaldoInicial)
            .GreaterThanOrEqualTo(0).WithMessage("El saldo inicial no puede ser negativo.")
            .LessThanOrEqualTo(1_000_000_000).WithMessage("El saldo inicial excede el máximo permitido.");

        RuleFor(x => x.NumeroCuenta)
            .Matches("^[0-9]{4,20}$").WithMessage("El número de cuenta debe tener entre 4 y 20 dígitos.")
            .When(x => !string.IsNullOrWhiteSpace(x.NumeroCuenta));
    }
}

public sealed class ValidadorActualizarCuenta : AbstractValidator<ActualizarCuentaSolicitud>
{
    public ValidadorActualizarCuenta()
    {
        RuleFor(x => x.TipoCuenta).IsInEnum().WithMessage("El tipo de cuenta debe ser Ahorros o Corriente.");
        RuleFor(x => x.SaldoInicial).GreaterThanOrEqualTo(0).WithMessage("El saldo inicial no puede ser negativo.");
    }
}

public sealed class ValidadorParchearCuenta : AbstractValidator<ParchearCuentaSolicitud>
{
    public ValidadorParchearCuenta()
    {
        RuleFor(x => x.TipoCuenta!.Value).IsInEnum().When(x => x.TipoCuenta is not null)
            .WithMessage("El tipo de cuenta debe ser Ahorros o Corriente.");

        RuleFor(x => x.SaldoInicial!.Value).GreaterThanOrEqualTo(0).When(x => x.SaldoInicial is not null)
            .WithMessage("El saldo inicial no puede ser negativo.");

        RuleFor(x => x)
            .Must(s => s.TipoCuenta is not null || s.SaldoInicial is not null || s.Estado is not null)
            .WithMessage("Envíe al menos un campo para modificar.")
            .OverridePropertyName(string.Empty);
    }
}

public sealed class ValidadorCrearMovimiento : AbstractValidator<CrearMovimientoSolicitud>
{
    public ValidadorCrearMovimiento()
    {
        RuleFor(x => x.NumeroCuenta)
            .NotEmpty().WithMessage("Indique el número de cuenta.")
            .Matches("^[0-9]{4,20}$").WithMessage("El número de cuenta debe tener entre 4 y 20 dígitos.");

        RuleFor(x => x.Valor)
            .NotEqual(0).WithMessage("El valor del movimiento no puede ser cero.")
            .Must(v => Math.Abs(v) <= 1_000_000_000).WithMessage("El valor del movimiento excede el máximo permitido.");

        RuleFor(x => x.TipoMovimiento!.Value).IsInEnum().When(x => x.TipoMovimiento is not null)
            .WithMessage("El tipo de movimiento debe ser Deposito o Retiro.");
    }
}

public sealed class ValidadorActualizarMovimiento : AbstractValidator<ActualizarMovimientoSolicitud>
{
    public ValidadorActualizarMovimiento()
    {
        RuleFor(x => x.Valor).NotEqual(0).WithMessage("El valor del movimiento no puede ser cero.");

        RuleFor(x => x.TipoMovimiento!.Value).IsInEnum().When(x => x.TipoMovimiento is not null)
            .WithMessage("El tipo de movimiento debe ser Deposito o Retiro.");
    }
}
