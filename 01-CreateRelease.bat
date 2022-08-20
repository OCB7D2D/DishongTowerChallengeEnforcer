@echo off

SET NAME=DishongTowerChallengeEnforcer
SET FOLDER=Dishong Tower Challenge Enforcer

if not exist build\ (
  mkdir build
)

if exist build\"%FOLDER%"\ (
  echo remove existing directory
  rmdir build\"%FOLDER%" /S /Q
)

mkdir build\"%FOLDER%"

SET VERSION=snapshot

if not "%1"=="" (
  SET VERSION=%1
)

echo create %VERSION%

xcopy *.xml build\"%FOLDER%"\
xcopy *.md build\"%FOLDER%"\
xcopy *.dll build\"%FOLDER%"\
xcopy Config build\"%FOLDER%"\Config\ /S
xcopy Resources build\"%FOLDER%"\Resources\ /S
xcopy UIAtlases build\"%FOLDER%"\UIAtlases\ /S

cd build
echo Packaging %NAME%-%VERSION%.zip
powershell Compress-Archive \"%FOLDER%\" %NAME%-%VERSION%.zip -Force
cd ..

SET RV=%ERRORLEVEL%
if "%CI%"=="" pause
exit /B %RV%
