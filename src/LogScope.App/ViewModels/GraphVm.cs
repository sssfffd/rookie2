using System;
using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 그래프 화면의 상태. 왼쪽 목록과 위쪽 도구 줄의 값들을 들고 있습니다.
    /// 여기서 바뀐 값은 OptionsChanged 로 알리고, 화면이 그것을 그래프 판에
    /// 옮겨 담습니다.
    /// </summary>
    public sealed class GraphVm : ObservableObject
    {
        private readonly AppState _state;

        public ChannelListVm List { get; private set; }

        /// <summary>도구 줄에서 뭔가 바뀌면 불립니다.</summary>
        public event EventHandler OptionsChanged;

        public GraphVm(AppState state)
        {
            _state = state;
            List = new ChannelListVm(state);
        }

        private AppSettings S { get { return _state.Settings; } }

        private void Changed()
        {
            EventHandler h = OptionsChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---------------- 보기 방식 ----------------

        public bool LaneMode
        {
            get { return S.LaneMode; }
            set
            {
                if (!value || S.LaneMode == value) return;   // 라디오는 켤 때만 반응
                S.LaneMode = true;
                Raise("LaneMode"); Raise("OverlayMode");
                Changed();
            }
        }

        public bool OverlayMode
        {
            get { return !S.LaneMode; }
            set
            {
                if (!value || !S.LaneMode) return;
                S.LaneMode = false;
                Raise("LaneMode"); Raise("OverlayMode");
                Changed();
            }
        }

        // ---------------- 세로 눈금 ----------------

        private void SetScale(string mode)
        {
            if (S.ValueScaleMode == mode) return;
            S.ValueScaleMode = mode;
            Raise("ScaleRaw"); Raise("ScaleNormalized"); Raise("ScaleDelta");
            Changed();
        }

        public bool ScaleRaw
        {
            get { return S.ValueScaleMode != "normalized" && S.ValueScaleMode != "delta"; }
            set { if (value) SetScale("raw"); }
        }

        public bool ScaleNormalized
        {
            get { return S.ValueScaleMode == "normalized"; }
            set { if (value) SetScale("normalized"); }
        }

        public bool ScaleDelta
        {
            get { return S.ValueScaleMode == "delta"; }
            set { if (value) SetScale("delta"); }
        }

        public bool FitVisible
        {
            get { return S.FitVisible; }
            set { if (S.FitVisible == value) return; S.FitVisible = value; Raise(); Changed(); }
        }

        public bool ShadeDifference
        {
            get { return S.ShadeDifference; }
            set { if (S.ShadeDifference == value) return; S.ShadeDifference = value; Raise(); Changed(); }
        }

        public bool SeparateTraces
        {
            get { return S.SeparateTraces; }
            set { if (S.SeparateTraces == value) return; S.SeparateTraces = value; Raise(); Changed(); }
        }

        /// <summary>
        /// 허용 오차 — 채널 값 범위에 대한 비율(%). 기본 0.1%.
        /// 대시보드·히트맵과 같은 값을 씁니다. 여기서 바꾸면 그쪽도 같이 바뀝니다.
        /// </summary>
        public string RelativeTolerancePercentText
        {
            get { return S.RelativeTolerancePercent.ToString("0.####", CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                if (v < 0 || v > 100 || S.RelativeTolerancePercent == v) return;
                S.RelativeTolerancePercent = v;
                Raise();
                Raise("ToleranceHint");
                Changed();
            }
        }

        /// <summary>절대 오차. 설정 창에서만 바꿉니다.</summary>
        public double AbsoluteTolerance { get { return S.AbsoluteTolerance; } }

        /// <summary>도구 줄 옆에 붙는 설명. 절대 오차도 걸려 있으면 같이 알려 줍니다.</summary>
        public string ToleranceHint
        {
            get
            {
                string s = "값 범위의 " + S.RelativeTolerancePercent.ToString("0.####",
                            CultureInfo.InvariantCulture) + "% 까지는 같은 것으로 봅니다";
                if (S.AbsoluteTolerance > 0)
                    s += " (절대 " + S.AbsoluteTolerance.ToString("0.######",
                          CultureInfo.InvariantCulture) + " 과 비교해 큰 쪽)";
                return s;
            }
        }

        // ---------------- 아래 띠의 글자 ----------------

        private string _readout = string.Empty;
        public string ReadoutText
        {
            get { return _readout; }
            private set { Set(ref _readout, value); }
        }

        public void UpdateReadout(double cursorA, double cursorB, double visibleStart, double visibleEnd)
        {
            LogDataset refDs = _state.TimeReference;

            string a = double.IsNaN(cursorA) ? "없음" : Format(refDs, cursorA);
            string b = double.IsNaN(cursorB) ? "없음" : Format(refDs, cursorB);

            string delta = "-";
            if (!double.IsNaN(cursorA) && !double.IsNaN(cursorB))
            {
                double d = cursorB - cursorA;
                delta = (refDs != null && refDs.TimeKind == TimeKind.ClockMs)
                    ? (d / 1000.0).ToString("0.###") + " 초"
                    : d.ToString("0.####");
            }

            string range = refDs != null
                ? Format(refDs, visibleStart) + "  ~  " + Format(refDs, visibleEnd)
                : "-";

            ReadoutText = "커서 A: " + a + "     커서 B: " + b + "     B − A: " + delta
                        + "     보이는 구간: " + range
                        + "     고른 IO: " + List.SelectedCount + "개";
        }

        private static string Format(LogDataset ds, double t)
        {
            return ds != null ? ds.FormatTime(t) : t.ToString("0.###");
        }

        /// <summary>로그가 바뀌었을 때 목록을 다시 만듭니다.</summary>
        public void Reload()
        {
            List.Rebuild(_state.Before, _state.After, _state.ChangedNames());
            Raise("LaneMode"); Raise("OverlayMode");
            Raise("ScaleRaw"); Raise("ScaleNormalized"); Raise("ScaleDelta");
            Raise("FitVisible"); Raise("ShadeDifference"); Raise("SeparateTraces");
            Raise("RelativeTolerancePercentText"); Raise("ToleranceHint");
        }
    }
}
