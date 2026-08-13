using FluentValidation;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

public class CriarNotebookRequestValidator : AbstractValidator<CriarNotebookRequest>
{
    public CriarNotebookRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AssetTag).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SistemaOperacional).NotEmpty().MaximumLength(100);
    }
}

public class CriarMobileRequestValidator : AbstractValidator<CriarMobileRequest>
{
    public CriarMobileRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AssetTag).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SistemaOperacional).NotEmpty().MaximumLength(100);
        // Tablet só-Wi-Fi não tem IMEI: valida o formato apenas quando informado.
        RuleFor(x => x.Imei).Length(15).Matches(@"^\d+$")
            .WithMessage("IMEI deve conter 15 dígitos numéricos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Imei));
    }
}

public class CriarPerifericoRequestValidator : AbstractValidator<CriarPerifericoRequest>
{
    public CriarPerifericoRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TipoPeriferico).IsInEnum();
    }
}

public class AtribuirDispositivoRequestValidator : AbstractValidator<AtribuirDispositivoRequest>
{
    public AtribuirDispositivoRequestValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.RegistradoPorAdmId).NotEmpty();
    }
}
