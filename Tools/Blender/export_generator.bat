@echo off
chcp 65001 >nul
rem Выгрузить генератор из Art\Generator\*.blend в Assets\Models\Generator (FBX и текстуры).
rem Запускать из любой папки. Путь к Blender можно поменять в BLENDER ниже.
setlocal
set "BLENDER=C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
set "ROOT=%~dp0..\.."
set "SRC=%ROOT%\Art\Generator"
set "OUT=%ROOT%\Assets\Models\Generator"
set "PY=%~dp0export_generator.py"

"%BLENDER%" -b "%SRC%\generator rig rigged.blend" --python "%PY%" -- --fbx "%OUT%\Generator.fbx" --textures "%OUT%\Textures" || goto :fail
"%BLENDER%" -b "%SRC%\generator idle.blend" --python "%PY%" -- --fbx "%OUT%\Generator@Idle.fbx" || goto :fail
"%BLENDER%" -b "%SRC%\gen cap opening.blend" --python "%PY%" -- --fbx "%OUT%\Generator@CapOpen.fbx" || goto :fail
"%BLENDER%" -b "%SRC%\gen cap closing.blend" --python "%PY%" -- --fbx "%OUT%\Generator@CapClose.fbx" || goto :fail
echo Готово: %OUT%
exit /b 0

:fail
echo Ошибка выгрузки
exit /b 1
