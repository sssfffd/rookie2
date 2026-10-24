using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.Analysis1.LogFile;
using LogScope.Common;
using LogScope.Themes;

namespace LogScope.Analysis1.Graph
{
    /// <summary>
    /// 그래프 판 — <b>눈금</b>. 세로 눈금(값)과 가로 눈금(시간), 그리고
    /// 커서 줄입니다.
    ///
    /// 눈금 글자는 선을 그을 때 쓰는 것과 똑같은 ValueToY/TimeToX 를
    /// 거칩니다. 그래서 숫자와 그림이 어긋날 수 없습니다.
    /// </summary>
    public sealed partial class GraphCanvas
    {

        // ---------------- 눈금 ----------------

        /// <summary>
        /// 세로 눈금.
        ///
        /// 눈금 글자는 선을 그은 바로 그 y 에 붙입니다. 그래서 숫자와 그래프
        /// 높이가 어긋날 수 없습니다. (요구사항 2번)
        ///
        /// 채널 종류에 따라 눈금을 다르게 답니다.
        ///  - 상태 채널: 숫자 대신 <b>상태 이름</b>. 값이 상태 표의 번호라서
        ///    0, 1, 2 를 적어 봐야 아무 뜻이 없습니다.
        ///  - 디지털 채널: 0 과 1 에만. 0.5 같은 눈금은 있을 수 없는 값입니다.
        ///  - 그 밖: 보기 좋은 간격으로 숫자.
        /// </summary>
        private void DrawValueAxis(DrawingContext dc, Palette p, Rect lane, Rect inner,
                                   double vlo, double vhi, IoRowVm[] ios)
        {
            DrawAxisCaption(dc, p, lane, ios);

            Channel single = SingleChannel(ios);

            // 눈금이 하나도 안 걸릴 만큼 확대했을 수 있습니다. 그러면 눈금선이
            // 통째로 사라져 버리니, 아래의 보통 숫자 눈금으로 넘어갑니다.
            if (_scale == ValueScaleMode.Raw && single != null)
            {
                if (single.Kind == ChannelKind.State && single.States != null
                    && single.States.Length > 0 && single.States.Length <= 12)
                {
                    if (DrawStateTicks(dc, p, inner, vlo, vhi, single)) return;
                }
                else if (single.Kind == ChannelKind.Digital)
                {
                    int drawn = 0;
                    if (DrawTick(dc, p, inner, vlo, vhi, 0, "0")) drawn++;
                    if (DrawTick(dc, p, inner, vlo, vhi, 1, "1")) drawn++;
                    if (drawn > 0) return;
                }
            }

            double step = NiceStep(vhi - vlo, Math.Max(2, (int)(inner.Height / 34)));
            if (!(step > 0)) return;

            double first = Math.Ceiling(vlo / step) * step;
            for (double v = first; v <= vhi + step * 0.001; v += step)
                DrawTick(dc, p, inner, vlo, vhi, v, FormatTick(v, step));
        }

        /// <summary>레인에 채널이 하나일 때 그 채널. 겹쳐보기면 null.</summary>
        private Channel SingleChannel(IoRowVm[] ios)
        {
            if (_state == null || ios == null || ios.Length != 1) return null;
            IoRowVm vm = ios[0];
            if (vm.InBefore && _state.Before != null) return _state.Before.Channels[vm.BeforeIndex];
            if (vm.InAfter && _state.After != null) return _state.After.Channels[vm.AfterIndex];
            return null;
        }

        /// <summary>눈금 하나. 보이는 자리가 아니면 그리지 않고 거짓을 돌려줍니다.</summary>
        private bool DrawTick(DrawingContext dc, Palette p, Rect inner,
                              double vlo, double vhi, double value, string label)
        {
            double y = ValueToY(value, inner, vlo, vhi);
            if (y < inner.Top - 1 || y > inner.Bottom + 1) return false;

            double gy = Snap(y);
            dc.DrawLine(p.GridPen, new Point(inner.Left + 1, gy), new Point(inner.Right - 2, gy));

            FormattedText ft = Text(label, FontSmall, p.MutedBrush);
            ft.MaxTextWidth = GutterW - 10;
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            // 글자가 레인 밖으로 삐져나가지 않게 가둡니다.
            double ty = gy - ft.Height * 0.5;
            if (ty < inner.Top) ty = inner.Top;
            if (ty + ft.Height > inner.Bottom) ty = inner.Bottom - ft.Height;

            dc.DrawText(ft, new Point(Math.Max(2, GutterW - 8 - ft.Width), ty));
            return true;
        }

        /// <summary>상태 이름을 눈금 글자로. 하나라도 그렸으면 참.</summary>
        private bool DrawStateTicks(DrawingContext dc, Palette p, Rect inner,
                                    double vlo, double vhi, Channel c)
        {
            int drawn = 0;
            for (int i = 0; i < c.States.Length; i++)
                if (DrawTick(dc, p, inner, vlo, vhi, i, c.States[i])) drawn++;
            return drawn > 0;
        }

        /// <summary>
        /// 세로축이 무엇을 뜻하는지 레인 왼쪽 위에 적습니다.
        /// 단위가 있으면 단위를, 눈금 모드가 기존 값이 아니면 그 모드를 적습니다.
        /// 숫자만 늘어놓으면 무슨 값인지 알 수 없어서 붙였습니다.
        /// </summary>
        private void DrawAxisCaption(DrawingContext dc, Palette p, Rect lane, IoRowVm[] ios)
        {
            string caption = AxisCaption(ios);
            if (caption.Length == 0) return;

            FormattedText ft = Text(caption, FontSmall, p.MutedBrush);
            ft.MaxTextWidth = GutterW - 10;
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            // 눈금 숫자와 오른쪽 끝을 맞춥니다. 레인 머리글(채널 이름)은 이
            // 자리보다 오른쪽, 눈금 글자는 이 자리보다 아래라 겹치지 않습니다.
            dc.DrawText(ft, new Point(Math.Max(2, GutterW - 8 - ft.Width), lane.Y + 4));
        }

        private string AxisCaption(IoRowVm[] ios)
        {
            switch (_scale)
            {
                case ValueScaleMode.Delta:
                    {
                        // Δ 는 "이후 − 이전" 이라는 뜻입니다. 눈금 칸이 좁아서
                        // 그걸 다 적을 수 없으니 기호로 줄이고, 자세한 것은
                        // 도구 줄의 풍선 도움말이 말해 줍니다.
                        string u = UnitOf(ios);
                        return u.Length > 0 ? "Δ " + u : "차이";
                    }
                default:
                    {
                        string u = UnitOf(ios);
                        if (u.Length > 0) return u;
                        Channel single = SingleChannel(ios);
                        if (single != null && single.Kind == ChannelKind.State) return "상태";
                        return ios != null && ios.Length > 1 ? "여러 채널" : string.Empty;
                    }
            }
        }

        /// <summary>
        /// 레인의 단위. 여러 채널이 한 눈금을 쓰는데 단위가 서로 다르면,
        /// 어느 하나를 적으면 거짓말이 되므로 아무것도 적지 않습니다.
        /// </summary>
        private string UnitOf(IoRowVm[] ios)
        {
            if (_state == null || ios == null || ios.Length == 0) return string.Empty;

            string unit = null;
            for (int k = 0; k < ios.Length; k++)
            {
                IoRowVm vm = ios[k];
                string u = string.Empty;
                if (vm.InBefore && _state.Before != null) u = _state.Before.Channels[vm.BeforeIndex].Unit;
                if (u.Length == 0 && vm.InAfter && _state.After != null) u = _state.After.Channels[vm.AfterIndex].Unit;
                if (u.Length == 0) continue;

                if (unit == null) unit = u;
                else if (!string.Equals(unit, u, StringComparison.Ordinal)) return string.Empty;
            }
            return unit ?? string.Empty;
        }

        private static string FormatTick(double v, double step)
        {
            if (Math.Abs(v) < step * 1e-9) v = 0;

            // 눈금 간격에 맞춰 소수점 자리를 정합니다. 지수 표기로는 절대
            // 바뀌지 않습니다 — 눈금에 "1.2e+06" 이 적히면 읽을 수가 없습니다.
            double a = Math.Abs(v);
            if (a >= 1e6) return NumberText.Plain(v);
            if (a != 0 && a < 1e-3) return NumberText.Plain(v);

            int dec = 0;
            double st = Math.Abs(step);
            while (st < 1 && dec < 6) { st *= 10; dec++; }
            return v.ToString("N" + dec);
        }

        private static double NiceStep(double span, int wanted)
        {
            if (!(span > 0) || wanted < 1) return 0;
            double raw = span / wanted;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double nice = norm <= 1 ? 1 : (norm <= 2 ? 2 : (norm <= 5 ? 5 : 10));
            return nice * mag;
        }

        private void DrawTimeAxis(DrawingContext dc, Palette p, Rect plot)
        {
            var strip = new Rect(0, plot.Bottom, ActualWidth, AxisH);
            dc.DrawRectangle(p.AxisStripBrush, null, strip);
            dc.DrawLine(p.BorderPen, new Point(0, strip.Top), new Point(strip.Right, strip.Top));

            if (StepMode) { DrawStepAxis(dc, p, plot, strip); return; }

            LogDataset refDs = _state != null ? _state.TimeReference : null;
            double span = _t1 - _t0;
            if (!(span > 0)) return;

            int wanted = Math.Max(2, (int)(plot.Width / 110));
            double step = NiceStep(span, wanted);
            if (!(step > 0)) return;

            double first = Math.Ceiling(_t0 / step) * step;
            for (double t = first; t <= _t1; t += step)
            {
                double x = TimeToX(t);
                if (x < plot.Left || x > plot.Right) continue;

                double gx = Snap(x);
                dc.DrawLine(p.GridPen, new Point(gx, plot.Top), new Point(gx, plot.Bottom));
                dc.DrawLine(p.BorderPen, new Point(gx, strip.Top), new Point(gx, strip.Top + 4));

                string label = TimeLabel(refDs, t);
                FormattedText ft = Text(label, FontSmall, p.MutedBrush);
                dc.DrawText(ft, new Point(x - ft.Width * 0.5, strip.Top + 5));
            }
        }

        /// <summary>
        /// 단계별로 나눠 볼 때의 가로 눈금. 칸마다 <b>단계 이름</b>을 적고,
        /// 칸이 넓으면 그 아래에 구간도 적습니다.
        ///
        /// 시각 눈금을 칸마다 몇 개씩 적으면 글자가 서로 붙어 버립니다.
        /// 단계로 나눠 본다는 것은 "몇 시 몇 분" 이 아니라 "어느 단계" 로
        /// 보겠다는 뜻이라, 이름이 먼저입니다.
        /// </summary>
        private void DrawStepAxis(DrawingContext dc, Palette p, Rect plot, Rect strip)
        {
            double[] xs, ws;
            StepLayout(plot, out xs, out ws);
            LogDataset refDs = _state != null ? _state.TimeReference : null;

            dc.PushClip(new RectangleGeometry(strip));
            for (int i = 0; i < _stepBands.Count; i++)
            {
                double x = xs[i];
                double width = ws[i];

                // 칸 밑에 테두리를 둘러 그림 쪽 칸과 같은 자리로 보이게 합니다.
                dc.DrawLine(p.BorderPen, new Point(Snap(x), strip.Top),
                                         new Point(Snap(x), strip.Top + 4));
                dc.DrawLine(p.BorderPen, new Point(Snap(x + width), strip.Top),
                                         new Point(Snap(x + width), strip.Top + 4));

                // 단계 이름은 <b>위</b> 칸 머리 줄에 적습니다 (1.16,
                // DrawStepHead). 여기 아래 줄에는 그 칸이 지금 보여 주는
                // 구간만 적습니다 — 칸을 확대하면 단계 전체가 아니라 그
                // 일부를 보고 있으니, 단계 구간을 적어 두면 거짓이 됩니다.
                double a0, a1;
                StepView(i, out a0, out a1);
                string when = refDs != null
                    ? refDs.FormatTime(a0) + " ~ " + refDs.FormatTime(a1)
                    : NumberText.Plain(a0) + " ~ " + NumberText.Plain(a1);
                FormattedText ft = Text(when, FontSmall, p.MutedBrush);
                if (ft.Width + 6 <= width)
                    dc.DrawText(ft, new Point(x + (width - ft.Width) * 0.5, strip.Top + 5));
            }
            dc.Pop();
        }

        /// <summary>
        /// 단계로 나눠 볼 때 그림 <b>위</b>의 칸 머리 줄 (1.16). 칸마다 단계
        /// 이름을 적고, <b>누르면 그 단계만</b> 봅니다 (다시 누르면 모든 칸).
        ///
        /// 1.15 까지는 이름이 아래 시간 줄에 있었습니다. 위에 있어야 칸을
        /// 내려다보기 전에 "이 칸이 어느 단계인가" 가 먼저 읽히고, 누르는
        /// 자리(머리)와 그림이 겹치지 않습니다.
        ///
        /// 한 단계만 보는 중이면 머리 끝에 "◀ 전체" 를 붙여, 다시 누르면
        /// 돌아간다는 것을 알려 줍니다.
        /// </summary>
        private void DrawStepHead(DrawingContext dc, Palette p, Rect plot)
        {
            var strip = new Rect(0, 0, ActualWidth, StepHeadH);
            dc.DrawRectangle(p.AxisStripBrush, null, strip);
            dc.DrawLine(p.BorderPen, new Point(0, StepHeadH - 0.5), new Point(ActualWidth, StepHeadH - 0.5));

            double[] xs, ws;
            StepLayout(plot, out xs, out ws);
            // 한 단계만 고른 상태인지 — 칸 수가 아니라 고른 것으로 봅니다
            // (단계가 원래 하나뿐인 로그에서 "◀ 전체" 가 뜨면 안 됩니다).
            bool single = _state != null && _state.StepPick >= 0;

            dc.PushClip(new RectangleGeometry(strip));
            for (int i = 0; i < _stepBands.Count; i++)
            {
                double x = xs[i], width = ws[i];
                var cell = new Rect(x, 2, Math.Max(1, width), StepHeadH - 5);
                dc.DrawRectangle(Frozen(p.Window), p.BorderPen, cell);

                string label = _stepBands[i].Label + (single ? "   ◀ 전체" : "");
                FormattedText name = Text(label, FontSmall, p.TextBrush);
                name.SetFontWeight(FontWeights.SemiBold);
                if (name.Width + 6 > width)
                {
                    // 칸이 좁으면 번호만. 이름을 잘라 적으면 옆 칸 글자와
                    // 붙어 한 낱말처럼 읽힙니다.
                    name = Text((i + 1).ToString(CultureInfo.InvariantCulture), FontSmall, p.TextBrush);
                    if (name.Width + 2 > width) continue;
                }
                dc.DrawText(name, new Point(x + (width - name.Width) * 0.5,
                                            cell.Top + (cell.Height - name.Height) * 0.5));
            }
            dc.Pop();
        }

        private void DrawCursor(DrawingContext dc, Palette p, Rect plot, double t, Pen pen, Color color, string tag)
        {
            if (double.IsNaN(t)) return;
            if (!StepMode && (t < _t0 || t > _t1)) return;

            double xx = TimeToX(t);
            // 단계별로 나눠 보는 중에는 칸 밖의 시각이 있습니다 (빈틈).
            // 그 커서는 그릴 자리가 없습니다.
            if (double.IsNaN(xx)) return;
            double x = Snap(xx);
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));

            var box = new Rect(x - 9, plot.Top + 1, 18, 15);
            dc.DrawRectangle(Frozen(color), null, box);

            FormattedText ft = Text(tag, FontSmall, Brushes.White);
            ft.SetFontWeight(FontWeights.Bold);
            dc.DrawText(ft, new Point(box.Left + (box.Width - ft.Width) * 0.5,
                                      box.Top + (box.Height - ft.Height) * 0.5));
        }
    }
}
