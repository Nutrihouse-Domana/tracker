using PdfSharpCore.Fonts;

namespace Tracker.Infrastructure.Pdf;

/// <summary>
/// Registro do resolver de fonte do PdfSharpCore. É GLOBAL e só pode ser atribuído uma vez por
/// processo, então todo gerador de PDF (termo de uso, relatórios) passa por aqui em vez de
/// escrever em <c>GlobalFontSettings.FontResolver</c> por conta própria. Sem resolver, a geração
/// estoura em runtime no servidor quando a fonte pedida não está instalada (ver CLAUDE.md).
/// </summary>
internal static class PdfFontes
{
    private static readonly object _lock = new();
    private static bool _registrado;

    public static void Garantir()
    {
        if (_registrado) return;
        lock (_lock)
        {
            if (_registrado) return;
            GlobalFontSettings.FontResolver = TermoFontResolver.Instancia;
            _registrado = true;
        }
    }
}
