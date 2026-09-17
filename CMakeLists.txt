# ---------------------------------------------------------------------------
#  LogScope — CMake 진입점
#
#  C# 프로젝트는 .csproj 로 두고, CMake 는 그것들을 묶은 Visual Studio
#  솔루션을 만들어 줍니다. 이렇게 한 이유:
#
#   - CMake 의 C# 지원(`enable_language(CSharp)`)은 Visual Studio 생성기에서만
#     돌아가고, 파일 목록/참조를 CMake 문법으로 다시 적어야 합니다. 손으로 쓴
#     .csproj 를 그대로 두면 VS2017 에서 바로 열 수 있고, CMake 가 없는 PC 에서도
#     build.bat 하나로 빌드됩니다.
#   - CMake 를 쓰고 싶을 때는 여기서 솔루션을 만들어 그대로 빌드하면 됩니다.
#
#  쓰는 법 (Windows, Visual Studio 2017):
#      cmake -S . -B build -G "Visual Studio 15 2017" -A x64
#      cmake --build build --config Release
#
#  CMake 없이:
#      build.bat
# ---------------------------------------------------------------------------
cmake_minimum_required(VERSION 3.8)
project(LogScope NONE)

file(READ "${CMAKE_CURRENT_SOURCE_DIR}/version.txt" LOGSCOPE_VERSION_RAW)
string(STRIP "${LOGSCOPE_VERSION_RAW}" LOGSCOPE_VERSION)
message(STATUS "LogScope 버전 ${LOGSCOPE_VERSION} (버전은 version.txt 한 곳에만 있습니다)")

if(NOT CMAKE_GENERATOR MATCHES "Visual Studio")
  message(WARNING
    "이 프로젝트는 Visual Studio 생성기에서만 솔루션을 만듭니다.\n"
    "지금 생성기: ${CMAKE_GENERATOR}\n"
    "Windows 에서 다음처럼 불러 주세요:\n"
    "  cmake -S . -B build -G \"Visual Studio 15 2017\" -A x64")
  return()
endif()

include_external_msproject(LogScope.Core
  "${CMAKE_CURRENT_SOURCE_DIR}/src/LogScope.Core/LogScope.Core.csproj"
  TYPE FAE04EC0-301F-11D3-BF4B-00C04F79EFBC
  GUID 1A6D0B2C-5E44-4C21-9D1E-0B7A31C6E001)

include_external_msproject(LogScope.App
  "${CMAKE_CURRENT_SOURCE_DIR}/src/LogScope.App/LogScope.App.csproj"
  TYPE FAE04EC0-301F-11D3-BF4B-00C04F79EFBC
  GUID 1A6D0B2C-5E44-4C21-9D1E-0B7A31C6E002
  PLATFORM "Any CPU"
  LogScope.Core)

include_external_msproject(LogScope.Tests
  "${CMAKE_CURRENT_SOURCE_DIR}/src/LogScope.Tests/LogScope.Tests.csproj"
  TYPE FAE04EC0-301F-11D3-BF4B-00C04F79EFBC
  GUID 1A6D0B2C-5E44-4C21-9D1E-0B7A31C6E003
  PLATFORM "Any CPU"
  LogScope.Core)

enable_testing()
add_test(NAME logscope-self-test
  COMMAND "${CMAKE_CURRENT_SOURCE_DIR}/src/LogScope.Tests/bin/$<CONFIG>/LogScope.Tests.exe")
