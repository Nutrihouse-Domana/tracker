using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        logger.LogInformation("Usuario ADM inicial criado: {Email}", email);
    }
}
