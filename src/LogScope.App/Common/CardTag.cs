using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace LogScope.App.Common
{
    /// <summary>
    /// 파일을 끌어다 놓는 칸의 <c>Tag</c> 를 읽고, 칸을 밝히거나 되돌립니다.
    ///
    /// <code>
    ///   Tag="before"                 어느 쪽 칸인지만
    ///   Tag="before|Brush.Window"    뒤는 밝히기를 끝낸 뒤 되돌릴 배경
    /// </code>
    ///
    /// <b>왜 한 곳인가.</b> 전에는 메인 화면과 DB 화면이 이 글자를 각자 손으로
    /// 쪼갰고, 밝히고 되돌리는 코드도 두 벌이었습니다 (글자만 다른 같은 코드).
    /// 0.59 에서 칸 배경이 Brush.Panel 에서 Brush.Window 로 바뀌었을 때
    /// 되돌리는 쪽이 옛 배경을 그대로 쓰고 있어서, <b>한 번 끌어다 놓은 칸만
    /// 색이 달라진 채</b> 남았습니다. 두 벌이면 한쪽만 고치게 됩니다.
    ///
    /// <b>XAML 이 적고 코드가 견주는 글자</b>라 컴파일러가 봐 주지 않습니다.
    /// 그래서 글자는 여기 상수로 두고, XAML 의 Tag 가 코드가 아는 글자인지는
    /// scripts/check_sources.py 가 봅니다.
    /// </summary>
    public static class CardTag
    {
        public const string Before = "before";
        public const string After = "after";

        /// <summary>Tag 에 되돌릴 배경을 안 적어 둔 칸의 배경.</summary>
        public const string DefaultOffBrush = "Brush.Panel";

        /// <summary>밝혔을 때의 배경과 테두리.</summary>
        public const string LitBrush = "Brush.DropTarget";
        public const string LitBorderBefore = "Brush.Before";
        public const string LitBorderAfter = "Brush.After";
        public const string OffBorder = "Brush.Border";

        /// <summary>이전 쪽 칸인가. 모르는 글자면 디버그 빌드에서 걸립니다.</summary>
        public static bool IsBefore(object sender)
        {
            string side = Side(sender);
            if (side == Before) return true;
            if (side == After) return false;

            // 끌어다 놓는 중에 창을 띄울 수는 없습니다. 대신 디버그 빌드에서
            // 바로 걸리게 합니다 — 모르는 글자를 조용히 '이후' 로 보면 단추가
            // 반대쪽으로 듭니다.
            Debug.Assert(false, "칸의 Tag 를 모릅니다: " + side);
            return false;
        }

        /// <summary>밝히기를 끝낸 뒤 되돌릴 배경. XAML 이 정합니다.</summary>
        public static string OffBrush(object sender)
        {
            string tag = Tag(sender);
            int bar = tag.IndexOf('|');
            if (bar < 0 || bar + 1 >= tag.Length) return DefaultOffBrush;
            return tag.Substring(bar + 1);
        }

        /// <summary>
        /// 칸을 밝히고 되돌립니다.
        ///
        /// 되돌릴 때 <b>값을 다시 걸어 줍니다</b> — ClearValue 는 XAML 에
        /// 적어 둔 DynamicResource 까지 지웁니다. SetResourceReference 는
        /// DynamicResource 를 코드로 거는 것이라, 테마를 바꾸면 이 칸도
        /// 같이 따라갑니다.
        /// </summary>
        public static void Lit(Border card, bool on)
        {
            if (card == null) return;

            bool before = IsBefore(card);

            card.SetResourceReference(Border.BackgroundProperty,
                on ? LitBrush : OffBrush(card));
            card.SetResourceReference(Border.BorderBrushProperty,
                on ? (before ? LitBorderBefore : LitBorderAfter) : OffBorder);
            card.BorderThickness = new Thickness(on ? 2 : 1);
        }

        private static string Side(object sender)
        {
            string tag = Tag(sender);
            int bar = tag.IndexOf('|');
            return bar < 0 ? tag : tag.Substring(0, bar);
        }

        private static string Tag(object sender)
        {
            var fe = sender as FrameworkElement;
            return fe == null ? string.Empty : (fe.Tag as string) ?? string.Empty;
        }
    }
}
