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
