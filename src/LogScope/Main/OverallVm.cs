using System;
using System.Collections.Generic;
using System.ComponentModel;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면의 <b>최종 판정</b> (1.92, 1.95 에 규칙 바꿈 — 사용자 결정).
    ///
    /// <b>최종 판정 = 결과가 있는 분석 (1 · 2 · 3) 중 가장 나쁜 판정</b>. 돌리지 않은 분석 · 못 쓰는 분석 (둘 다 "결과 없음") 은 무시합니다.
    /// 근거는 그 분석의 <b>이름</b> (config.txt) 으로 적습니다. 칸 셋의 판정 등을 그대로 읽어 칸과 어긋날 일이 없습니다.
    /// </summary>
    public sealed class OverallVm : ObservableObject
    {
        private readonly List<AnalysisCardVm> _cards;

        public OverallVm(List<AnalysisCardVm> cards)
        {
            _cards = cards ?? new List<AnalysisCardVm>();
            foreach (AnalysisCardVm c in _cards) c.PropertyChanged += OnCardChanged;
        }

        private void OnCardChanged(object sender, PropertyChangedEventArgs e)
        {
            string n = e.PropertyName;
            if (n == "Grade" || n == "LampGrade" || n == "LampLine" || n == "LampWord" || n == "Finding" || string.IsNullOrEmpty(n))
            {
                Raise("Lines");
                Raise("Grade"); Raise("LampGrade"); Raise("Word"); Raise("Icon"); Raise("Reason"); Raise("HasReason"); Raise("ReferenceNote"); Raise("HasReference");
            }
        }

        /// <summary>결과가 있는 칸 중 가장 나쁜 것 (같으면 앞 번호). 없으면 null.</summary>
        private AnalysisCardVm Worst
        {
            get
            {
                AnalysisCardVm w = null;
                foreach (AnalysisCardVm c in _cards)
                {
                    int r = AnalysisCardVm.RankOf(c.LampGrade);
                    if (r == 0) continue;
                    if (w == null || r > AnalysisCardVm.RankOf(w.LampGrade)) w = c;
                }
                return w;
            }
        }

        /// <summary>
        /// 2.00: 최종 판정 칸 아래 줄들 — 결과가 있는 분석마다 "이름  무엇이 있었나" (사용자 요청 — 이상한 설명 대신).
        /// 분석 1 "차이 IO 5개 — 이상 2 · 주의 3", 분석 2 "바뀐 값 3 개", 분석 3 모듈이 적은 유의점.
        /// </summary>
        public List<OverallLine> Lines
        {
            get
            {
                var list = new List<OverallLine>();
                foreach (AnalysisCardVm c in _cards)
                {
                    if (AnalysisCardVm.RankOf(c.LampGrade) == 0) continue;
                    list.Add(new OverallLine { Title = c.Title, Text = c.Finding, LampGrade = c.LampGrade });
                }
                return list;
            }
        }

        /// <summary>색 — 가장 나쁜 칸의 것.</summary>
        public string Grade { get { AnalysisCardVm w = Worst; return w != null ? w.LampGrade : ScoreBands.GradeNone; } }

        /// <summary>판정 등 모양 (칸과 같은 스타일을 쓰려고 같은 이름).</summary>
        public string LampGrade { get { return Grade; } }

        public string Word { get { return AnalysisCardVm.WordOf(Grade); } }



        public string Icon { get { return AnalysisCardVm.IconOf(Grade); } }

        /// <summary>왜 그 판정인지 한 줄 — 가장 나쁜 분석의 이름과 근거.</summary>
        public string Reason
        {
            get
            {
                AnalysisCardVm w = Worst;
                if (w == null) return string.Empty;           // 1.97: 결과가 없으면 "결과 없음" 한 마디만
                return w.Title + " — " + w.LampLine;
            }
        }

        /// <summary>근거 줄이 있는지 (1.97 — 결과가 없으면 줄을 감춰 "결과 없음" 이 칸 가운데에 옴).</summary>
        public bool HasReason { get { return Reason.Length > 0; } }

        /// <summary>결과가 있는 분석마다 "이름 판정" (예: "로그 비교 주의 · DB 분석 정상"). 하나뿐이면 빈 글자.</summary>
        public string ReferenceNote
        {
            get
            {
                var parts = new List<string>();
                foreach (AnalysisCardVm c in _cards)
                    if (AnalysisCardVm.RankOf(c.LampGrade) > 0) parts.Add(c.Title + " " + c.LampWord);
                return parts.Count < 2 ? string.Empty : string.Join(" · ", parts.ToArray());
            }
        }

        public bool HasReference { get { return ReferenceNote.Length > 0; } }
    }

    /// <summary>2.00: 최종 판정 칸의 한 줄 — 분석 이름 · 무엇이 있었나 · 색.</summary>
    public sealed class OverallLine
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public string LampGrade { get; set; }
    }
}
