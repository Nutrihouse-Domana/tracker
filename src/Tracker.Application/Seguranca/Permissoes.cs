namespace Tracker.Application.Seguranca;

/// <summary>
/// Catálogo central de permissões do Tracker. Cada permissão é gravada como um claim do tipo
/// <see cref="TipoClaim"/> no usuário ADM (tabela AspNetUserClaims) e vira uma policy de mesmo nome
/// em <c>Program.cs</c>. Novos valores entram aqui e no <see cref="Todas"/> — não há schema a migrar.
/// </summary>
public static class Permissoes
{
    /// <summary>Tipo do claim que carrega cada permissão no principal do usuário logado.</summary>
    public const string TipoClaim = "perm";

    // Permissão-mestra: administra os LOGINS (UsuarioADM) e suas permissões. Não confundir com
    // "usuarios.gerir", que trata dos DETENTORES de dispositivos (entidade Usuario, sem login).
    public const string AcessosGerir = "acessos.gerir";

    public const string DispositivosVer = "dispositivos.ver";
    public const string DispositivosCadastrar = "dispositivos.cadastrar";
    public const string DispositivosMovimentar = "dispositivos.movimentar";
    public const string DispositivosExcluir = "dispositivos.excluir";
    public const string DispositivosExcluirDefinitivo = "dispositivos.excluir_definitivo";
    public const string DispositivosBloquear = "dispositivos.bloquear";

    public const string AgentePublicar = "agente.publicar";

    public const string UsuariosGerir = "usuarios.gerir";
    public const string UnidadesGerir = "unidades.gerir";

    public const string AuditoriaVer = "auditoria.ver";

    /// <summary>Uma permissão do catálogo, com os metadados que a tela de Acessos usa nos checkboxes.</summary>
    public record Definicao(string Valor, string Rotulo, string Descricao, string Grupo);

    /// <summary>
    /// Catálogo completo, na ordem em que aparece na UI. A ordem define a apresentação; o valor é o
    /// que é gravado e verificado — nunca renomeie um valor já em uso (viraria permissão órfã).
    /// </summary>
    public static readonly IReadOnlyList<Definicao> Catalogo = new List<Definicao>
    {
        new(DispositivosVer, "Ver dispositivos", "Ver o dashboard, a busca, a lista e os detalhes dos dispositivos.", "Dispositivos"),
        new(DispositivosCadastrar, "Cadastrar/editar", "Criar e editar dispositivos e definir a unidade que atendem.", "Dispositivos"),
        new(DispositivosMovimentar, "Movimentar", "Trocar posse, movimentar, registrar reparos, comentários e termos de uso.", "Dispositivos"),
        new(DispositivosExcluir, "Arquivar", "Arquivar dispositivos (exclusão lógica, restaurável).", "Dispositivos"),
        new(DispositivosExcluirDefinitivo, "Excluir definitivo", "Apagar de vez um dispositivo e todo o histórico. Ação irreversível.", "Dispositivos"),
        new(DispositivosBloquear, "Bloquear/desbloquear", "Enviar comando remoto de bloqueio e desbloqueio de aparelhos.", "Dispositivos"),
        new(AgentePublicar, "Publicar APK do agente", "Publicar uma nova versão do agente Android para a frota (OTA).", "Agente"),
        new(UsuariosGerir, "Gerir usuários (detentores)", "Cadastrar e editar os detentores de dispositivos.", "Cadastros"),
        new(UnidadesGerir, "Gerir unidades", "Cadastrar e editar as unidades (matriz, filiais).", "Cadastros"),
        new(AuditoriaVer, "Ver auditoria", "Consultar a trilha de auditoria (quem fez o quê e quando).", "Administração"),
        new(AcessosGerir, "Gerir acessos", "Criar/editar logins e definir as permissões de cada usuário.", "Administração"),
    };

    /// <summary>Só os valores, para registrar as policies e conceder o conjunto completo ao ADM inicial.</summary>
    public static readonly IReadOnlyList<string> Todas = Catalogo.Select(d => d.Valor).ToList();

    /// <summary>True se o valor pertence ao catálogo — usado para rejeitar permissões inventadas no request.</summary>
    public static bool EhValida(string valor) => Todas.Contains(valor);
}
