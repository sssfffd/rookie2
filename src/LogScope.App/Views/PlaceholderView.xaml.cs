using System.Windows;
using System.Windows.Controls;

namespace LogScope.App.Views
{
    /// <summary>아직 만들지 않은 분석 화면. 여기가 어디인지만 적어 둡니다.</summary>
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView()
        {
            InitializeComponent();
        }

        /// <summary>화면 가운데에 크게 적을 이름.</summary>
        public static readonly DependencyProperty HeadingProperty =
            DependencyProperty.Register("Heading", typeof(string), typeof(PlaceholderView),
                new PropertyMetadata("분석", OnHeadingChanged));

        public string Heading
        {
            get { return (string)GetValue(HeadingProperty); }
            set { SetValue(HeadingProperty, value); }
        }

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = d as PlaceholderView;
            if (v != null) v.TitleText.Text = (string)e.NewValue ?? string.Empty;
        }
    }
}
