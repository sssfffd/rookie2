using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 가로축과 시간 맞추기. 히트맵 화면과 <b>같은 것</b>을 씁니다.
        /// </summary>
        public AlignVm Align { get; private set; }

        /// <summary>도구 줄에서 뭔가 바뀌면 불립니다.</summary>
        public event EventHandler OptionsChanged;

        public GraphVm(AppState state, AlignVm align)
        {
            _state = state;
            Align = align;
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
            Raise("ScaleRaw"); Raise("ScaleDelta");
            Changed();
        }

        /// <summary>
        /// 로그에 적힌 값 그대로 그립니다.
        ///
        /// 예전에는 여기에 "0–1 정규화" 가 하나 더 있었습니다. 없앴습니다 —
        /// 레인 보기에서는 값과 눈금에 똑같은 변환이 걸려 그림이 하나도
        /// 안 바뀌었고, 겹쳐보기에서도 "이 채널의 최소~최대 안에서 몇 %" 라는
        /// 숫자는 로그를 읽을 때 쓸 데가 없었습니다.
        /// </summary>
        public bool ScaleRaw
        {
            get { return S.ValueScaleMode != "delta"; }
            set { if (value) SetScale("raw"); }
        }

        /// <summary>
        /// 변화량 (차분) — <b>이웃한 두 표본의 차이</b>를 그립니다.
        ///
        ///   안 바뀜   →   0
        ///   1 오름    →  +1
        ///   1 내림    →  -1
        ///
        /// 값이 얼마인지가 아니라 <b>언제 얼마나 움직였는지</b>를 봅니다.
        /// 6466.3 에서 6466.6 으로 가는 변화는 값 눈금으로는 직선이나 다름없지만
        /// 여기서는 +0.3 짜리 막대로 또렷하게 섭니다.
        ///
        /// 첫 표본과, 값이 빈 구간 뒤의 첫 표본은 0 입니다 — 직전 값을 모르는
        /// 자리라 없는 변화를 지어내지 않습니다.
        /// </summary>
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
                Raise("ToleranceHint"); Raise("ToleranceHintDetail");
                Changed();
            }
        }

        /// <summary>절대 오차. 설정 창에서만 바꿉니다.</summary>
        public double AbsoluteTolerance { get { return S.AbsoluteTolerance; } }

        /// <summary>도구 줄 옆에 붙는 설명. 절대 오차도 걸려 있으면 같이 알려 줍니다.</summary>
        /// <summary>
        /// 도구 줄에 적는 짧은 안내.
        ///
        /// 짧게 둡니다 — 길이가 들쭉날쭉하면 도구 줄이 다시 접혀서, 숫자를
        /// 고칠 때마다 단추들이 아래로 내려갑니다.
        /// 절대 오차까지 걸려 있는지는 여기서 한 글자로만 알리고, 자세한 것은
        /// ToleranceHintDetail 이 풍선 도움말로 보여 줍니다.
        /// </summary>
        public string ToleranceHint
        {
            get
            {
                // 두 글 모두 XAML 에 잡아 둔 고정 폭 안에 들어가야 합니다.
                // 넘치면 끝이 "…" 로 잘립니다.
                return S.AbsoluteTolerance > 0
                    ? "까지는 같은 값 + 절대 오차"
                    : "까지는 같은 값";
            }
        }

        /// <summary>풍선 도움말에 띄울 긴 설명.</summary>
        public string ToleranceHintDetail
        {
            get
            {
                string s = "이전 값 대비 "
                         + S.RelativeTolerancePercent.ToString("0.####", CultureInfo.InvariantCulture)
                         + "% 까지는 같은 것으로 봅니다.";
                if (S.AbsoluteTolerance > 0)
                    s += "\n절대 오차 "
                       + S.AbsoluteTolerance.ToString("0.######", CultureInfo.InvariantCulture)
                       + " 과 비교해 큰 쪽이 기준이 됩니다.";
                s += "\n\n오차 = |이후 − 이전| / |이전| x 100"
                   + "\n어느 채널이든 같은 잣대입니다."
                   + "\n화면에 적히는 퍼센트가 바로 이 값이라 그대로 견주면 됩니다.";
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
            Raise("ScaleRaw"); Raise("ScaleDelta"); Raise("FitVisible"); Raise("ShadeDifference"); Raise("SeparateTraces");
            Raise("RelativeTolerancePercentText"); Raise("ToleranceHint"); Raise("ToleranceHintDetail");
        }
    }
}
