using FluentValidation;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class CriarUnidadeRequestValidator : AbstractValidator<CriarUnidadeRequest>
{
    public CriarUnidadeRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Codigo).MaximumLength(20);
    }
}

public class AtualizarUnidadeRequestValidator : AbstractValidator<AtualizarUnidadeRequest>
{
    public AtualizarUnidadeRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Codigo).MaximumLength(20);
    }
}
