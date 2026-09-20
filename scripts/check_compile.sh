#!/usr/bin/env sh
# =====================================================================
#  진짜로 컴파일해 보는 검사.
#
#  윈도우 밖(개발을 맡은 Claude 가 도는 리눅스 컨테이너)에서도 C# 을
#  실제로 컴파일해서, "빌드는 해 봤나" 를 눈이 아니라 컴파일러가 답하게
#  합니다. 이게 없던 동안 컴파일 오류를 세 번 올렸습니다.
#
#  하는 일
#    1. LogScope.Core 컴파일
#    2. LogScope.Tests 컴파일 + <b>실제 실행</b>
#    3. LogScope.App 컴파일 (WPF 가 없으므로 scripts/wpfstub 으로 대신)
#
#  필요한 것: mono-mcs (apt-get install -y mono-mcs mono-runtime
#             libmono-system-io-compression4.0-cil
#             libmono-system-io-compression-filesystem4.0-cil)
#
#  <b>윈도우에서 진짜 빌드하는 것을 대신하지 못합니다.</b> XAML 컴파일과
#  WPF 진짜 타입은 여기서 확인할 수 없습니다. build.bat 이 여전히 정답입니다.
# =====================================================================
set -e
ROOT=$(cd "$(dirname "$0")/.." && pwd)
OUT=${TMPDIR:-/tmp}/logscope-check
mkdir -p "$OUT/app"

if ! command -v mcs >/dev/null 2>&1; then
  echo "mcs 가 없습니다. 이 검사는 건너뜁니다."
  echo "  apt-get install -y mono-mcs mono-runtime \\"
  echo "    libmono-system-io-compression4.0-cil \\"
  echo "    libmono-system-io-compression-filesystem4.0-cil"
  exit 0
fi

REFS="-r:System.dll -r:System.Core.dll -r:System.Xml.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll"

# BuildInfo.g.cs 는 빌드할 때 만들어지는 파일이라 여기서 흉내 냅니다.
cat > "$OUT/BuildInfo.g.cs" <<'CS'
namespace LogScope.Core
{
    public static class BuildInfo
    {
        public const string Version = "check";
        public const string Commit = "check";
        public const bool  Modified = false;
        public const string BuiltAt = "check";
        public const string Product = "LogScope";
    }
}
CS

echo "[1/4] LogScope.Core"
mcs -langversion:latest -target:library -out:"$OUT/LogScope.Core.dll" $REFS \
    $(find "$ROOT/src/LogScope.Core" -name '*.cs' ! -name '*.g.cs') "$OUT/BuildInfo.g.cs"

echo "[2/4] LogScope.Tests"
mcs -langversion:latest -target:exe -out:"$OUT/LogScope.Tests.exe" $REFS \
    -r:"$OUT/LogScope.Core.dll" $(find "$ROOT/src/LogScope.Tests" -name '*.cs')

echo "[3/4] 테스트 실행"
if command -v mono >/dev/null 2>&1; then
  mono "$OUT/LogScope.Tests.exe"
else
  echo "  mono 런타임이 없어 실행은 건너뜁니다."
fi

echo "[4/4] LogScope.App (WPF 스텁으로)"
python3 "$ROOT/scripts/make_xaml_stubs.py" "$OUT/app/XamlStubs.cs"
mcs -langversion:latest -target:library -out:"$OUT/app/WpfStub.dll" \
    -r:System.dll -r:System.Core.dll -r:System.Xml.dll -nowarn:0067 \
    "$ROOT"/scripts/wpfstub/*.cs
mcs -langversion:latest -target:library -out:"$OUT/app/LogScope.App.dll" \
    -r:System.dll -r:System.Core.dll -r:System.Xml.dll \
    -r:"$OUT/LogScope.Core.dll" -r:"$OUT/app/WpfStub.dll" \
    -nowarn:0067,0414,0169,0649 \
    $(find "$ROOT/src/LogScope.App" -name '*.cs') "$OUT/app/XamlStubs.cs"

echo
echo "모두 컴파일됐습니다."
