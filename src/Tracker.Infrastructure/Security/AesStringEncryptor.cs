using System.Security.Cryptography;

namespace Tracker.Infrastructure.Security;

/// <summary>
/// Criptografia AES-GCM para campos sensíveis (CPF) em repouso.
/// A chave vem de configuração (appsettings/variável de ambiente) — em produção,
/// mover para um cofre de segredos (ex.: Azure Key Vault, ou DPAPI se on-prem puro).
/// </summary>
public class AesStringEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesStringEncryptor(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("A chave de criptografia deve ter 32 bytes (AES-256).", nameof(key));
        _key = key;
    }

    public string Encrypt(string plainText)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipherBytes.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var nonce = data[..NonceSize];
        var tag = data[^TagSize..];
        var cipherBytes = data[NonceSize..^TagSize];
        var plainBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
