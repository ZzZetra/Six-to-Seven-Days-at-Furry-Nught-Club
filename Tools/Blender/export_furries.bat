@echo off
chcp 65001 >nul
rem Выгрузить Стрея, Доли и Дали из Art\Furries\*.blend в Assets\Models\<имя> (FBX и текстуры материалов —
rem вшитые в .blend или из Art\Furries\textures\<Stray|Doli|Dali> с теми же именами файлов, что в .blend). В каждом файле автора по 2–3 варианта персонажа — какой риг брать,
rem написано ниже (третий параметр). Путь к Blender можно поменять в BLENDER.
setlocal
set "BLENDER=C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
set "ROOT=%~dp0..\.."

call :one Strey.blend Stray "Blocky Rig (calico full)" || goto :fail
call :one Dolie.blend Doli "Blocky Rig (Dolie alt)" || goto :fail
call :one Dalie.blend Dali "Dalie alt" || goto :fail
echo Готово: %ROOT%\Assets\Models
exit /b 0

:one
set "OUT=%ROOT%\Assets\Models\%~2"
"%BLENDER%" -b "%ROOT%\Art\Furries\%~1" --python "%~dp0export_furry.py" -- --rig "%~3" --name %~2 --fbx "%OUT%\%~2.fbx" --textures "%OUT%\Textures"
exit /b %errorlevel%

:fail
echo Ошибка выгрузки
exit /b 1
