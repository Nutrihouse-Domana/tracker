@echo off
REM ===========================================================================
REM  OPCIONAL - so para investigar falha de elevacao.
REM  O Instalar-Agente.exe NAO precisa deste arquivo: ele e autocontido.
REM
REM  Este .cmd existe porque o Instalar-Agente.exe tem manifesto
REM  requireAdministrator e, por isso, nao inicia sem elevacao. Quando a maquina
REM  se recusa a elevar, e este script (batch, sem exigir admin) que mostra qual
REM  politica esta barrando.
REM
REM  Roda como usuario comum, nao instala nada, nao altera nada.
REM ===========================================================================

setlocal EnableExtensions
title Diagnostico de elevacao - Tracker Agent

set "SAIDA=%~dp0logs\UAC-%COMPUTERNAME%.txt"
if not exist "%~dp0logs" mkdir "%~dp0logs" >nul 2>&1
if not exist "%~dp0logs" set "SAIDA=%TEMP%\UAC-%COMPUTERNAME%.txt"

set "CHAVE=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"

call :emitir "Maquina : %COMPUTERNAME%"
call :emitir "Usuario : %USERDOMAIN%\%USERNAME%"

net session >nul 2>&1
if %errorlevel% equ 0 (call :emitir "Elevado : SIM") else (call :emitir "Elevado : NAO")

net localgroup Administradores 2>nul | findstr /i "%USERNAME%" >nul
if %errorlevel% equ 0 (
  call :emitir "Admin local do PC : SIM"
) else (
  net localgroup Administrators 2>nul | findstr /i "%USERNAME%" >nul
  if errorlevel 1 (call :emitir "Admin local do PC : NAO") else (call :emitir "Admin local do PC : SIM")
)

call :emitir ""
call :emitir "--- Politica de UAC (HKLM ... Policies\System) ---"
call :valor EnableLUA
call :valor ConsentPromptBehaviorUser
call :valor ConsentPromptBehaviorAdmin
call :valor FilterAdministratorToken
call :valor EnableInstallerDetection

call :emitir ""
call :emitir "--- Como ler ---"
REM Atencao ao editar: o texto e ecoado sem aspas, entao NAO use os caracteres
REM maior/menor, E comercial ou barra vertical aqui - viram redirecionamento.
call :emitir "ConsentPromptBehaviorUser = 0 : usuario comum NUNCA recebe caixa de usuario e senha"
call :emitir "                                (o pedido de elevacao e negado automaticamente)."
call :emitir "ConsentPromptBehaviorUser = 1 ou 3 : deveria pedir usuario e senha."
call :emitir "EnableLUA = 0 : UAC desligado; elevacao depende so de ser admin local."

echo.
echo Resultado salvo em: %SAIDA%
echo.
echo Pressione qualquer tecla para fechar.
pause >nul
exit /b 0

:valor
set "V=(nao definido - usa o padrao do Windows)"
for /f "tokens=3" %%a in ('reg query "%CHAVE%" /v %~1 2^>nul ^| findstr /i "%~1"') do set "V=%%a"
call :emitir "%~1 = %V%"
exit /b 0

REM "echo(" imprime linha em branco quando o argumento e vazio (echo sozinho
REM mostraria "ECHO esta desativado").
:emitir
echo(%~1
echo(%~1 >>"%SAIDA%"
exit /b 0
