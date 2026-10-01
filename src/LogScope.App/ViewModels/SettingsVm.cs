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

        public SettingsVm(AppSettings settings) : this(settings, null) { }

        /// <param name="ioNames">
        /// 열어 둔 로그의 IO 이름. IO 별 허용 오차를 걸 때 고르라고 내어
        /// 줍니다. 없으면(로그를 안 열었으면) 손으로 적으면 됩니다.
        /// </param>
        public SettingsVm(AppSettings settings, IEnumerable<string> ioNames)
        {
            _s = settings;
            IoNames = new ObservableCollection<string>();
            if (ioNames != null) foreach (string n in ioNames) IoNames.Add(n);

            var rows = new ObservableCollection<ToleranceRowVm>();
            for (int i = 0; i < _s.Tolerances.Count; i++) rows.Add(new ToleranceRowVm(_s.Tolerances[i]));
            Tolerances = rows;
        }

        // 로그 세트(쌍을 여러 개 골라 가며 보기)는 없앴습니다. 설정은 그대로
        // 쓰므로 지금 세트 하나만 다룹니다 — 기본 폴더와 기억된 경로가 거기
        // 들어 있어서, 세트를 지웠다고 그 값을 버리면 안 됩니다.
        private LogSet Current { get { return _s.ActiveLogSet; } }

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

        // ---------------- IO 별 허용 오차 ----------------
        //
        // 기본값 하나로 다 보는 것이 맞지 않는 경우가 있습니다. 온도처럼 원래
        // 조금씩 흔들리는 값과 밸브 열림처럼 조금도 달라지면 안 되는 값에
        // 같은 잣대를 대면, 한쪽은 늘 빨갛고 다른 쪽은 놓칩니다.

        /// <summary>고르라고 내어 주는 IO 이름. 비어 있으면 손으로 적습니다.</summary>
        public ObservableCollection<string> IoNames { get; private set; }

        public ObservableCollection<ToleranceRowVm> Tolerances { get; private set; }

        public bool HasTolerances { get { return Tolerances.Count > 0; } }

        /// <summary>빈 줄을 하나 답니다. IO 이름은 화면에서 고릅니다.</summary>
        public void AddTolerance()
        {
            if (_s.Tolerances.Count >= AppSettings.MaxToleranceRules) return;

            var rule = new ToleranceOverride();
            _s.Tolerances.Add(rule);
            Tolerances.Add(new ToleranceRowVm(rule));
            Raise("HasTolerances");
        }

        public void RemoveTolerance(ToleranceRowVm row)
        {
            if (row == null) return;
            _s.Tolerances.Remove(row.Rule);
            Tolerances.Remove(row);
            Raise("HasTolerances");
        }

        /// <summary>
        /// 창을 닫을 때 <b>쓸모없는 줄을 버립니다.</b> 이름을 안 고르고 닫거나
        /// 두 칸을 다 비워 둔 줄이 남아 있으면, 다음에 열었을 때 왜 있는지
        /// 알 수 없는 빈 줄이 됩니다.
        /// </summary>
        public void PruneTolerances()
        {
            for (int i = _s.Tolerances.Count - 1; i >= 0; i--)
                if (_s.Tolerances[i] == null || _s.Tolerances[i].IsEmpty) _s.Tolerances.RemoveAt(i);

            for (int i = Tolerances.Count - 1; i >= 0; i--)
                if (Tolerances[i].Rule.IsEmpty) Tolerances.RemoveAt(i);

            Raise("HasTolerances");
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
