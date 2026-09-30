@echo off
REM ==================================================================
REM  FORZAR RESPALDO INMEDIATO - SACTI-BACKUP
REM
REM  La aplicacion NO respalda si ya respaldo hoy (o si aun no se
REM  cumplen los dias de "RespaldarCada" de la licencia). Este script
REM  limpia la fecha del ultimo respaldo para que el proximo arranque
REM  ejecute el respaldo de inmediato.
REM
REM  Clic derecho -> "Ejecutar como administrador".
REM ==================================================================
setlocal EnableDelayedExpansion
title Forzar respaldo - SACTI-BACKUP
color 0E

echo.
echo  ============================================
echo    FORZAR RESPALDO INMEDIATO
echo  ============================================
echo.

net session >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] Ejecute este archivo COMO ADMINISTRADOR.
    echo.
    pause
    exit /b 1
)

REM --- Detectar carpeta de instalacion ---
echo  [1/4] Buscando la instalacion...
set "DESTINO="
for /f "usebackq delims=" %%i in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "try{$t=Get-ScheduledTask -TaskName 'SACTI Backup' -ErrorAction Stop; $e=@($t.Actions.Execute)[0]; if($e){Split-Path ($e.Trim([char]34)) -Parent}}catch{}"`) do set "DESTINO=%%i"

if not defined DESTINO (
    for %%p in ("C:\SACTI\SactiBackup" "C:\SACTI\SACTIBACKUP" "C:\SactiBackup") do (
        if exist "%%~p\SACTIBACKUP.exe" set "DESTINO=%%~p"
    )
)
if not defined DESTINO (
    echo.
    set /p "DESTINO=  Escriba la ruta donde esta SACTIBACKUP.exe: "
)

if not exist "%DESTINO%\SactiBackup.json" (
    echo.
    echo  [ERROR] No existe "%DESTINO%\SactiBackup.json"
    echo          Sin configuracion no se puede respaldar.
    echo.
    pause
    exit /b 1
)
echo        Instalacion: %DESTINO%

REM --- Cerrar la app ---
echo  [2/4] Cerrando SACTI-BACKUP...
taskkill /IM SACTIBACKUP.exe /F >nul 2>&1
timeout /t 3 /nobreak >nul
echo        Cerrado.

REM --- Limpiar fecha de ultimo respaldo ---
echo  [3/4] Limpiando la fecha del ultimo respaldo...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "$f = Join-Path '%DESTINO%' 'SactiBackup.json';" ^
  "Copy-Item $f ($f + '.bak_' + (Get-Date -Format 'yyyyMMdd_HHmmss')) -Force;" ^
  "$json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((Get-Content $f -Raw)));" ^
  "$o = $json | ConvertFrom-Json;" ^
  "Write-Host ('       Ultimo respaldo registrado: ' + $(if($o.FechaUltimoRespaldo){$o.FechaUltimoRespaldo}else{'(ninguno)'}));" ^
  "$o.FechaUltimoRespaldo = $null;" ^
  "if ($o.PSObject.Properties.Name -contains 'FechaUltimoRespaldoNube') { $o.FechaUltimoRespaldoNube = $null };" ^
  "$nuevo = $o | ConvertTo-Json -Depth 10;" ^
  "[IO.File]::WriteAllText($f, [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($nuevo)));" ^
  "Write-Host '       Fecha limpiada. Copia de seguridad creada.'"

if errorlevel 1 (
    echo.
    echo  [ERROR] No se pudo modificar la configuracion.
    echo          Restaure el archivo .bak_ mas reciente de la carpeta.
    echo.
    pause
    exit /b 1
)

REM --- Arrancar ---
echo  [4/4] Iniciando SACTI-BACKUP ^(el respaldo arranca solo^)...
start "" "%DESTINO%\SACTIBACKUP.exe"

echo.
echo  ============================================
echo    LISTO
echo  ============================================
echo.
echo  La aplicacion va a iniciar el respaldo en unos segundos.
echo  NO la cierre hasta que termine.
echo.
echo  Al terminar, para ver si el correo salio, ejecute:
echo    %DESTINO%\VerRegistroCorreo.bat
echo.
pause
endlocal
