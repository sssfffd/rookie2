using System;
using System.Collections.Generic;
using LogScope.Common;
using LogScope.Main;
using LogScope.Settings;

namespace LogScope.Analysis1.Judge
{
    /// <summary>허용 고르는 창에서 고른 것.</summary>
    public enum AllowChoice
    {
        Cancel = 0,
        /// <summary>허용 풀기.</summary>
        Unallow = 1,
        /// <summary>이번만.</summary>
        Once = 2,
        /// <summary>앞으로도 (본 크기까지).</summary>
        Always = 3,
        /// <summary>이 IO 의 오차 모두 이번만.</summary>
        IoOnce = 4,
    }

    /// <summary>
    /// 분석 1 <b>오차 목록</b> 화면 (1.56). WPF 를 쓰지 않습니다 (시험이 닿음).
    ///
    /// <code>
    ///   오차 목록                                   분석 1 판정  WARNING
    ///   오차 3 개 · 허용 1 개 · IO 280 개 · 단계 5 개 · 0.1 초 간격
    ///   판정 까닭  WARNING  PRES_MAIN · 2 · 12:10:00 · 8분 3초 …
    ///   ┌ WARNING  PRES_MAIN · 2 · 12:10:00 · 8분 3초                       ┐  ← 누르면 허용 고르기
    ///   │          이후 63.1 · 이전 범위 59.8 ~ 60.2 · 최대 2.9 · 이 단계 오차 16.2 % │
    ///   └ PASS     TEMP_1 · 1 · 12:00:00 · 1분          허용됨 · 이번만          ┘  (회색)
    ///   앞으로도 허용한 것   SPIKE · 4 — 0.1초 · 250 까지 (2026-10-31)  [지우기]
    /// </code>
    /// </summary>
    public sealed class JudgeVm : ObservableObject
    {
        private readonly AppState _state;

        /// <summary>목록에 싣는 줄 상한. 판정에는 모두 들어갑니다.</summary>
        public const int MaxRows = 2000;

        public JudgeVm(AppState state)
        {
            _state = state;
            Rows = new List<JudgeRowVm>();
            Rules = new List<AllowRuleRowVm>();
        }

        /// <summary>허용을 바꿨을 때 — 창이 설정을 저장하고 메인 칸을 다시 그립니다.</summary>
        public event EventHandler Changed;

        public JudgeResult Result { get { return _state.Judgement; } }
        public bool HasResult { get { return _state.Judgement != null; } }
        public bool NoResult { get { return _state.Judgement == null; } }

        public string Word { get { return HasResult ? _state.Judgement.Word : string.Empty; } }
        public string Grade { get { return HasResult ? GradeOf(_state.Judgement.Grade) : ScoreBands.GradeNone; } }

        public string Summary
        {
            get
            {
                JudgeResult r = _state.Judgement;
                if (r == null) return string.Empty;
                string s = "오차 " + r.OpenCount.ToString("N0") + " 개 · 허용 " + r.AllowedCount.ToString("N0")
                         + " 개 · 견준 IO " + r.IoCount.ToString("N0") + " 개";
                if (r.ByStep) s += " · 단계 " + r.StepCount + " 개";
                else s += " · 단계 없이 전체를 한 구간으로";
                return s;
            }
        }

        public string Reasons
        {
            get
            {
                JudgeResult r = _state.Judgement;
                if (r == null || r.Reasons.Count == 0) return string.Empty;
                return string.Join("\n", r.Reasons.ToArray());
            }
        }
        public bool HasReasons { get { return Reasons.Length > 0; } }

        public string Note { get { return HasResult ? _state.Judgement.Note : string.Empty; } }
        public bool HasNote { get { return Note.Length > 0; } }

        /// <summary>오차가 하나도 없음.</summary>
        public bool NoItems { get { return HasResult && _state.Judgement.Items.Count == 0; } }

        public List<JudgeRowVm> Rows { get; private set; }

        /// <summary>줄이 너무 많아 다 싣지 않았을 때 그 글. 아니면 빈 글자.</summary>
        public string MoreText { get; private set; }
        public bool HasMore { get { return MoreText.Length > 0; } }

        public List<AllowRuleRowVm> Rules { get; private set; }
        public bool HasRules { get { return Rules.Count > 0; } }
        public bool NoRules { get { return Rules.Count == 0; } }

        /// <summary>판정이 바뀐 뒤 (견준 뒤 · 허용을 바꾼 뒤) 다시 적습니다.</summary>
        public void Refresh()
        {
            var rows = new List<JudgeRowVm>();
            JudgeResult r = _state.Judgement;
            MoreText = string.Empty;
            if (r != null)
            {
                int n = Math.Min(MaxRows, r.Items.Count);
                for (int i = 0; i < n; i++) rows.Add(new JudgeRowVm(r.Items[i]));
                if (r.Items.Count > n)
                    MoreText = (r.Items.Count - n).ToString("N0") + " " + JudgeTexts.MoreRows;
            }
            Rows = rows;

            var rules = new List<AllowRuleRowVm>();
            List<AllowRule> list = _state.Settings.AllowRules;
            bool clock = r == null || r.Clock;
            string unit = r != null ? r.TimeUnit : string.Empty;
            for (int i = 0; i < list.Count; i++)
            {
                string text = list[i].Describe(
                    delegate (double d) { return JudgeEngine.DurationText(d, clock, unit); },
                    delegate (double v) { return NumberText.Plain(v); });
                rules.Add(new AllowRuleRowVm(i, text));
            }
            Rules = rules;

            Raise("Rows"); Raise("Rules"); Raise("HasRules"); Raise("NoRules");
            Raise("HasResult"); Raise("NoResult"); Raise("Word"); Raise("Grade"); Raise("Summary");
            Raise("Reasons"); Raise("HasReasons"); Raise("Note"); Raise("HasNote"); Raise("NoItems");
            Raise("MoreText"); Raise("HasMore");
        }

        /// <summary>같은 IO 의 허용 안 된 값 오차 수 (고르는 창의 "이 IO 모두").</summary>
        public int OpenCountOf(string io)
        {
            JudgeResult r = _state.Judgement;
            if (r == null || string.IsNullOrEmpty(io)) return 0;
            int n = 0;
            foreach (JudgeItem it in r.Items)
                if (!it.IsAllowed && it.Kind == JudgeKind.Value && it.Io == io) n++;
            return n;
        }

        /// <summary>고른 대로 허용을 바꾸고 판정을 다시 냅니다 (오차를 다시 찾지는 않음).</summary>
        public void Apply(JudgeItem it, AllowChoice choice)
        {
            if (it == null || choice == AllowChoice.Cancel) return;
            List<AllowRule> rules = _state.Settings.AllowRules;

            switch (choice)
            {
                case AllowChoice.Unallow:
                    _state.AllowOnce.Remove(it.Key);
                    // 이 오차를 덮는 규칙을 모두 지웁니다 — 하나만 지우면 다른 규칙이 그대로 덮습니다.
                    for (int i = rules.Count - 1; i >= 0; i--)
                        if (JudgeEngine.Matches(it, rules[i])) rules.RemoveAt(i);
                    break;
                case AllowChoice.Once:
                    _state.AllowOnce.Add(it.Key);
                    break;
                case AllowChoice.Always:
                    if (!it.CanAllowAlways) { _state.AllowOnce.Add(it.Key); break; }
                    if (rules.Count < AppSettings.MaxAllowRules) rules.Add(JudgeEngine.RuleFor(it));
                    break;
                case AllowChoice.IoOnce:
                    JudgeResult r = _state.Judgement;
                    if (r == null) break;
                    foreach (JudgeItem x in r.Items)
                        if (!x.IsAllowed && x.Kind == JudgeKind.Value && x.Io == it.Io) _state.AllowOnce.Add(x.Key);
                    break;
            }
            Done();
        }

        /// <summary>"앞으로도 허용" 하나를 지웁니다.</summary>
        public void RemoveRule(int index)
        {
            List<AllowRule> rules = _state.Settings.AllowRules;
            if (index < 0 || index >= rules.Count) return;
            rules.RemoveAt(index);
            Done();
        }

        private void Done()
        {
            _state.Regrade();
            Refresh();
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        public static string GradeOf(JudgeGrade g)
        {
            return g == JudgeGrade.Fail ? ScoreBands.GradeBad
                 : g == JudgeGrade.Warning ? ScoreBands.GradeWarn
                 : ScoreBands.GradeGood;
        }
    }

    /// <summary>오차 목록 한 줄.</summary>
    public sealed class JudgeRowVm
    {
        public JudgeRowVm(JudgeItem item)
        {
            Item = item;
        }

        public JudgeItem Item { get; private set; }
        public string What { get { return Item.What; } }
        public string Detail { get { return Item.Detail; } }
        public bool IsAllowed { get { return Item.IsAllowed; } }

        /// <summary>왼쪽 낱말 — 허용됐으면 비움 (판정에서 빠졌으므로).</summary>
        public string Word { get { return Item.IsAllowed ? string.Empty : JudgeResult.WordOf(Item.Grade); } }

        /// <summary>낱말 색. 허용됐으면 회색.</summary>
        public string Grade { get { return Item.IsAllowed ? ScoreBands.GradeNone : JudgeVm.GradeOf(Item.Grade); } }

        public string AllowText
        {
            get
            {
                if (Item.Allowed == AllowState.Once) return JudgeTexts.AllowedOnce;
                if (Item.Allowed == AllowState.Rule) return JudgeTexts.AllowedRule;
                return string.Empty;
            }
        }
    }

    /// <summary>"앞으로도 허용" 한 줄.</summary>
    public sealed class AllowRuleRowVm
    {
        public AllowRuleRowVm(int index, string text)
        {
            Index = index;
            Text = text;
        }

        public int Index { get; private set; }
        public string Text { get; private set; }
    }
}
