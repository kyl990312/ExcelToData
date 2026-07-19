@echo off
chcp 65001 >nul 2>&1

:: 탭 이름을 프로젝트 폴더명으로 설정
for %%I in ("%~dp0.") do set "PROJECT_NAME=%%~nxI"
title %PROJECT_NAME%

cls

cd /d "%~dp0\.ai"

if not exist "%CD%\.claude\skills" (
    echo ========================================
    echo   Development Assistant System
    echo ========================================
    echo.
    echo   [NOT INSTALLED] .ai directory or skills not found.
    echo   Please follow the README to complete installation.
    echo.
    pause
    exit /b 1
)

:: Read patch version from config/patchinfo.md
set PATCH_VER=-
if exist "config\patchinfo.md" (
    for /f "tokens=2 delims=: " %%i in ('findstr "current_version" "config\patchinfo.md"') do set PATCH_VER=%%i
)

echo ========================================
echo   Development Assistant System (v%PATCH_VER%)
echo ========================================
echo.
echo   1. /architector   - Analyze and document project structure
echo   2. /developer     - Design and implement
echo   3. /reviewer      - Code review
echo   4. Other          - General questions
echo.
echo   First time? Run /initialize to configure Discord.
echo ========================================
echo.

claude

if errorlevel 1 (
    echo.
    echo Claude Code exited with error.
)

pause
