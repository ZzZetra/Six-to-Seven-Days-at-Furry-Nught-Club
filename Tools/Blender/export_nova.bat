@echo off
chcp 65001 >nul
rem Выгрузить Нову из Art\Nova\nova wip.blend в Assets\Models\Nova (FBX и текстура тела, если она вшита).
rem Путь к Blender можно поменять в BLENDER ниже.
setlocal
set "BLENDER=C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
set "ROOT=%~dp0..\.."
set "OUT=%ROOT%\Assets\Models\Nova"

"%BLENDER%" -b "%ROOT%\Art\Nova\nova wip.blend" --python "%~dp0export_nova.py" -- --fbx "%OUT%\Nova.fbx" --textures "%OUT%\Textures" || goto :fail
echo Готово: %OUT%
exit /b 0

:fail
echo Ошибка выгрузки
exit /b 1
