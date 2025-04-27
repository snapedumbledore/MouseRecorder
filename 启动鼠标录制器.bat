@echo off
echo 正在以管理员身份启动鼠标录制器...
powershell -Command "Start-Process '%~dp0MouseRecorder.exe' -Verb RunAs"
pause