using System;
using LogScope.Analysis2;
using LogScope.Settings;
using LogScope.Shell;

namespace LogScope.Common
{
    /// <summary>
    /// <b>Visual Studio 디자이너에서 화면을 볼 때만</b> 쓰는 견본 (1.22).
    ///
    /// 각 화면 XAML 이 <c>d:DataContext="{x:Static common:DesignData.Graph}"</c>
    /// 처럼 이걸 가리킵니다. <c>d:</c> 는 디자이너만 읽는 자리라 프로그램을
    /// 실제로 돌릴 때는 아무 일도 안 합니다.
    ///
    /// 로그는 비어 있는 채입니다 — 디자이너는 단추 · 칸 · 글자의 <b>자리</b>를
    /// 보는 곳이라 그 정도면 됩니다. 만들다가 무엇이 터지면 null 을 돌려줘서
    /// 디자이너가 그냥 빈 화면을 보이게 합니다 (디자이너 오류 창 대신).
    /// </summary>
    public static class DesignData
    {
        private static ShellVm _shell;
        private static bool _tried;

        private static ShellVm ShellVm
        {
            get
            {
                if (_tried) return _shell;
                _tried = true;
                try { _shell = new ShellVm(new AppState()); }
                catch (Exception) { _shell = null; }
                return _shell;
            }
        }

        public static object Shell { get { return ShellVm; } }
        public static object Main { get { return ShellVm != null ? ShellVm.Main : null; } }
        public static object Dashboard { get { return ShellVm != null ? ShellVm.Dashboard : null; } }
        public static object Graph { get { return ShellVm != null ? ShellVm.Graph : null; } }
        public static object Heatmap { get { return ShellVm != null ? ShellVm.Heatmap : null; } }
        public static object Align { get { return ShellVm != null ? ShellVm.Align : null; } }

        public static object Db
        {
            get { try { return new DbVm(); } catch (Exception) { return null; } }
        }

        public static object Settings
        {
            get { try { return new SettingsVm(new AppSettings()); } catch (Exception) { return null; } }
        }
    }
}
