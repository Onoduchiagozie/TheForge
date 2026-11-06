@echo off
set "FORGE_PWSH=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
where pwsh.exe >nul 2>nul
if not errorlevel 1 set "FORGE_PWSH=pwsh.exe"
"%FORGE_PWSH%" -NoProfile -File "%~dp0tools\local_frontend\Start-TheForge.ps1" %*
if errorlevel 1 (
    echo.
    echo TheForge could not start. The error is shown above.
    pause
    exit /b 1
)
