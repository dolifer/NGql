@echo off
where bash >nul 2>nul
if errorlevel 1 (
    echo make requires Bash. Install Git for Windows ^(includes Git Bash^) or WSL, then re-run.
    exit /b 1
)
bash -c "make %*"
