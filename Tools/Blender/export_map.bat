@echo off
chcp 65001 >nul
rem Выгрузить карту клуба из Art\Map в Assets\Models\Map (FBX, текстуры, map_materials.json).
rem Запускать из любой папки. Путь к Blender можно поменять в BLENDER ниже.
rem Недостающие текстуры можно положить в Art\Map\textures — выгрузка возьмёт их оттуда.
setlocal
set "BLENDER=C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
set "ROOT=%~dp0..\.."
set "SRC=%ROOT%\Art\Map\map 67dfn wip.blend"
set "OUT=%ROOT%\Assets\Models\Map"
set "PY=%~dp0export_map.py"

"%BLENDER%" -b "%SRC%" --python "%PY%" -- --out "%OUT%" || goto :fail
echo Готово: %OUT%
echo Переключись в Unity и пересобери офис: SSDFNC - Build Test Platform
exit /b 0

:fail
echo Ошибка выгрузки
exit /b 1
