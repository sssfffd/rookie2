using System;
using System.Collections.Generic;
using System.ComponentModel;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면 왼쪽 기둥의 <b>최종 판정</b> (1.92, 사용자 결정).
    ///
    /// <b>최종 판정 = 분석 1 의 판정</b> (분석 1 이 메인). 분석 2 · 3 은 참고 — 분석 1 보다 나쁘면 아래에
    /// "참고: 분석 2 주의" 로 적어 놓치지 않게 합니다. 칸 셋의 판정 등 (정상 · 주의 · 이상) 을 그대로 읽어서
    /// 칸과 기둥이 어긋날 일이 없습니다 (칸이 바뀌면 여기도 바로 바뀜).
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
            if (n == "Grade" || n == "LampGrade" || n == "LampLine" || n == "LampWord" || string.IsNullOrEmpty(n))
            {
                Raise("Grade"); Raise("LampGrade"); Raise("Word"); Raise("Icon"); Raise("Reason"); Raise("ReferenceNote"); Raise("HasReference");
            }
        }

        private AnalysisCardVm Main { get { return _cards.Count > 0 ? _cards[0] : null; } }

        /// <summary>색 — 분석 1 칸의 것 그대로.</summary>
        public string Grade { get { return Main != null ? Main.Grade : ScoreBands.GradeNone; } }

        /// <summary>판정 등 모양 (칸과 같은 스타일을 쓰려고 같은 이름).</summary>
        public string LampGrade { get { return Grade; } }

        public string Word { get { return AnalysisCardVm.WordOf(Grade); } }



        public string Icon { get { return AnalysisCardVm.IconOf(Grade); } }

        /// <summary>왜 그 판정인지 한 줄.</summary>
        public string Reason
        {
            get
            {
                if (Main == null || Grade == ScoreBands.GradeNone) return "분석 1 을 돌리면 최종 판정이 나옵니다";
                return "분석 1 기준 — " + Main.LampLine;
            }
        }

        /// <summary>분석 2 · 3 중 분석 1 보다 나쁜 것 (참고). 없으면 빈 글자.</summary>
        public string ReferenceNote
        {
            get
            {
                int mine = AnalysisCardVm.RankOf(Grade);
                var parts = new List<string>();
                for (int i = 1; i < _cards.Count; i++)
                {
                    AnalysisCardVm c = _cards[i];
                    int r = AnalysisCardVm.RankOf(c.LampGrade);   // 못 쓰는 칸 (회색) 은 참고에서 뺌
                    if (r > mine && r >= 2)
                        parts.Add("분석 " + c.Number + " " + c.LampWord);
                }
                return parts.Count == 0 ? string.Empty : "참고: " + string.Join(" · ", parts.ToArray()) + " — 따로 확인해 보세요";
            }
        }

        public bool HasReference { get { return ReferenceNote.Length > 0; } }
    }
}
