@echo off
rem ===================================================================
rem  LogScope build script.
rem
rem  ENCODING -- read this before editing the file.
rem
rem    This file is saved in CP949 (the Korean Windows ANSI/OEM code
rem    page). It does NOT call "chcp", on purpose.
rem
rem    The previous version did the opposite: UTF-8 plus "chcp 65001".
rem    That is the advice you find everywhere and it did not work.
rem      - Under code page 65001 the legacy console can only show
rem        Hangul with a TrueType font that actually contains Hangul.
rem        The Korean default console font is a raster font, so the
rem        text came out as boxes or garbage.
rem      - On Windows 7 it is worse: cmd.exe re-reads the batch file
rem        while it runs, and under 65001 it can misread its own bytes
rem        and lose "goto" targets.
rem
rem    A Korean Windows console already starts on CP949. Writing the
rem    bytes it is expecting, and leaving the code page alone, is the
rem    thing that actually works -- on 7, 10 and 11 alike.
rem
rem    Every message below exists twice: once in Korean (CP949 bytes)
rem    and once in plain ASCII. If the console is not on code page 949
rem    the ASCII half is printed instead, so this script can never
rem    print garbage, whatever machine it lands on.
rem
rem    IF YOU EDIT THIS FILE, save it as CP949 / ANSI / EUC-KR.
rem    In VS Code: click the encoding button at the bottom right,
rem    "Save with Encoding", "Korean (EUC-KR)". No BOM. No "chcp".
rem    On GitHub the Korean lines will look like mojibake, because
rem    GitHub assumes UTF-8. That is expected; these ASCII comments
rem    are here so the file still explains itself.
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
rem  Logs:
rem     build.log       everything, UTF-8, meant to be opened in an editor
rem     build.err.log   error lines only, console encoding, printed below
rem
rem  See ":cp" at the bottom: after every child process we put the code
rem  page back, because a child can change it for the whole window.
rem ===================================================================

setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"
set "SKIPTEST=%~2"
set "LOG=%ROOT%build.log"
set "ERRLOG=%ROOT%build.err.log"

rem ---- keep the window open when started by double-click ------------
set "HOLD="
echo %cmdcmdline% 2>nul | find /i "%~nx0" >nul 2>nul && set "HOLD=1"

rem ---- which half of the messages can this console show? -------------
rem  KO is set only when the console is on 949, which is where the
rem  Korean bytes in this file render correctly.
set "CP="
for /f "tokens=2 delims=:" %%a in ('chcp') do set "CP=%%a"
set "CP=%CP: =%"
set "CP=%CP:.=%"
set "KO="
if "%CP%"=="949" set "KO=1"

echo.
if not defined KO echo   [note] console code page is %CP%, not 949 -- using English messages.
if not defined KO echo.

call :msg "LogScope 빌드  [%CONFIG%]" "LogScope build  [%CONFIG%]"
echo   ------------------------------------------------------------

rem ---- find MSBuild -------------------------------------------------
set "MSBUILD="

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  for /f "usebackq delims=" %%p in (`"%VSWHERE%" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%p"
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
rem  It may not understand C# 7.3 or WPF. Use VS2017 if so.
if not defined MSBUILD (
  if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
  )
)

if not defined MSBUILD (
  echo.
  call :msg "[오류] MSBuild 를 찾지 못했습니다." "[error] MSBuild was not found."
  call :msg "       Visual Studio 2017 또는 Build Tools for" "       Install Visual Studio 2017, or the"
  call :msg "       Visual Studio 2017 을 설치해 주세요." "       Build Tools for Visual Studio 2017."
  goto :fail
)

call :msg "MSBuild : %MSBUILD%" "MSBuild : %MSBUILD%"
call :msg "로그     : %LOG%" "Log     : %LOG%"

rem ---- which revision is this? --------------------------------------
rem  Printed so "I applied the fix but the error is still there" can be
rem  told apart from "this working copy is older than the fix" without
rem  guessing. Silently skipped when git is not on PATH.
set "REV="
set "DIRTY="
git --version >nul 2>nul
if not errorlevel 1 (
  for /f "usebackq delims=" %%h in (`git -C "%ROOT%." rev-parse --short=12 HEAD 2^>nul`) do set "REV=%%h"
  for /f "usebackq delims=" %%d in (`git -C "%ROOT%." status --porcelain --untracked-files=no 2^>nul`) do set "DIRTY=1"
)
if defined REV call :msg "커밋     : %REV%" "Commit  : %REV%"
if defined DIRTY call :msg "           (고친 내용이 커밋 안 된 채로 섞여 있습니다)" "           (uncommitted changes are mixed in)"
set "VER="
if exist "%ROOT%version.txt" set /p VER=<"%ROOT%version.txt"
if defined VER call :msg "버전     : %VER%" "Version : %VER%"
echo.

rem ---- build --------------------------------------------------------
rem  Two log files on purpose.
rem    build.log      UTF-8, so it opens cleanly in an editor.
rem    build.err.log  default (ANSI) encoding, so the "type" below
rem                   prints the compiler's own messages without
rem                   turning them into garbage on a CP949 console.
if exist "%ERRLOG%" del "%ERRLOG%" >nul 2>nul

"%MSBUILD%" "%ROOT%LogScope.sln" /nologo /m /v:minimal /p:Configuration=%CONFIG% /p:Platform="Any CPU" /fl1 "/flp1:logfile=%LOG%;verbosity=normal;encoding=UTF-8" /fl2 "/flp2:logfile=%ERRLOG%;errorsonly;verbosity=normal"
set "RC=%ERRORLEVEL%"
call :cp

if not "%RC%"=="0" (
  echo.
  echo   ============================================================
  call :msg " 빌드 실패. 오류 줄만 추려 보면:" " Build failed. Just the error lines:"
  echo   ============================================================
  if exist "%ERRLOG%" type "%ERRLOG%"
  echo   ------------------------------------------------------------
  call :msg " 전체 내용은 build.log 에 있습니다." " The full log is in build.log."
  goto :fail
)

rem ---- tests --------------------------------------------------------
if /i "%SKIPTEST%"=="nt" goto :collect
echo.
call :msg "테스트" "Tests"
echo   ------------------------------------------------------------
"%ROOT%src\LogScope.Tests\bin\%CONFIG%\LogScope.Tests.exe"
set "RC=%ERRORLEVEL%"
call :cp

if not "%RC%"=="0" (
  echo.
  call :msg "[오류] 테스트가 실패했습니다. 위의 FAIL 줄을 보세요." "[error] A test failed. See the FAIL lines above."
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
call :msg " 완료." " Done."
echo.
call :msg " 실행 파일 : %OUT%\LogScope.exe" " Executable : %OUT%\LogScope.exe"
call :msg " out 폴더를 통째로 복사해서 쓰면 됩니다." " Copy the whole out folder and run it."
call :msg " 설치 프로그램은 필요 없습니다." " No installer is needed."
echo   ============================================================
echo.
call :hold
endlocal
exit /b 0

:fail
echo.
call :hold
endlocal
exit /b 1

rem ===================================================================
rem  Subroutines. They must sit past an "exit /b" so the script cannot
rem  fall into them.
rem ===================================================================

rem  Print %1 on a Korean console, %2 everywhere else.
rem  Written without parenthesised if-blocks on purpose: a ")" inside
rem  a message would otherwise close the block and break the line.
:msg
if defined KO goto :msg_ko
echo   %~2
goto :eof
:msg_ko
echo   %~1
goto :eof

rem  Put the console code page back the way we found it.
rem
rem  A child process can change the code page, and the change OUTLIVES
rem  that process -- the code page belongs to the console window, not to
rem  the process that set it. .NET's "Console.OutputEncoding" setter does
rem  exactly this. When it happens, every Korean line this script prints
rem  afterwards is CP949 bytes going into a console that is no longer on
rem  CP949, so the Hangul vanishes and the lines look truncated.
rem
rem  This is NOT the "chcp 65001" the header warns about. We only ever
rem  restore the page the console already had when the script started,
rem  so a console that was never Korean is left exactly as it was.
:cp
if not defined CP goto :eof
chcp %CP% >nul 2>nul
goto :eof

rem  Wait for a key, but only when the window would vanish otherwise.
:hold
if not defined HOLD goto :eof
call :msg "아무 키나 누르면 이 창이 닫힙니다." "Press any key to close this window."
pause >nul
goto :eof
