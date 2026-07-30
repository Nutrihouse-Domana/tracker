using FluentValidation;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class GerarTermoDeUsoRequestValidator : AbstractValidator<GerarTermoDeUsoRequest>
{
    public GerarTermoDeUsoRequestValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.VersaoTermo).NotEmpty().MaximumLength(20);
        RuleFor(x => x.EstadoDispositivo).MaximumLength(500);
    }
}
