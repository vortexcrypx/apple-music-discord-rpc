@echo off
cd /d "%~dp0"
echo Starting Apple Music Discord RPC in the background...
start "" "%~dp0publish\AppleMusicDiscordRPC.exe"
echo Application started! Look for the Apple Music icon in your system tray (bottom-right).
ping 127.0.0.1 -n 2 >nul
