@echo off
rem ==========================================================================
rem  Tracker Agent - instalacao/atualizacao pela GPO "Deploy Tracker Agent"
rem
rem  Roda como SYSTEM pela tarefa agendada que a GPO cria (Preferencias > Tarefas
rem  Agendadas): ao ligar, ao conectar numa rede e a cada 1 hora.
rem  Instala o agente quando falta e ATUALIZA quando a versao publicada na rede
rem  muda. Nao precisa editar a GPO a cada versao: o CI (ou o
rem  deploy\publicar-agente-gpo.ps1) troca o instalador e o versao.txt na pasta.
rem
rem  Compara  <PASTA>\versao.txt  com  C:\TrackerAgent\versao.txt  (gravado pelo
rem  instalador SO ao terminar com sucesso). Iguais: sai na hora, sem baixar o
rem  .exe de ~80 MB. Diferentes ou ausente: roda Instalar-Agente-<versao>.exe.
rem  O token do dispositivo e preservado: reinstalar nao duplica o inventario.
rem
rem  Guia: docs\agentes\atualizacao-agente-gpo.md
rem  Arquivo em ASCII puro (sem acentos): o cmd le na codepage OEM.
rem ==========================================================================
setlocal EnableExtensions

rem Pasta do pacote. Ajuste SO esta linha se o compartilhamento mudar.
set "PASTA=\\192.168.0.254\AgentTracker"

set "LOCAL=C:\TrackerAgent\versao.txt"
set "LOG=%ProgramData%\TrackerAgent\gpo-atualizacao.log"
if not exist "%ProgramData%\TrackerAgent" mkdir "%ProgramData%\TrackerAgent" >nul 2>&1

rem Sem rede ou sem pacote publicado: nao faz nada (tenta de novo no proximo boot).
if not exist "%PASTA%\versao.txt" (
    echo [%date% %time%] Pasta ou versao.txt inacessivel: %PASTA% >> "%LOG%"
    exit /b 0
)

set "VERSAO_REDE="
set /p VERSAO_REDE=<"%PASTA%\versao.txt"
if not defined VERSAO_REDE (
    echo [%date% %time%] versao.txt vazio em %PASTA% >> "%LOG%"
    exit /b 0
)

set "VERSAO_LOCAL="
if exist "%LOCAL%" set /p VERSAO_LOCAL=<"%LOCAL%"

if /i "%VERSAO_REDE%"=="%VERSAO_LOCAL%" exit /b 0

set "PACOTE=%PASTA%\Instalar-Agente-%VERSAO_REDE%.exe"
if not exist "%PACOTE%" (
    echo [%date% %time%] Pacote ausente: %PACOTE% >> "%LOG%"
    exit /b 0
)

echo [%date% %time%] Instalando %VERSAO_REDE% (local: %VERSAO_LOCAL%) >> "%LOG%"
"%PACOTE%" --silencioso
set "CODIGO=%errorlevel%"
echo [%date% %time%] Instalador terminou com codigo %CODIGO% >> "%LOG%"
exit /b %CODIGO%
