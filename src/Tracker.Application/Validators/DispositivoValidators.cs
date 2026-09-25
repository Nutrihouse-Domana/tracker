using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using FluentValidation;
using Tracker.Application.Dtos;
using Tracker.Domain.Enums;

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

/// <summary>
/// Formato de endereço de rede e MAC aceitos no cadastro de impressora. Regras deliberadamente
/// simples: o objetivo é barrar erro de digitação, não validar a rede.
/// </summary>
public static class FormatoRede
{
    private static readonly Regex Hostname = new(
        @"^(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$",
        RegexOptions.Compiled);

    private static readonly Regex SoDigitosEPontos = new(@"^[\d.]+$", RegexOptions.Compiled);

    private static readonly Regex Mac = new(
        @"^[0-9A-Fa-f]{2}([:-])(?:[0-9A-Fa-f]{2}\1){4}[0-9A-Fa-f]{2}$|^[0-9A-Fa-f]{12}$",
        RegexOptions.Compiled);

    /// <summary>IPv4 (4 octetos), IPv6 ou nome DNS/NetBIOS.</summary>
    public static bool EnderecoValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        var v = valor.Trim();

        // "192.168.0" ou "300.1.1.1" parecem IP e não podem passar como nome DNS. O
        // IPAddress.TryParse sozinho aceitaria "1" (vira 0.0.0.1), por isso a contagem de octetos.
        if (SoDigitosEPontos.IsMatch(v))
            return v.Split('.').Length == 4 && IPAddress.TryParse(v, out _);

        if (v.Contains(':'))
            return IPAddress.TryParse(v, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6;

        return Hostname.IsMatch(v);
    }

    /// <summary>MAC em AA:BB:CC:DD:EE:FF, AA-BB-..., ou os 12 dígitos hexadecimais juntos.</summary>
    public static bool MacValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Mac.IsMatch(valor.Trim());
}

/// <summary>Regras de conexão comuns ao cadastro e à edição da impressora.</summary>
internal static class RegrasConexaoImpressora
{
    public static void Aplicar<T>(
        AbstractValidator<T> v,
        Expression<Func<T, ConexaoImpressora>> conexao,
        Expression<Func<T, string?>> endereco,
        Expression<Func<T, string?>> mac,
        Expression<Func<T, bool>> monitorar)
    {
        var obterConexao = conexao.Compile();
        var obterEndereco = endereco.Compile();
        var obterMonitorar = monitorar.Compile();
        var obterMac = mac.Compile();

        v.RuleFor(conexao).IsInEnum().WithMessage("Conexão da impressora inválida.");

        v.RuleFor(endereco)
            .MaximumLength(255)
            .Must(FormatoRede.EnderecoValido)
            .WithMessage("Endereço de rede inválido. Informe um IP (ex.: 192.168.0.50) ou um nome de rede.")
            .When(x => !string.IsNullOrWhiteSpace(obterEndereco(x)) && obterConexao(x) == ConexaoImpressora.Rede);

        v.RuleFor(mac)
            .Must(FormatoRede.MacValido)
            .WithMessage("MAC inválido. Use o formato AA:BB:CC:DD:EE:FF.")
            .When(x => !string.IsNullOrWhiteSpace(obterMac(x)));

        // Impressora USB está ligada a um computador: o servidor não a alcança por SNMP.
        v.RuleFor(monitorar)
            .Must(m => !m)
            .When(x => obterConexao(x) == ConexaoImpressora.Usb)
            .WithMessage("Impressora ligada por USB não pode ser monitorada por SNMP.");

        v.RuleFor(endereco)
            .NotEmpty()
            .When(x => obterMonitorar(x) && obterConexao(x) == ConexaoImpressora.Rede)
            .WithMessage("Informe o endereço de rede (IP ou nome) para monitorar a impressora por SNMP.");
    }
}

public class CriarImpressoraRequestValidator : AbstractValidator<CriarImpressoraRequest>
{
    public CriarImpressoraRequestValidator()
    {
        RuleFor(x => x.NumeroSerie).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AssetTag).MaximumLength(50);
        RuleFor(x => x.TipoImpressora).IsInEnum().WithMessage("Tipo de impressora inválido.");
        RuleFor(x => x.Fabricante).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(100);
        // Impressora é sempre do local: é o nome da unidade que aparece como responsável.
        RuleFor(x => x.UnidadeId).NotEmpty()
            .WithMessage("Selecione a unidade da impressora: ela pertence ao local, não a um usuário.");
        RegrasConexaoImpressora.Aplicar(this, x => x.Conexao, x => x.EnderecoRede, x => x.MacAddress, x => x.MonitorarSnmp);
    }
}

public class DefinirConexaoImpressoraRequestValidator : AbstractValidator<DefinirConexaoImpressoraRequest>
{
    public DefinirConexaoImpressoraRequestValidator()
    {
        RegrasConexaoImpressora.Aplicar(this, x => x.Conexao, x => x.EnderecoRede, x => x.MacAddress, x => x.MonitorarSnmp);
    }
}

public class DefinirDadosImpressoraRequestValidator : AbstractValidator<DefinirDadosImpressoraRequest>
{
    public DefinirDadosImpressoraRequestValidator()
    {
        RuleFor(x => x.TipoImpressora).IsInEnum().WithMessage("Tipo de impressora inválido.");
        RuleFor(x => x.Fabricante).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(100);
    }
}

public class DefinirAluguelImpressoraRequestValidator : AbstractValidator<DefinirAluguelImpressoraRequest>
{
    public DefinirAluguelImpressoraRequestValidator()
    {
        RuleFor(x => x.Motivo).MaximumLength(400);
    }
}

public class AtribuirDispositivoRequestValidator : AbstractValidator<AtribuirDispositivoRequest>
{
    public AtribuirDispositivoRequestValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}

public class AtualizarStatusDispositivoRequestValidator : AbstractValidator<AtualizarStatusDispositivoRequest>
{
    public AtualizarStatusDispositivoRequestValidator()
    {
        // O conversor de enum aceita número no JSON: sem isto, "status": 99 seria gravado no banco.
        RuleFor(x => x.Status).IsInEnum().WithMessage("Status de dispositivo inválido.");
    }
}

public class ExcluirDispositivoRequestValidator : AbstractValidator<ExcluirDispositivoRequest>
{
    public ExcluirDispositivoRequestValidator()
    {
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe sua senha para confirmar a exclusão.");
        RuleFor(x => x.Motivo).MaximumLength(400);
    }
}

public class DefinirIdentificacaoInternaRequestValidator : AbstractValidator<DefinirIdentificacaoInternaRequest>
{
    public DefinirIdentificacaoInternaRequestValidator()
    {
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe sua senha para confirmar a alteração.");
        // Opcional (vazio = limpar), mas quando informada tem o mesmo teto de NumeroSerie/Hostname.
        RuleFor(x => x.IdentificacaoInterna).MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.IdentificacaoInterna));
        RuleFor(x => x.Motivo).MaximumLength(400);
    }
}

public class DefinirPosseDispositivoRequestValidator : AbstractValidator<DefinirPosseDispositivoRequest>
{
    public DefinirPosseDispositivoRequestValidator()
    {
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe sua senha para confirmar a alteração.");
        RuleFor(x => x.Motivo).MaximumLength(400);
        // Ativo do local sem unidade não diz nada a ninguém: o nome da unidade é justamente o
        // que passa a aparecer no lugar do usuário.
        RuleFor(x => x.UnidadeId).NotEmpty()
            .When(x => !x.PertenceAUsuario)
            .WithMessage("Selecione a unidade a que o dispositivo pertence.");
    }
}
