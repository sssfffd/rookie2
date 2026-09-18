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
                Raise("LaneMode"); Raise("OverlayMode"); Raise("ScaleHint"); Raise("ScaleHintDetail");
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
                Raise("LaneMode"); Raise("OverlayMode"); Raise("ScaleHint"); Raise("ScaleHintDetail");
                Changed();
            }
        }

        // ---------------- 세로 눈금 ----------------

        private void SetScale(string mode)
        {
            if (S.ValueScaleMode == mode) return;
            S.ValueScaleMode = mode;
            Raise("ScaleRaw"); Raise("ScaleNormalized"); Raise("ScaleDelta");
            Raise("ScaleHint"); Raise("ScaleHintDetail");
            Changed();
        }

        /// <summary>
        /// 세로 눈금 모드가 지금 그림을 실제로 바꾸는지 알려 주는 짧은 글.
        ///
        /// <b>레인 보기에서는 세 모드가 똑같은 그림을 그립니다.</b> 우연이
        /// 아니라 계산상 반드시 그렇습니다. 레인은 그 채널의 값 범위에 딱
        /// 맞춰 그려지는데,
        ///
        ///   정규화  값에서 (v − 최소) / (최대 − 최소) 를 하고
        ///           눈금도 0~1 로 바꿈  → 같은 자리에 같은 모양
        ///   변화량  값에서 첫 값을 빼고
        ///           눈금도 그만큼 내림  → 같은 자리에 같은 모양
        ///
        /// 즉 값과 눈금에 똑같은 변환을 걸어서 화면 좌표가 하나도 안 바뀝니다.
        /// 바뀌는 것은 <b>눈금에 적히는 숫자</b>뿐입니다. 그것도 쓸모가 있어서
        /// (6466.3 대신 +0.3 을 읽는 편이 낫습니다) 남겨 두지만, "그래프가
        /// 안 변한다" 는 것을 화면에서 말해 주지 않으면 고장으로 보입니다.
        ///
        /// 겹쳐보기에서는 여러 채널이 눈금 하나를 같이 써서, 채널마다 다른
        /// 변환이 걸리므로 그림이 실제로 달라집니다.
        ///
        /// <b>글을 짧게 두는 이유</b>가 있습니다. 이 글은 도구 줄 안에 있고,
        /// 도구 줄은 좁아지면 저절로 줄을 바꾸는 WrapPanel 입니다. 긴 문장이
        /// 나타났다 사라졌다 하면 그때마다 도구 줄 전체가 다시 접혀서, 단추를
        /// 누를 때마다 줄 수가 바뀝니다. 그래서 짧게 적고, 긴 설명은
        /// ScaleHintDetail 로 넘겨 풍선 도움말에만 띄웁니다.
        /// 자리도 XAML 에서 고정 너비로 잡아 두어, 글이 없을 때도 폭이
        /// 그대로라 도구 줄이 움직이지 않습니다.
        /// </summary>
        public string ScaleHint
        {
            get
            {
                if (ScaleRaw) return string.Empty;
                return S.LaneMode ? "레인에선 눈금 숫자만 바뀜" : "채널마다 따로 변환됨";
            }
        }

        /// <summary>풍선 도움말에 띄울 긴 설명. ScaleHint 의 자세한 판입니다.</summary>
        public string ScaleHintDetail
        {
            get
            {
                if (ScaleRaw) return null;
                return S.LaneMode
                    ? "레인 보기에서는 세 모드가 같은 그림을 그립니다.\n"
                      + "값과 눈금에 똑같은 변환을 걸기 때문에 화면 좌표가 바뀌지 않고,\n"
                      + "눈금에 적히는 숫자만 바뀝니다 (6466.3 대신 +0.3).\n"
                      + "그림이 달라지는 것은 겹쳐보기입니다."
                    : "겹쳐보기라 채널마다 따로 변환됩니다.\n"
                      + "여러 채널이 눈금 하나를 같이 쓰므로,\n"
                      + "값 크기나 값 위치가 다른 채널끼리 모양을 견줄 수 있습니다.";
            }
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
        /// ScaleHint 와 같은 이유로 짧게 둡니다 — 길이가 들쭉날쭉하면 도구 줄이
        /// 다시 접혀서, 숫자를 고칠 때마다 단추들이 아래로 내려갑니다.
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
                string s = "채널 값 크기의 "
                         + S.RelativeTolerancePercent.ToString("0.####", CultureInfo.InvariantCulture)
                         + "% 까지는 같은 것으로 봅니다.";
                if (S.AbsoluteTolerance > 0)
                    s += "\n절대 오차 "
                       + S.AbsoluteTolerance.ToString("0.######", CultureInfo.InvariantCulture)
                       + " 과 비교해 큰 쪽이 기준이 됩니다.";
                s += "\n\n무엇의 몇 % 인지는 채널마다 다릅니다 —"
                   + "\n값이 오르내린 폭과 값의 크기 중 큰 쪽입니다."
                   + "\n실제로 쓰인 기준값은 대시보드의 기준값 칸에서 볼 수 있습니다.";
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
            Raise("ScaleHint"); Raise("ScaleHintDetail"); Raise("FitVisible"); Raise("ShadeDifference"); Raise("SeparateTraces");
            Raise("RelativeTolerancePercentText"); Raise("ToleranceHint"); Raise("ToleranceHintDetail");
        }
    }
}
