using System;
using System.Collections.Generic;
using LogScope.Core.Settings;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LogScope.App.Themes;
using LogScope.Logs.Compare;
using LogScope.Core.Model;
using LogScope.Logs.Align;
using LogScope.Logs.Render;
using LogScope.App.Common;
using LogScope.App.Settings;

namespace LogScope.App.Logs
{
    /// <summary>
    /// 그래프 판 — <b>레인 하나</b>. 그 레인에 들어갈 IO 들의 값 범위를
    /// 정하고, 머리글(이름·값·차이)을 적고, 선 그리기를 부릅니다.
    ///
    /// 값 범위를 정하는 셈이 여기 모여 있습니다. 세로 눈금 글자와 선의
    /// 높이가 어긋나지 않는 까닭은 둘이 같은 범위를 쓰기 때문입니다.
    /// </summary>
    public sealed partial class GraphCanvas
    {

        /// <summary>레인 하나(겹쳐보기면 전체)를 그립니다.</summary>
        private void DrawLane(DrawingContext dc, Palette p, Rect rect, IoRowVm[] ios, string laneKey, int columns)
        {
            // 칸으로 나눠 그릴 때는 레인 바탕을 <b>창 색</b>으로 둡니다.
            // 그러면 칸 사이의 틈이 그 색으로 비어서 칸이 따로따로 보입니다.
            // 그림 색으로 두면 틈과 칸이 같은 색이라 띄운 뜻이 사라집니다.
            dc.DrawRectangle(StepMode ? Frozen(p.Window) : p.PlotBrush, p.BorderPen, rect);

            var inner = new Rect(rect.X, rect.Y + HeaderH, rect.Width,
                                 Math.Max(8, rect.Height - HeaderH - 4));

            double lo, hi;
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);

            LaneY ly = LaneFor(laneKey);
            double span = (hi - lo) / ly.Zoom;
            if (!(span > 0)) span = 1;
            double center = CenterOf(ly, lo, hi, usable);
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            DrawValueAxis(dc, p, rect, inner, vlo, vhi, ios);

            if (StepMode) DrawLaneInBands(dc, p, inner, ios, vlo, vhi);
            else
            {
                for (int k = 0; k < ios.Length; k++)
                {
                    Pen before = ColorByIo ? p.SeriesPen(k) : p.BeforePen;
                    Pen after = ColorByIo ? PairPen(p, k) : p.AfterPen;
                    DrawChannel(dc, p, inner, ios[k], vlo, vhi, before, after, columns);
                }
            }

            DrawLaneHeader(dc, p, rect, ios);
        }

        /// <summary>
        /// 레인 하나를 <b>단계 칸으로 나눠</b> 그립니다.
        ///
        /// 칸마다 보이는 구간(_t0·_t1)과 밀기 값(_stepShift)을 바꿔 끼우고,
        /// 자리를 그 칸만큼 좁혀서 평소에 쓰는 그리기 함수를 그대로 부릅니다.
        /// 세로 눈금은 레인에 하나뿐이라 <b>칸끼리 높이를 그대로 견줄 수
        /// 있습니다</b> (칸마다 세로를 다시 맞추면 같은 값이 칸마다 다른
        /// 높이로 그려집니다).
        ///
        /// 칸 사이에는 금을 하나 긋습니다. 금이 없으면 두 칸의 선이 이어진
        /// 하나로 보이는데, 칸 사이는 시간이 <b>건너뛴</b> 자리입니다.
        /// </summary>
        private void DrawLaneInBands(DrawingContext dc, Palette p, Rect inner, IoRowVm[] ios,
                                     double vlo, double vhi)
        {
            double[] xs, ws;
            StepLayout(new Rect(inner.X, inner.Y, inner.Width, inner.Height), out xs, out ws);

            double saveT0 = _t0, saveT1 = _t1;

            for (int bi = 0; bi < _stepBands.Count; bi++)
            {
                var sub = new Rect(xs[bi], inner.Y, Math.Max(1, ws[bi]), inner.Height);

                // 칸이 선 한 줄만큼도 안 되면 그리지 않습니다 (그려도 안
                // 보이는데 셈은 다 합니다).
                if (sub.Width < 2) continue;

                // 칸을 하나의 판처럼 그립니다 — 바탕을 칠하고 테두리를
                // 두릅니다. 칸 사이의 틈은 레인 바탕이 그대로 보여서
                // "여기는 시간이 건너뛴 자리" 가 눈에 들어옵니다.
                dc.DrawRectangle(p.PlotBrush, p.BorderPen, sub);

                StepView(bi, out _t0, out _t1);
                _stepShift = _stepBands[bi].Shift;

                int cols = (int)Math.Max(1, Math.Floor(sub.Width * _pixelsPerDip));

                // 두 로그 단계 길이가 다르면(1.18) — 둘 다 있는 자리까지는
                // 평소대로, 그 뒤 꼬리는 <b>긴 쪽 로그만</b> 그립니다. 짧은
                // 쪽은 제 단계 끝에서 선이 끊깁니다. 안 끊으면 그 꼬리에
                // 짧은 쪽의 다음 단계 값이 이어져 그려집니다.
                StepBand band = _stepBands[bi];
                double xB = BandX(sub, band.BeforeEnd), xA = BandX(sub, band.AfterEnd);
                double xBoth = Math.Min(xB, xA);

                DrawBandPart(dc, p, sub, sub.Left, xBoth, ios, vlo, vhi, cols, 0);
                if (xB > xBoth) DrawBandPart(dc, p, sub, xBoth, xB, ios, vlo, vhi, cols, 1);
                if (xA > xBoth) DrawBandPart(dc, p, sub, xBoth, xA, ios, vlo, vhi, cols, 2);
            }

            _t0 = saveT0;
            _t1 = saveT1;
            _stepShift = double.NaN;
        }

        /// <summary>
        /// 단계 칸 하나에서 [x0, x1] 사이만 그립니다. side: 0 = 두 로그 다,
        /// 1 = 이전만, 2 = 이후만 (_sideOnly 를 그동안만 바꿔 끼웁니다).
        /// </summary>
        private void DrawBandPart(DrawingContext dc, Palette p, Rect sub, double x0, double x1,
                                  IoRowVm[] ios, double vlo, double vhi, int cols, int side)
        {
            if (!(x1 > x0)) return;
            _sideOnly = side;
            dc.PushClip(new RectangleGeometry(new Rect(x0, sub.Top, x1 - x0, sub.Height)));
            for (int k = 0; k < ios.Length; k++)
            {
                Pen before = ColorByIo ? p.SeriesPen(k) : p.BeforePen;
                Pen after = ColorByIo ? PairPen(p, k) : p.AfterPen;
                DrawChannel(dc, p, sub, ios[k], vlo, vhi, before, after, cols);
            }
            dc.Pop();
            _sideOnly = 0;
        }

        /// <summary>단계 칸 안에서 그 시각의 x (칸 밖이면 칸 끝으로 가둠).</summary>
        private double BandX(Rect sub, double t)
        {
            if (double.IsNaN(t)) return sub.Right;
            double span = _t1 - _t0;
            if (!(span > 0)) return sub.Right;
            double x = sub.Left + (t - _t0) * sub.Width / span;
            return Math.Max(sub.Left, Math.Min(sub.Right, x));
        }

        /// <summary>
        /// 이 레인의 세로 가운데. <b>기준 범위 밖으로 나가지 못하게 가둡니다.</b>
        ///
        /// 가운데 값은 끌기와 Shift+휠, [구간 맞춤] 이 적어 두는 절대값입니다.
        /// 그런데 적어 두는 그 순간에 쓸 만한 범위가 없었으면(레인/겹쳐보기를
        /// 오가는 사이, 한쪽 로그에만 있는 IO, 겹치는 구간이 없는 차이 눈금 …)
        /// 0~1 이라는 가짜 범위에서 나온 값이 적힙니다. 그러면 그 뒤로 값이
        /// 제대로 돌아와도 눈금은 0 언저리에 붙박이고, 선은 화면 밖에 있어
        /// <b>IO 값이 전부 0 으로 고정된 것처럼</b> 보입니다.
        ///
        /// 가두면 그런 자리가 아예 생기지 않습니다. 적어 둔 값이 범위 밖이면
        /// 가장 가까운 끝으로 끌어당겨, 데이터가 언제나 화면 안에 들어옵니다.
        /// </summary>
        private static double CenterOf(LaneY ly, double lo, double hi, bool usable)
        {
            double mid = (lo + hi) * 0.5;
            if (double.IsNaN(ly.Center)) return mid;

            // 쓸 만한 범위가 없으면 적어 둔 값도 믿을 수 없습니다.
            if (!usable) return mid;

            if (ly.Center < lo) return lo;
            if (ly.Center > hi) return hi;
            return ly.Center;
        }

        private readonly Dictionary<int, Pen> _pairPens = new Dictionary<int, Pen>();

        /// <summary>겹쳐보기에서 "이후" 쪽 선. 같은 계열의 다른 밝기로 구분합니다.</summary>
        /// <summary>
        /// 색이 <b>IO 를 가리키는지</b> (참) 아니면 <b>이전/이후를 가리키는지</b>
        /// (거짓).
        ///
        /// 레인으로 나눠 보면 IO 마다 칸이 따로 있으니 색으로 IO 를 가릴
        /// 필요가 없습니다 — 그때 색은 이전(파랑) / 이후(빨강)입니다.
        /// 겹쳐 보기에서 IO 가 여럿이면 거꾸로입니다. 모두 파랑·빨강으로
        /// 그리면 어느 선이 어느 IO 인지 알 수 없어서, 색이 IO 를 가리키고
        /// 이전/이후는 같은 색의 짙고 옅음으로 나뉩니다.
        ///
        /// <b>겹쳐 보기라도 IO 가 하나면 거짓입니다.</b> 가릴 IO 가 없으니
        /// 파랑·빨강이 더 잘 보입니다.
        ///
        /// 범례와 선 그리기가 <b>같은 답</b>을 봐야 합니다. 두 곳에 따로
        /// 적어 두면 범례가 "파랑은 이전" 이라고 하면서 선은 다른 색인 일이
        /// 생깁니다.
        /// </summary>
        private bool ColorByIo
        {
            get { return TraceColors.ByIo(_laneMode, _channels.Count); }
        }

        private Pen PairPen(Palette p, int index)
        {
            Pen pen;
            if (_pairPens.TryGetValue(index, out pen)) return pen;

            Color c = p.SeriesColor(index);
            Color paired = p.IsDark
                ? Color.FromRgb(Mix(c.R, 255), Mix(c.G, 255), Mix(c.B, 255))
                : Color.FromRgb((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55));

            var brush = new SolidColorBrush(paired);
            brush.Freeze();
            pen = new Pen(brush, 1.4);
            pen.Freeze();
            _pairPens[index] = pen;
            return pen;
        }

        private static byte Mix(byte a, byte b)
        {
            return (byte)((a + b) / 2);
        }

        private void DrawLaneHeader(DrawingContext dc, Palette p, Rect rect, IoRowVm[] ios)
        {
            double x = rect.X + 7;
            double y = rect.Y + 3;
            double right = rect.Right - 8;

            if (ios.Length == 1)
            {
                IoRowVm vm = ios[0];
                string extra = string.Empty;
                if (!vm.InAfter && _state != null && _state.After != null) extra = "   (이후 로그에 없음)";
                else if (!vm.InBefore && _state != null && _state.Before != null) extra = "   (이전 로그에 없음)";

                FormattedText name = Text(vm.Name + extra, FontNormal, p.TextBrush);
                name.SetFontWeight(FontWeights.SemiBold);
                name.MaxTextWidth = Math.Max(40, right - x - 210);
                name.MaxLineCount = 1;
                name.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(name, new Point(x, y));

                string readout = ValueReadout(vm);
                if (readout.Length > 0)
                {
                    FormattedText ft = Text(readout, FontSmall, p.MutedBrush);
                    ft.MaxLineCount = 1;
                    double rx = Math.Max(x + name.Width + 12, right - ft.Width);
                    if (rx + ft.Width <= right + 1) dc.DrawText(ft, new Point(rx, y + 1));
                }
                return;
            }

            // 겹쳐보기: 색 표시와 이름을 나란히
            for (int k = 0; k < ios.Length && x < right - 60; k++)
            {
                var swatch = new Rect(x, y + 4, 11, 8);
                dc.DrawRectangle(Frozen(p.SeriesColor(k)), null, swatch);
                x += 15;

                FormattedText ft = Text(ios[k].Name, FontSmall, p.TextBrush);
                ft.MaxLineCount = 1;
                dc.DrawText(ft, new Point(x, y + 1));
                x += ft.Width + 14;
            }
        }

        private string ValueReadout(IoRowVm vm)
        {
            if (double.IsNaN(_cursorA) || _state == null) return string.Empty;
            string s = string.Empty;
            if (vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                s += "이전 " + c.FormatValue(_state.Before.SampleAt(vm.BeforeIndex, _cursorA));
            }
            if (vm.InAfter && _state.After != null)
            {
                if (s.Length > 0) s += "    ";
                Channel c = _state.After.Channels[vm.AfterIndex];
                // 커서가 든 칸의 밀기 값으로 읽습니다 (단계별로 나눠
                // 보는 중이면 칸마다 다릅니다).
                double t = _cursorA - ShiftAt(_cursorA);
                s += "이후 " + c.FormatValue(_state.After.SampleAt(vm.AfterIndex, t));
            }
            return s + DiffNote(vm);
        }

        /// <summary>
        /// 차이 눈금일 때 값 뒤에 붙이는 "차이 +0.3".
        ///
        /// 이 눈금에서 화면에 그려진 선은 두 값이 아니라 <b>그 차이</b>입니다.
        /// 이전/이후 값만 적으면 커서가 가리키는 숫자와 선이 서로 다른 것을
        /// 말하게 되고, 차이만 적으면 값이 얼마인지 알 수 없어서 둘 다 적습니다.
        /// </summary>
        private string DiffNote(IoRowVm vm)
        {
            if (!Diffing || !CanDiff(vm)) return string.Empty;

            double d = DifferenceSeries.At(_state.Before, vm.BeforeIndex,
                                           _state.After, vm.AfterIndex,
                                           ShiftAt(_cursorA), _cursorA);
            if (double.IsNaN(d)) return string.Empty;

            // 상태 채널은 0/1 이 "같다/다르다" 라는 뜻이라 숫자로 적으면 헷갈립니다.
            if (DifferenceSeries.ByName(_state.Before.Channels[vm.BeforeIndex],
                                        _state.After.Channels[vm.AfterIndex]))
                return "    " + (d == 0 ? "차이 없음" : "상태 다름");

            return "    차이 " + (d > 0 ? "+" : "") + NumberText.Plain(d);
        }

        /// <summary>
        /// 레인의 기준 세로 범위. 기본은 채널 전체 범위라서 시간축을 밀어도
        /// 세로 배율이 갑자기 풀리지 않습니다. "보이는 구간에 맞춤" 을 켰을
        /// 때만 지금 보이는 구간으로 다시 잡습니다. (요구사항 2번)
        /// </summary>
        private void BaseRange(IoRowVm[] ios, out double lo, out double hi)
        {
            bool any;
            BaseRange(ios, out lo, out hi, out any);
        }

        /// <summary>
        /// 레인의 기준 범위와, <b>그게 진짜 값에서 나온 것인지</b>.
        ///
        /// 쓸 만한 값이 하나도 없으면 0~1 을 돌려주되 <paramref name="usable"/>
        /// 을 거짓으로 둡니다. 그 0~1 은 "그릴 게 없어서 아무 눈금이나 그린
        /// 것" 이지 데이터가 아닙니다. 그걸 데이터인 줄 알고 세로 위치를
        /// 적어 두면, 나중에 값이 돌아와도 눈금이 0 언저리에 붙박여
        /// <b>IO 값이 0 으로 고정된 것처럼</b> 보입니다.
        /// </summary>
        private void BaseRange(IoRowVm[] ios, out double lo, out double hi, out bool usable)
        {
            double a = double.PositiveInfinity, b = double.NegativeInfinity;
            for (int k = 0; k < ios.Length; k++)
            {
                double clo, chi;
                if (!ChannelRange(ios[k], out clo, out chi)) continue;
                if (clo < a) a = clo;
                if (chi > b) b = chi;
            }
            usable = a <= b;
            if (!usable) { a = 0; b = 1; }

            // 차이는 두 로그가 같은 동안 계속 0 이라, 0 이 눈금에 들어 있지
            // 않으면 어디가 "차이 없음" 인지 알 수 없습니다.
            if (_scale == ValueScaleMode.Delta)
            {
                if (a > 0) a = 0;
                if (b < 0) b = 0;
            }

            if (b - a < 1e-12) { double m = (a + b) * 0.5; a = m - 0.5; b = m + 0.5; }

            double pad = (b - a) * 0.08;
            lo = a - pad; hi = b + pad;
        }

        private bool ChannelRange(IoRowVm vm, out double lo, out double hi)
        {
            lo = double.PositiveInfinity; hi = double.NegativeInfinity;
            if (_state == null) return false;
            bool any = false;
            double x0, x1;

            // 차이 눈금은 두 로그를 함께 읽어야 나오는 값이라, 채널에 적어 둔
            // 최소/최대를 쓸 수가 없습니다. 대신 겹치는 구간을 훑습니다.
            //
            // "계속 맞춤" 이 꺼져 있으면 겹치는 구간 전체를 봅니다.
            // 그래야 시간축을 밀어도 세로 배율이 흔들리지 않는데, 그 계산은
            // 로그를 통째로 훑는 일이라 프레임마다 하면 못 씁니다. 그래서
            // 갈무리해 두고 로그나 밀기 값이 바뀔 때만 버립니다.
            if (Diffing)
            {
                if (!CanDiff(vm)) return false;
                if (_fitVisible)
                {
                    // 단계별로 나눠 보는 중이면 <b>칸들만</b> 모아 봅니다.
                    // 칸 사이의 빈틈은 그리지 않는 자리라, 거기 값까지
                    // 넣으면 보이지도 않는 값이 눈금을 벌려 놓습니다.
                    bool got = false;
                    foreach (Span sp in Spans())
                    {
                        if (!DifferenceSeries.RangeIn(_state.Before, vm.BeforeIndex,
                                                      _state.After, vm.AfterIndex,
                                                      sp.Shift, sp.T0, sp.T1, out x0, out x1)) continue;
                        lo = got ? Math.Min(lo, x0) : x0;
                        hi = got ? Math.Max(hi, x1) : x1;
                        got = true;
                    }
                    return got;
                }
                return CachedDiffRange(vm, out lo, out hi);
            }

            if (_fitVisible)
            {
                foreach (Span sp in Spans())
                {
                    if (ShownB && vm.InBefore && _state.Before != null
                        && Decimator.RangeIn(_state.Before, vm.BeforeIndex, sp.T0, sp.T1, out x0, out x1))
                    { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                    if (ShownA && vm.InAfter && _state.After != null
                        && Decimator.RangeIn(_state.After, vm.AfterIndex,
                            sp.T0 - sp.Shift, sp.T1 - sp.Shift, out x0, out x1))
                    { lo = Math.Min(lo, x0); hi = Math.Max(hi, x1); any = true; }
                }
                if (any) return true;
            }

            if (ShownB && vm.InBefore && _state.Before != null)
            {
                Channel c = _state.Before.Channels[vm.BeforeIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            if (ShownA && vm.InAfter && _state.After != null)
            {
                Channel c = _state.After.Channels[vm.AfterIndex];
                if (!double.IsNaN(c.Min)) { lo = Math.Min(lo, c.Min); hi = Math.Max(hi, c.Max); any = true; }
            }
            return any;
        }

        /// <summary>
        /// 값 범위를 잴 구간 하나. 평소에는 보이는 구간 하나뿐이고,
        /// 단계별로 나눠 보는 중에는 칸마다 하나씩입니다 (밀기 값도 칸마다
        /// 다릅니다).
        /// </summary>
        private struct Span
        {
            public double T0;
            public double T1;
            public double Shift;
        }

        /// <summary>
        /// 지금 화면이 실제로 보여 주는 구간들. <b>값 범위를 재는 코드가
        /// 한 줄 시간이든 단계 칸이든 같은 모양으로 돌게</b> 하려고 냅니다 —
        /// 두 갈래로 적어 두면 한쪽만 고치는 날이 옵니다.
        /// </summary>
        private IEnumerable<Span> Spans()
        {
            if (StepMode)
            {
                for (int i = 0; i < _stepBands.Count; i++)
                {
                    StepBand b = _stepBands[i];
                    var sp = new Span();
                    sp.T0 = b.T0; sp.T1 = b.T1; sp.Shift = b.Shift;
                    yield return sp;
                }
                yield break;
            }

            var one = new Span();
            one.T0 = _t0; one.T1 = _t1;
            one.Shift = _state != null ? _state.AppliedShift : 0.0;
            yield return one;
        }

        // 겹치는 구간 전체의 차이 폭. IO 마다 한 번만 재고 들고 있습니다.
        private readonly Dictionary<string, double[]> _diffRange =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private double _diffRangeShift = double.NaN;

        private bool CachedDiffRange(IoRowVm vm, out double lo, out double hi)
        {
            lo = 0; hi = 1;

            // 단계별로 나눠 보는 중이면 밀기 값이 칸마다 다릅니다. 갈무리를
            // 가르는 열쇠로 NaN 을 씁니다 (칸 목록이 바뀌면 StepBands 가
            // 갈무리를 버립니다).
            double shift = StepMode ? double.NaN : _state.AppliedShift;

            // 밀기 값이 바뀌면 겹치는 자리가 통째로 달라집니다. 옛 값을 그대로
            // 쓰면 맞추기를 바꿔도 세로 눈금이 안 따라옵니다.
            bool same = double.IsNaN(shift) ? double.IsNaN(_diffRangeShift) : _diffRangeShift == shift;
            if (!same) { _diffRange.Clear(); _diffRangeShift = shift; }

            double[] got;
            if (!_diffRange.TryGetValue(vm.Name, out got))
            {
                double a = 0, b = 1;
                bool any2 = false;
                foreach (Span sp in Spans())
                {
                    // 평소에는 보이는 구간이 아니라 <b>겹치는 구간 전체</b>를
                    // 봅니다 (시간축을 밀어도 세로 배율이 안 흔들리게).
                    // 단계 칸은 그 칸이 곧 보는 구간입니다.
                    double t0 = StepMode ? sp.T0
                        : Math.Max(_state.Before.TimeStart, _state.After.TimeStart + sp.Shift);
                    double t1 = StepMode ? sp.T1
                        : Math.Min(_state.Before.TimeEnd, _state.After.TimeEnd + sp.Shift);

                    double x0, x1;
                    if (!DifferenceSeries.RangeIn(_state.Before, vm.BeforeIndex,
                                                  _state.After, vm.AfterIndex,
                                                  sp.Shift, t0, t1, out x0, out x1)) continue;
                    a = any2 ? Math.Min(a, x0) : x0;
                    b = any2 ? Math.Max(b, x1) : x1;
                    any2 = true;
                }
                got = any2 ? new double[] { a, b } : null;
                _diffRange[vm.Name] = got;
            }
            if (got == null) return false;
            lo = got[0]; hi = got[1];
            return true;
        }

        /// <summary>
        /// 지금 차이(이후−이전) 눈금인지.
        ///
        /// 이 눈금에서는 선이 <b>하나</b>입니다. 이전과 이후를 각각 그리는
        /// 대신 "이후 − 이전" 을 그립니다. 그래서 값 하나를 바꿔 주는
        /// 함수로는 처리할 수 없습니다 — 두 로그를 같은 시각에서 함께 읽어야
        /// 나오는 값이라, 접기 · 범위 · 그리기가 모두 다른 길로 갑니다.
        /// </summary>
        private bool Diffing { get { return _scale == ValueScaleMode.Delta; } }

        /// <summary>
        /// 이 IO 를 차이로 그릴 수 있는지. 한쪽 로그에만 있으면 견줄 상대가
        /// 없습니다. 그 레인은 비워 두고, 왜 비었는지는 레인 머리글의
        /// "이전에만 / 이후에만" 이 말해 줍니다.
        /// </summary>
        private bool CanDiff(IoRowVm vm)
        {
            return _state != null && vm != null
                && vm.InBefore && vm.InAfter
                && _state.Before != null && _state.After != null;
        }
    }
}
