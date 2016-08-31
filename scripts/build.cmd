@echo off

set name=%1
set version=%2
set reporoot=%~dp0\..

if "%name%"=="" (
  echo Please remember to specify the name to build as an argument.
  goto exit_fail
)

if "%version%"=="" (
  echo Please remember to specify which version to build as an argument.
  goto exit_fail
)

set msbuild=%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe

if not exist "%msbuild%" (
  echo Could not find MSBuild here:
  echo.
  echo   "%msbuild%"
  echo.
  goto exit_fail
)

set sln=%reporoot%\%name%.sln

if not exist "%sln%" (
  echo Could not find SLN to build here:
  echo.
  echo    "%sln%"
  echo.
  goto exit_fail
)

set nuget=%reporoot%\tools\NuGet\NuGet.exe

if not exist "%nuget%" (
  echo Could not find NuGet here:
  echo.
  echo    "%nuget%"
  echo.
  goto exit_fail
)

set destination=%reporoot%\deploy

if exist "%destination%" (
  rd "%destination%" /s/q
)

mkdir "%destination%"
if %ERRORLEVEL% neq 0 goto exit_fail

"%msbuild%" "%sln%" /p:Configuration=Release /t:rebuild
if %ERRORLEVEL% neq 0 goto exit_fail

"%nuget%" pack "%name%\%name%.nuspec" -OutputDirectory "%destination%" -Version %version%
if %ERRORLEVEL% neq 0 goto exit_fail

goto exit




:exit_fail

exit /b 1



:exit