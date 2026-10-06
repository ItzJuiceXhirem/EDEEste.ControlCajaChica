@echo off
rem Doble clic aqui para preparar (la primera vez) y abrir el sistema.
rem Todo el trabajo lo hace app\Iniciar.ps1; este archivo solo existe porque un
rem .ps1 no se ejecuta con doble clic.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\Iniciar.ps1"
