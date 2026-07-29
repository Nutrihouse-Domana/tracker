using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>Detentor de um ou mais dispositivos. Não possui login na plataforma.</summary>
public class Usuario : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();
}
