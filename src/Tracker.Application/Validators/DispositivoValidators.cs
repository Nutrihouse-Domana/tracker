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

public class CriarTelefoneRequestValidator : AbstractValidator<CriarTelefoneRequest>
{
    public CriarTelefoneRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AssetTag).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SistemaOperacional).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Imei).NotEmpty().Length(15).Matches(@"^\d+$").WithMessage("IMEI deve conter 15 dígitos numéricos.");
    }
}

public class CriarPerifericoRequestValidator : AbstractValidator<CriarPerifericoRequest>
{
    public CriarPerifericoRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AssetTag).NotEmpty().MaximumLength(50);
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
