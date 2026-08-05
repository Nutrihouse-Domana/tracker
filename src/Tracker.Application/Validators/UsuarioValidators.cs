using FluentValidation;
using Tracker.Application.Common;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class CriarUsuarioRequestValidator : AbstractValidator<CriarUsuarioRequest>
{
    public CriarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cpf).Must(CpfValidation.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Telefone).NotEmpty().Matches(@"^\d{10,11}$")
            .WithMessage("Telefone deve conter DDD + número (10 ou 11 dígitos).");
    }
}

public class AtualizarUsuarioRequestValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Telefone).NotEmpty().Matches(@"^\d{10,11}$")
            .WithMessage("Telefone deve conter DDD + número (10 ou 11 dígitos).");
    }
}
