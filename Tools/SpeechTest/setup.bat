@echo off
chcp 65001 >nul
rem Установка с нуля (если папки .venv или models нет): окружение Python 3.14, пакеты, русская и английская модели Vosk
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" py -3.14 -m venv .venv
".venv\Scripts\python.exe" -m pip install vosk sounddevice
if not exist "models\vosk-model-small-ru-0.22" (
    if not exist models mkdir models
    curl -L --fail -o models\model.zip https://alphacephei.com/vosk/models/vosk-model-small-ru-0.22.zip
    ".venv\Scripts\python.exe" -c "import zipfile; zipfile.ZipFile('models/model.zip').extractall('models')"
    del models\model.zip
)
if not exist "models\vosk-model-small-en-us-0.15" (
    curl -L --fail -o models\model-en.zip https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip
    ".venv\Scripts\python.exe" -c "import zipfile; zipfile.ZipFile('models/model-en.zip').extractall('models')"
    del models\model-en.zip
)
echo Готово. Запуск: run.bat
