@echo off
cd /d "%~dp0"
set "EXE_PATH=%~dp0publish\AppleMusicDiscordRPC.exe"
if not exist "%EXE_PATH%" (
    echo Executable not found at %EXE_PATH%!
    echo Please run Build.bat first.
    pause
    exit /b 1
)
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "AppleMusicDiscordRPC" /t REG_SZ /d "\"%EXE_PATH%\" --silent" /f
echo Apple Music Discord RPC has been added to Windows Startup!
echo It will now start automatically in the background whenever Windows boots.
pause
