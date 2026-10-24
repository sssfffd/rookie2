using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.Analysis1.Compare;
using LogScope.Core.Model;
using LogScope.Analysis1.Render;
using LogScope.App.Themes;

namespace LogScope.App.Analysis1.Graph
{
    /// <summary>
    /// 그래프 판 — <b>한 판을 그리는 큰 틀</b>. 레인을 몇 개 둘지 정하고
    /// 차례로 그립니다. 범례, 네모 확대 표시, 빈 화면 안내도 여기입니다.
    ///
    /// 한 클래스를 파일 다섯으로 나눈 것입니다 (partial). 2000 줄 한 파일로
    /// 두면 눈금 글자 하나 고치려고 그리기·마우스·확대를 다 지나가야 합니다.
    /// 나누는 금은 "무엇을 하는 코드인가" 입니다 — 틀 · 레인 · 선 · 눈금 ·
    /// 마우스.
    /// </summary>
    public sealed partial class GraphCanvas
    {

        // ---------------- 그리기 ----------------

        protected override void OnRender(DrawingContext dc)
        {
            Palette p = ThemeManager.Palette;

            // 바탕을 칠해 두어야 이 요소가 마우스를 받습니다.
            dc.DrawRectangle(Frozen(p.Window), null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (!_timeInit && _state != null)
            {
                _state.FullTimeRange(out _t0, out _t1);
                _timeInit = true;
            }

            if (_channels.Count == 0)
            {
                DrawEmpty(dc, p);
                UpdateScrollInfo(0, 1);
                return;
            }

            Rect plot = PlotArea;
            int lanes = _laneMode ? _channels.Count : 1;
            double laneH = _laneMode ? Math.Max(MinLaneH, plot.Height / Math.Max(1, lanes)) : plot.Height;
            double needed = _laneMode ? laneH * lanes : plot.Height;

            UpdateScrollInfo(needed, plot.Height);
            double scrollY = needed > plot.Height ? VerticalOffset : 0;

            RefreshPixelsPerDip();
            int columns = (int)Math.Max(1, Math.Floor(plot.Width * _pixelsPerDip));
            EnsureBuffers(columns);

            // 레인은 plot.Top 부터 쌓습니다 — 단계로 나눠 보면 그 위에 칸
            // 머리 줄(StepHeadH)이 있습니다 (1.16).
            dc.PushClip(new RectangleGeometry(new Rect(0, plot.Top, ActualWidth, plot.Height)));
            if (_laneMode)
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    double top = plot.Top + i * laneH - scrollY;
                    if (top + laneH < plot.Top || top > plot.Bottom) continue;
                    var rect = new Rect(plot.Left, top, plot.Width, laneH);
                    DrawLane(dc, p, rect, new[] { _channels[i] }, _channels[i].Name, columns);
                }
            }
            else
            {
                var rect = new Rect(plot.Left, plot.Top, plot.Width, plot.Height);
                DrawLane(dc, p, rect, _channels.ToArray(), OverlayLaneKey, columns);
            }
            dc.Pop();

            if (!Diffing && !_showBefore && !_showAfter) DrawBothOff(dc, p, plot);

            DrawTimeAxis(dc, p, plot);
            if (StepMode) DrawStepHead(dc, p, plot);
            DrawCursor(dc, p, plot, _cursorA, p.CursorAPen, p.CursorA, "A");
            DrawCursor(dc, p, plot, _cursorB, p.CursorBPen, p.CursorB, "B");
            DrawLegend(dc, p, plot);
            DrawBand(dc, p, plot);
        }

        /// <summary>
        /// 어느 선이 이전이고 어느 선이 이후인지.
        ///
        /// 색만 다르게 그려 놓고 어디에도 적어 두지 않으면, 두 선 중 무엇이
        /// 무엇인지 알 수가 없습니다. 도구 줄의 글자로 적어 두는 것으로는
        /// 부족합니다 — 보고 있는 자리에서 멀고, 겹쳐보기에서는 IO 마다 색이
        /// 달라 설명이 성립하지 않습니다.
        ///
        /// 그래서 <b>그 화면에서 실제로 쓰는 선을 그대로</b> 보여 줍니다.
        ///   레인   : 파란 선 = 이전, 빨간 선 = 이후 (어느 IO 든 같습니다)
        ///   겹쳐보기 : 첫 IO 의 두 선. IO 마다 색이 짝을 이룹니다
        ///   차이 눈금 : 선이 하나뿐이라 그 하나만
        ///
        /// 자리는 <b>그림 오른쪽 위</b>, 레인 머리글 아래입니다. 0.60 에 왼쪽
        /// IO 칸에도 같은 설명을 뒀다가 0.67 에 뺐습니다 — 같은 말이 두
        /// 군데에 적혀 있었고, 왼쪽 칸이 그만큼 좁아졌습니다. 그림 위의 글은
        /// 선에 가려질 수 있어서 판을 살짝 투명하게 깔아 둡니다.
        /// </summary>
        private void DrawLegend(DrawingContext dc, Palette p, Rect plot)
        {
            if (_channels.Count == 0) return;

            Pen firstPen, secondPen;
            string firstText, secondText, note;

            if (Diffing)
            {
                firstPen = p.DiffPen; firstText = "차이 (이후 − 이전)";
                secondPen = null; secondText = null;
                note = null;
            }
            else if (!_showBefore && !_showAfter)
            {
                return;   // 그릴 선이 없습니다. 아래 안내가 대신 적힙니다.
            }
            else
            {
                Pen bPen = ColorByIo ? p.SeriesPen(0) : p.BeforePen;
                Pen aPen = ColorByIo ? PairPen(p, 0) : p.AfterPen;
                note = ColorByIo ? "IO 마다 색이 짝을 이룹니다" : null;

                // 색이 이전/이후를 가리킬 때만 색 이름을 적습니다.
                //
                // 겹쳐 보기에서 IO 가 여럿이면 색이 IO 를 가리키므로
                // "파란 선 = 이전" 이 거짓이 됩니다. 그때는 색 이름을 떼고
                // 옆의 선 조각으로만 보여 줍니다 — 그 조각은 그 화면에서
                // 실제로 쓰는 펜이라 틀릴 수가 없습니다.
                string before = ColorByIo ? "이전" : "파란 선 = 이전";
                string after = ColorByIo ? "이후" : "빨간 선 = 이후";

                // 꺼 둔 쪽은 적지 않습니다. 없는 선을 설명하면 그게 어디
                // 있는지 찾게 됩니다.
                if (_showBefore && _showAfter)
                {
                    firstPen = bPen; firstText = before;
                    secondPen = aPen; secondText = after;
                }
                else if (_showBefore)
                {
                    firstPen = bPen; firstText = before + " (이것만 보기)";
                    secondPen = null; secondText = null;
                }
                else
                {
                    firstPen = aPen; firstText = after + " (이것만 보기)";
                    secondPen = null; secondText = null;
                }
            }

            const double sample = 18, gap = 6, padX = 8, padY = 5, lineH = 16;

            FormattedText t1 = Text(firstText, FontSmall, p.TextBrush);
            FormattedText t2 = secondText != null ? Text(secondText, FontSmall, p.TextBrush) : null;
            FormattedText t3 = note != null ? Text(note, FontSmall, p.MutedBrush) : null;

            double textW = t1.Width;
            if (t2 != null) textW = Math.Max(textW, t2.Width);
            double w = sample + gap + textW + padX * 2;
            if (t3 != null) w = Math.Max(w, t3.Width + padX * 2);

            int rows = 1 + (t2 != null ? 1 : 0);
            double h = rows * lineH + (t3 != null ? lineH : 0) + padY * 2;

            // 오른쪽 위, 레인 머리글 아래. 머리글 오른쪽에는 커서 값이
            // 적히므로 그 줄을 비켜야 합니다.
            double right = plot.Right - 6, top = plot.Top + HeaderH + 6;
            var box = new Rect(right - w, top, w, h);
            if (box.Left < plot.Left || box.Bottom > plot.Bottom) return;   // 너무 좁으면 접습니다

            // 살짝 투명하게. 뒤에 선이 지나가도 아주 가려 버리지는 않습니다.
            var back = new SolidColorBrush(p.Panel);
            back.Opacity = 0.88;
            back.Freeze();
            dc.DrawRectangle(back, p.BorderPen, box);

            double y = box.Top + padY + lineH * 0.5;
            double x0 = box.Left + padX, x1 = x0 + sample;

            dc.DrawLine(firstPen, new Point(x0, Snap(y)), new Point(x1, Snap(y)));
            dc.DrawText(t1, new Point(x1 + gap, y - t1.Height * 0.5));

            if (t2 != null)
            {
                y += lineH;
                dc.DrawLine(secondPen, new Point(x0, Snap(y)), new Point(x1, Snap(y)));
                dc.DrawText(t2, new Point(x1 + gap, y - t2.Height * 0.5));
            }

            if (t3 != null)
            {
                y += lineH;
                dc.DrawText(t3, new Point(x0, y - t3.Height * 0.5));
            }
        }

        /// <summary>
        /// 이전·이후를 둘 다 꺼 두었을 때. 말없이 비워 두면 고장으로 보입니다.
        /// </summary>
        private void DrawBothOff(DrawingContext dc, Palette p, Rect plot)
        {
            FormattedText ft = Text("이전 선과 이후 선을 둘 다 꺼 두었습니다.\n왼쪽 IO 칸의 [그릴 로그] 에서 하나를 켜 주세요.",
                                    FontNormal, p.MutedBrush);
            dc.DrawText(ft, new Point(plot.Left + Math.Max(0, (plot.Width - ft.Width) * 0.5),
                                      plot.Top + Math.Max(0, (plot.Height - ft.Height) * 0.5)));
        }

        /// <summary>오른쪽 단추로 끌고 있는 동안의 네모.</summary>
        private void DrawBand(DrawingContext dc, Palette p, Rect plot)
        {
            if (!_band) return;

            Rect r = BandRect(plot);
            if (r.Width < 1 && r.Height < 1) return;

            var fill = new SolidColorBrush(p.Accent);
            fill.Opacity = 0.16;
            fill.Freeze();
            dc.DrawRectangle(fill, p.CursorAPen, r);
        }

        private Rect BandRect(Rect plot)
        {
            double x0 = Math.Min(_bandFrom.X, _bandTo.X), x1 = Math.Max(_bandFrom.X, _bandTo.X);
            double y0 = Math.Min(_bandFrom.Y, _bandTo.Y), y1 = Math.Max(_bandFrom.Y, _bandTo.Y);

            x0 = Clamp(x0, plot.Left, plot.Right);
            x1 = Clamp(x1, plot.Left, plot.Right);
            y0 = Clamp(y0, plot.Top, plot.Bottom);
            y1 = Clamp(y1, plot.Top, plot.Bottom);
            return new Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private void UpdateScrollInfo(double extent, double viewport)
        {
            double max = Math.Max(0, extent - viewport);
            if (Math.Abs(ScrollMaximum - max) > 0.5) ScrollMaximum = max;
            if (Math.Abs(ScrollViewport - viewport) > 0.5) ScrollViewport = Math.Max(1, viewport);
            if (VerticalOffset > max) VerticalOffset = max;
        }

        private void DrawEmpty(DrawingContext dc, Palette p)
        {
            string msg = (_state != null && _state.HasAny)
                ? "왼쪽 목록에서 IO 를 골라 주세요."
                : "위쪽 메뉴의 '이전 로그 열기' / '이후 로그 열기' 로 파일을 열어 주세요.";
            FormattedText ft = Text(msg, FontNormal, p.MutedBrush);
            ft.MaxTextWidth = Math.Max(100, ActualWidth - 48);
            dc.DrawText(ft, new Point(24, 24));
        }

        private void EnsureBuffers(int columns)
        {
            if (_colsBefore.Length < columns)
            {
                _colsBefore = new Decimator.Column[columns];
                _colsAfter = new Decimator.Column[columns];
                _bandBefore = new float[columns];
                _bandAfter = new float[columns];
                _colsDiff = new Decimator.Column[columns];
                // 두 로그의 격자를 합쳐 훑으므로 표본이 열 수보다 많을 수
                // 있습니다. 넉넉히 두고, 그래도 넘치면 접은 쪽으로 넘어갑니다.
                _diffT = new double[columns * 2 + 8];
                _diffV = new float[columns * 2 + 8];
            }
        }

        /// <summary>열 번호를 화면 가로 좌표(DIP)로.</summary>
        private double ColumnToX(Rect inner, int column)
        {
            return inner.Left + column / _pixelsPerDip;
        }

        /// <summary>
        /// 1 픽셀짜리 선을 장치 픽셀 한가운데로 맞춥니다.
        /// 예전에는 요소 전체를 EdgeMode.Aliased 로 두어 눈금선을 또렷하게
        /// 했는데, 그러면 비스듬한 파형까지 계단처럼 보였습니다. 지금은 파형은
        /// 부드럽게 두고 눈금선만 이 함수로 맞춥니다.
        /// </summary>
        private double Snap(double v)
        {
            return (Math.Round(v * _pixelsPerDip) + 0.5) / _pixelsPerDip;
        }

        /// <summary>시각을 화면 가로 좌표(DIP)로. t0/t1 은 그 로그의 시간 기준입니다.</summary>
        private static double TimeToXIn(Rect inner, double t, double t0, double t1)
        {
            double span = t1 - t0;
            if (!(span > 0)) return inner.Left;
            return inner.Left + (t - t0) / span * inner.Width;
        }

        private FormattedText Text(string s, double size, Brush brush)
        {
            return new FormattedText(s ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, _face, size, brush);
        }
    }
}
