using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.App.Themes;
using LogScope.Core;
using LogScope.Core.Compare;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 설정 창. 로그 세트 6 개, 세트마다의 기본 폴더, 테마, 비교 기준,
    /// 그리고 프로그램 버전과 커밋 해시를 봅니다.
    /// </summary>
    public sealed class SettingsVm : ObservableObject
    {
        private readonly AppSettings _s;

        public SettingsVm(AppSettings settings)
        {
            _s = settings;
            var names = new ObservableCollection<string>();
            for (int i = 0; i < AppSettings.SetCount; i++) names.Add(_s.Sets[i].DisplayName(i));
            SetNames = names;
            _selectedSet = _s.ActiveSet;
        }

        public ObservableCollection<string> SetNames { get; private set; }

        private int _selectedSet;
        public int SelectedSet
        {
            get { return _selectedSet; }
            set
            {
                if (value < 0 || value >= AppSettings.SetCount) return;
                if (!Set(ref _selectedSet, value)) return;
                RaiseSetDetail();
            }
        }

        private LogSet Current { get { return _s.Sets[_selectedSet]; } }

        private void RaiseSetDetail()
        {
            Raise("SetTitle");
            Raise("BeforeFolder");
            Raise("AfterFolder");
            Raise("BeforeFile");
            Raise("AfterFile");
        }

        private void RefreshSetName()
        {
            SetNames[_selectedSet] = Current.DisplayName(_selectedSet);
        }

        public string SetTitle
        {
            get { return Current.Title ?? string.Empty; }
            set { Current.Title = value ?? string.Empty; Raise(); RefreshSetName(); }
        }

        public string BeforeFolder
        {
            get { return Current.BeforeFolder ?? string.Empty; }
            set { Current.BeforeFolder = value ?? string.Empty; Raise(); }
        }

        public string AfterFolder
        {
            get { return Current.AfterFolder ?? string.Empty; }
            set { Current.AfterFolder = value ?? string.Empty; Raise(); }
        }

        public string BeforeFile
        {
            get { return string.IsNullOrEmpty(Current.BeforePath) ? "(없음)" : Current.BeforePath; }
        }

        public string AfterFile
        {
            get { return string.IsNullOrEmpty(Current.AfterPath) ? "(없음)" : Current.AfterPath; }
        }

        public void ForgetFiles()
        {
            Current.BeforePath = string.Empty;
            Current.AfterPath = string.Empty;
            Raise("BeforeFile");
            Raise("AfterFile");
        }

        // ---------------- 표시 ----------------

        public IEnumerable<string> ThemeNames
        {
            get { return new[] { "밝은 테마 (흰 바탕)", "어두운 테마" }; }
        }

        public int ThemeIndex
        {
            get { return string.Equals(_s.Theme, "dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0; }
            set
            {
                string name = value == 1 ? "dark" : "light";
                if (_s.Theme == name) return;
                _s.Theme = name;
                // WPF 는 자원 사전만 바꿔 끼우면 바로 반영됩니다.
                ThemeManager.Apply(name);
                Raise();
            }
        }

        // ---------------- 비교 ----------------

        /// <summary>허용 오차 — 채널 값 범위에 대한 비율(%). 기본 0.1%.</summary>
        public string RelativeTolerancePercentText
        {
            get { return _s.RelativeTolerancePercent.ToString("0.####", CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                if (v < 0 || v > 100) return;
                _s.RelativeTolerancePercent = v;
                Raise();
            }
        }

        /// <summary>허용 오차 — 값 그대로. 기본 0 (끔). 비율과 비교해 큰 쪽이 기준.</summary>
        public string AbsoluteToleranceText
        {
            get { return _s.AbsoluteTolerance.ToString("0.######", CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                if (v < 0) return;
                _s.AbsoluteTolerance = v;
                Raise();
            }
        }

        public void ResetTolerance()
        {
            _s.RelativeTolerancePercent = ToleranceRule.DefaultPercent;
            _s.AbsoluteTolerance = 0;
            Raise("RelativeTolerancePercentText");
            Raise("AbsoluteToleranceText");
        }

        public IEnumerable<string> Metrics { get { return DashboardVm.MetricNames; } }

        /// <summary>
        /// 콤보의 몇 번째인지. 차이량 enum 값을 그대로 쓰지 않습니다 —
        /// 화면에 안 내놓는 차이량(차이 면적)이 있어서 번호가 어긋납니다.
        /// </summary>
        public int SortMetricIndex
        {
            get
            {
                int i = Array.IndexOf(DashboardVm.ShownMetrics, _s.SortMetric);
                return i < 0 ? 0 : i;
            }
            set
            {
                if (value < 0 || value >= DashboardVm.ShownMetrics.Length) return;
                _s.SortMetric = DashboardVm.ShownMetrics[value];
                Raise();
            }
        }

        public bool AutoAlign
        {
            get { return _s.AutoAlign; }
            set { _s.AutoAlign = value; Raise(); }
        }

        public string ManualShiftText
        {
            get { return _s.ManualShift.ToString("0.######", CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                _s.ManualShift = v;
                Raise();
            }
        }

        // ---------------- AI 모듈 (선택) ----------------

        public bool AiEnabled
        {
            get { return _s.AiEnabled; }
            set { _s.AiEnabled = value; Raise(); }
        }

        public string PythonPath
        {
            get { return _s.PythonPath ?? string.Empty; }
            set { _s.PythonPath = value ?? string.Empty; Raise(); }
        }

        public string AiScriptPath
        {
            get { return _s.AiScriptPath ?? string.Empty; }
            set { _s.AiScriptPath = value ?? string.Empty; Raise(); }
        }

        // ---------------- 정보 ----------------

        public string VersionText
        {
            get
            {
                return "버전        " + BuildInfo.Version
                     + (BuildInfo.Modified ? "   (빌드 당시 소스가 수정된 상태였습니다)" : "") + "\n"
                     + "커밋        " + BuildInfo.Commit + "\n"
                     + "빌드 시각   " + BuildInfo.BuiltAt + "\n"
                     + ".NET        " + Environment.Version + "\n"
                     + "실행 방식   " + (Environment.Is64BitProcess ? "64비트" : "32비트") + "\n"
                     + "운영체제    " + Environment.OSVersion;
            }
        }

        public string SettingsPath { get { return SettingsStore.ResolvePath(); } }
    }
}
