@echo off
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "ROOT=%%~fI"
cd /d "%ROOT%"

if not exist ".\scripts\BUILD_V13.bat" (
    echo ========================================
    echo LOI: KHONG TIM THAY scripts\BUILD_V13.bat
    echo Hay ap dung lai PATCH_DON_GON_BAT_V13.
    echo ========================================
    pause
    exit /b 1
)

call ".\scripts\BUILD_V13.bat" --no-pause
if errorlevel 1 (
    echo.
    echo ========================================
    echo BUILD FAILED - KHONG MO TOOL
    echo ========================================
    pause
    exit /b 1
)

if not exist ".\dist_v13\ToolTikTokManagerV13.exe" (
    echo.
    echo ========================================
    echo BUILD OK NHUNG KHONG TIM THAY FILE EXE
    echo .\dist_v13\ToolTikTokManagerV13.exe
    echo ========================================
    pause
    exit /b 1
)

start "" ".\dist_v13\ToolTikTokManagerV13.exe"
exit /b 0
