@echo off
setlocal enabledelayedexpansion
rem ===================================================================
rem  LogScope 빌드
rem
rem  쓰는 법:
rem     build.bat              Release 로 빌드하고 테스트까지 돌립니다
rem     build.bat Debug        Debug 로 빌드
rem     build.bat Release nt   테스트 건너뛰기
rem
rem  확장 프로그램이나 NuGet 복원이 필요 없습니다.
rem  Visual Studio 2017 (Community 포함) 이 깔려 있으면 그대로 됩니다.
rem
rem  결과물:
rem     out\LogScope.exe                        <- 이 폴더를 통째로 복사하면 됩니다
rem     src\LogScope.App\bin\Release\LogScope.exe
rem
rem  모든 출력은 build.log 에도 남습니다. 창이 닫혀 버려도 그 파일을 보면 됩니다.
rem ===================================================================

set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"
set "SKIPTEST=%~2"
set "LOG=%ROOT%build.log"

rem 더블클릭으로 띄운 경우에는 끝나고 창을 붙들어 둡니다.
rem (그냥 닫히면 오류 메시지를 읽을 수가 없습니다.)
set "HOLD="
echo %cmdcmdline% 2>nul | find /i "%~nx0" >nul 2>nul && set "HOLD=1"

echo.
echo   LogScope 빌드  (%CONFIG%)
echo   ------------------------------------------------------------

rem ---- MSBuild 찾기 -------------------------------------------------
set "MSBUILD="

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2^>nul`) do (
    if not defined MSBUILD set "MSBUILD=%%i"
  )
)

if not defined MSBUILD (
  for %%E in (Enterprise Professional Community BuildTools) do (
    if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\%%E\MSBuild\15.0\Bin\MSBuild.exe" (
      set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2017\%%E\MSBuild\15.0\Bin\MSBuild.exe"
    )
  )
)

rem 마지막 수단: .NET Framework 에 딸려 오는 MSBuild.
rem C# 7.3 과 WPF(.xaml) 를 못 다룰 수 있습니다. 그때는 VS2017 을 쓰세요.
if not defined MSBUILD (
  if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
  )
)

if not defined MSBUILD (
  echo.
  echo   [오류] MSBuild 를 찾지 못했습니다.
  echo          Visual Studio 2017 또는 Build Tools for Visual Studio 2017 을 설치해 주세요.
  goto :fail
)
echo   MSBuild : %MSBUILD%
echo   로그    : %LOG%
echo.

rem ---- 빌드 ---------------------------------------------------------
rem /fl 로 build.log 에 전부 남깁니다. 화면에는 요점만 나옵니다.
"%MSBUILD%" "%ROOT%LogScope.sln" /nologo /m /v:minimal /p:Configuration=%CONFIG% /p:Platform="Any CPU" /fl "/flp:logfile=%LOG%;verbosity=normal"

if errorlevel 1 (
  echo.
  echo   ============================================================
  echo    빌드 실패. 오류만 추려 보면:
  echo   ============================================================
  findstr /i /c:": error" "%LOG%"
  echo   ------------------------------------------------------------
  echo    전체 내용은 build.log 에 있습니다.
  goto :fail
)

rem ---- 테스트 -------------------------------------------------------
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
rem ---- 결과 모으기 ---------------------------------------------------
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
if defined HOLD pause
endlocal
exit /b 0

:fail
echo.
if defined HOLD pause
endlocal
exit /b 1
