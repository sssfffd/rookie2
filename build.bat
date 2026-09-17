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
rem  결과물은 out\ 에 모입니다. 그 폴더를 통째로 복사하면 실행됩니다.
rem ===================================================================

set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"
set "SKIPTEST=%~2"

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
rem C# 7.3 을 못 알아들을 수 있습니다. 그때는 VS2017 을 쓰세요.
if not defined MSBUILD (
  if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
  )
)

if not defined MSBUILD (
  echo   [오류] MSBuild 를 찾지 못했습니다.
  echo          Visual Studio 2017 또는 Build Tools for Visual Studio 2017 을 설치해 주세요.
  exit /b 1
)
echo   MSBuild : %MSBUILD%

rem ---- 빌드 ---------------------------------------------------------
"%MSBUILD%" "%ROOT%LogScope.sln" /nologo /m /v:minimal /p:Configuration=%CONFIG% /p:Platform="Any CPU"
if errorlevel 1 (
  echo.
  echo   [오류] 빌드에 실패했습니다.
  exit /b 1
)

rem ---- 테스트 -------------------------------------------------------
if /i "%SKIPTEST%"=="nt" goto :collect
echo.
echo   테스트
echo   ------------------------------------------------------------
"%ROOT%src\LogScope.Tests\bin\%CONFIG%\LogScope.Tests.exe"
if errorlevel 1 (
  echo.
  echo   [오류] 테스트가 실패했습니다.
  exit /b 1
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
echo   완료.  out\LogScope.exe
echo   out 폴더를 통째로 복사해서 쓰면 됩니다. 설치 프로그램은 필요 없습니다.
echo.
endlocal
exit /b 0
