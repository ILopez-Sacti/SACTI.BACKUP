@echo off
REM ==================================================================
REM  ACTUALIZADOR SACTI-BACKUP
REM  1. Descomprimir TODA la carpeta en el Escritorio del cliente.
REM  2. Clic derecho sobre este archivo -> "Ejecutar como administrador".
REM  NO borra ni modifica SactiBackup.json (configuracion del cliente).
REM ==================================================================
setlocal EnableDelayedExpansion
title Actualizador SACTI-BACKUP
color 0B

set "ORIGEN=%~dp0"

echo.
echo  ============================================
echo    ACTUALIZADOR SACTI-BACKUP
echo  ============================================
echo.

REM --- Verificar permisos de administrador ---
net session >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] Este archivo debe ejecutarse COMO ADMINISTRADOR.
    echo          Cierre esta ventana, de clic derecho sobre
    echo          ActualizarSactiBackup.bat y elija
    echo          "Ejecutar como administrador".
    echo.
    pause
    exit /b 1
)

REM --- Detectar carpeta de instalacion desde la tarea programada ---
echo  [1/6] Buscando la instalacion actual...
set "DESTINO="
for /f "usebackq delims=" %%i in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "try{$t=Get-ScheduledTask -TaskName 'SACTI Backup' -ErrorAction Stop; $e=@($t.Actions.Execute)[0]; if($e){Split-Path ($e.Trim([char]34)) -Parent}}catch{}"`) do set "DESTINO=%%i"

if not defined DESTINO (
    echo        No se encontro la tarea programada. Buscando el ejecutable...
    for %%p in ("C:\SACTI\SactiBackup" "C:\SACTI\SACTIBACKUP" "C:\SactiBackup" "C:\Program Files\SactiBackup" "C:\Program Files (x86)\SactiBackup") do (
        if exist "%%~p\SACTIBACKUP.exe" set "DESTINO=%%~p"
    )
)

if not defined DESTINO (
    echo.
    echo  No se pudo detectar la carpeta automaticamente.
    set /p "DESTINO=  Escriba la ruta donde esta SACTIBACKUP.exe: "
)

if not exist "%DESTINO%\SACTIBACKUP.exe" (
    echo.
    echo  [ERROR] No existe "%DESTINO%\SACTIBACKUP.exe"
    echo          Verifique la ruta de instalacion e intente de nuevo.
    echo.
    pause
    exit /b 1
)

echo        Instalacion detectada en: %DESTINO%
echo.

REM --- Cerrar la aplicacion ---
echo  [2/6] Cerrando SACTI-BACKUP...
taskkill /IM SACTIBACKUP.exe /F >nul 2>&1
timeout /t 3 /nobreak >nul
tasklist /FI "IMAGENAME eq SACTIBACKUP.exe" 2>nul | find /I "SACTIBACKUP.exe" >nul
if not errorlevel 1 (
    echo  [ERROR] No se pudo cerrar SACTIBACKUP.exe. Cierrelo manualmente
    echo          desde el Administrador de tareas y vuelva a ejecutar.
    pause
    exit /b 1
)
echo        Cerrado.

REM --- Respaldo de seguridad ---
echo  [3/6] Respaldando configuracion actual...
set "RESP=%DESTINO%\_respaldo_previo"
if not exist "%RESP%" mkdir "%RESP%" >nul 2>&1
copy /Y "%DESTINO%\SactiBackup.json" "%RESP%\SactiBackup.json.bak" >nul 2>&1
copy /Y "%DESTINO%\backup.log"       "%RESP%\backup.log.bak"       >nul 2>&1
echo        Copia guardada en: %RESP%

REM --- Copiar archivos nuevos ---
echo  [4/6] Copiando la version nueva...
robocopy "%ORIGEN%." "%DESTINO%" /E /IS /IT /NFL /NDL /NJH /NJS /NP /R:2 /W:2 ^
    /XF SactiBackup.json backup.log scheduler.log ActualizarSactiBackup.bat >nul
if %ERRORLEVEL% GEQ 8 (
    echo.
    echo  [ERROR] Fallo la copia de archivos ^(codigo %ERRORLEVEL%^).
    echo          Verifique que la aplicacion este cerrada y que tenga permisos.
    pause
    exit /b 1
)
echo        Archivos copiados.

REM --- Verificar ---
echo  [5/6] Verificando archivos requeridos...
set "FALTA=0"
if not exist "%DESTINO%\SACTIBACKUP.exe"                 ( echo        FALTA: SACTIBACKUP.exe & set "FALTA=1" )
if not exist "%DESTINO%\PlantillaCorreoSactiBackUp.html" ( echo        FALTA: PlantillaCorreoSactiBackUp.html & set "FALTA=1" )
if not exist "%DESTINO%\SactiBackup.json"                ( echo        FALTA: SactiBackup.json ^(configuracion^) & set "FALTA=1" )
if "%FALTA%"=="1" (
    echo.
    echo  [ERROR] Faltan archivos. NO se inicia la aplicacion.
    echo          Restaure desde: %RESP%
    pause
    exit /b 1
)
echo        Todo correcto.

REM --- Iniciar ---
echo  [6/6] Iniciando SACTI-BACKUP...
start "" "%DESTINO%\SACTIBACKUP.exe"

echo.
echo  ============================================
echo    ACTUALIZACION TERMINADA
echo  ============================================
echo.
echo  Siguiente paso:
echo    1. En la aplicacion entre a Configuracion.
echo    2. En "Correo Notificaciones" capture los correos
echo       separados por punto y coma. Ejemplo:
echo       juan@empresa.com; ana@empresa.com
echo    3. Guarde. Con eso se dispara un respaldo.
echo.
echo  Utilitarios que quedaron en %DESTINO%:
echo    VerRegistroCorreo.bat    - ver si el correo salio y por que no
echo    ForzarRespaldoAhora.bat  - forzar un respaldo inmediato
echo.
pause
endlocal
