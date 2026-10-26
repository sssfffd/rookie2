using System.Windows.Controls;
using LogScope.Settings;

namespace LogScope.Analysis3
{
    /// <summary>
    /// 분석 3 자리 — 아직 만들지 않은 화면. 여기가 어디인지만 적어 둡니다.
    ///
    /// 따로 만든 분석 3 폴더로 이 폴더를 통째로 갈아 끼우면 그쪽의
    /// <c>Analysis3View</c> 가 이 자리에 뜹니다 (docs/ANALYSIS3-PORTING.md).
    /// </summary>
    public partial class Analysis3View : UserControl
    {
        public Analysis3View()
        {
            InitializeComponent();

            // 이름은 config.txt 의 analysis3 줄. 창이 넘겨 주지 않고 여기서 읽는 것은
            // 갈아 끼운 화면이 Heading 같은 속성을 몰라도 되게 하려는 것입니다.
            TitleText.Text = AppConfig.Current.Title(3);
        }
    }
}
