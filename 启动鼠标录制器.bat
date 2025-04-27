@echo off
echo 正在以管理员身份启动鼠标录制器...
cd /d "%~dp0"
powershell -Command "Start-Process -FilePath \"MouseRecorder.exe\" -Verb RunAs"
pause
