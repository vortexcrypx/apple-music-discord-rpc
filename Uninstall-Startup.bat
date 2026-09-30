@echo off
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "AppleMusicDiscordRPC" /f
echo Apple Music Discord RPC has been removed from Windows Startup.
pause
