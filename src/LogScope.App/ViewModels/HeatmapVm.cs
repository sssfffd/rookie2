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
        /// 채널 값 범위에 대한 비율(%). 기본 0.1% 입니다.
        /// IO 범위가 몇천인데 1 정도 흔들리는 것을 오류로 세지 않기 위한 값입니다.
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
            }
        }

        public double RelativeTolerance
        {
            get { return ToleranceRule.FromPercent(_state.Settings.RelativeTolerancePercent); }
        }

        private bool _includeUnchanged;
        public bool IncludeUnchanged
        {
            get { return _includeUnchanged; }
            set { Set(ref _includeUnchanged, value); }
        }

        public HeatmapOptions BuildOptions()
        {
            var o = new HeatmapOptions();
            o.BucketSpan = BucketSpan;
            o.AbsoluteTolerance = _state.Settings.AbsoluteTolerance;
            o.RelativeTolerance = RelativeTolerance;
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
                Summary = "아직 계산하지 않았습니다.";
                return;
            }
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
                    + "        허용 오차 값 범위의 " + RelativeTolerancePercent + "%"
                    + (_state.Settings.AbsoluteTolerance > 0
                        ? " 또는 절대 " + _state.Settings.AbsoluteTolerance.ToString("0.######") + " 중 큰 쪽"
                        : "");
        }
    }
}
