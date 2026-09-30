@echo off
cd /d "%~dp0"
echo Building Apple Music Discord RPC...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b %ERRORLEVEL%
)
echo Build completed successfully! Output: %~dp0publish\AppleMusicDiscordRPC.exe
pause
