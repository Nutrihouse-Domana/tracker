using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tracker.Application.Seguranca;
using Tracker.Infrastructure.Identity;

namespace Tracker.Infrastructure.Data;

/// <summary>
/// Cria a role Admin e o primeiro Usuario ADM no primeiro start, a partir de
/// Seed:AdmEmail / Seed:AdmSenha (configuração/user-secrets) — resolve o problema
/// de bootstrap: sem isso, ninguém consegue logar para criar o primeiro ADM.
/// </summary>
public static class DbSeeder
{
    public const string RoleAdmin = "Admin";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<UsuarioADM>>();

        if (!await roleManager.RoleExistsAsync(RoleAdmin))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(RoleAdmin));
        }

        // Caminho de UPGRADE (idempotente): instalações anteriores têm ADMs na role Admin, mas ZERO
        // permissões — e, com as policies em vigor, ficariam trancados fora de tudo após o deploy.
        // Roda a cada start, mas só toca quem NÃO tem permissão nenhuma (ver PrecisaDoBackfill).
        await GarantirPermissoesDosAdminsAsync(userManager, logger);

        if (userManager.Users.Any()) return;

        var email = config["Seed:AdmEmail"];
        var senha = config["Seed:AdmSenha"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            logger.LogWarning(
                "Nenhum Usuario ADM encontrado e Seed:AdmEmail/Seed:AdmSenha não configurados — " +
                "defina via 'dotnet user-secrets' para criar o primeiro ADM automaticamente.");
            return;
        }

        var admin = new UsuarioADM
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = config["Seed:AdmNome"] ?? "Administrador",
        };

        var resultado = await userManager.CreateAsync(admin, senha);
        if (!resultado.Succeeded)
        {
            logger.LogError("Falha ao criar o ADM inicial: {Erros}",
                string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, RoleAdmin);
        await ConcederTodasAsPermissoesAsync(userManager, admin);
        logger.LogInformation("Usuario ADM inicial criado: {Email}", email);
    }

    /// <summary>Concede a um ADM toda permissão do catálogo que ele ainda não tenha (idempotente).</summary>
    private static async Task ConcederTodasAsPermissoesAsync(UserManager<UsuarioADM> userManager, UsuarioADM usuario)
    {
        var existentes = (await userManager.GetClaimsAsync(usuario))
            .Where(c => c.Type == Permissoes.TipoClaim)
            .Select(c => c.Value)
            .ToHashSet();

        var faltando = Permissoes.Todas
            .Where(p => !existentes.Contains(p))
            .Select(p => new Claim(Permissoes.TipoClaim, p))
            .ToList();

        if (faltando.Count > 0)
            await userManager.AddClaimsAsync(usuario, faltando);
    }

    /// <summary>
    /// Backfill do upgrade: concede o conjunto completo SÓ aos ADMs da role Admin que não têm
    /// nenhuma permissão (a instalação antiga, anterior às policies).
    /// </summary>
    /// <remarks>
    /// Antes, completava as permissões de TODO Admin a cada start — o que desfazia em silêncio
    /// qualquer revogação feita na tela de Acessos assim que o site reiniciava (deploy, reciclagem
    /// do IIS). Quem já tem ao menos uma permissão foi configurado por alguém e não é tocado.
    /// </remarks>
    private static async Task GarantirPermissoesDosAdminsAsync(UserManager<UsuarioADM> userManager, ILogger logger)
    {
        var admins = await userManager.GetUsersInRoleAsync(RoleAdmin);
        var concedidos = 0;
        foreach (var admin in admins)
        {
            var claims = await userManager.GetClaimsAsync(admin);

            if (PrecisaDoBackfill(claims))
            {
                await ConcederTodasAsPermissoesAsync(userManager, admin);
                concedidos++;
            }

            // Marca o ADM como "já configurado": se um dia alguém lhe tirar TODAS as permissões
            // na tela de Acessos, o próximo start não as devolve (sem a marca, zero permissões
            // seria indistinguível da instalação antiga).
            if (!claims.Any(c => c.Type == TipoClaimPermissoesConfiguradas))
                await userManager.AddClaimAsync(admin, new Claim(TipoClaimPermissoesConfiguradas, "1"));
        }

        if (concedidos > 0)
            logger.LogInformation(
                "Upgrade: permissões completas concedidas a {Total} ADM(s) da role Admin sem permissão alguma.",
                concedidos);
    }

    /// <summary>
    /// Claim que marca um ADM cujas permissões já passaram pelo backfill (ou já existiam). Não é
    /// permissão: a <c>PermissoesClaimsTransformation</c> e as policies só olham o tipo "perm".
    /// </summary>
    public const string TipoClaimPermissoesConfiguradas = "tracker.permissoes-configuradas";

    /// <summary>
    /// True só para o ADM da instalação antiga: nenhuma claim de permissão E sem a marca de
    /// "já configurado". Ter qualquer permissão — ou a marca — significa que o acesso já foi
    /// definido por alguém e não deve ser tocado.
    /// </summary>
    public static bool PrecisaDoBackfill(IEnumerable<Claim> claims)
    {
        var lista = claims as IReadOnlyCollection<Claim> ?? claims.ToList();
        return !lista.Any(c => c.Type == Permissoes.TipoClaim)
            && !lista.Any(c => c.Type == TipoClaimPermissoesConfiguradas);
    }

    /// <summary>
    /// Redefine a senha de um Usuario ADM existente a partir de Seed:AdmSenha.
    /// </summary>
    /// <remarks>
    /// Recuperação de acesso: o SeedAsync só cria o ADM quando não existe nenhum usuário, então
    /// uma senha perdida deixaria a TI trancada fora do sistema. Este caminho preserva a conta e
    /// todo o histórico que aponta para ela (movimentações, comentários, comandos emitidos) —
    /// apagar o usuário para o seeder recriá-lo perderia esses vínculos.
    ///
    /// Só roda quando explicitamente pedido pela linha de comando, nunca no start normal.
    /// </remarks>
    public static async Task<bool> RedefinirSenhaAdmAsync(
        IServiceProvider services, string? email = null, string? senha = null)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = services.GetRequiredService<TrackerDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<UsuarioADM>>();
        var userManager = services.GetRequiredService<UserManager<UsuarioADM>>();

        var alvo = email ?? config["Seed:AdmEmail"];
        var novaSenha = senha ?? config["Seed:AdmSenha"];

        if (string.IsNullOrWhiteSpace(novaSenha))
        {
            logger.LogError("Informe a senha em --senha (ou configure Seed:AdmSenha).");
            return false;
        }

        // Projeção em vez de carregar a entidade: UsuarioADM tem Cpf criptografado em repouso, e
        // materializá-lo exigiria a chave AES correta. Recuperar o acesso não pode depender dela
        // — é exatamente o tipo de configuração que falta quando algo já deu errado.
        var contas = await db.Users
            .Select(u => new { u.Id, u.Email, u.Ativo })
            .ToListAsync();

        // Sem e-mail informado, cai para o único ADM existente: o caso comum de recuperação, em
        // que há uma só conta e quem executa não lembra qual é.
        var conta = string.IsNullOrWhiteSpace(alvo)
            ? (contas.Count == 1 ? contas[0] : null)
            : contas.FirstOrDefault(c => string.Equals(c.Email, alvo, StringComparison.OrdinalIgnoreCase));

        if (conta is null)
        {
            logger.LogError(
                string.IsNullOrWhiteSpace(alvo)
                    ? "Existem {Total} contas ADM: informe qual em --email."
                    : "Nenhum Usuario ADM com o e-mail {Email}.",
                string.IsNullOrWhiteSpace(alvo) ? contas.Count.ToString() : alvo);
            return false;
        }

        // Instância em memória (sem o Cpf criptografado) para os validadores e o hasher, que não
        // dependem do registro do banco.
        var emMemoria = new UsuarioADM { Id = conta.Id, Email = conta.Email, UserName = conta.Email };

        // Mesmas regras de senha do Identity (tamanho, dígito, maiúscula, símbolo...) que a tela de
        // Acessos aplica. Checar só o comprimento aceitava uma senha que o próprio sistema recusaria
        // numa troca normal.
        var errosSenha = new List<string>();
        foreach (var validador in userManager.PasswordValidators)
        {
            var resultado = await validador.ValidateAsync(userManager, emMemoria, novaSenha);
            if (!resultado.Succeeded) errosSenha.AddRange(resultado.Errors.Select(e => e.Description));
        }
        if (errosSenha.Count > 0)
        {
            logger.LogError("Senha recusada pelas regras de senha: {Erros}", string.Join("; ", errosSenha));
            return false;
        }

        // O formato do hash fica idêntico ao de um reset normal.
        var hash = hasher.HashPassword(emMemoria, novaSenha);

        // SecurityStamp novo invalida os cookies emitidos antes. LockoutEnd/AccessFailedCount
        // zerados porque uma conta travada por tentativas erradas continuaria recusando o login
        // mesmo com a senha certa, e o motivo não seria óbvio para quem está tentando entrar.
        var linhas = await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE AspNetUsers
               SET PasswordHash = {0}, SecurityStamp = {1}, LockoutEnd = NULL, AccessFailedCount = 0
             WHERE Id = {2}
            """,
            hash, Guid.NewGuid().ToString("N"), conta.Id);

        if (linhas != 1)
        {
            logger.LogError("A atualização afetou {Linhas} linhas — esperado 1.", linhas);
            return false;
        }

        logger.LogInformation("Senha redefinida para {Email}.", conta.Email);

        // Não reativa sozinho: desativar foi decisão de alguém (ex.: saída do colaborador), e a
        // recuperação de senha não deve desfazê-la em silêncio. Só avisa por que o login vai falhar.
        if (!conta.Ativo)
        {
            logger.LogWarning(
                "ATENÇÃO: o login {Email} está DESATIVADO — a senha foi trocada, mas o login continuará " +
                "recusado. Reative-o na tela de Acessos com outro ADM, ou no banco (AspNetUsers.Ativo = 1) " +
                "se ele for o único.", conta.Email);
        }

        return true;
    }
}
