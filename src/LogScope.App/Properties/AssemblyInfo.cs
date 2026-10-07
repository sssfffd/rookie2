using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using LogScope.Core.Settings;

// 여기는 어셈블리의 이름입니다. 화면에 보이는 이름이 아니라 파일 속성
// 창에 적히는 값이라, 실행할 때 바뀌는 config.txt 를 따라갈 수 없습니다
// (어트리뷰트 인자는 컴파일할 때 값이 박혀야 합니다).
//
// 화면에 보이는 이름은 config.txt 의 name 줄입니다. 실행 파일 이름도 그걸
// 따라갑니다 — build.bat 이 복사할 때 붙여 줍니다.
// 이름 글자를 여기 또 적지 않습니다 — AppConfig 의 기본값을 그대로
// 씁니다. const 라서 어트리뷰트 인자로 쓸 수 있습니다.
[assembly: AssemblyTitle(AppConfig.DefaultName)]
[assembly: AssemblyDescription(AppConfig.DefaultSubtitle)]
[assembly: AssemblyProduct(AppConfig.DefaultName)]
[assembly: ComVisible(false)]
[assembly: Guid("1a6d0b2c-5e44-4c21-9d1e-0b7a31c6e002")]

// 테마 사전을 코드에서 직접 합치므로 ResourceDictionaryLocation.None 입니다.
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.None)]
