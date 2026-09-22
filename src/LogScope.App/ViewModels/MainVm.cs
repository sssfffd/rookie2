using System.Collections.Generic;
using System.IO;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Compare;
using LogScope.Core.Model;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 메인 화면. 지금 열어 둔 로그를 한 줄로 알려 주고, 분석 셋을 칸으로
    /// 늘어놓아 각각의 점수를 보여 줍니다. 칸을 누르면 그 분석 화면으로
    /// 넘어갑니다.
    /// </summary>
    public sealed class MainVm : ObservableObject
    {
        private readonly AppState _state;

        public List<AnalysisCardVm> Cards { get; private set; }

        public MainVm(AppState state)
        {
            _state = state;
            Cards = new List<AnalysisCardVm>
            {
                new AnalysisCardVm(1, "로그 비교",
                    "이전 로그와 이후 로그를 견줍니다. 대시보드 · 그래프 · 히트맵.", true),
                new AnalysisCardVm(2, "분석 2",
                    "아직 정해지지 않았습니다.", false),
                new AnalysisCardVm(3, "분석 3",
                    "아직 정해지지 않았습니다.", false),
            };
            Refresh();
        }

        /// <summary>
        /// 점수와 숫자들을 다시 셉니다. 로그를 열거나 다시 견줄 때마다 불립니다.
        ///
        /// <b>아직 점수를 계산하지는 않습니다.</b> 견주기 전에는 "??" 로 비워
        /// 두고, 견주고 나면 만점으로 둡니다. 무엇을 어떻게 점수로 매길지
        /// 정해지기 전에 아무 수식이나 넣어 두면 그 수가 진짜인 줄 알고 읽게
        /// 됩니다. 나중에 채울 자리는 여기 한 곳입니다.
        /// </summary>
        public void Refresh()
        {
            CompareResult r = _state.Comparison;

            if (r == null)
            {
                Cards[0].ClearScore();
                Cards[0].SetStats(new List<StatVm>());
            }
            else
            {
                Cards[0].SetScore(AnalysisCardVm.MaxScore);   // ← 진짜 점수가 들어갈 자리
                Cards[0].SetStats(new List<StatVm>
                {
                    new StatVm("견준 IO", Count(r.CommonCount)),
                    new StatVm("달라진 IO", Count(r.ChangedCount)),
                    new StatVm("한쪽에만", Count(r.OnlyBefore.Count + r.OnlyAfter.Count)),
                });
            }

            // 분석 2·3 은 아직 없습니다. 점수를 비워 둬야 "안 만들었다" 가
            // 그대로 읽힙니다.
            for (int i = 1; i < Cards.Count; i++) Cards[i].ClearScore();

            Raise("HasLogs"); Raise("HasBoth"); Raise("Hint");
            Raise("BeforeText"); Raise("AfterText"); Raise("RangeText");
        }

        private static string Count(int n)
        {
            return n.ToString("N0") + "개";
        }

        // ---------------- 지금 열어 둔 로그 ----------------
        //
        // 여기 있던 것이 아래 알림 줄 한 줄뿐이었습니다. 메인 화면이라면
        // "지금 무엇을 보고 있는가" 가 제일 먼저 보여야 합니다.

        public bool HasLogs { get { return _state.HasAny; } }
        public bool HasBoth { get { return _state.HasBoth; } }

        public string BeforeText { get { return Describe(_state.Before, "이전 로그를 열어 주세요"); } }
        public string AfterText { get { return Describe(_state.After, "이후 로그를 열어 주세요"); } }

        private static string Describe(LogDataset ds, string none)
        {
            if (ds == null) return none;
            return Path.GetFileName(ds.SourcePath)
                 + "    ·  IO " + ds.ChannelCount.ToString("N0")
                 + "  ·  표본 " + ds.SampleCount.ToString("N0");
        }

        /// <summary>두 로그가 겹치는 시간 구간.</summary>
        public string RangeText
        {
            get
            {
                CompareResult r = _state.Comparison;
                LogDataset ds = _state.TimeReference;
                if (r == null || ds == null) return string.Empty;
                if (r.Warning.Length > 0) return r.Warning;
                return "겹치는 구간  " + ds.FormatTime(r.OverlapStart)
                     + "  ~  " + ds.FormatTime(r.OverlapEnd);
            }
        }

        /// <summary>칸 위에 적는 안내.</summary>
        public string Hint
        {
            get
            {
                if (!_state.HasAny)
                    return "위쪽의 [이전 로그 열기] / [이후 로그 열기] 로 파일을 먼저 열어 주세요.";
                if (!_state.HasBoth)
                    return "로그가 하나뿐입니다. 두 개를 다 열어야 견줄 수 있습니다.";
                return "칸을 누르면 그 분석 화면으로 넘어갑니다.";
            }
        }
    }
}
