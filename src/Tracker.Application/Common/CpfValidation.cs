using System.Text.RegularExpressions;

namespace Tracker.Application.Common;

/// <summary>Validação de formato e dígitos verificadores de CPF.</summary>
public static partial class CpfValidation
{
    public static bool EhValido(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;

        var digits = SomenteDigitosRegex().Replace(cpf, "");
        if (digits.Length != 11) return false;
        if (new string(digits[0], 11) == digits) return false; // 111.111.111-11 etc.

        var numeros = digits.Select(c => c - '0').ToArray();

        var soma1 = 0;
        for (var i = 0; i < 9; i++) soma1 += numeros[i] * (10 - i);
        var resto1 = soma1 % 11;
        var digito1 = resto1 < 2 ? 0 : 11 - resto1;
        if (numeros[9] != digito1) return false;

        var soma2 = 0;
        for (var i = 0; i < 10; i++) soma2 += numeros[i] * (11 - i);
        var resto2 = soma2 % 11;
        var digito2 = resto2 < 2 ? 0 : 11 - resto2;
        return numeros[10] == digito2;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex SomenteDigitosRegex();
}
