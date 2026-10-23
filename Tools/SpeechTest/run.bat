@echo off
rem Тест распознавания речи: окно с микрофоном. Аргументы передаются в speech_test.py
cd /d "%~dp0"
".venv\Scripts\python.exe" speech_test.py %*
