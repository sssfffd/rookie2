using System.Collections.Generic;
using LogScope.Common;
using LogScope.Settings;

namespace LogScope.Analysis1.Judge
{
    /// <summary>오차 하나에 고른 허용.</summary>
    public enum AllowChoice
    {
        Cancel = 0,
        /// <summary>허용 풀기 — 이번만 허용과 이 오차를 덮는 규칙을 모두 지움.</summary>
        Unallow = 1,
        /// <summary>이번만 (이 로그 짝에서만).</summary>
        Once = 2,
        /// <summary>앞으로도 — 같은 IO · 같은 단계에서 이번에 본 크기까지 (설정에 저장).</summary>
        Always = 3,
    }

    /// <summary>
    /// 허용을 바꾸는 한 곳 (1.59). 메인 분석 1 칸의 오차 줄 단추와 설정 창의 허용 목록이 부릅니다.
    /// 1.56 ~ 1.58 의 [오차 목록] 화면 · 따로 뜨는 창은 1.59 에 뺐습니다 (사용자 요청 — 따로 창 없이,
    /// 메인 칸에서 그룹 → IO → 구간별 오차를 펼쳐 그 자리에서 고름).
    /// </summary>
    public static class JudgeAllow
    {
        /// <summary>고른 대로 바꾸고 판정을 다시 냅니다 (오차를 다시 찾지는 않음). 바뀌었으면 true.</summary>
        public static bool Apply(AppState state, JudgeItem it, AllowChoice choice)
        {
            if (state == null || it == null || choice == AllowChoice.Cancel) return false;
            List<AllowRule> rules = state.Settings.AllowRules;

            switch (choice)
            {
                case AllowChoice.Unallow:
                    state.AllowOnce.Remove(it.Key);
                    // 이 오차를 덮는 규칙을 모두 지웁니다 — 하나만 지우면 다른 규칙이 그대로 덮습니다.
                    for (int i = rules.Count - 1; i >= 0; i--)
                        if (JudgeEngine.Matches(it, rules[i])) rules.RemoveAt(i);
                    break;
                case AllowChoice.Once:
                    state.AllowOnce.Add(it.Key);
                    break;
                case AllowChoice.Always:
                    // 단계가 없어지거나 차례가 바뀐 것은 앞으로도 허용하지 않습니다 — 이번만으로.
                    if (!it.CanAllowAlways) { state.AllowOnce.Add(it.Key); break; }
                    if (rules.Count < AppSettings.MaxAllowRules) rules.Add(JudgeEngine.RuleFor(it));
                    break;
            }
            state.Regrade();
            return true;
        }

        /// <summary>"앞으로도 허용" 하나를 지우고 판정을 다시 냅니다.</summary>
        public static bool RemoveRule(AppState state, int index)
        {
            if (state == null) return false;
            List<AllowRule> rules = state.Settings.AllowRules;
            if (index < 0 || index >= rules.Count) return false;
            rules.RemoveAt(index);
            state.Regrade();
            return true;
        }
    }
}
