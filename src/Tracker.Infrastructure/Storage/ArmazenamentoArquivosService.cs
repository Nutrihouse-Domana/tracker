using System.Security.Cryptography;

namespace Tracker.Infrastructure.Storage;

public interface IArmazenamentoArquivosService
{
    /// <summary>Salva o arquivo em disco, organizado por dispositivo, e retorna (caminho relativo, hash SHA-256).</summary>
    Task<(string caminhoRelativo, string hashSha256)> SalvarTermoAssinadoAsync(
        Guid dispositivoId, Stream conteudo, string nomeArquivoOriginal, CancellationToken ct = default);

    Task<Stream> AbrirAsync(string caminhoRelativo);
}

/// <summary>
/// Armazena os termos de uso assinados em um file share local (on-premises).
/// Caminho raiz configurável via "Storage:TermosDeUsoPath".
/// </summary>
public class ArmazenamentoArquivosService : IArmazenamentoArquivosService
{
    private readonly string _raiz;

    public ArmazenamentoArquivosService(string raiz)
    {
        _raiz = raiz;
        Directory.CreateDirectory(_raiz);
    }

    public async Task<(string caminhoRelativo, string hashSha256)> SalvarTermoAssinadoAsync(
        Guid dispositivoId, Stream conteudo, string nomeArquivoOriginal, CancellationToken ct = default)
    {
        var extensao = Path.GetExtension(nomeArquivoOriginal) is { Length: > 0 } ext ? ext : ".pdf";
        var pastaDispositivo = Path.Combine(_raiz, dispositivoId.ToString());
        Directory.CreateDirectory(pastaDispositivo);

        var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
        var caminhoCompleto = Path.Combine(pastaDispositivo, nomeArquivo);

        using (var destino = File.Create(caminhoCompleto))
        {
            await conteudo.CopyToAsync(destino, ct);
        }

        string hash;
        using (var sha256 = SHA256.Create())
        using (var leitura = File.OpenRead(caminhoCompleto))
        {
            hash = Convert.ToHexString(await sha256.ComputeHashAsync(leitura, ct));
        }

        var caminhoRelativo = Path.Combine(dispositivoId.ToString(), nomeArquivo);
        return (caminhoRelativo, hash);
    }

    public Task<Stream> AbrirAsync(string caminhoRelativo)
    {
        Stream stream = File.OpenRead(Path.Combine(_raiz, caminhoRelativo));
        return Task.FromResult(stream);
    }
}
