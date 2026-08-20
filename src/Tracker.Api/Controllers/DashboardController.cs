using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissoes.DispositivosVer)]
public class DashboardController : ControllerBase
{
    private readonly TrackerDbContext _db;

    public DashboardController(TrackerDbContext db)
    {
        _db = db;
    }

    [HttpGet("estatisticas")]
    public async Task<ActionResult<EstatisticasDashboardDto>> Estatisticas()
    {
        var porTipo = await _db.Dispositivos.AsNoTracking()
            .GroupBy(d => d.Tipo)
            .Select(g => new { Tipo = g.Key, Quantidade = g.Count() })
            .ToListAsync();

        var porStatus = await _db.Dispositivos.AsNoTracking()
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Quantidade = g.Count() })
            .ToListAsync();

        int Qtd<T>(List<(T chave, int qtd)> lista, T valor) =>
            lista.FirstOrDefault(x => EqualityComparer<T>.Default.Equals(x.chave, valor)).qtd;

        var tipos = porTipo.Select(x => (x.Tipo, x.Quantidade)).ToList();
        var status = porStatus.Select(x => (x.Status, x.Quantidade)).ToList();

        var dto = new EstatisticasDashboardDto(
            TotalNotebooks: Qtd(tipos, TipoDispositivo.Notebook),
            TotalMobiles: Qtd(tipos, TipoDispositivo.Mobile),
            TotalPerifericos: Qtd(tipos, TipoDispositivo.Periferico),
            EmUso: Qtd(status, StatusDispositivo.EmUso),
            EmEstoque: Qtd(status, StatusDispositivo.EmEstoque),
            EmManutencao: Qtd(status, StatusDispositivo.EmManutencao),
            Bloqueados: Qtd(status, StatusDispositivo.Bloqueado),
            Baixados: Qtd(status, StatusDispositivo.Baixado));

        return Ok(dto);
    }
}
