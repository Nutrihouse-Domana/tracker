using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tracker.Agent.Windows;

/// <summary>
/// Tela de bloqueio, executada NA SESSÃO DO USUÁRIO (via tarefa agendada). Cobre TODOS os
/// monitores com uma janela por tela, fica sempre por cima e não pode ser fechada pelo usuário.
/// Reconstrói as janelas quando a configuração de monitores muda (plugar/desplugar), evitando
/// que o desktop apareça. Só some quando o serviço remove o arquivo-sinal (Desbloquear da TI).
/// </summary>
public static class LockOverlay
{
    public static void Executar()
    {
        if (!File.Exists(Caminhos.FlagBloqueio)) return;

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.Run(new LockContext());
    }
}

/// <summary>
/// Gerencia o conjunto de janelas de bloqueio (uma por monitor), o gancho de teclado, o
/// marcador "vivo" e o encerramento. Um único timer cuida de tudo: some se o sinal sumir,
/// reconstrói se os monitores mudarem e reafirma o "sempre no topo".
/// </summary>
internal sealed class LockContext : ApplicationContext
{
    private readonly List<LockForm> _forms = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private GanchoTeclado? _hook;
    private string _assinaturaTelas = "";
    private bool _encerrando;

    public bool PodeFechar => _encerrando;

    /// <summary>Quando o painel de Wi-Fi está aberto, pausamos a reafirmação de "topo" e a
    /// reconstrução de telas, para não causar flicker/perda de foco durante a digitação.</summary>
    public static bool InteragindoWifi;

    public LockContext()
    {
        InteragindoWifi = false;
        _hook = new GanchoTeclado();
        DesabilitarGerenciadorTarefas(true);
        EscreverMarcadorVivo();
        Reconstruir();
        _timer.Tick += Tick;
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (!File.Exists(Caminhos.FlagBloqueio)) { Encerrar(); return; }
        EscreverMarcadorVivo();

        // Enquanto o usuário mexe no Wi-Fi, não reconstrói nem reafirma topo (evita flicker).
        if (InteragindoWifi) return;

        if (AssinaturaTelas() != _assinaturaTelas) Reconstruir();
        foreach (var f in _forms) f.TopMost = true;
    }

    private static string AssinaturaTelas() =>
        string.Join("|", Screen.AllScreens.Select(s => $"{s.Bounds}:{s.Primary}"));

    private void Reconstruir()
    {
        _assinaturaTelas = AssinaturaTelas();
        var antigos = _forms.ToList();
        _forms.Clear();

        foreach (var scr in Screen.AllScreens)
        {
            var f = new LockForm(scr, scr.Primary, this);
            _forms.Add(f);
            f.Show();
        }

        foreach (var f in antigos) f.FecharForcado();
        foreach (var f in _forms) { f.TopMost = true; f.Activate(); }
    }

    private void Encerrar()
    {
        _encerrando = true;
        _timer.Stop();
        _hook?.Dispose();
        _hook = null;
        DesabilitarGerenciadorTarefas(false);
        try { if (File.Exists(Caminhos.MarcadorOverlay)) File.Delete(Caminhos.MarcadorOverlay); } catch { }
        foreach (var f in _forms.ToList()) f.FecharForcado();
        ExitThread();
    }

    internal static void EscreverMarcadorVivo()
    {
        try { File.WriteAllText(Caminhos.MarcadorOverlay, DateTime.Now.ToString("o")); } catch { }
    }

    /// <summary>Liga/desliga a política que bloqueia o Gerenciador de Tarefas para o usuário atual.</summary>
    internal static void DesabilitarGerenciadorTarefas(bool desabilitar)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Policies\System");
            if (desabilitar) key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
            else key.DeleteValue("DisableTaskMgr", false);
        }
        catch { }
    }
}

/// <summary>Uma janela de bloqueio cobrindo exatamente um monitor. Só a principal traz o Wi-Fi.</summary>
internal sealed class LockForm : Form
{
    private readonly LockContext _ctx;
    private bool _permitirFechar;

    private ListBox _lstRedes = null!;
    private TextBox _txtSenha = null!;
    private Label _lblStatusWifi = null!;

    public LockForm(Screen tela, bool principal, LockContext ctx)
    {
        _ctx = ctx;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = tela.Bounds;
        BackColor = Color.FromArgb(20, 24, 33);

        if (principal) MontarConteudoPrincipal();
        else MontarConteudoSecundario();

        FormClosing += (_, e) => { if (!_permitirFechar && !_ctx.PodeFechar) e.Cancel = true; };
    }

    public void FecharForcado()
    {
        _permitirFechar = true;
        try { Close(); Dispose(); } catch { }
    }

    private void MontarConteudoSecundario()
    {
        Controls.Add(new Label
        {
            Text = "Dispositivo bloqueado — Nutrihouse",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        });
    }

    private void MontarConteudoPrincipal()
    {
        var titulo = new Label
        {
            Text = "Dispositivo bloqueado",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 34, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 200,
        };
        var msg = new Label
        {
            Text = "Este equipamento foi bloqueado remotamente pela equipe de TI da Nutrihouse.\n" +
                   "Entre em contato com o suporte para liberar o acesso.\n\n" +
                   "O desbloqueio só pode ser feito pela TI.",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 15),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
        };

        // Top e Bottom primeiro; Fill (msg) por último.
        Controls.Add(titulo);
        Controls.Add(MontarPainelWifi());
        Controls.Add(msg);
    }

    private Control MontarPainelWifi()
    {
        int cx = Width / 2; // centraliza no MONITOR principal (não na área total)
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 240, BackColor = Color.FromArgb(14, 17, 24) };

        var btnAbrir = new Button
        {
            Text = "Conectar a uma rede Wi-Fi",
            Font = new Font("Segoe UI", 11),
            Size = new Size(280, 40),
            Location = new Point(cx - 140, 10),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 46, 58),
        };

        var grupo = new Panel { Visible = false, Size = new Size(560, 170), Location = new Point(cx - 280, 58) };

        // Lista FIXA (sem dropdown) — evita o flicker de lista suspensa sobre janela topmost.
        _lstRedes = new ListBox
        {
            Width = 560, Height = 96, Location = new Point(0, 0),
            Font = new Font("Segoe UI", 11),
            BackColor = Color.FromArgb(28, 33, 44), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
        };
        var btnAtualizar = new Button
        {
            Text = "Atualizar lista", Location = new Point(0, 104), Size = new Size(130, 30),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 46, 58),
        };
        _txtSenha = new TextBox
        {
            Width = 230, Location = new Point(140, 106), UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 11), PlaceholderText = "Senha (vazio p/ rede aberta)",
        };
        var btnConectar = new Button
        {
            Text = "Conectar", Location = new Point(380, 104), Size = new Size(180, 30),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 125, 50),
        };
        _lblStatusWifi = new Label
        {
            Location = new Point(0, 142), Size = new Size(560, 26), ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 10),
        };

        grupo.Controls.AddRange([_lstRedes, btnAtualizar, _txtSenha, btnConectar, _lblStatusWifi]);

        btnAbrir.Click += (_, _) =>
        {
            grupo.Visible = !grupo.Visible;
            LockContext.InteragindoWifi = grupo.Visible;
            if (grupo.Visible) AtualizarRedes();
        };
        btnAtualizar.Click += (_, _) => AtualizarRedes();
        btnConectar.Click += (_, _) => ConectarWifi();

        bar.Controls.Add(grupo);
        bar.Controls.Add(btnAbrir);
        return bar;
    }

    private void AtualizarRedes()
    {
        _lstRedes.Items.Clear();
        _lblStatusWifi.Text = "Procurando redes...";
        try
        {
            if (!WifiConnect.TemInterfaceWifi()) { _lblStatusWifi.Text = "Nenhum adaptador Wi-Fi neste dispositivo."; return; }
            var redes = WifiConnect.ListarRedes();
            foreach (var r in redes) _lstRedes.Items.Add(r);
            if (_lstRedes.Items.Count > 0)
            {
                _lstRedes.SelectedIndex = 0;
                _lblStatusWifi.Text = $"{redes.Count} rede(s) encontrada(s). Selecione e conecte.";
            }
            else _lblStatusWifi.Text = "Nenhuma rede encontrada. Tente 'Atualizar lista'.";
        }
        catch (Exception ex) { _lblStatusWifi.Text = "Erro ao listar redes: " + ex.Message; }
    }

    private void ConectarWifi()
    {
        var ssid = _lstRedes.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(ssid)) { _lblStatusWifi.Text = "Selecione uma rede na lista."; return; }
        _lblStatusWifi.Text = "Conectando...";
        var (_, msg) = WifiConnect.Conectar(ssid, _txtSenha.Text);
        _lblStatusWifi.Text = msg;
        _txtSenha.Clear();
    }
}

/// <summary>
/// Gancho de teclado de baixo nível que engole atalhos de fuga (Alt+Tab, tecla Windows,
/// Alt+Esc, Ctrl+Esc, Ctrl+Shift+Esc). Ctrl+Alt+Del não é bloqueável em modo usuário.
/// </summary>
internal sealed class GanchoTeclado : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
    private readonly IntPtr _hook;
    private readonly LowLevelKeyboardProc _proc;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public GanchoTeclado()
    {
        _proc = Callback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vk = info.vkCode;
            bool alt = (GetKeyState(0x12) & 0x8000) != 0;
            bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;

            bool bloquear =
                vk is 0x5B or 0x5C ||       // LWin / RWin
                (alt && vk == 0x09) ||      // Alt+Tab
                (alt && vk == 0x1B) ||      // Alt+Esc
                (ctrl && vk == 0x1B);       // Ctrl+Esc / Ctrl+Shift+Esc

            if (bloquear) return 1;
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
    }
}
