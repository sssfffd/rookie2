using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LogScope.Themes;

namespace LogScope.Main
{
    // ==========================================================
    //  메인 대시보드의 그림 (1.94) — 점수 게이지 · 시간대별 차이 막대 · 오차율 막대.
    //
    //  XAML 템플릿으로 짜면 칸 수 (100 칸 막대) 만큼 요소가 생기고 바인딩 변환기가 늘어납니다.
    //  그래서 그래프 그림판처럼 OnRender 로 직접 그립니다 (바깥 라이브러리 없음).
    //  색은 테마의 Brush.Good · Warn · Bad (· Soft) 를 그때그때 찾아 — 밝게 / 어둡게를 바꾸면 다시 그립니다.
    // ==========================================================

    /// <summary>그림 셋이 같이 쓰는 것 — 테마 붓 찾기 · 글자.</summary>
    public abstract class DashVisual : FrameworkElement
    {
        protected static readonly Typeface Face = new Typeface("Segoe UI");

        protected DashVisual()
        {
            ThemeManager.Changed += delegate { InvalidateVisual(); };
        }

        protected Brush B(string key, Color fallback)
        {
            var b = TryFindResource(key) as Brush;
            return b ?? new SolidColorBrush(fallback);
        }

        /// <summary>판정 색 (good · warn · bad · none) — 진한 것 / 옅은 것.</summary>
        protected Brush GradeBrush(string grade, bool soft)
        {
            if (grade == ScoreBands.GradeGood) return soft ? B("Brush.GoodSoft", Color.FromRgb(0xE3, 0xF4, 0xE6)) : B("Brush.Good", Color.FromRgb(0x1E, 0x8E, 0x3E));
            if (grade == ScoreBands.GradeWarn) return soft ? B("Brush.WarnSoft", Color.FromRgb(0xFB, 0xEF, 0xD6)) : B("Brush.Warn", Color.FromRgb(0xC7, 0x7D, 0x00));
            if (grade == ScoreBands.GradeBad) return soft ? B("Brush.BadSoft", Color.FromRgb(0xFF, 0xE4, 0xE4)) : B("Brush.Bad", Color.FromRgb(0xC5, 0x29, 0x1A));
            return B("Brush.Muted", Color.FromRgb(0x80, 0x80, 0x80));
        }

        protected Brush Text { get { return B("Brush.Text", Color.FromRgb(0x20, 0x20, 0x20)); } }
        protected Brush Muted { get { return B("Brush.Muted", Color.FromRgb(0x70, 0x70, 0x70)); } }
        protected Brush Line { get { return B("Brush.Border", Color.FromRgb(0xD0, 0xD0, 0xD0)); } }

        protected FormattedText Label(string s, double size, Brush brush)
        {
            return new FormattedText(s ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, brush);
        }

        /// <summary>굵은 선으로 그린 반원 호 — 0 (왼쪽) ~ 1 (오른쪽) 사이의 a ~ b.</summary>
        protected static void Arc(DrawingContext dc, Pen pen, Point c, double r, double a, double b)
        {
            if (!(b > a)) return;
            var pts = new List<Point>();
            int n = Math.Max(2, (int)Math.Ceiling((b - a) * 60));
            for (int i = 0; i <= n; i++)
            {
                double f = a + (b - a) * i / n;
                double ang = Math.PI * (1 - f);
                pts.Add(new Point(c.X + r * Math.Cos(ang), c.Y - r * Math.Sin(ang)));
            }
            var g = new StreamGeometry();
            using (StreamGeometryContext ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                pts.RemoveAt(0);
                ctx.PolyLineTo(pts, true, false);
            }
            dc.DrawGeometry(null, pen, g);
        }
    }

    /// <summary>점수 게이지 — 반원, 바탕은 이상 · 주의 · 정상 구간 (config.txt 경계), 그 위에 점수만큼.</summary>
    public sealed class ScoreGauge : DashVisual
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(double), typeof(ScoreGauge),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty GradeProperty = DependencyProperty.Register(
            "Grade", typeof(string), typeof(ScoreGauge),
            new FrameworkPropertyMetadata(ScoreBands.GradeNone, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
        public string Grade { get { return (string)GetValue(GradeProperty); } set { SetValue(GradeProperty, value); } }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 40 || h < 30) return;
            double thick = 10;
            double r = Math.Min(w / 2 - thick, h - thick - 4);
            var c = new Point(w / 2, h - 4);
            double good = ScoreBands.GoodFrom / 100.0, warn = ScoreBands.WarnFrom / 100.0;
            Arc(dc, new Pen { Brush = GradeBrush(ScoreBands.GradeBad, true), Thickness = thick }, c, r, 0, warn);
            Arc(dc, new Pen { Brush = GradeBrush(ScoreBands.GradeWarn, true), Thickness = thick }, c, r, warn, good);
            Arc(dc, new Pen { Brush = GradeBrush(ScoreBands.GradeGood, true), Thickness = thick }, c, r, good, 1);
            double v = Value;
            if (!double.IsNaN(v))
                Arc(dc, new Pen { Brush = GradeBrush(Grade, false), Thickness = thick }, c, r, 0, Math.Max(0.004, Math.Min(1, v / 100.0)));

            // 1.95: "공정 일치율" — % 를 붙이고, 결과가 없으면 "결과 없음" (작게)
            FormattedText t = double.IsNaN(v)
                ? Label(MainDashVm.NoResult, 15, Muted)
                : Label(v.ToString("0.#", CultureInfo.InvariantCulture) + "%", 24, Text);
            if (!double.IsNaN(v)) t.SetFontWeight(FontWeights.Bold);
            dc.DrawText(t, new Point(c.X - t.Width / 2, c.Y - t.Height - 2));
        }
    }

    /// <summary>
    /// 시간대별 차이 — 이전 로그 100 칸 막대 (높이 = 그 칸에서 차이 난 IO 수, 진한 빨강 = 이전 값보다 100 % 넘게),
    /// 이후 로그가 없는 칸은 옅은 빨강 바탕, 아래에 공정 단계 띠와 시각 5 개.
    /// </summary>
    public sealed class TimelineStrip : DashVisual
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            "Data", typeof(DashTimeline), typeof(TimelineStrip),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public DashTimeline Data { get { return (DashTimeline)GetValue(DataProperty); } set { SetValue(DataProperty, value); } }

        private const double Gutter = 28, StepH = 20, AxisH = 16, Gap = 4;

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            DashTimeline d = Data;
            if (w < 80 || h < 60 || d == null || d.Counts.Length == 0) return;
            int n = d.Counts.Length;
            double plotW = w - Gutter, barsH = h - StepH - AxisH - Gap * 2;
            var plot = new Rect(Gutter, 0, plotW, barsH);
            Palette p = ThemeManager.Palette;
            dc.DrawRectangle(new SolidColorBrush(p.Plot), null, plot);

            double colW = plotW / n;
            int max = Math.Max(1, d.MaxCount);
            Brush warn = GradeBrush(ScoreBands.GradeWarn, false), bad = GradeBrush(ScoreBands.GradeBad, false), gapB = GradeBrush(ScoreBands.GradeBad, true);
            for (int k = 0; k < n; k++)
            {
                double x = Gutter + k * colW;
                if (k < d.Gaps.Length && d.Gaps[k]) dc.DrawRectangle(gapB, null, new Rect(x, 0, Math.Max(1, colW), barsH));
                int c = d.Counts[k];
                if (c <= 0) continue;
                double bh = Math.Max(3, barsH * c / max);
                double wt = k < d.Weights.Length ? d.Weights[k] : 1;
                dc.DrawRectangle(wt >= 3 ? bad : warn, null, new Rect(x + 0.5, barsH - bh, Math.Max(1, colW - 1), bh));
            }
            var axis = new Pen { Brush = Line, Thickness = 1 };
            dc.DrawLine(axis, new Point(Gutter, barsH + 0.5), new Point(w, barsH + 0.5));

            FormattedText top = Label(max.ToString(CultureInfo.InvariantCulture), 10, Muted);
            dc.DrawText(top, new Point(Gutter - 6 - top.Width, 0));
            FormattedText zero = Label("0", 10, Muted);
            dc.DrawText(zero, new Point(Gutter - 6 - zero.Width, barsH - zero.Height));

            // 공정 단계 띠
            double sy = barsH + Gap;
            if (d.Steps.Count == 0)
            {
                FormattedText none = Label("공정 단계 없음", 11, Muted);
                dc.DrawText(none, new Point(Gutter, sy + (StepH - none.Height) / 2));
            }
            for (int i = 0; i < d.Steps.Count; i++)
            {
                DashStep s = d.Steps[i];
                var r = new Rect(Gutter + s.From * plotW, sy, Math.Max(1, (s.To - s.From) * plotW - 1), StepH);
                dc.DrawRectangle(new SolidColorBrush(i % 2 == 0 ? p.Grid : p.GridStrong), null, r);
                FormattedText name = Label(s.Name, 11, Text);
                name.MaxTextWidth = Math.Max(1, r.Width - 6);
                name.MaxLineCount = 1;
                name.Trimming = TextTrimming.CharacterEllipsis;
                if (r.Width > 14) dc.DrawText(name, new Point(r.X + Math.Max(3, (r.Width - name.Width) / 2), sy + (StepH - name.Height) / 2));
            }

            // 시각
            double ay = sy + StepH + Gap;
            for (int i = 0; i < d.Labels.Length; i++)
            {
                FormattedText t = Label(d.Labels[i], 10, Muted);
                double x = Gutter + plotW * i / Math.Max(1, d.Labels.Length - 1) - t.Width / 2;
                x = Math.Max(Gutter, Math.Min(w - t.Width, x));
                dc.DrawText(t, new Point(x, ay));
            }
        }
    }

    /// <summary>
    /// 판정 표시 (1.97) — 글자 (✔ ⚠ ✖ ○ –) 는 글꼴마다 크기 · 굵기가 제각각이라 같은 크기의 원 하나로 그립니다.
    /// 정상 = 체크, 주의 = 느낌표, 이상 = ×, 그 밖 (결과 없음) = 빈 원. 크기는 Width · Height (기본 14).
    /// 메인 화면은 결과가 없으면 이 그림을 감추고 "결과 없음" 글자만 둡니다 (LampIconTile 꼴).
    /// </summary>
    public sealed class LampIcon : DashVisual
    {
        public static readonly DependencyProperty GradeProperty = DependencyProperty.Register(
            "Grade", typeof(string), typeof(LampIcon), new FrameworkPropertyMetadata(ScoreBands.GradeNone, FrameworkPropertyMetadataOptions.AffectsRender));

        public string Grade { get { return (string)GetValue(GradeProperty); } set { SetValue(GradeProperty, value); } }

        public LampIcon()
        {
            Width = 14; Height = 14;
            VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double s = Math.Min(ActualWidth, ActualHeight);
            if (s < 4) return;
            double r = s / 2 - 0.75;
            var c = new Point(ActualWidth / 2, ActualHeight / 2);
            string g = Grade;
            bool has = g == ScoreBands.GradeGood || g == ScoreBands.GradeWarn || g == ScoreBands.GradeBad;
            Brush main = GradeBrush(g, false);
            if (!has)
            {
                var ring = new Pen { Brush = Muted, Thickness = 1.5 };
                dc.DrawEllipse(null, ring, c, r, r);
                return;
            }
            dc.DrawEllipse(main, null, c, r + 0.75, r + 0.75);
            var mark = new Pen { Brush = Brushes.White, Thickness = Math.Max(1.4, s / 9), StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            double u = r * 0.5;
            if (g == ScoreBands.GradeGood)
            {
                dc.DrawLine(mark, new Point(c.X - u, c.Y), new Point(c.X - u * 0.25, c.Y + u * 0.7));
                dc.DrawLine(mark, new Point(c.X - u * 0.25, c.Y + u * 0.7), new Point(c.X + u, c.Y - u * 0.6));
            }
            else if (g == ScoreBands.GradeWarn)
            {
                dc.DrawLine(mark, new Point(c.X, c.Y - u), new Point(c.X, c.Y + u * 0.25));
                dc.DrawLine(mark, new Point(c.X, c.Y + u * 0.8), new Point(c.X, c.Y + u * 0.85));
            }
            else
            {
                dc.DrawLine(mark, new Point(c.X - u * 0.75, c.Y - u * 0.75), new Point(c.X + u * 0.75, c.Y + u * 0.75));
                dc.DrawLine(mark, new Point(c.X - u * 0.75, c.Y + u * 0.75), new Point(c.X + u * 0.75, c.Y - u * 0.75));
            }
        }
    }

    /// <summary>오차율 막대 — 진한 = 시간 오차율, 옅은 = 크기 반영 오차율 (0 ~ 100).</summary>
    public sealed class RateBar : DashVisual
    {
        public static readonly DependencyProperty TimeRateProperty = DependencyProperty.Register(
            "TimeRate", typeof(double), typeof(RateBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty WeightedRateProperty = DependencyProperty.Register(
            "WeightedRate", typeof(double), typeof(RateBar), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty GradeProperty = DependencyProperty.Register(
            "Grade", typeof(string), typeof(RateBar), new FrameworkPropertyMetadata(ScoreBands.GradeWarn, FrameworkPropertyMetadataOptions.AffectsRender));

        public double TimeRate { get { return (double)GetValue(TimeRateProperty); } set { SetValue(TimeRateProperty, value); } }
        public double WeightedRate { get { return (double)GetValue(WeightedRateProperty); } set { SetValue(WeightedRateProperty, value); } }
        public string Grade { get { return (string)GetValue(GradeProperty); } set { SetValue(GradeProperty, value); } }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 4 || h < 2) return;
            double y = (h - 8) / 2;
            dc.DrawRectangle(new SolidColorBrush(ThemeManager.Palette.Grid), null, new Rect(0, y, w, 8));
            double ww = w * Math.Max(0, Math.Min(100, WeightedRate)) / 100.0;
            double tw = w * Math.Max(0, Math.Min(100, TimeRate)) / 100.0;
            if (ww > 0) dc.DrawRectangle(GradeBrush(Grade, true), new Pen { Brush = GradeBrush(Grade, false), Thickness = 0.5 }, new Rect(0, y, Math.Max(2, ww), 8));
            if (tw > 0) dc.DrawRectangle(GradeBrush(Grade, false), null, new Rect(0, y, Math.Max(2, tw), 8));
        }
    }
}
