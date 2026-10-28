using System.Windows;
using System.Windows.Controls;
using LogScope.Settings;

namespace LogScope.Shell
{
    /// <summary>
    /// 분석 3 자리 — Analysis3 폴더에 <c>Analysis3View</c> 가 없거나, 만들다 오류가 났을 때
    /// 대신 뜨는 화면 (1.30). 여기가 어디인지와, 오류면 그 까닭을 적습니다.
    /// </summary>
    public partial class Analysis3Placeholder : UserControl
    {
        public Analysis3Placeholder() : this(string.Empty) { }

        /// <param name="error">Analysis3View 를 만들다 난 오류. 없으면 빈 글자.</param>
        public Analysis3Placeholder(string error)
        {
            InitializeComponent();

            // 이름은 config.txt 의 analysis3 줄.
            TitleText.Text = AppConfig.Current.Title(3);

            if (!string.IsNullOrEmpty(error))
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
