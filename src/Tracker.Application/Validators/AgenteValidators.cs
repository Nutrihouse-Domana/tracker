using FluentValidation;
using Tracker.Application.Common;
using Tracker.Application.Dtos;

namespace Tracker.Application.Validators;

/// <summary>
/// Auto-cadastro do agente. O ponto sensível é o número de série: ele é chave de reencontro do
/// ativo, então um serial genérico de BIOS (ver <see cref="SerialGenerico"/>) faria duas máquinas
/// diferentes caírem no MESMO cadastro. Só é tolerado quando há patrimônio — e aí o controller
/// NÃO usa o serial recebido (nem para reencontrar, nem para gravar).
/// </summary>
public class AutoEnrollmentRequestValidator : AbstractValidator<AutoEnrollmentRequest>
{
    public AutoEnrollmentRequestValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.NivelProtecao).IsInEnum().When(x => x.NivelProtecao.HasValue);

        RuleFor(x => x.NumeroSerie)
            .Must((request, serie) => !SerialGenerico.EhGenerico(serie) || !string.IsNullOrWhiteSpace(request.AssetTag))
            .WithMessage(x =>
                $"O número de série informado ('{x.NumeroSerie?.Trim()}') é vazio ou genérico de BIOS e não " +
                "identifica o aparelho. Informe o patrimônio no registro ou cadastre o dispositivo no painel.");
    }
}

public class EmitirComandoRequestValidator : AbstractValidator<EmitirComandoRequest>
{
    public EmitirComandoRequestValidator()
    {
        RuleFor(x => x.TipoComando).IsInEnum().WithMessage("Tipo de comando inválido.");
    }
}

public class AlertaAgenteRequestValidator : AbstractValidator<AlertaAgenteRequest>
{
    public AlertaAgenteRequestValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum().WithMessage("Tipo de alerta inválido.");
    }
}
