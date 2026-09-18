@echo off
rem ===================================================================
rem  LogScope build script.
rem
rem  NOTE ON ENCODING (read this before editing):
rem    This file is saved as UTF-8 WITHOUT a BOM, and the messages below
rem    are in Korean. A Korean Windows console starts on code page 949,
rem    so it would print those bytes as garbage. The "chcp 65001" a few
rem    lines down switches the console to UTF-8 first, and the original
rem    code page is put back at the end.
rem
rem    Keep this file UTF-8 without BOM. cmd.exe chokes on a BOM, and a
rem    BOM would also be printed as stray characters on the first line.
rem
rem  Usage:
rem     build.bat              Release, then run the self tests
rem     build.bat Debug        Debug
rem     build.bat Release nt   skip the tests
rem
rem  Output:
rem     out\LogScope.exe                          <- copy this folder as-is
rem     src\LogScope.App\bin\Release\LogScope.exe
rem
rem  Everything is also written to build.log.
rem ===================================================================

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"
set "SKIPTEST=%~2"
set "LOG=%ROOT%build.log"

rem ---- keep the window open when started by double-click ------------
rem  Done before chcp, while the console is still on its default page,
rem  so a non-ASCII path cannot upset the comparison.
set "HOLD="
echo %cmdcmdline% 2>nul | find /i "%~nx0" >nul 2>nul && set "HOLD=1"

rem ---- find MSBuild -------------------------------------------------
rem  Done before chcp as well. "for /f" over a command's output is the
rem  one thing that has historically misbehaved under code page 65001,
rem  so the vswhere result goes through a temp file and "set /p" instead.
set "MSBUILD="

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  "%VSWHERE%" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe > "%TEMP%\logscope_msbuild.txt" 2>nul
  if exist "%TEMP%\logscope_msbuild.txt" (
    set /p MSBUILD=<"%TEMP%\logscope_msbuild.txt"
    del "%TEMP%\logscope_msbuild.txt" >nul 2>nul
  )
)

if not defined MSBUILD (
  for %%E in (Enterprise Professional Community BuildTools) do (
    if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\%%E\MSBuild\15.0\Bin\MSBuild.exe" (
      set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\%%E\MSBuild\15.0\Bin\MSBuild.exe"
    )
  )
)

rem  Last resort: the MSBuild that ships with the .NET Framework.
rem  It may not understand C# 7.3 or WPF (.xaml). Use VS2017 if so.
if not defined MSBUILD (
  if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
  )
)

rem ---- switch the console to UTF-8 ----------------------------------
rem  Remember the current code page so it can be restored on the way out.
set "OLDCP="
for /f "tokens=2 delims=:" %%a in ('chcp') do set "OLDCP=%%a"
set "OLDCP=%OLDCP: =%"
set "OLDCP=%OLDCP:.=%"
chcp 65001 >nul 2>nul

rem  From here on the Korean text below prints correctly.

echo.
echo   LogScope 빌드  (%CONFIG%)
echo   ------------------------------------------------------------

if not defined MSBUILD (
  echo.
  echo   [오류] MSBuild 를 찾지 못했습니다.
  echo          Visual Studio 2017 또는 Build Tools for Visual Studio 2017 을
  echo          설치해 주세요.
  goto :fail
)
echo   MSBuild : %MSBUILD%
echo   로그    : %LOG%
echo.

rem ---- build --------------------------------------------------------
rem  /fl writes the whole thing to build.log. The console stays terse.
"%MSBUILD%" "%ROOT%LogScope.sln" /nologo /m /v:minimal /p:Configuration=%CONFIG% /p:Platform="Any CPU" /fl "/flp:logfile=%LOG%;verbosity=normal;encoding=UTF-8"

if errorlevel 1 (
  echo.
  echo   ============================================================
  echo    빌드 실패. 오류 줄만 추려 보면:
  echo   ============================================================
  findstr /i /c:": error" "%LOG%"
  echo   ------------------------------------------------------------
  echo    전체 내용은 build.log 에 있습니다.
  goto :fail
)

rem ---- tests --------------------------------------------------------
if /i "%SKIPTEST%"=="nt" goto :collect
echo.
echo   테스트
echo   ------------------------------------------------------------
"%ROOT%src\LogScope.Tests\bin\%CONFIG%\LogScope.Tests.exe"
if errorlevel 1 (
  echo.
  echo   [오류] 테스트가 실패했습니다. 위의 FAIL 줄을 보세요.
  goto :fail
)

:collect
rem ---- gather the output --------------------------------------------
set "OUT=%ROOT%out"
if not exist "%OUT%" mkdir "%OUT%"
copy /y "%ROOT%src\LogScope.App\bin\%CONFIG%\LogScope.exe"      "%OUT%\" >nul
copy /y "%ROOT%src\LogScope.App\bin\%CONFIG%\LogScope.Core.dll" "%OUT%\" >nul
if exist "%ROOT%src\LogScope.App\bin\%CONFIG%\LogScope.exe.config" (
  copy /y "%ROOT%src\LogScope.App\bin\%CONFIG%\LogScope.exe.config" "%OUT%\" >nul
)
if /i "%CONFIG%"=="Debug" (
  copy /y "%ROOT%src\LogScope.App\bin\%CONFIG%\*.pdb" "%OUT%\" >nul 2>nul
)

echo.
echo   ============================================================
echo    완료.
echo.
echo    실행 파일 : %OUT%\LogScope.exe
echo    out 폴더를 통째로 복사해서 쓰면 됩니다. 설치 프로그램은 필요 없습니다.
echo   ============================================================
echo.
if defined HOLD (
  echo   아무 키나 누르면 이 창이 닫힙니다.
  pause >nul
)
if defined OLDCP chcp %OLDCP% >nul 2>nul
endlocal
exit /b 0

:fail
echo.
if defined HOLD (
  echo   아무 키나 누르면 이 창이 닫힙니다.
  pause >nul
)
if defined OLDCP chcp %OLDCP% >nul 2>nul
endlocal
exit /b 1
