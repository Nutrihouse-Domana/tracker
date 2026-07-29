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
        RuleFor(x => x.Telefone).NotEmpty().MaximumLength(20);
    }
}

public class AtualizarUsuarioRequestValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Telefone).NotEmpty().MaximumLength(20);
    }
}
