using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Tracker.Infrastructure.Security;

/// <summary>EF Core ValueConverter que criptografa/descriptografa um campo de texto (ex.: CPF) em repouso.</summary>
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(AesStringEncryptor encryptor)
        : base(
            v => encryptor.Encrypt(v),
            v => encryptor.Decrypt(v))
    {
    }
}
