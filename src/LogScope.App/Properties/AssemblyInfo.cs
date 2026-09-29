using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

// 이름을 여기에 또 적지 않습니다. BuildInfo.Product 는 const 이고,
// const 는 컴파일할 때 값이 박히므로 어트리뷰트 인자로 그대로 쓸 수 있습니다.
// 값은 저장소 루트의 appname.txt 에서 옵니다.
[assembly: AssemblyTitle(LogScope.Core.BuildInfo.Product)]
[assembly: AssemblyDescription("IO 로그 그래프 뷰어")]
[assembly: AssemblyProduct(LogScope.Core.BuildInfo.Product)]
[assembly: ComVisible(false)]
[assembly: Guid("1a6d0b2c-5e44-4c21-9d1e-0b7a31c6e002")]

// 테마 사전을 코드에서 직접 합치므로 ResourceDictionaryLocation.None 입니다.
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.None)]
