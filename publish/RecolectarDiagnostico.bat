@echo off
REM ==================================================================
REM  RECOLECTOR DE DIAGNOSTICO - SACTI-BACKUP
REM  Junta backup.log, scheduler.log y los logs de correo en un ZIP
REM  en el Escritorio, listo para enviarlo a soporte.
REM  NO incluye SactiBackup.json (trae contrasenas).
REM ==================================================================
setlocal EnableDelayedExpansion
title Recolectar diagnostico - SACTI-BACKUP
color 0A

echo.
echo  ============================================
echo    RECOLECTAR DIAGNOSTICO
echo  ============================================
echo.

REM --- Detectar carpeta de instalacion ---
set "DESTINO="
for /f "usebackq delims=" %%i in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "try{$t=Get-ScheduledTask -TaskName 'SACTI Backup' -ErrorAction Stop; $e=@($t.Actions.Execute)[0]; if($e){Split-Path ($e.Trim([char]34)) -Parent}}catch{}"`) do set "DESTINO=%%i"

if not defined DESTINO set "DESTINO=%~dp0"
if not exist "%DESTINO%\SACTIBACKUP.exe" set "DESTINO=%~dp0"

echo  Carpeta de la aplicacion: %DESTINO%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Continue';" ^
  "$app = '%DESTINO%'.TrimEnd('\');" ^
  "$tmp = Join-Path $env:TEMP ('SactiDiag_' + (Get-Date -Format 'yyyyMMdd_HHmmss'));" ^
  "New-Item -ItemType Directory -Path $tmp -Force | Out-Null;" ^
  "foreach ($n in @('backup.log','scheduler.log')) {" ^
  "  $p = Join-Path $app $n;" ^
  "  if (Test-Path $p) { Copy-Item $p $tmp -Force; Write-Host ('  [OK]    ' + $n) }" ^
  "  else { Write-Host ('  [FALTA] ' + $n) } };" ^
  "if (Test-Path 'C:\Logs\Correo') {" ^
  "  $d = Join-Path $tmp 'LogsCorreo'; New-Item -ItemType Directory -Path $d -Force | Out-Null;" ^
  "  $n = @(Get-ChildItem 'C:\Logs\Correo\*.txt' -ErrorAction SilentlyContinue);" ^
  "  if ($n.Count -gt 0) { $n | Copy-Item -Destination $d -Force; Write-Host ('  [OK]    C:\Logs\Correo (' + $n.Count + ' archivos)') }" ^
  "  else { Write-Host '  [VACIO] C:\Logs\Correo sin archivos' } }" ^
  "else { Write-Host '  [FALTA] C:\Logs\Correo no existe' };" ^
  "$info = Join-Path $tmp 'info.txt';" ^
  "@('Equipo: ' + $env:COMPUTERNAME, 'Usuario: ' + $env:USERNAME, 'Fecha: ' + (Get-Date), 'Carpeta app: ' + $app) | Set-Content $info -Encoding UTF8;" ^
  "$exe = Join-Path $app 'SACTIBACKUP.exe';" ^
  "if (Test-Path $exe) { ('Version exe: ' + (Get-Item $exe).LastWriteTime) | Add-Content $info -Encoding UTF8 };" ^
  "$cfg = Join-Path $app 'SactiBackup.json';" ^
  "if (Test-Path $cfg) { try {" ^
  "  $o = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((Get-Content $cfg -Raw))) | ConvertFrom-Json;" ^
  "  ('CorreoFTP: ' + $o.CorreoFTP) | Add-Content $info -Encoding UTF8;" ^
  "  ('CorreoNotificaciones: ' + $o.CorreoNotificaciones) | Add-Content $info -Encoding UTF8;" ^
  "  ('FechaUltimoRespaldo: ' + $o.FechaUltimoRespaldo) | Add-Content $info -Encoding UTF8;" ^
  "  ('FechaUltimoRespaldoNube: ' + $o.FechaUltimoRespaldoNube) | Add-Content $info -Encoding UTF8;" ^
  "  ('RespaldarNube: ' + $o.RespaldarNube) | Add-Content $info -Encoding UTF8;" ^
  "  ('HoraRespaldo: ' + $o.HoraRespaldo) | Add-Content $info -Encoding UTF8 } catch { 'No se pudo leer SactiBackup.json' | Add-Content $info -Encoding UTF8 } };" ^
  "$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) ('SactiDiag_' + $env:COMPUTERNAME + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.zip');" ^
  "Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $zip -Force;" ^
  "Remove-Item $tmp -Recurse -Force;" ^
  "Write-Host '';" ^
  "Write-Host ('  ZIP generado en el Escritorio:') -ForegroundColor Green;" ^
  "Write-Host ('  ' + $zip) -ForegroundColor Green"

echo.
echo  Envie ese archivo ZIP a soporte.
echo  (No contiene contrasenas: se excluye SactiBackup.json)
echo.
pause
endlocal
