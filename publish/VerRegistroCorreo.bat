@echo off
REM ==================================================================
REM  Muestra el resultado de los ultimos envios de correo
REM  de SACTI-BACKUP. Ejecutar desde la carpeta de la aplicacion.
REM ==================================================================
setlocal
title SACTI-BACKUP - Registro de correos
color 0B

set "APP=%~dp0"
set "LOG=%APP%backup.log"

echo.
echo  ============================================
echo    REGISTRO DE ENVIO DE CORREOS
echo  ============================================
echo.

if not exist "%LOG%" (
    echo  No existe "%LOG%".
    echo  Todavia no se ha ejecutado ningun respaldo con esta version.
    echo.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$l = Select-String -Path '%LOG%' -Pattern 'EmailService|Correo ENVIADO|Correo NO ENVIADO' | Select-Object -Last 25;" ^
  "if (-not $l) { Write-Host '  Sin registros de correo todavia.' -ForegroundColor Yellow; exit }" ^
  "foreach ($x in $l) {" ^
  "  $t = $x.Line;" ^
  "  if ($t -match 'Correo ENVIADO') { Write-Host $t -ForegroundColor Green }" ^
  "  elseif ($t -match 'NO ENVIADO|ADVERTENCIA|error=') { Write-Host $t -ForegroundColor Red }" ^
  "  else { Write-Host $t } }"

echo.
echo  --------------------------------------------
echo   VERDE = el correo si salio del servidor.
echo   ROJO  = no salio; la linea dice el motivo.
echo  --------------------------------------------
echo.
echo  Detalle tecnico completo (si hubo error):
echo    C:\Logs\Correo\CorreoError_AAAAMMDD.txt
echo.
pause
endlocal
