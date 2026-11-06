@echo off
chcp 65001 >nul
rem Скачать с Poly Haven (CC0) недостающие текстуры карты клуба в Art\Map\textures (1K, jpg).
rem Нужен интернет. Потом запусти export_map.bat и пересобери офис.
setlocal
set "BLENDER=C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
set "ROOT=%~dp0..\.."
set "SRC=%ROOT%\Art\Map\map 67dfn wip.blend"
set "OUT=%ROOT%\Art\Map\textures"
set "PY=%~dp0download_map_textures.py"

"%BLENDER%" -b "%SRC%" --python "%PY%" -- --out "%OUT%" || goto :fail
echo Готово: %OUT%
exit /b 0

:fail
echo Ошибка скачивания
exit /b 1
