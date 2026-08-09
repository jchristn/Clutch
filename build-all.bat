@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-all.bat ^<docker-image-tag^>
  echo Example: build-all.bat v0.1.0
  exit /b 1
)

set "IMAGE_TAG=%~1"
call build-server.bat "%IMAGE_TAG%" || exit /b 1
call build-dashboard.bat "%IMAGE_TAG%" || exit /b 1

echo.
echo ============================================
echo Clutch Docker build-all completed successfully!
echo.
echo Components built and pushed:
echo   - Clutch Server: jchristn77/clutch-server:%IMAGE_TAG%
echo   - Clutch Server: jchristn77/clutch-server:latest
echo   - Clutch Dashboard: jchristn77/clutch-ui:%IMAGE_TAG%
echo   - Clutch Dashboard: jchristn77/clutch-ui:latest
echo ============================================

endlocal
