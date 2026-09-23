using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        // Aqui garantimos que todo ADM da role Admin tenha o conjunto completo. Roda a cada start,
        // mas só concede o que falta.
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

    /// <summary>Garante que todo ADM da role Admin tenha o conjunto completo de permissões.</summary>
    private static async Task GarantirPermissoesDosAdminsAsync(UserManager<UsuarioADM> userManager, ILogger logger)
    {
        var admins = await userManager.GetUsersInRoleAsync(RoleAdmin);
        foreach (var admin in admins)
            await ConcederTodasAsPermissoesAsync(userManager, admin);

        if (admins.Count > 0)
            logger.LogInformation("Permissões conferidas para {Total} ADM(s) da role Admin.", admins.Count);
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
        var opcoes = services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        var alvo = email ?? config["Seed:AdmEmail"];
        var novaSenha = senha ?? config["Seed:AdmSenha"];

        if (string.IsNullOrWhiteSpace(novaSenha))
        {
            logger.LogError("Informe a senha em --senha (ou configure Seed:AdmSenha).");
            return false;
        }

        var minimo = opcoes.Password.RequiredLength;
        if (novaSenha.Length < minimo)
        {
            logger.LogError("A senha precisa ter ao menos {Minimo} caracteres.", minimo);
            return false;
        }

        // Projeção em vez de carregar a entidade: UsuarioADM tem Cpf criptografado em repouso, e
        // materializá-lo exigiria a chave AES correta. Recuperar o acesso não pode depender dela
        // — é exatamente o tipo de configuração que falta quando algo já deu errado.
        var contas = await db.Users
            .Select(u => new { u.Id, u.Email })
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

        // O hash é gerado sobre uma instância em memória — o hasher do Identity não depende do
        // registro do banco, então o formato fica idêntico ao de um reset normal.
        var hash = hasher.HashPassword(new UsuarioADM { Email = conta.Email, UserName = conta.Email }, novaSenha);

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
        return true;
    }
}
