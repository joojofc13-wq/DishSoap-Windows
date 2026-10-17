@echo off
if exist "%~dp0DishSoap.Builder.exe" (
    start "" "%~dp0DishSoap.Builder.exe"
) else (
    powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0tools\windows\owner-builder.ps1"
)
