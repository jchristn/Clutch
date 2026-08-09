@echo off
REM Reset the local Clutch stack to factory state: stop everything, drop the data
REM volumes (Postgres, Prometheus, Grafana), and restore pristine settings.
setlocal
cd /d "%~dp0.."

echo This will STOP the Clutch stack and DELETE all local data (Postgres, Prometheus, Grafana).
set /p CONFIRM="Type 'RESET' to confirm: "
if not "%CONFIRM%"=="RESET" (
  echo Aborted.
  exit /b 1
)

echo Stopping stack and removing volumes...
docker compose down -v

echo Restoring pristine settings from factory\templates ...
copy /y factory\templates\clutch.node1.json server\clutch.node1.json >nul
copy /y factory\templates\clutch.node2.json server\clutch.node2.json >nul

echo Factory reset complete. Start again with: docker compose up -d
endlocal
