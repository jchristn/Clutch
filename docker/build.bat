@echo off
REM Build and push the Clutch server and dashboard images.
REM   build.bat            build only
REM   build.bat --push     build and push to the registry
setlocal
cd /d "%~dp0.."

set SERVER_IMAGE=jchristn77/clutch-server:v0.1.0
set UI_IMAGE=jchristn77/clutch-ui:v0.1.0

echo Building %SERVER_IMAGE% ...
docker build -f src/Clutch.Server/Dockerfile -t %SERVER_IMAGE% .
if errorlevel 1 exit /b 1

echo Building %UI_IMAGE% ...
docker build -f dashboard/Dockerfile -t %UI_IMAGE% dashboard
if errorlevel 1 exit /b 1

if "%1"=="--push" (
  echo Pushing images ...
  docker push %SERVER_IMAGE%
  docker push %UI_IMAGE%
)

echo Done.
endlocal
