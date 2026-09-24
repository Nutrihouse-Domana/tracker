using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tracker.Agent.Windows;

/// <summary>
/// Tela de bloqueio, executada NA SESSÃO DO USUÁRIO (via tarefa agendada). Cobre TODOS os
/// monitores com uma janela por tela, fica sempre por cima e não pode ser fechada pelo usuário.
/// Reconstrói as janelas quando a configuração de monitores muda (plugar/desplugar), evitando
/// que o desktop apareça. Só some quando o serviço remove o arquivo-sinal (Desbloquear da TI).
///
/// A tela NÃO grava nada em disco: o serviço descobre se ela está viva olhando o PROCESSO na
/// sessão (ver <see cref="BloqueioTela"/>). As pastas do agente são somente leitura para usuários.
/// </summary>
public static class LockOverlay
{
    /// <summary>Uma tela por sessão. "Local\" é o namespace da sessão: cada usuário logado tem o seu.</summary>
    private const string NomeMutex = @"Local\TrackerAgentOverlay";

    public static void Executar()
    {
        // Sem sinal de bloqueio: é o disparo de logon, o de limpeza pós-desbloqueio (o serviço
        // roda a tarefa ao desbloquear) ou uma tela que abriu tarde demais. Em todos os casos,
        // garante que a política do Gerenciador de Tarefas não ficou para trás — ela é do HKCU
        // e sobreviveria a um crash ou a um encerramento forçado da tela anterior.
        if (!File.Exists(Caminhos.FlagBloqueio))
        {
            LockContext.DesabilitarGerenciadorTarefas(false);
            return;
        }

        // Já há uma tela nesta sessão (a tarefa roda com MultipleInstances=Parallel): sai sem
        // tocar na política — quem a ligou foi a outra instância, que continua na tela.
        using var mutex = new Mutex(initiallyOwned: true, NomeMutex, out var criado);
        if (!criado) return;

        // Qualquer saída do processo (normal, exceção, encerramento) desliga a política.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => LockContext.DesabilitarGerenciadorTarefas(false);
        AppDomain.CurrentDomain.UnhandledException += (_, _) => LockContext.DesabilitarGerenciadorTarefas(false);
        // Exceção num evento de UI não derruba a tela nem abre a caixa de erro do WinForms por
        // cima dela: a trava continua (e o serviço relançaria a tela de qualquer forma).
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, _) => { };

        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.Run(new LockContext());
        }
        finally
        {
            LockContext.DesabilitarGerenciadorTarefas(false);
        }
    }
}

/// <summary>
/// Gerencia o conjunto de janelas de bloqueio (uma por monitor), o gancho de teclado e o
/// encerramento. Um único timer cuida de tudo: some se o sinal sumir, reconstrói se os
/// monitores mudarem e reafirma o "sempre no topo".
/// </summary>
internal sealed class LockContext : ApplicationContext
{
    private readonly List<LockForm> _forms = [];
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private GanchoTeclado? _hook;
    private string _assinaturaTelas = "";
    private bool _encerrando;

    public bool PodeFechar => _encerrando;

    /// <summary>Painel de Wi-Fi aberto. Enquanto isso o "topo" é reafirmado SEM ativar janela
    /// (senão o foco sairia da caixa de senha a cada 500ms) e a troca de monitores reconstrói
    /// só as janelas secundárias, preservando o painel e o que já foi digitado.</summary>
    public static bool InteragindoWifi;

    public LockContext()
    {
        InteragindoWifi = false;
        _hook = new GanchoTeclado();
        DesabilitarGerenciadorTarefas(true);
        Reconstruir();
        _timer.Tick += Tick;
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (!File.Exists(Caminhos.FlagBloqueio)) { Encerrar(); return; }

        // Hotplug de monitor é checado SEMPRE, inclusive com o painel de Wi-Fi aberto: antes o
        // Tick retornava cedo e um monitor plugado durante a digitação mostrava o desktop.
        if (AssinaturaTelas() != _assinaturaTelas) Reconstruir();

        if (InteragindoWifi) ReafirmarTopoSemAtivar();
        else foreach (var f in _forms) f.TopMost = true;
    }

    private static string AssinaturaTelas() =>
        string.Join("|", Screen.AllScreens.Select(s => $"{s.Bounds}:{s.Primary}"));

    private void Reconstruir()
    {
        _assinaturaTelas = AssinaturaTelas();
        var telas = Screen.AllScreens;
        var principalAtual = _forms.FirstOrDefault(f => f.Principal);
        var telaPrincipal = telas.FirstOrDefault(s => s.Primary);

        // Painel de Wi-Fi aberto e o monitor principal não mudou: troca só as secundárias.
        if (InteragindoWifi && principalAtual is not null && telaPrincipal is not null &&
            principalAtual.AreaTela == telaPrincipal.Bounds)
        {
            ReconstruirSecundarias(principalAtual, telas);
            return;
        }

        // Reconstrução completa (o painel, se estava aberto, fecha junto com a janela principal).
        InteragindoWifi = false;
        var antigos = _forms.ToList();
        _forms.Clear();

        foreach (var scr in telas)
        {
            var f = new LockForm(scr, scr.Primary, this, semAtivar: false);
            _forms.Add(f);
            f.Show();
        }

        foreach (var f in antigos) f.FecharForcado();
        foreach (var f in _forms) { f.TopMost = true; f.Activate(); }
    }

    /// <summary>Mantém a janela principal (com o painel) e cria/fecha as dos demais monitores,
    /// sem roubar o foco. O monitor novo fica coberto no mesmo ciclo.</summary>
    private void ReconstruirSecundarias(LockForm principal, Screen[] telas)
    {
        var sobra = _forms.Where(f => f != principal).ToList();
        var novas = new List<LockForm> { principal };

        foreach (var scr in telas.Where(s => !s.Primary))
        {
            var existente = sobra.FirstOrDefault(f => f.AreaTela == scr.Bounds);
            if (existente is not null)
            {
                sobra.Remove(existente);
                novas.Add(existente);
                continue;
            }

            var f = new LockForm(scr, principal: false, this, semAtivar: true);
            novas.Add(f);
            f.Show();
        }

        foreach (var f in sobra) f.FecharForcado();
        _forms.Clear();
        _forms.AddRange(novas);
        ReafirmarTopoSemAtivar();
    }

    /// <summary>Coloca todas as janelas no topo sem ativá-las; a principal por último, para o
    /// painel de Wi-Fi ficar acima de qualquer outra janela de bloqueio.</summary>
    private void ReafirmarTopoSemAtivar()
    {
        foreach (var f in _forms.Where(f => !f.Principal)) f.TopoSemAtivar();
        foreach (var f in _forms.Where(f => f.Principal)) f.TopoSemAtivar();
    }

    private void Encerrar()
    {
        _encerrando = true;
        _timer.Stop();
        _hook?.Dispose();
        _hook = null;
        DesabilitarGerenciadorTarefas(false);
        foreach (var f in _forms.ToList()) f.FecharForcado();
        ExitThread();
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
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private readonly LockContext _ctx;
    private readonly bool _semAtivar;
    private bool _permitirFechar;
    private bool _conectando;

    private ListBox _lstRedes = null!;
    private TextBox _txtSenha = null!;
    private Label _lblStatusWifi = null!;

    /// <summary>Área do monitor que esta janela cobre (a de criação, não a atual da janela).</summary>
    public Rectangle AreaTela { get; }

    /// <summary>Janela do monitor principal (a que tem o painel de Wi-Fi).</summary>
    public bool Principal { get; }

    public LockForm(Screen tela, bool principal, LockContext ctx, bool semAtivar)
    {
        _ctx = ctx;
        _semAtivar = semAtivar;
        AreaTela = tela.Bounds;
        Principal = principal;

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

    /// <summary>Janela criada com o painel de Wi-Fi aberto aparece sem tomar o foco da digitação.</summary>
    protected override bool ShowWithoutActivation => _semAtivar;

    /// <summary>"Sempre no topo" sem ativar a janela (o setter de TopMost ativa e rouba o foco).</summary>
    public void TopoSemAtivar()
    {
        if (!IsHandleCreated || IsDisposed) return;
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
        // A varredura nativa (WlanScan) bloqueia até ~5s. Rodamos fora da thread de UI para não
        // congelar a tela de bloqueio, e devolvemos o resultado por Invoke.
        _lstRedes.Items.Clear();
        _lblStatusWifi.Text = "Procurando redes...";

        Task.Run(() =>
        {
            List<string> redes;
            string? erro = null;
            bool temAdaptador = true;
            try
            {
                temAdaptador = WifiConnect.TemInterfaceWifi();
                redes = temAdaptador ? WifiConnect.ListarRedes() : [];
            }
            catch (Exception ex) { redes = []; erro = ex.Message; }

            try
            {
                BeginInvoke(() =>
                {
                    if (erro != null) { _lblStatusWifi.Text = "Erro ao listar redes: " + erro; return; }
                    if (!temAdaptador) { _lblStatusWifi.Text = "Nenhum adaptador Wi-Fi neste dispositivo."; return; }

                    _lstRedes.Items.Clear();
                    foreach (var r in redes) _lstRedes.Items.Add(r);
                    if (_lstRedes.Items.Count > 0)
                    {
                        _lstRedes.SelectedIndex = 0;
                        _lblStatusWifi.Text = $"{redes.Count} rede(s) encontrada(s). Selecione e conecte.";
                    }
                    else _lblStatusWifi.Text = "Nenhuma rede encontrada. Tente 'Atualizar lista'.";
                });
            }
            catch { /* form já fechado */ }
        });
    }

    private void ConectarWifi()
    {
        if (_conectando) return;
        var ssid = _lstRedes.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(ssid)) { _lblStatusWifi.Text = "Selecione uma rede na lista."; return; }

        var senha = _txtSenha.Text;
        _txtSenha.Clear();
        _conectando = true;
        _lblStatusWifi.Text = "Conectando...";

        // O netsh leva segundos: fora da thread de UI, senão a tela congela e o gancho de teclado
        // (que roda nesta mesma thread) deixa de responder — o Windows o remove por timeout.
        Task.Run(() =>
        {
            string msg;
            try { msg = WifiConnect.Conectar(ssid, senha).mensagem; }
            catch (Exception ex) { msg = "Erro ao conectar: " + ex.Message; }

            try
            {
                BeginInvoke(() =>
                {
                    _conectando = false;
                    _lblStatusWifi.Text = msg;
                });
            }
            catch { /* form já fechado */ }
        });
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
