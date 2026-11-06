@echo off
set "ORACLE_PWSH=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
where pwsh.exe >nul 2>nul
if not errorlevel 1 set "ORACLE_PWSH=pwsh.exe"
"%ORACLE_PWSH%" -NoProfile -File "%~dp0Start-OracleConsole.ps1" %*
if errorlevel 1 (
    echo.
    echo The console could not start. The error is shown above.
    pause
    exit /b 1
)
