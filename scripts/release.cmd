@echo off

set reporoot=%~dp0\..
set build=%reporoot%\scripts\build.cmd

if not exist "%build%" (
  echo Could not find %build%
  goto exit_fail
)

set push=%reporoot%\scripts\push.cmd

if not exist "%push%" (
  echo Could not find %push%
  goto exit_fail
)

call "%build%" %*
if %ERRORLEVEL% neq 0 (
  echo Error executing build script.
  goto exit_fail
)

call "%push%" %2
if %ERRORLEVEL% neq 0 (
  echo Error executing push script.
  goto exit_fail
)

goto exit




:exit_fail

exit /b 1



:exit