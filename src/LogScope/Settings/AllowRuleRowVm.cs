namespace LogScope.Settings
{
    /// <summary>설정 창 [허용한 오차] 의 한 줄 (1.59).</summary>
    public sealed class AllowRuleRowVm
    {
        public AllowRuleRowVm(int index, string text)
        {
            Index = index;
            Text = text ?? string.Empty;
        }

        /// <summary>AppSettings.AllowRules 의 몇 번째인지.</summary>
        public int Index { get; private set; }
        public string Text { get; private set; }
    }
}
