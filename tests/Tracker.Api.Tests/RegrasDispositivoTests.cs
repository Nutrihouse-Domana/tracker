using Tracker.Application.Dispositivos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege as regras de status compartilhadas por heartbeat, confirmação de comando, posse e
/// exclusão. O bug que originou a principal: o heartbeat que entregava um Bloquear pendente
/// respondia "deve estar desbloqueado" (o status só vira Bloqueado na confirmação) e o agente
/// Android destravava logo depois de aplicar a trava.
/// </summary>
public class RegrasDispositivoTests
{
    private static ComandoRemoto Comando(TipoComandoRemoto tipo, StatusComandoRemoto status) =>
        new() { TipoComando = tipo, Status = status };

    [Theory]
    [InlineData(StatusComandoRemoto.Pendente)]
    [InlineData(StatusComandoRemoto.Enviado)]
    [InlineData(StatusComandoRemoto.Confirmado)]
    public void BloquearMaisRecente_DeixaBloqueado_MesmoAntesDeConfirmar(StatusComandoRemoto status)
    {
        var ultimo = Comando(TipoComandoRemoto.Bloquear, status);

        // Status ainda EmUso: é exatamente o momento em que o heartbeat entrega o comando.
        Assert.True(RegrasDispositivo.DeveEstarBloqueado(StatusDispositivo.EmUso, ultimo));
    }

    [Fact]
    public void DesbloquearMaisRecente_Desbloqueia_MesmoComStatusBloqueado()
    {
        var ultimo = Comando(TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Pendente);

        Assert.False(RegrasDispositivo.DeveEstarBloqueado(StatusDispositivo.Bloqueado, ultimo));
    }

    [Theory]
    [InlineData(StatusDispositivo.Bloqueado, true)]
    [InlineData(StatusDispositivo.EmUso, false)]
    [InlineData(StatusDispositivo.EmEstoque, false)]
    public void SemComando_ValeOStatus(StatusDispositivo status, bool esperado)
    {
        Assert.Equal(esperado, RegrasDispositivo.DeveEstarBloqueado(status, ultimoComando: null));
    }

    [Theory]
    [InlineData(TipoComandoRemoto.Bloquear, StatusComandoRemoto.Pendente, true)]
    [InlineData(TipoComandoRemoto.Bloquear, StatusComandoRemoto.Enviado, true)]
    [InlineData(TipoComandoRemoto.Bloquear, StatusComandoRemoto.Confirmado, false)]
    [InlineData(TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Pendente, false)]
    public void BloqueioAChegar_SoParaBloquearNaoConfirmado(
        TipoComandoRemoto tipo, StatusComandoRemoto status, bool esperado)
    {
        Assert.Equal(esperado, RegrasDispositivo.TemBloqueioAChegar(Comando(tipo, status)));
    }

    [Fact]
    public void BloqueioAChegar_SemComando_EhFalso()
    {
        Assert.False(RegrasDispositivo.TemBloqueioAChegar(null));
    }

    /// <summary>Desbloquear não tira o aparelho de Baixado ou Em manutenção — decisões da TI.</summary>
    [Theory]
    [InlineData(StatusDispositivo.Baixado)]
    [InlineData(StatusDispositivo.EmManutencao)]
    [InlineData(StatusDispositivo.EmEstoque)]
    public void Desbloqueio_SoMexeEmBloqueado(StatusDispositivo status)
    {
        var notebook = new Notebook { Status = status, UsuarioAtualId = Guid.NewGuid() };

        Assert.Equal(status, RegrasDispositivo.StatusAposDesbloqueio(notebook));
    }

    [Fact]
    public void Desbloqueio_DeUsuarioComDetentor_VoltaParaEmUso()
    {
        var notebook = new Notebook { Status = StatusDispositivo.Bloqueado, UsuarioAtualId = Guid.NewGuid() };

        Assert.Equal(StatusDispositivo.EmUso, RegrasDispositivo.StatusAposDesbloqueio(notebook));
    }

    [Fact]
    public void Desbloqueio_DeUsuarioSemDetentor_VaiParaEstoque()
    {
        var notebook = new Notebook { Status = StatusDispositivo.Bloqueado };

        Assert.Equal(StatusDispositivo.EmEstoque, RegrasDispositivo.StatusAposDesbloqueio(notebook));
    }

    /// <summary>Ativo do local não tem detentor: quem o mantém "em uso" é a unidade.</summary>
    [Fact]
    public void Desbloqueio_DeLocalComUnidade_VoltaParaEmUso()
    {
        var mobile = new Mobile
        {
            Status = StatusDispositivo.Bloqueado,
            PertenceAUsuario = false,
            UnidadeId = Guid.NewGuid(),
        };

        Assert.Equal(StatusDispositivo.EmUso, RegrasDispositivo.StatusAposDesbloqueio(mobile));
    }

    [Fact]
    public void Desbloqueio_DeLocalSemUnidade_VaiParaEstoque()
    {
        var mobile = new Mobile { Status = StatusDispositivo.Bloqueado, PertenceAUsuario = false };

        Assert.Equal(StatusDispositivo.EmEstoque, RegrasDispositivo.StatusAposDesbloqueio(mobile));
    }

    /// <summary>De local para usuário: ainda sem detentor, "Em uso" não se sustenta.</summary>
    [Fact]
    public void TrocaDePosse_EmUsoSemResponsavel_VoltaParaEstoque()
    {
        var notebook = new Notebook
        {
            Status = StatusDispositivo.EmUso,
            PertenceAUsuario = true,
            UnidadeId = Guid.NewGuid(),
        };

        Assert.Equal(StatusDispositivo.EmEstoque, RegrasDispositivo.StatusCoerenteComResponsavel(notebook));
    }

    [Fact]
    public void TrocaDePosse_NaoMexeEmBloqueado()
    {
        var notebook = new Notebook { Status = StatusDispositivo.Bloqueado, PertenceAUsuario = true };

        Assert.Equal(StatusDispositivo.Bloqueado, RegrasDispositivo.StatusCoerenteComResponsavel(notebook));
    }

    [Fact]
    public void TrocaDePosse_ParaLocalComUnidade_MantemEmUso()
    {
        var notebook = new Notebook
        {
            Status = StatusDispositivo.EmUso,
            PertenceAUsuario = false,
            UnidadeId = Guid.NewGuid(),
        };

        Assert.Equal(StatusDispositivo.EmUso, RegrasDispositivo.StatusCoerenteComResponsavel(notebook));
    }
}
