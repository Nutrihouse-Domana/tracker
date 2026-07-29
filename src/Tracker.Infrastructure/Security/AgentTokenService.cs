using System.Security.Cryptography;

namespace Tracker.Infrastructure.Security;

/// <summary>
/// Gera e valida tokens de agente por dispositivo. O token em texto puro é entregue
/// uma única vez no enrollment; no banco guardamos apenas o hash SHA-256.
/// </summary>
public class AgentTokenService
{
    /// <summary>Gera um novo token (texto puro) e o hash correspondente para persistir.</summary>
    public (string token, string hash) Gerar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return (token, CalcularHash(token));
    }

    public string CalcularHash(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    /// <summary>Comparação em tempo constante entre o token recebido e o hash armazenado.</summary>
    public bool Validar(string token, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(hashArmazenado)) return false;
        var hashRecebido = CalcularHash(token);
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(hashRecebido),
            System.Text.Encoding.UTF8.GetBytes(hashArmazenado));
    }
}
