using System.Globalization;
using System.Windows;
using LogScope.Core.History;

namespace LogScope.App.Views
{
    /// <summary>
    /// 저장해 둔 분석 한 건을 펼쳐 보는 창.
    ///
    /// 목록 줄에는 자리가 없어서 이름과 몇 개의 수만 적습니다. 여기서는
    /// 그때 견준 로그의 <b>전체 이름과 경로</b>, 점수 셋, 그리고 어떤 기준으로
    /// 나온 수인지를 다 보여 줍니다.
    ///
    /// [현재 분석과 비교] 는 <b>아직 안이 비어 있습니다.</b> 무엇을 어떻게
    /// 나란히 놓을지 정해지지 않았습니다 — 자리만 잡아 둡니다.
    /// </summary>
    public partial class SavedResultWindow : Window
    {
        private SavedResultWindow(AnalysisRecord r)
        {
            InitializeComponent();
            Fill(r);
        }

        public static void Show(Window owner, AnalysisRecord record)
        {
            if (record == null) return;
            var w = new SavedResultWindow(record);
            if (owner != null && owner.IsVisible) w.Owner = owner;
            w.ShowDialog();
        }

        private void Fill(AnalysisRecord r)
        {
            NameText.Text = r.DisplayName;
            SavedAtText.Text = r.SavedAt == System.DateTime.MinValue
                ? "저장 시각을 알 수 없습니다"
                : "저장  " + r.SavedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            BeforeNameText.Text = Or(r.BeforeName, "—");
            AfterNameText.Text = Or(r.AfterName, "—");
            BeforePathText.Text = r.BeforePath ?? string.Empty;
            AfterPathText.Text = r.AfterPath ?? string.Empty;

            Score1Text.Text = r.ScoreText(1);
            Score2Text.Text = r.ScoreText(2);
            Score3Text.Text = r.ScoreText(3);

            CountsText.Text = "견준 IO " + r.ComparedCount.ToString("N0")
                            + "개   ·   달라진 IO " + r.ChangedCount.ToString("N0")
                            + "개   ·   한쪽에만 " + r.OneSidedCount.ToString("N0") + "개";
            BasisText.Text = r.BasisText;
        }

        private static string Or(string s, string fallback)
        {
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        /// <summary>
        /// [현재 분석과 비교]. 아직 안이 없습니다. 눌러도 아무 일이 없으면
        /// 고장으로 보이므로, 아직 없다는 것만 그 자리에 말해 줍니다.
        /// </summary>
        private void OnCompare(object sender, RoutedEventArgs e)
        {
            CompareNote.Text = "미구현 — 무엇을 어떻게 나란히 놓을지 아직 정해지지 않았습니다.";
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
