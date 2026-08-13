using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Tracker.Instalador;

/// <summary>
/// Instalador autocontido do Tracker Agent: UM único arquivo .exe, sem nada ao lado.
///
/// Dois motivos moldaram este desenho:
///   1. arquivos .cmd/.bat não têm manifesto e por isso nunca conseguem pedir elevação —
///      só um executável com <c>requireAdministrator</c> faz o Windows exibir a caixa de
///      usuário e senha (ver app.manifest);
///   2. a Execution Policy do PowerShell está bloqueada por diretiva de grupo na rede da
///      Nutrihouse, então nenhum .ps1 vindo do pendrive executaria.
///
/// O agente publicado e o appsettings.json já configurado viajam embutidos como
/// <c>payload.zip</c>; o modelo da tarefa de bloqueio, como <c>TrackerAgentLock.template.xml</c>.
/// </summary>
internal static class Program
{
    private const string PastaInstalacao = @"C:\TrackerAgent";
    private const string NomeExecutavel = "Tracker.Agent.Windows.exe";
    private const string NomeServico = "TrackerAgent";
    private const string NomeTarefa = "TrackerAgentLock";
    private const string RecursoPayload = "payload.zip";
    private const string RecursoTarefa = "TrackerAgentLock.template.xml";

    /// <summary>
    /// Nega parada/remoção do serviço a usuários comuns (IU/SU), mantendo SYSTEM (SY) e
    /// Administradores (BA) com CONTROLE TOTAL.
    ///
    /// Atenção ao editar: a versão anterior concedia "CCLCSWRPWPDTLOCRRC" a BA, que NÃO inclui
    /// SD (delete), DC (alterar configuração) nem WD (alterar permissões) — o resultado era um
    /// serviço que nem administrador conseguia remover ou reconfigurar, e a reinstalação
    /// morria com "sc create" 1073 ("o serviço já existe"). Mantenha o conjunto completo
    /// CCDCLCSWRPWPDTLOCRSDRCWDWO para SY e BA.
    /// </summary>
    private const string SddlServico =
        "D:(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)" +
        "(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)";

    private const string ChaveServico = @"SYSTEM\CurrentControlSet\Services\";

    private static string _caminhoLog = "";

    private static int Main(string[] args)
    {
        var desinstalar = TemArgumento(args, "--desinstalar");
        var diagnostico = TemArgumento(args, "--diagnostico");

        Console.Title = desinstalar ? "Desinstalacao do Tracker Agent" : "Instalacao do Tracker Agent";
        _caminhoLog = MontarCaminhoDeLog(desinstalar ? "desinstalacao" : diagnostico ? "diagnostico" : "instalacao");

        try
        {
            if (diagnostico) return Diagnosticar();
            if (!GarantirElevacao()) return Encerrar(1);

            return desinstalar ? Desinstalar() : Instalar();
        }
        catch (Exception ex)
        {
            Registrar($"ERRO INESPERADO: {ex.Message}");
            Registrar(ex.ToString());
            return Encerrar(1);
        }
    }

    // ---------------------------------------------------------------- instalação

    private static int Instalar()
    {
        Registrar("===============================================================");
        Registrar($"Instalacao iniciada - maquina {Environment.MachineName}");
        Registrar($"Executando como: {Environment.UserDomainName}\\{Environment.UserName}");
        Registrar($"Administrador local: {(EhAdministrador() ? "SIM" : "NAO")}");

        var payload = LerRecurso(RecursoPayload);
        if (payload is null)
        {
            Registrar("ERRO: este executavel foi compilado sem o payload do agente.");
            Registrar("Gere o instalador com deploy\\gerar-pendrive.ps1.");
            return Encerrar(1);
        }

        var exeDestino = Path.Combine(PastaInstalacao, NomeExecutavel);

        PararEremoverServico();

        // Preserva o token do dispositivo em reinstalações, para não duplicar o registro no Tracker.
        var tokenOriginal = Path.Combine(PastaInstalacao, "token.dat");
        byte[]? token = File.Exists(tokenOriginal) ? File.ReadAllBytes(tokenOriginal) : null;
        if (token is not null) Registrar("token.dat existente preservado.");

        Registrar($"Criando {PastaInstalacao} e extraindo o agente...");
        Directory.CreateDirectory(PastaInstalacao);
        using (var zip = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            ExtrairSobrescrevendo(zip, PastaInstalacao);
        }

        if (token is not null) File.WriteAllBytes(tokenOriginal, token);

        if (!File.Exists(exeDestino))
        {
            Registrar($"ERRO: {NomeExecutavel} nao apareceu em {PastaInstalacao} apos a extracao.");
            return Encerrar(1);
        }

        Registrar("Configurando o servico do Windows...");
        if (!GarantirServico(exeDestino)) return Encerrar(1);

        Registrar($"Registrando a tarefa da tela de bloqueio ({NomeTarefa})...");
        var xml = EscreverXmlDaTarefa(exeDestino);
        if (xml is null) return Encerrar(1);
        if (!Executar("schtasks", "/create", "/tn", NomeTarefa, "/xml", xml, "/f"))
        {
            Registrar("ERRO: nao foi possivel registrar a tarefa da tela de bloqueio.");
            return Encerrar(1);
        }
        File.Delete(xml);

        Registrar("Iniciando o servico...");
        Executar("sc.exe", "start", NomeServico);
        if (!AguardarServicoEmExecucao())
        {
            Registrar($"ERRO: o servico nao entrou em execucao. Veja {PastaInstalacao}\\agent.log.");
            return Encerrar(1);
        }

        Registrar("");
        Registrar("================================================");
        Registrar("  INSTALACAO CONCLUIDA");
        Registrar("  O dispositivo aparece no Tracker em ate 1 min.");
        Registrar("================================================");
        return Encerrar(0);
    }

    // -------------------------------------------------------------- desinstalação

    private static int Desinstalar()
    {
        Registrar($"Desinstalacao iniciada - maquina {Environment.MachineName}");

        PararEremoverServico();
        Executar("schtasks", "/delete", "/tn", NomeTarefa, "/f");

        // Remove o sinal de bloqueio para não deixar a máquina travada.
        var flag = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TrackerAgent", "locked.flag");
        if (File.Exists(flag))
        {
            File.Delete(flag);
            Registrar("Sinal de bloqueio removido.");
        }

        Registrar($"Desinstalado. A pasta {PastaInstalacao} foi mantida (log e token).");
        return Encerrar(0);
    }

    // ---------------------------------------------------------------- diagnóstico

    /// <summary>
    /// Mostra a política de UAC da máquina. Não altera nada, mas ATENÇÃO: por causa do
    /// manifesto <c>requireAdministrator</c>, este executável só inicia elevado — se a
    /// máquina nem deixa elevar, use o Diagnostico-UAC.cmd, que roda como usuário comum.
    /// </summary>
    private static int Diagnosticar()
    {
        Registrar($"Maquina : {Environment.MachineName}");
        Registrar($"Usuario : {Environment.UserDomainName}\\{Environment.UserName}");
        Registrar($"Elevado : {(EhAdministrador() ? "SIM" : "NAO")}");
        Registrar("");
        Registrar("--- Politica de UAC (HKLM ... Policies\\System) ---");

        using var chave = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
        foreach (var nome in new[]
                 {
                     "EnableLUA", "ConsentPromptBehaviorUser", "ConsentPromptBehaviorAdmin",
                     "FilterAdministratorToken", "EnableInstallerDetection",
                 })
        {
            var valor = chave?.GetValue(nome);
            Registrar($"{nome} = {valor ?? "(nao definido)"}");
        }

        Registrar("");
        Registrar("Como ler:");
        Registrar("  ConsentPromptBehaviorUser = 0 : usuario comum NUNCA recebe caixa de credencial");
        Registrar("                                  (o pedido de elevacao e negado automaticamente).");
        Registrar("  ConsentPromptBehaviorUser = 1 ou 3 : pede usuario e senha, como esperado.");
        Registrar("  EnableLUA = 0 : UAC desligado; elevar depende so de ser admin local.");
        return Encerrar(0);
    }

    // ------------------------------------------------------------------ apoio

    private static bool GarantirElevacao()
    {
        if (EhAdministrador()) return true;

        // Só chega aqui se o manifesto foi removido do executável ou se o UAC está
        // desligado e o usuário não pertence ao grupo de administradores.
        Registrar("Este processo nao esta elevado.");
        Registrar("Clique com o botao direito no Instalar-Agente.exe e escolha");
        Registrar("\"Executar como administrador\", ou use SHIFT + botao direito e");
        Registrar("\"Executar como usuario diferente\" com uma conta de administrador.");
        Registrar("Rode  Instalar-Agente.exe --diagnostico  para ver a politica de UAC da maquina.");
        return false;
    }

    private static bool EhAdministrador()
    {
        using var identidade = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Garante o serviço no estado correto, sem depender de apagar e recriar.
    ///
    /// Se ele já existe apontando para o mesmo executável — caso das máquinas que receberam o
    /// agente por GPO —, os binários recém-extraídos já são a atualização: basta corrigir as
    /// permissões e iniciar. Isso evita o beco sem saída do <c>sc delete</c> negado por um
    /// SDDL antigo, que fazia o <c>sc create</c> falhar com 1073.
    /// </summary>
    private static bool GarantirServico(string exeDestino)
    {
        var caminhoAtual = CaminhoDoServicoInstalado();

        if (caminhoAtual is not null &&
            caminhoAtual.Trim('"').Equals(exeDestino, StringComparison.OrdinalIgnoreCase))
        {
            Registrar("Servico ja existe apontando para o mesmo caminho: reaproveitando o registro.");
            LiberarPermissoesDoServico();
            AplicarConfiguracaoDoServico();
            return true;
        }

        if (caminhoAtual is not null)
        {
            Registrar($"Servico existente aponta para outro caminho: {caminhoAtual}");
            Registrar("Removendo para recriar no caminho correto...");
            LiberarPermissoesDoServico();
            PararEremoverServico();
        }

        if (!CriarServico(exeDestino)) return false;
        AplicarConfiguracaoDoServico();
        return true;
    }

    /// <summary>Caminho do executável do serviço já instalado, ou <c>null</c> se ele não existe.
    /// Lê do registro em vez de interpretar o <c>sc qc</c>, cuja saída é traduzida.</summary>
    private static string? CaminhoDoServicoInstalado()
    {
        try
        {
            using var chave = Registry.LocalMachine.OpenSubKey(ChaveServico + NomeServico);
            return chave?.GetValue("ImagePath") as string;
        }
        catch (Exception ex)
        {
            RegistrarSomenteNoArquivo($"  [registro] falha ao ler ImagePath: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Remove o descritor de segurança gravado no registro do serviço, fazendo o Windows voltar
    /// ao descritor padrão. É a saída para os serviços criados com o SDDL antigo, em que nem
    /// administrador tinha permissão de apagar ou de alterar a própria DACL.
    /// </summary>
    private static void LiberarPermissoesDoServico()
    {
        try
        {
            using var chave = Registry.LocalMachine.OpenSubKey(ChaveServico + NomeServico, writable: true);
            if (chave is null) return;

            if (chave.GetSubKeyNames().Any(n => n.Equals("Security", StringComparison.OrdinalIgnoreCase)))
            {
                chave.DeleteSubKeyTree("Security", throwOnMissingSubKey: false);
                Registrar("Permissoes antigas do servico liberadas (descritor redefinido).");
            }
        }
        catch (Exception ex)
        {
            RegistrarSomenteNoArquivo($"  [registro] nao foi possivel liberar as permissoes: {ex.Message}");
        }
    }

    /// <summary>Descrição, reinício automático em falha e DACL. Melhor esforço: um erro aqui
    /// não impede o agente de funcionar.</summary>
    private static void AplicarConfiguracaoDoServico()
    {
        Executar("sc.exe", silencioso: true, "config", NomeServico, "start=", "auto", "obj=", "LocalSystem");
        Executar("sc.exe", silencioso: true, "description", NomeServico,
                 "Agente de rastreabilidade de ativos (heartbeat, localizacao e bloqueio remoto).");
        Executar("sc.exe", silencioso: true, "failure", NomeServico, "reset=", "86400",
                 "actions=", "restart/5000/restart/5000/restart/5000");
        if (!Executar("sc.exe", silencioso: true, "sdset", NomeServico, SddlServico))
        {
            Registrar("AVISO: nao foi possivel aplicar as permissoes do servico (sc sdset).");
            Registrar("O agente funciona, mas usuario comum pode conseguir parar o servico.");
        }
    }

    /// <summary>
    /// Cria o serviço e explica o erro do <c>sc.exe</c> quando falha, em vez de supor a causa.
    /// O caminho vai sem aspas de propósito: <c>C:\TrackerAgent</c> não tem espaços, e aspas
    /// literais dentro de um argumento são fonte clássica de erro de parsing.
    /// </summary>
    private static bool CriarServico(string exeDestino)
    {
        for (var tentativa = 1; tentativa <= 2; tentativa++)
        {
            var codigo = ExecutarObtendoCodigo("sc.exe", out var saida,
                "create", NomeServico, "binPath=", exeDestino,
                "start=", "auto", "obj=", "LocalSystem",
                "DisplayName=", "Tracker Agent Nutrihouse");

            if (codigo == 0) return true;

            Registrar($"ERRO: sc create falhou com codigo {codigo} - {DescreverErroSc(codigo)}");
            if (!string.IsNullOrWhiteSpace(saida)) Registrar($"Saida do sc.exe: {saida.Trim()}");

            // 1072 = marcado para exclusão, 1073 = já existe. Vale uma segunda tentativa
            // depois de dar tempo ao Gerenciador de Servicos de concluir a remoção.
            if (tentativa == 1 && codigo is 1072 or 1073)
            {
                Registrar("Liberando permissoes e removendo o registro anterior antes de tentar de novo...");
                LiberarPermissoesDoServico();
                Executar("sc.exe", silencioso: true, "delete", NomeServico);
                Thread.Sleep(8000);
                continue;
            }

            if (codigo == 5)
            {
                Registrar("Acesso negado mesmo com o processo elevado: a conta informada no UAC");
                Registrar("nao e administrador LOCAL desta maquina (ser admin do dominio nao basta");
                Registrar("se a conta nao estiver no grupo Administradores do PC).");
            }

            return false;
        }

        return false;
    }

    /// <summary>Traduz os códigos de erro do <c>sc.exe</c> que aparecem na prática.</summary>
    private static string DescreverErroSc(int codigo) => codigo switch
    {
        1 => "funcao incorreta / sintaxe recusada pelo sc.exe",
        2 => "arquivo nao encontrado",
        5 => "acesso negado (a conta nao e administrador local desta maquina)",
        87 => "parametro invalido",
        1053 => "o servico nao respondeu ao iniciar",
        1056 => "uma instancia do servico ja esta em execucao",
        1060 => "o servico nao existe",
        1072 => "o servico esta marcado para exclusao (reinicie a maquina e repita)",
        1073 => "o servico ja existe",
        _ => "consulte a saida do sc.exe abaixo",
    };

    /// <summary>
    /// Remove instalação anterior (por exemplo a que veio por GPO) e ESPERA o serviço
    /// desaparecer de fato. Sem essa espera, o <c>sc create</c> seguinte falha com 1072
    /// ("marcado para exclusão") enquanto o Gerenciador de Serviços ainda mantém o registro.
    /// </summary>
    private static void PararEremoverServico()
    {
        if (!Executar("sc.exe", silencioso: true, "query", NomeServico)) return;

        Registrar("Parando e removendo instalacao anterior do servico...");
        Executar("sc.exe", silencioso: true, "stop", NomeServico);

        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            Thread.Sleep(1500);
            Executar("sc.exe", silencioso: true, "delete", NomeServico);

            // 1060 = "o servico especificado nao existe": a remocao terminou.
            if (ExecutarObtendoCodigo("sc.exe", out _, "query", NomeServico) == 1060)
            {
                Registrar("Instalacao anterior removida.");
                return;
            }
        }

        Registrar("AVISO: o servico anterior ainda aparece registrado. Se a criacao falhar com");
        Registrar("codigo 1072, feche o console de Servicos (services.msc) e o Gerenciador de");
        Registrar("Tarefas, ou reinicie a maquina, e rode o instalador de novo.");
    }

    /// <summary>Aguarda o serviço reportar estado 4 (em execução). O texto do <c>sc query</c> é
    /// traduzido conforme o idioma do Windows, por isso a checagem usa o código numérico.</summary>
    private static bool AguardarServicoEmExecucao()
    {
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            Thread.Sleep(1000);
            var saida = CapturarSaida("sc.exe", "query", NomeServico);
            if (saida.Contains(": 4") || saida.Contains(":4")) return true;
        }
        return false;
    }

    private static string? EscreverXmlDaTarefa(string exeDestino)
    {
        using var recurso = LerRecurso(RecursoTarefa);
        if (recurso is null)
        {
            Registrar($"ERRO: recurso {RecursoTarefa} ausente do executavel.");
            return null;
        }

        using var leitor = new StreamReader(recurso);
        var xml = leitor.ReadToEnd().Replace("__CAMINHO_EXE__", exeDestino);

        // schtasks /xml exige o arquivo em Unicode (UTF-16 LE com BOM).
        var destino = Path.Combine(Path.GetTempPath(), $"{NomeTarefa}.xml");
        File.WriteAllText(destino, xml, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
        return destino;
    }

    private static Stream? LerRecurso(string nome) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(nome);

    private static void ExtrairSobrescrevendo(ZipArchive zip, string destino)
    {
        var raiz = Path.GetFullPath(destino);
        foreach (var item in zip.Entries)
        {
            if (string.IsNullOrEmpty(item.Name)) continue; // diretório

            var alvo = Path.GetFullPath(Path.Combine(raiz, item.FullName));
            // Defesa contra caminho de zip apontando para fora da pasta de destino.
            if (!alvo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
            item.ExtractToFile(alvo, overwrite: true);
        }
    }

    private static bool Executar(string programa, params string[] argumentos) =>
        Executar(programa, silencioso: false, argumentos);

    private static bool Executar(string programa, bool silencioso, params string[] argumentos)
    {
        var codigo = ExecutarObtendoCodigo(programa, out var saida, argumentos);
        if (codigo != 0 && !silencioso)
        {
            Registrar($"  [{programa} {string.Join(' ', argumentos)}] codigo {codigo}: {saida.Trim()}");
        }
        return codigo == 0;
    }

    /// <summary>Executa e devolve o código de saída junto da saída combinada, para que o
    /// chamador possa explicar o erro em vez de supor a causa.</summary>
    private static int ExecutarObtendoCodigo(string programa, out string saida, params string[] argumentos)
    {
        saida = "";

        var psi = new ProcessStartInfo(programa)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in argumentos) psi.ArgumentList.Add(a);

        using var processo = Process.Start(psi);
        if (processo is null) return -1;

        saida = processo.StandardOutput.ReadToEnd() + processo.StandardError.ReadToEnd();
        processo.WaitForExit();

        RegistrarSomenteNoArquivo($"  [{programa} {string.Join(' ', argumentos)}] codigo {processo.ExitCode}");
        return processo.ExitCode;
    }

    private static string CapturarSaida(string programa, params string[] argumentos)
    {
        var psi = new ProcessStartInfo(programa)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in argumentos) psi.ArgumentList.Add(a);

        using var processo = Process.Start(psi);
        if (processo is null) return "";
        var saida = processo.StandardOutput.ReadToEnd();
        processo.WaitForExit();
        return saida;
    }

    // ------------------------------------------------------------------- log

    /// <summary>Log ao lado do executável (pendrive) quando possível; senão em %ProgramData%.</summary>
    private static string MontarCaminhoDeLog(string acao)
    {
        var nome = $"{Environment.MachineName}-{acao}.log";

        try
        {
            var pastaExe = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var pastaLogs = Path.Combine(pastaExe, "logs");
            Directory.CreateDirectory(pastaLogs);
            var caminho = Path.Combine(pastaLogs, nome);
            File.AppendAllText(caminho, "");
            return caminho;
        }
        catch
        {
            // Pendrive somente leitura, por exemplo.
            var alternativa = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "TrackerAgent");
            Directory.CreateDirectory(alternativa);
            return Path.Combine(alternativa, nome);
        }
    }

    private static void Registrar(string mensagem)
    {
        Console.WriteLine(mensagem);
        RegistrarSomenteNoArquivo(mensagem);
    }

    private static void RegistrarSomenteNoArquivo(string mensagem)
    {
        try
        {
            File.AppendAllText(_caminhoLog, $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {mensagem}{Environment.NewLine}");
        }
        catch
        {
            // Nunca deixe a falha de log interromper a instalação.
        }
    }

    /// <summary>Mantém a janela aberta — o instalador normalmente é aberto por duplo clique.</summary>
    private static int Encerrar(int codigo)
    {
        if (codigo != 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== FALHA ===");
            Console.WriteLine($"Detalhes no log: {_caminhoLog}");
        }

        Console.WriteLine();
        Console.WriteLine("Pressione ENTER para fechar.");
        Console.ReadLine();
        return codigo;
    }

    private static bool TemArgumento(string[] args, string nome) =>
        args.Any(a => a.Equals(nome, StringComparison.OrdinalIgnoreCase));
}
