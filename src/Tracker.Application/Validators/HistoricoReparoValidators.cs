using FluentValidation;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class CriarReparoRequestValidator : AbstractValidator<CriarReparoRequest>
{
    public CriarReparoRequestValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.CustoEstimado).GreaterThanOrEqualTo(0).When(x => x.CustoEstimado.HasValue);
    }
}

public class AtualizarStatusReparoRequestValidator : AbstractValidator<AtualizarStatusReparoRequest>
{
    public AtualizarStatusReparoRequestValidator()
    {
        // O conversor de enum aceita número no JSON: sem isto, "status": 99 seria gravado no banco.
        RuleFor(x => x.Status).IsInEnum().WithMessage("Status de reparo inválido.");
    }
}
