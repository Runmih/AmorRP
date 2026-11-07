@echo off
setlocal
rem Set this once to the same SSH target you use for your Ubuntu login.
set "AMORRP_SSH_TARGET=REPLACE_WITH_YOUR_SSH_TARGET"

if "%AMORRP_SSH_TARGET%"=="REPLACE_WITH_YOUR_SSH_TARGET" (
    echo Edit this file in Notepad and set AMORRP_SSH_TARGET to your Ubuntu SSH target.
    pause
    exit /b 1
)

ssh -t "%AMORRP_SSH_TARGET%" "python3 /opt/amorrp/source/deploy/home/prepare-update.py"
if errorlevel 1 (
    echo.
    echo Update preparation failed. The running server has not been replaced.
    pause
    exit /b 1
)
echo.
echo Follow the Portainer instructions printed above to apply the update.
pause
