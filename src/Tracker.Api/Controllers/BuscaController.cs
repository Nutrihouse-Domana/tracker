using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Controllers;

/// <summary>
/// Busca global do topo do site: procura dispositivos (nº de série, asset tag, hostname, IMEI,
/// nome do usuário atual) e usuários (nome, e-mail, telefone). CPF é criptografado em repouso,
/// portanto não é pesquisável.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissoes.DispositivosVer)]
public class BuscaController : ControllerBase
{
    private const int TamanhoMinimoTermo = 2;
    private readonly TrackerDbContext _db;

    public BuscaController(TrackerDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<BuscaGlobalDto>> Buscar([FromQuery] string? q, [FromQuery] int limite = 8)
    {
        var termo = (q ?? string.Empty).Trim();
        if (termo.Length < TamanhoMinimoTermo)
            return Ok(new BuscaGlobalDto(termo, Array.Empty<BuscaDispositivoDto>(), Array.Empty<BuscaUsuarioDto>()));

        limite = Math.Clamp(limite, 1, 50);
        var padrao = $"%{EscaparCuringas(termo)}%";

        var dispositivos = await BuscarDispositivos(padrao, limite);
        var usuarios = await BuscarUsuarios(padrao, limite);

        return Ok(new BuscaGlobalDto(termo, dispositivos, usuarios));
    }

    /// <summary>
    /// Coleta os IDs em consultas separadas por tipo concreto porque a hierarquia TPH não permite
    /// consultar <c>DispositivoComAgente</c> (classe intermediária sem discriminador) diretamente.
    /// Ao criar um novo tipo com agente, é obrigatório incluí-lo aqui — senão ele fica invisível
    /// na busca por hostname/IMEI.
    /// </summary>
    private async Task<List<BuscaDispositivoDto>> BuscarDispositivos(string padrao, int limite)
    {
        var idsBase = _db.Dispositivos
            .Where(d => EF.Functions.Like(d.NumeroSerie, padrao, "\\")
                        || EF.Functions.Like(d.AssetTag, padrao, "\\")
                        || (d.UsuarioAtual != null && EF.Functions.Like(d.UsuarioAtual.Nome, padrao, "\\"))
                        || (d.Unidade != null && EF.Functions.Like(d.Unidade.Nome, padrao, "\\")))
            .Select(d => d.Id);

        var idsNotebooks = _db.Set<Notebook>()
            .Where(n => EF.Functions.Like(n.Hostname, padrao, "\\"))
            .Select(n => n.Id);

        var idsMobiles = _db.Set<Mobile>()
            .Where(m => EF.Functions.Like(m.Hostname, padrao, "\\")
                        || EF.Functions.Like(m.Imei, padrao, "\\"))
            .Select(m => m.Id);

        var ids = await idsBase.Union(idsNotebooks).Union(idsMobiles).ToListAsync();
        if (ids.Count == 0) return [];

        var encontrados = await _db.Dispositivos.AsNoTracking()
            .Include(d => d.UsuarioAtual)
            .Include(d => d.Unidade)
            .Where(d => ids.Contains(d.Id))
            .OrderBy(d => d.Tipo)
            .ThenBy(d => d.AssetTag)
            .Take(limite)
            .ToListAsync();

        return encontrados.Select(d => new BuscaDispositivoDto(
            d.Id,
            d.Tipo,
            d.Status,
            d.NumeroSerie,
            d.AssetTag,
            (d as DispositivoComAgente)?.Hostname,
            (d as Mobile)?.Imei,
            d.UsuarioAtual?.Nome,
            d.Unidade?.Nome,
            d.PertenceAUsuario ? d.UsuarioAtual?.Nome : d.Unidade?.Nome)).ToList();
    }

    private Task<List<BuscaUsuarioDto>> BuscarUsuarios(string padrao, int limite) =>
        _db.Usuarios.AsNoTracking()
            .Where(u => EF.Functions.Like(u.Nome, padrao, "\\")
                        || EF.Functions.Like(u.Email, padrao, "\\")
                        || EF.Functions.Like(u.Telefone, padrao, "\\"))
            .OrderByDescending(u => u.Ativo)
            .ThenBy(u => u.Nome)
            .Take(limite)
            .Select(u => new BuscaUsuarioDto(u.Id, u.Nome, u.Email, u.Telefone, u.Ativo, u.Dispositivos.Count))
            .ToListAsync();

    /// <summary>Neutraliza os curingas do LIKE para que o termo digitado seja tratado como texto puro.</summary>
    private static string EscaparCuringas(string termo) =>
        termo.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
