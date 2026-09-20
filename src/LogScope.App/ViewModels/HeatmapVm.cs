using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Compare;
using LogScope.Core.Model;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 히트맵 화면의 설정과 요약.
    /// 실제 계산은 배경 스레드에서 돌아야 해서 화면(HeatmapView)이 맡습니다.
    /// </summary>
    public sealed class HeatmapVm : ObservableObject
    {
        private readonly AppState _state;

        public HeatmapVm(AppState state)
        {
            _state = state;
        }

        // ---------------- 칸 폭 ----------------

        public IEnumerable<string> BucketChoices
        {
            get { return new[] { "자동 (시각이면 1분)", "1분", "5분", "10분", "30분", "1시간" }; }
        }

        private int _bucketIndex;
        public int BucketIndex
        {
            get { return _bucketIndex; }
            set { if (Set(ref _bucketIndex, value)) Raise("BucketHint"); }
        }

        /// <summary>고른 칸 폭을 시간축 단위로. 0 이면 자동.</summary>
        public double BucketSpan
        {
            get
            {
                LogDataset ds = _state.TimeReference;
                bool clock = ds != null && (ds.TimeKind == TimeKind.ClockMs || ds.TimeKind == TimeKind.DateMs);
                if (!clock) return 0;   // 시각 로그가 아니면 "분" 이 뜻이 없습니다.

                switch (_bucketIndex)
                {
                    case 1: return 60000.0;
                    case 2: return 5 * 60000.0;
                    case 3: return 10 * 60000.0;
                    case 4: return 30 * 60000.0;
                    case 5: return 60 * 60000.0;
                    default: return 0;
                }
            }
        }

        public string BucketHint
        {
            get
            {
                LogDataset ds = _state.TimeReference;
                bool clock = ds != null && (ds.TimeKind == TimeKind.ClockMs || ds.TimeKind == TimeKind.DateMs);
                return clock
                    ? "시간축이 시각이라 분 단위로 자를 수 있습니다."
                    : "시간축이 시각이 아니라(샘플 번호 또는 단위 없는 숫자) 전체를 60 칸으로 나눕니다.";
            }
        }

        // ---------------- 허용 오차 ----------------

        /// <summary>
        /// 이전 값 대비 몇 %까지 같은 것으로 볼지. 기본 0.1% 입니다.
        /// 칸에 적히는 퍼센트와 <b>같은 잣대</b>라 눈으로 바로 견줄 수 있습니다.
        ///
        /// 설정에 저장되는 값 하나를 대시보드·그래프와 함께 씁니다.
        /// 여기서 바꾸면 그쪽 "차이 난 IO" 판정도 같이 바뀝니다.
        /// </summary>
        public string RelativeTolerancePercent
        {
            get
            {
                return _state.Settings.RelativeTolerancePercent.ToString(
                    "0.####", CultureInfo.InvariantCulture);
            }
            set
            {
                double v;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                if (v < 0 || v > 100 || _state.Settings.RelativeTolerancePercent == v) return;
                _state.Settings.RelativeTolerancePercent = v;
                Raise();
                Raise("StaleText");
            }
        }

        /// <summary>허용 오차 퍼센트 (숫자). 설정에 적힌 그 값 그대로입니다.</summary>
        public double RelativePercent
        {
            get { return _state.Settings.RelativeTolerancePercent; }
        }

        // ---------------- 값 / 퍼센트 ----------------

        /// <summary>
        /// 칸에 적는 차이를 퍼센트로 볼지, 값 그대로 볼지.
        ///
        /// 라디오 단추 두 개에 묶으려고 짝이 되는 속성을 둘 다 둡니다
        /// (그래프 화면의 세로 눈금 고르기와 같은 방식입니다).
        /// 고른 값은 설정에 남아 다음에 열 때도 그대로입니다.
        /// </summary>
        public bool ShowPercent
        {
            get { return _state.Settings.HeatmapPercent; }
            set
            {
                if (_state.Settings.HeatmapPercent == value) return;
                _state.Settings.HeatmapPercent = value;
                Raise("ShowPercent");
                Raise("ShowValue");
                Raise("LegendText");
            }
        }

        /// <summary>ShowPercent 의 반대. 라디오 단추 "값" 쪽에 묶습니다.</summary>
        public bool ShowValue
        {
            get { return !ShowPercent; }
            set { if (value) ShowPercent = false; }
        }

        /// <summary>색 설명 줄에 적을 글. 무엇이 칸에 적히는지 알려 줍니다.</summary>
        public string LegendText
        {
            get
            {
                return ShowPercent
                    ? "차이 발생 (칸 안의 숫자 = 그 구간에서 가장 크게 벌어진 순간의 오차 %. 허용 오차와 같은 잣대입니다)"
                    : "차이 발생 (칸 안의 숫자 = 그 구간에서 가장 크게 벌어진 순간의 차이. % 로 보면 허용 오차와 바로 견줄 수 있습니다)";
            }
        }

        private bool _includeUnchanged;
        public bool IncludeUnchanged
        {
            get { return _includeUnchanged; }
            set { Set(ref _includeUnchanged, value); }
        }

        // ---------------- 화면에 떠 있는 히트맵이 최신인지 ----------------

        /// <summary>
        /// 지금 그려져 있는 히트맵을 만들 때 쓴 허용 오차. 계산은 무거워서
        /// 값을 고칠 때마다 다시 돌리지 않습니다. 그래서 "위에 적힌 허용 오차" 와
        /// "화면에 그려진 히트맵" 이 잠깐 어긋날 수 있는데, 그걸 말없이 두면
        /// 아래 칸의 퍼센트가 위 설정과 안 맞는 것처럼 보입니다.
        /// 어긋나 있는 동안에는 안내 줄을 띄웁니다.
        /// </summary>
        private double _shownRelative = double.NaN;
        private double _shownAbsolute = double.NaN;

        /// <summary>안내 줄. 최신이면 빈 글자라 줄 자체가 사라집니다.</summary>
        public string StaleText
        {
            get
            {
                if (double.IsNaN(_shownRelative)) return string.Empty;
                if (_shownRelative == _state.Settings.RelativeTolerancePercent
                    && _shownAbsolute == _state.Settings.AbsoluteTolerance) return string.Empty;

                return "허용 오차를 바꿨습니다. 아래 히트맵은 아직 예전 기준("
                     + _shownRelative.ToString("0.####", CultureInfo.InvariantCulture)
                     + "%)으로 그려져 있습니다 — [히트맵 다시 계산] 을 눌러 주세요.";
            }
        }

        public HeatmapOptions BuildOptions()
        {
            var o = new HeatmapOptions();
            o.BucketSpan = BucketSpan;
            o.AbsoluteTolerance = _state.Settings.AbsoluteTolerance;
            o.RelativePercent = RelativePercent;
            o.IncludeUnchanged = _includeUnchanged;
            o.Shift = _state.AppliedShift;
            return o;
        }

        // ---------------- 요약과 안내 ----------------

        private string _summary = "이전 로그와 이후 로그를 연 다음 [히트맵 다시 계산] 을 눌러 주세요.";
        public string Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        private string _hover = string.Empty;
        public string HoverText
        {
            get { return _hover; }
            set { Set(ref _hover, value); }
        }

        public void Describe(HeatmapResult r)
        {
            if (r == null)
            {
                _shownRelative = double.NaN;
                _shownAbsolute = double.NaN;
                Raise("StaleText");
                Summary = "아직 계산하지 않았습니다.";
                return;
            }

            // 이 결과를 만든 기준을 적어 둡니다. 이후에 설정이 바뀌면
            // StaleText 가 그 사실을 알립니다.
            _shownRelative = _state.Settings.RelativeTolerancePercent;
            _shownAbsolute = _state.Settings.AbsoluteTolerance;
            Raise("StaleText");

            if (r.Warning.Length > 0 && r.Rows.Count == 0)
            {
                Summary = r.Warning;
                return;
            }

            LogDataset ds = r.TimeReference;
            string from = ds != null ? ds.FormatTime(r.Start) : r.Start.ToString("0.###");
            string to = ds != null ? ds.FormatTime(r.End) : r.End.ToString("0.###");

            Summary = "차이가 난 IO " + r.ChangedChannels + "개"
                    + "   /   양쪽에 다 있는 IO " + r.ComparedChannels + "개"
                    + "        칸 폭 " + r.BucketLabel
                    + "   /   칸 수 " + r.BucketCount
                    + "        구간 " + from + " ~ " + to
                    + "        허용 오차 " + RelativeTolerancePercent + "% (이전 값 대비)"
                    + (_state.Settings.AbsoluteTolerance > 0
                        ? " 또는 절대 " + _state.Settings.AbsoluteTolerance.ToString("0.######") + " 중 큰 쪽"
                        : "");
        }
    }
}
