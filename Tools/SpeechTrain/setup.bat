@echo off
chcp 65001 >nul
rem Установка с нуля: окружение Python 3.13, PyTorch (только процессор), пакеты, голоса Piper и модели признаков
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" py -3.13 -m venv .venv
".venv\Scripts\python.exe" -m pip install torch --index-url https://download.pytorch.org/whl/cpu
".venv\Scripts\python.exe" -m pip install -r requirements.txt
".venv\Scripts\python.exe" download.py
echo Готово. Дальше: generate.py, train.py, evaluate.py (см. README.md)
