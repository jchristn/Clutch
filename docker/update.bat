@echo off
setlocal
cd /d "%~dp0"

echo Pulling latest images...
docker compose pull || exit /b 1

echo Stopping the stack...
docker compose down || exit /b 1

echo Starting the stack...
docker compose up -d || exit /b 1

echo.
docker ps -a

endlocal
