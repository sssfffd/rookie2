using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Core.Model;
using LogScope.Logs.Render;
using LogScope.Core.Settings;

using LogScope.App.Common;
using LogScope.App.Settings;
namespace LogScope.App.Logs
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
        /// 이전 로그와 이후 로그를 각각 그립니다.
        ///
        /// 예전에는 여기에 "0–1 정규화" 가 하나 더 있었습니다. 없앴습니다 —
        /// 레인 보기에서는 값과 눈금에 똑같은 변환이 걸려 그림이 하나도
        /// 안 바뀌었고, 겹쳐보기에서도 "이 채널의 최소~최대 안에서 몇 %" 라는
        /// 숫자는 로그를 읽을 때 쓸 데가 없었습니다.
        /// </summary>
        public bool ScaleRaw
        {
            get { return S.ValueScaleMode != AppSettings.ScaleDelta; }
            set { if (value) SetScale(AppSettings.ScaleRaw); }
        }

        /// <summary>
        /// 차이 (이후−이전) — <b>두 로그의 차이</b>를 선 하나로 그립니다.
        ///
        ///   차이 = 이후 로그의 값 − 이전 로그의 값
        ///
        ///   같음        →   0
        ///   이후가 1 큼 →  +1
        ///   이후가 1 작음 → -1
        ///
        /// 이전과 이후를 따로 그려 놓고 눈으로 견주는 대신 <b>벌어진 만큼만</b>
        /// 봅니다. 6466.3 과 6466.6 은 값 눈금에서 겹쳐 보이지만 여기서는
        /// +0.3 으로 또렷이 섭니다.
        ///
        /// 한쪽 로그에만 있는 IO 는 견줄 상대가 없어 비워 둡니다.
        /// 상태(문자열) 채널은 이름이 같으면 0, 다르면 1 입니다 — 상태 번호는
        /// 로그마다 따로 매겨져서 빼도 뜻이 없습니다.
        /// </summary>
        public bool ScaleDelta
        {
            get { return S.ValueScaleMode == AppSettings.ScaleDelta; }
            set { if (value) SetScale(AppSettings.ScaleDelta); }
        }

        public bool FitVisible
        {
            get { return S.FitVisible; }
            set { if (S.FitVisible == value) return; S.FitVisible = value; Raise(); Changed(); }
        }

        /// <summary>
        /// 차이 영역 표시 — 허용 오차를 넘은 구간을 칠합니다.
        ///
        /// <b>파형 분리 보기와 같이 켜지지 않습니다.</b> 벌려 놓고 그 사이를
        /// 칠하면 칠해진 넓이가 "값 차이" 가 아니라 "값 차이 + 벌린 간격" 이
        /// 되어, 눈으로 재는 넓이가 눈금과 안 맞게 됩니다. 둘 다 끄는 것은
        /// 됩니다 — 켜져 있는 것을 다시 누르면 꺼집니다.
        /// </summary>
        /// <summary>
        /// 이전 선만 / 이후 선만 보기.
        ///
        /// 두 선이 거의 같은 자리에 겹쳐 있으면 뒤에 그려진 쪽이 앞의 쪽을
        /// 덮습니다. 한쪽을 끄면 가려졌던 선이 그대로 보입니다.
        ///
        /// <b>둘 다 끌 수 있습니다.</b> 그때는 그래프에 그 사실을 적습니다 —
        /// 끄지 못하게 막으면 왜 안 꺼지는지 알 수 없고, 말없이 비워 두면
        /// 고장으로 보입니다.
        ///
        /// 차이 눈금에서는 뜻이 없습니다 (선이 하나뿐이고 두 로그가 다 필요한
        /// 값입니다). 그 모드에서는 단추가 꺼져 보입니다 — ScaleRaw 를 봅니다.
        /// </summary>
        public bool ShowBefore
        {
            get { return S.ShowBefore; }
            set
            {
                if (S.ShowBefore == value) return;
                S.ShowBefore = value;
                Raise();
                Changed();
            }
        }

        public bool ShowAfter
        {
            get { return S.ShowAfter; }
            set
            {
                if (S.ShowAfter == value) return;
                S.ShowAfter = value;
                Raise();
                Changed();
            }
        }

        public bool ShadeDifference
        {
            get { return S.ShadeDifference; }
            set
            {
                if (S.ShadeDifference == value) return;
                S.ShadeDifference = value;
                if (value && S.SeparateTraces) { S.SeparateTraces = false; Raise("SeparateTraces"); }
                Raise();
                Changed();
            }
        }

        /// <summary>
        /// 파형 분리 보기 — 이전과 이후를 위아래로 조금 벌려 그립니다.
        /// 두 선이 겹쳐 하나로 보일 때 씁니다.
        /// 차이 영역 표시와 같이 켜지지 않습니다 (위 설명 참고).
        /// </summary>
        public bool SeparateTraces
        {
            get { return S.SeparateTraces; }
            set
            {
                if (S.SeparateTraces == value) return;
                S.SeparateTraces = value;
                if (value && S.ShadeDifference) { S.ShadeDifference = false; Raise("ShadeDifference"); }
                Raise();
                Changed();
            }
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

        /// <summary>
        /// 그룹이 groups.txt 로 정해져 있으면 그렇다고 적습니다. 아니면 빈 글자.
        ///
        /// <b>말없이 되돌아가면 안 됩니다.</b> 여기서 그룹을 고치면 그 자리에서는
        /// 바뀌지만 다시 켤 때 파일 쪽으로 돌아갑니다. 그걸 적어 두지 않으면
        /// 고친 사람은 저장이 고장 난 줄 압니다.
        /// </summary>
        public string GroupSourceNote
        {
            get
            {
                if (!GroupsFromFile) return string.Empty;
                return "그룹은 groups.txt 가 정합니다. 여기서 고친 것은 다시 켜면 "
                     + "그 파일 쪽으로 돌아갑니다 — 그대로 두려면 파일을 고치세요. "
                     + "[groups.txt 글 복사] 로 지금 묶은 것을 그 꼴로 뽑을 수 있습니다.";
            }
        }

        /// <summary>창이 켤 때 알려 줍니다 (Core 는 어느 파일이 이겼는지 모릅니다).</summary>
        public bool GroupsFromFile { get; set; }

        /// <summary>지금 그룹을 groups.txt 꼴로. 저장소의 파일에 붙여넣는 데 씁니다.</summary>
        public string GroupFileText()
        {
            return GroupFile.Write(_state.Settings.Groups);
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
