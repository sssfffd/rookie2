using System;
using System.Collections.Generic;
using System.ComponentModel;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면의 <b>최종 판정</b> (1.92, 1.95 에 규칙 바꿈 — 사용자 결정).
    ///
    /// 2.02 (사용자 요청 — "가장 나쁜 판정" 대신 분석 1 비중이 큰 기준):
    /// <b>최종 판정 = 결과가 있는 분석의 가중 종합 점수</b> + 넉아웃 규칙.
    ///   종합 점수 = Σ 가중치 × 점수 / Σ 가중치 (결과가 있는 분석만 — 없는 분석은 빼고 나머지로 다시 나눔)
    ///     가중치 = config.txt final.weights (기본 54 · 30 · 16, AHP 쌍대비교 — AppConfig 참고)
    ///     점수   = 분석 1 은 공정 일치율, 분석 2 · 3 은 판정 구간의 가운데 (정상 97.5 · 주의 87.5 · 이상 40, 경계 95 · 80 일 때)
    ///   판정 = 종합 점수를 공정 일치율과 같은 경계 (95 · 80) 로
    ///   넉아웃 (final.gate) = 분석 1 이 이상이면 최종 이상, 다른 분석이 이상이면 최종은 적어도 주의
    /// 돌리지 않은 분석 · 못 쓰는 분석 (둘 다 "결과 없음") 은 무시합니다.
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
                Raise("Lines"); Raise("CompositeScore"); Raise("CompositeText"); Raise("HasComposite"); Raise("GateNote"); Raise("CompositeTip");
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

        /// <summary>2.02: 분석 하나의 0 ~ 100 점수 — 분석 1 은 공정 일치율, 그 밖은 판정 구간의 가운데.</summary>
        public static double ScoreFor(AnalysisCardVm c)
        {
            if (c.Number == 1 && c.HasScore && !double.IsNaN(c.Score)) return Math.Max(0, Math.Min(100, c.Score));
            return GradeScore(c.LampGrade);
        }

        /// <summary>판정 → 그 구간의 가운데 점수 (경계 95 · 80 이면 정상 97.5 · 주의 87.5 · 이상 40). 결과 없음 NaN.</summary>
        public static double GradeScore(string grade)
        {
            double good = ScoreBands.GoodFrom, warn = ScoreBands.WarnFrom;
            if (grade == ScoreBands.GradeGood) return (good + 100) / 2;
            if (grade == ScoreBands.GradeWarn) return (warn + good) / 2;
            if (grade == ScoreBands.GradeBad) return warn / 2;
            return double.NaN;
        }

        /// <summary>2.02: 종합 점수 (가중 평균). 결과가 있는 분석이 없으면 NaN.</summary>
        public double CompositeScore
        {
            get
            {
                double[] w = Settings.AppConfig.Current.FinalWeights;
                double sum = 0, sumW = 0;
                foreach (AnalysisCardVm c in _cards)
                {
                    if (AnalysisCardVm.RankOf(c.LampGrade) == 0) continue;
                    int k = c.Number - 1;
                    double wk = k >= 0 && k < w.Length ? w[k] : 0;
                    if (!(wk > 0)) continue;
                    sum += wk * ScoreFor(c);
                    sumW += wk;
                }
                return sumW > 0 ? sum / sumW : double.NaN;
            }
        }

        /// <summary>2.02: 넉아웃이 판정을 바꿨으면 그 까닭 ("로그 비교 이상 — 주 분석" 등), 아니면 빈 글.</summary>
        public string GateNote { get { string g; return GateOf(out g); } }

        private string GateOf(out string grade)
        {
            double s = CompositeScore;
            grade = double.IsNaN(s) ? ScoreBands.GradeNone : ScoreBands.GradeOf(s);
            if (double.IsNaN(s) || !Settings.AppConfig.Current.FinalGate) return string.Empty;
            AnalysisCardVm first = _cards.Count > 0 ? _cards[0] : null;
            if (first != null && first.LampGrade == ScoreBands.GradeBad && grade != ScoreBands.GradeBad)
            {
                grade = ScoreBands.GradeBad;
                return first.Title + " 이상 — 주 분석이 이상이면 최종 이상";
            }
            if (grade == ScoreBands.GradeGood)
                foreach (AnalysisCardVm c in _cards)
                    if (c.LampGrade == ScoreBands.GradeBad)
                    {
                        grade = ScoreBands.GradeWarn;
                        return c.Title + " 이상 — 최종은 적어도 주의";
                    }
            return string.Empty;
        }

        /// <summary>색 — 2.02: 가중 종합 점수 (+ 넉아웃).</summary>
        public string Grade { get { string g; GateOf(out g); return g; } }

        /// <summary>2.02: 칸에 적는 종합 점수 글 ("종합 점수 93.4"). 결과가 없으면 빈 글.</summary>
        public string CompositeText
        {
            get
            {
                double s = CompositeScore;
                if (double.IsNaN(s)) return string.Empty;
                string t = "종합 점수 " + s.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                string gate = GateNote;
                return gate.Length > 0 ? t + " · " + gate : t;
            }
        }

        public bool HasComposite { get { return CompositeText.Length > 0; } }

        /// <summary>2.02: 마우스를 올리면 — 셈법 한 줄.</summary>
        public string CompositeTip
        {
            get
            {
                double[] w = Settings.AppConfig.Current.FinalWeights;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string names = string.Empty;
                for (int k = 0; k < _cards.Count && k < w.Length; k++)
                    names += (k > 0 ? " · " : string.Empty) + _cards[k].Title + " " + w[k].ToString("0.#", inv);
                return "종합 점수 = 결과가 있는 분석의 가중 평균 (가중치 " + names + " — AHP 쌍대비교).\n"
                     + "분석 1 은 공정 일치율, 다른 분석은 판정 구간의 가운데 점수 (정상 " + GradeScore(ScoreBands.GradeGood).ToString("0.#", inv)
                     + " · 주의 " + GradeScore(ScoreBands.GradeWarn).ToString("0.#", inv) + " · 이상 " + GradeScore(ScoreBands.GradeBad).ToString("0.#", inv) + ").\n"
                     + "판정 경계는 공정 일치율과 같음 (정상 " + ScoreBands.GoodFrom.ToString("0.#", inv) + " · 주의 " + ScoreBands.WarnFrom.ToString("0.#", inv) + ")."
                     + (Settings.AppConfig.Current.FinalGate ? "\n주 분석 (분석 1) 이 이상이면 최종 이상, 다른 분석이 이상이면 최종은 적어도 주의." : string.Empty);
            }
        }

        /// <summary>판정 등 모양 (칸과 같은 스타일을 쓰려고 같은 이름).</summary>
        public string LampGrade { get { return Grade; } }

        public string Word { get { return AnalysisCardVm.WordOf(Grade); } }



        public string Icon { get { return AnalysisCardVm.IconOf(Grade); } }

        /// <summary>
        /// 가장 나쁜 분석의 이름과 근거 한 줄 (LLM 글 · 예전 칸에서 씀). 2.02 부터 최종 판정의 기준은 아니고
        /// "어느 분석이 가장 나빴나" 를 알려 주는 글입니다.
        /// </summary>
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
