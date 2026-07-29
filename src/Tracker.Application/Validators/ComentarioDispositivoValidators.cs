using FluentValidation;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class CriarComentarioRequestValidator : AbstractValidator<CriarComentarioRequest>
{
    public CriarComentarioRequestValidator()
    {
        RuleFor(x => x.Texto).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.CriadoPorAdmId).NotEmpty();
    }
}
