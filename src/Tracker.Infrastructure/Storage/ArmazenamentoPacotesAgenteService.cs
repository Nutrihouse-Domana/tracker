using System.Security.Cryptography;

namespace Tracker.Infrastructure.Storage;

public interface IArmazenamentoPacotesAgenteService
{
    /// <summary>Salva o APK em disco e retorna (caminho relativo, hash SHA-256 hex, tamanho em bytes).</summary>
    Task<(string caminhoRelativo, string hashSha256, long tamanhoBytes)> SalvarPacoteAsync(
        Stream conteudo, CancellationToken ct = default);

    Task<Stream> AbrirAsync(string caminhoRelativo);

    /// <summary>Apaga o arquivo do pacote. Usado quando o upload é rejeitado ou uma versão é removida.</summary>
    void Excluir(string caminhoRelativo);
}

/// <summary>
/// Guarda os APKs do agente Android publicados para atualização OTA, em um diretório local
/// (on-premises). Caminho raiz configurável via "Storage:PacotesAgentePath".
/// </summary>
public class ArmazenamentoPacotesAgenteService : IArmazenamentoPacotesAgenteService
{
    private readonly string _raiz;

    public ArmazenamentoPacotesAgenteService(string raiz)
    {
        _raiz = raiz;
        Directory.CreateDirectory(_raiz);
    }

    public async Task<(string caminhoRelativo, string hashSha256, long tamanhoBytes)> SalvarPacoteAsync(
        Stream conteudo, CancellationToken ct = default)
    {
        // Nome próprio (não o original): evita colisão e qualquer caractere problemático no share.
        var nomeArquivo = $"{Guid.NewGuid()}.apk";
        var caminhoCompleto = Path.Combine(_raiz, nomeArquivo);

        using (var destino = File.Create(caminhoCompleto))
        {
            await conteudo.CopyToAsync(destino, ct);
        }

        string hash;
        long tamanho;
        using (var sha256 = SHA256.Create())
        using (var leitura = File.OpenRead(caminhoCompleto))
        {
            tamanho = leitura.Length;
            hash = Convert.ToHexString(await sha256.ComputeHashAsync(leitura, ct));
        }

        return (nomeArquivo, hash, tamanho);
    }

    public Task<Stream> AbrirAsync(string caminhoRelativo)
    {
        Stream stream = File.OpenRead(Path.Combine(_raiz, caminhoRelativo));
        return Task.FromResult(stream);
    }

    public void Excluir(string caminhoRelativo)
    {
        var caminho = Path.Combine(_raiz, caminhoRelativo);
        if (File.Exists(caminho)) File.Delete(caminho);
    }
}
