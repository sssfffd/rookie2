using System;
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
            Raise("BeforeTime"); Raise("AfterTime");
            Raise("BeforeText"); Raise("AfterText");
            Raise("BeforePath"); Raise("AfterPath");
            Raise("ComparedText");
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

        /// <summary>
        /// 로그가 언제 것인지. <b>파일 이름 앞부분</b>을 그대로 씁니다.
        ///
        /// 로그 파일 안에는 "이 로그가 언제 기록된 것인가" 가 적혀 있지
        /// 않습니다. 시간 열은 0 부터 세는 경과 시간일 수도 있고, 시각이어도
        /// 날짜가 없을 수 있습니다. 대신 파일 이름 앞에 날짜를 붙이는 것이
        /// 보통이라 그걸 그대로 보여 줍니다 — 짐작해서 만들어 내지 않습니다.
        /// </summary>
        public string BeforeTime { get { return Occurred(_state.Before); } }
        public string AfterTime { get { return Occurred(_state.After); } }

        /// <summary>이름에서 앞부분을 떼어 내는 글자 수.</summary>
        private const int OccurredLength = 10;

        private static string Occurred(LogDataset ds)
        {
            if (ds == null) return string.Empty;

            string name;
            try { name = Path.GetFileNameWithoutExtension(ds.SourcePath); }
            catch (ArgumentException) { name = string.Empty; }
            if (string.IsNullOrEmpty(name)) return string.Empty;

            if (name.Length > OccurredLength) name = name.Substring(0, OccurredLength);

            // 자른 자리에 구분 기호가 걸리면 지저분합니다 ("2026-03-14_" 같은).
            return name.TrimEnd(' ', '_', '-', '.');
        }

        public string BeforeText { get { return Describe(_state.Before, "이전 로그를 열어 주세요"); } }
        public string AfterText { get { return Describe(_state.After, "이후 로그를 열어 주세요"); } }

        private static string Describe(LogDataset ds, string none)
        {
            if (ds == null) return none;
            return "IO " + ds.ChannelCount.ToString("N0")
                 + "  ·  표본 " + ds.SampleCount.ToString("N0");
        }

        /// <summary>풍선 도움말에 띄울 전체 경로. 이름을 잘라 보여 주므로 필요합니다.</summary>
        public string BeforePath { get { return _state.BeforePath; } }
        public string AfterPath { get { return _state.AfterPath; } }

        /// <summary>
        /// 지금 보고 있는 결과가 언제 나온 것인지.
        ///
        /// 허용 오차나 시간 맞추기를 바꾸고 다시 견주는 일이 잦습니다.
        /// 이걸 안 적어 두면 옛 결과를 새것으로 착각하게 됩니다.
        /// </summary>
        public string ComparedText
        {
            get
            {
                if (_state.Comparison == null) return string.Empty;
                if (_state.ComparedAt == DateTime.MinValue) return string.Empty;
                return "비교 실행  " + _state.ComparedAt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        /// <summary>
        /// 화면 오른쪽 아래의 색 설명. 점수를 어느 색으로 칠하는지와 그 색이
        /// 무슨 뜻인지 알려 줍니다. 칸의 점수 색과 <b>같은 값</b>을 봅니다.
        /// </summary>
        public List<BandVm> Bands { get { return ScoreBands.Legend(); } }

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
