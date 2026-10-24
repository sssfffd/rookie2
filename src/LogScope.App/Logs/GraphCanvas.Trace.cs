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
using LogScope.Logs.Render;
using LogScope.App.Common;
using LogScope.App.Settings;

namespace LogScope.App.Logs
{
    /// <summary>
    /// 그래프 판 — <b>선 하나</b>. 표본을 픽셀 열로 접고(Decimator),
    /// StreamGeometry 한 덩어리로 만들어 한 번에 넘깁니다.
    ///
    /// 여기가 로그 10M 칸을 버티는 자리입니다. 고칠 때는 "선 하나에
    /// DrawLine 을 몇 번 부르는가" 를 먼저 보세요.
    /// </summary>
    public sealed partial class GraphCanvas
    {

        private void DrawChannel(DrawingContext dc, Palette p, Rect inner, IoRowVm vm,
                                 double vlo, double vhi, Pen beforePen, Pen afterPen, int columns)
        {
            if (_state == null || inner.Width <= 1) return;

            bool haveB = ShownB && vm.InBefore && _state.Before != null && _sideOnly != 2;
            bool haveA = ShownA && vm.InAfter && _state.After != null && _sideOnly != 1;

            double shift = ShiftNow;   // 단계 칸을 그리는 중이면 그 칸의 값
            double bt0 = _t0, bt1 = _t1;                  // 이전 로그의 시간 기준
            double at0 = _t0 - shift, at1 = _t1 - shift;  // 이후 로그의 시간 기준

            // 한쪽만 켜 두었으면 벌리지 않습니다. 벌릴 상대가 없는데 비켜
            // 그리면 그 선만 눈금에서 어긋나 보입니다.
            double sep = (_separate && haveB && haveA) ? inner.Height * 0.02 : 0.0;

            // ---- 차이 눈금: 선 하나 -------------------------------------
            if (Diffing)
            {
                // 한쪽 로그만 남은 자리(단계 칸 꼬리)에는 차이가 없습니다.
                if (_sideOnly != 0) return;
                if (!CanDiff(vm)) return;     // 한쪽에만 있는 IO 는 견줄 상대가 없습니다.

                if (_shade)
                {
                    // 칠하려면 양쪽 값이 같은 자리에서 하나씩 있어야 합니다.
                    Decimator.SampleColumns(_state.Before, vm.BeforeIndex, bt0, bt1, _bandBefore, columns);
                    Decimator.SampleColumns(_state.After, vm.AfterIndex, at0, at1, _bandAfter, columns);
                    ShadeGap(dc, p, inner, columns, vlo, vhi, 0);
                }

                DrawDifference(dc, p, inner, vm, bt0, bt1, shift, columns, vlo, vhi);
                return;
            }

            if (_shade && haveB && haveA && _sideOnly == 0)
            {
                // 칠하려면 양쪽 값이 같은 자리에서 하나씩 있어야 합니다.
                // 접은 값(최소/최대)이 아니라 열마다 한 값씩 뽑아 씁니다.
                Decimator.SampleColumns(_state.Before, vm.BeforeIndex, bt0, bt1, _bandBefore, columns);
                Decimator.SampleColumns(_state.After, vm.AfterIndex, at0, at1, _bandAfter, columns);
                ShadeGap(dc, p, inner, columns, vlo, vhi, sep);
            }

            if (haveB) DrawSide(dc, inner, _state.Before, vm.BeforeIndex, bt0, bt1,
                                _colsBefore, columns, vlo, vhi, beforePen, -sep);
            if (haveA) DrawSide(dc, inner, _state.After, vm.AfterIndex, at0, at1,
                                _colsAfter, columns, vlo, vhi, afterPen, +sep);
        }

        /// <summary>
        /// 두 로그의 차이 선 하나. "이후 − 이전" 입니다.
        ///
        /// 한쪽 로그만 그릴 때(DrawSide)와 갈림길이 같습니다 — 표본이 픽셀보다
        /// 성기면 그대로 잇고, 촘촘하면 열마다 최소/최대만 뽑아 그립니다.
        /// 다만 두 로그의 시간 격자가 서로 달라서 표본 개수를 미리 알 수 없으니,
        /// 한 번 훑으면서 둘 다 채우고 넘치면 접은 쪽을 씁니다.
        /// </summary>
        private void DrawDifference(DrawingContext dc, Palette p, Rect inner, IoRowVm vm,
                                    double t0, double t1, double shift, int columns,
                                    double vlo, double vhi)
        {
            int count = DifferenceSeries.Build(_state.Before, vm.BeforeIndex,
                                               _state.After, vm.AfterIndex,
                                               shift, t0, t1,
                                               _colsDiff, columns,
                                               _diffT, _diffV, _diffT.Length);

            // 0 선. 어디가 "차이 없음" 인지 눈에 보여야 합니다.
            double zero = ValueToY(0, inner, vlo, vhi);
            if (zero >= inner.Top && zero <= inner.Bottom)
            {
                dc.DrawLine(p.GridPen, new Point(inner.Left, Snap(zero)),
                                       new Point(inner.Right, Snap(zero)));
            }

            Pen pen = p.DiffPen;
            if (count < 0)
            {
                DrawColumns(dc, inner, _colsDiff, columns, vlo, vhi, pen, 0);
                return;
            }
            if (count == 0) return;

            // 상태 채널의 차이는 0/1 이라 계단으로 그립니다. 그 사이에
            // "반쯤 다름" 같은 값은 없습니다.
            bool stepped = DifferenceSeries.ByName(_state.Before.Channels[vm.BeforeIndex],
                                                   _state.After.Channels[vm.AfterIndex]);

            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                _pts.Clear();
                double prevY = 0;
                for (int i = 0; i < count; i++)
                {
                    double x = TimeToXIn(inner, _diffT[i], t0, t1);
                    double y = Clamp(ValueToY(_diffV[i], inner, vlo, vhi), top, bottom);

                    if (i == 0) { ctx.BeginFigure(new Point(x, y), false, false); }
                    else
                    {
                        if (stepped) _pts.Add(new Point(x, prevY));
                        _pts.Add(new Point(x, y));
                    }
                    prevY = y;
                }
                if (_pts.Count > 0) ctx.PolyLineTo(_pts, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        /// <summary>
        /// 한쪽 로그의 선 하나. 표본이 픽셀보다 촘촘한지 성긴지에 따라
        /// 그리는 방식을 바꿉니다.
        ///
        /// 촘촘하면 열마다 최소/최대만 뽑아 그립니다(Decimator). 그러지 않으면
        /// 한 픽셀 폭에 수천 번 선을 긋게 됩니다.
        ///
        /// 성기면 <b>표본을 그대로</b> 잇습니다. 예전에는 이때도 접어서 그렸는데,
        /// 표본이 없는 열을 "값이 끊긴 자리" 로 보고 선을 끊는 바람에 표본마다
        /// 점 하나짜리 도형이 되어 <b>아무것도 안 보였습니다</b>. 확대할수록
        /// 심해지던 그 증상이 이것입니다.
        /// </summary>
        private void DrawSide(DrawingContext dc, Rect inner, LogDataset ds, int ch,
                              double t0, double t1, Decimator.Column[] cols, int columns,
                              double vlo, double vhi, Pen pen, double dy)
        {
            int first, last;
            if (!Decimator.VisibleRange(ds, t0, t1, out first, out last)) return;

            long visible = (long)last - first + 1;
            if (visible <= columns)
                DrawSamples(dc, inner, ds, ch, first, last, t0, t1, vlo, vhi, pen, dy);
            else
            {
                Decimator.Build(ds, ch, t0, t1, cols, columns);
                DrawColumns(dc, inner, cols, columns, vlo, vhi, pen, dy);
            }
        }

        /// <summary>
        /// 표본을 그대로 잇습니다. 디지털과 상태 채널은 계단으로 그립니다 —
        /// 값이 다음 표본까지 그대로 유지되다가 거기서 한 번에 바뀌는 것이므로,
        /// 비스듬한 선으로 이으면 없던 중간값을 그린 셈이 됩니다.
        ///
        /// </summary>
        private void DrawSamples(DrawingContext dc, Rect inner, LogDataset ds, int ch,
                                 int first, int last, double t0, double t1,
                                 double vlo, double vhi, Pen pen, double dy)
        {
            Channel c = ds.Channels[ch];
            float[] v = c.Values;
            double[] times = ds.Times;
            bool stepped = c.IsStepped;

            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();

            using (StreamGeometryContext ctx = geo.Open())
            {
                bool open = false;
                _pts.Clear();

                for (int i = first; i <= last; i++)
                {
                    float value = v[i];
                    if (float.IsNaN(value)) { if (open) Flush(ctx, ref open); continue; }

                    double x = TimeToXIn(inner, times[i], t0, t1);
                    double y = Clamp(ValueToY(value, inner, vlo, vhi) + dy,
                                     top, bottom);

                    if (!open) { ctx.BeginFigure(new Point(x, y), false, false); open = true; }
                    else _pts.Add(new Point(x, y));

                    // 계단: 다음 표본 시각까지 이 값을 그대로 끌고 갑니다.
                    // 그 다음 바퀴가 세로 변을 그립니다.
                    if (stepped && i < last && !float.IsNaN(v[i + 1]))
                        _pts.Add(new Point(TimeToXIn(inner, times[i + 1], t0, t1), y));
                }
                if (open) Flush(ctx, ref open);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        /// <summary>
        /// 접은 열을 그립니다.
        ///
        /// 한 열은 가로로 1 픽셀이라 그 열의 점들은 <b>x 가 전부 같습니다.</b>
        /// 결국 최소~최대를 잇는 세로 선 하나입니다. 그래서 위끝과 아래끝
        /// <b>두 점</b>이면 충분합니다. 예전에는 첫값과 끝값까지 네 점을
        /// 찍었는데, 어차피 같은 세로 선 위에 겹쳐 찍히는 점이라 화면에는
        /// 아무 차이가 없으면서 점 개수만 두 배였습니다. 0.1 초마다 값이
        /// 바뀌는 채널처럼 모든 열이 위아래로 꽉 찬 경우에 특히 무거웠습니다.
        ///
        /// 두 점을 찍는 <b>차례</b>는 직전 열이 끝난 높이에 맞춥니다. 가까운
        /// 쪽부터 찍어야 열과 열 사이를 잇는 선이 쓸데없이 가로지르지 않습니다.
        ///
        /// 여기서 값이 없는 열은 정말로 기록이 끊긴 자리입니다 — 표본이 픽셀보다
        /// 촘촘한 경우에만 이 길로 오기 때문입니다.
        /// </summary>
        private void DrawColumns(DrawingContext dc, Rect inner, Decimator.Column[] cols, int columns,
                                 double vlo, double vhi, Pen pen, double dy)
        {
            double top = inner.Top - 4, bottom = inner.Bottom + 4;
            var geo = new StreamGeometry();

            using (StreamGeometryContext ctx = geo.Open())
            {
                bool open = false;
                double prevY = double.NaN;
                _pts.Clear();

                for (int x = 0; x < columns; x++)
                {
                    Decimator.Column c = cols[x];
                    if (!c.HasValue) { if (open) Flush(ctx, ref open); prevY = double.NaN; continue; }

                    double px = ColumnToX(inner, x);
                    // 화면 y 는 값이 클수록 작아집니다. yTop 이 c.Max, yBottom 이 c.Min.
                    double yTop = Clamp(ValueToY(c.Max, inner, vlo, vhi) + dy, top, bottom);
                    double yBottom = Clamp(ValueToY(c.Min, inner, vlo, vhi) + dy, top, bottom);

                    // 직전 열이 끝난 높이에서 가까운 쪽부터 찍습니다.
                    double enter = yTop, leave = yBottom;
                    if (!double.IsNaN(prevY) && Math.Abs(prevY - yBottom) < Math.Abs(prevY - yTop))
                    {
                        enter = yBottom; leave = yTop;
                    }

                    if (!open) { ctx.BeginFigure(new Point(px, enter), false, false); open = true; }
                    else _pts.Add(new Point(px, enter));

                    if (leave != enter) _pts.Add(new Point(px, leave));
                    prevY = leave;
                }
                if (open) Flush(ctx, ref open);
            }

            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        private void Flush(StreamGeometryContext ctx, ref bool open)
        {
            if (_pts.Count > 0)
            {
                ctx.PolyLineTo(_pts, true, false);
                _pts.Clear();
            }
            open = false;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        /// <summary>
        /// 이전과 이후가 벌어진 구간을 옅은 색으로 채웁니다.
        /// 점선을 쓰지 않는 이유는, 점선이 파형의 빈 구간과 섞여 파형을
        /// 읽기 어렵게 만들기 때문입니다. (요구사항 5번)
        ///
        /// 열마다 뽑아 둔 값(_bandBefore / _bandAfter)을 씁니다. 접은 값이
        /// 아니라 "그 시각의 값" 이라, 확대해서 표본이 성겨져도 띠가 끊기지
        /// 않습니다.
        ///
        /// 1 픽셀짜리 네모를 열마다 하나씩 담되, 전부 한 도형에 모아 한 번에
        /// 칠합니다. 붙어 있는 네모들이 모여 자연스러운 띠가 됩니다.
        ///
        /// 그 안쪽 차이는 칠하지 않습니다 — 대시보드가 "차이 없음" 으로 세는
        /// 것과 같은 기준이라야 화면끼리 말이 맞습니다.
        /// </summary>
        /// <summary>
        /// 이전/이후가 벌어진 구간을 칠합니다.
        ///
        /// 예전에는 <b>픽셀 열마다 닫힌 사각형을 하나씩</b> 만들었습니다.
        /// 값이 잔잔한 채널은 칠할 열이 몇 개 안 되니 티가 안 났는데,
        /// 0.1 초마다 값이 바뀌는 채널처럼 거의 모든 열이 벌어져 있으면
        /// 도형이 열 수만큼(2000~3500 개) 생깁니다. WPF 는 닫힌 도형마다
        /// 따로 삼각형으로 쪼개므로, 이게 프레임마다 반복되면서 화면이
        /// 눈에 띄게 굼떠졌습니다.
        ///
        /// 이제 <b>이어져 있는 열들을 한 덩어리로 묶어</b> 폴리곤 하나로
        /// 칠합니다. 위쪽 경계를 왼쪽에서 오른쪽으로 따라간 뒤, 아래쪽
        /// 경계를 오른쪽에서 왼쪽으로 되짚어 닫습니다. 점 개수는 비슷하지만
        /// 도형 개수가 수천 개에서 몇 개로 줄어듭니다.
        /// </summary>
        private void ShadeGap(DrawingContext dc, Palette p, Rect inner, int columns,
                              double vlo, double vhi, double sep)
        {
            var geo = new StreamGeometry();
            bool any = false;
            double width = 1.0 / _pixelsPerDip;   // 한 열의 가로 폭 (DIP)

            using (StreamGeometryContext ctx = geo.Open())
            {
                int x = 0;
                while (x < columns)
                {
                    if (!Differs(x)) { x++; continue; }

                    _edgeTop.Clear();
                    _edgeBottom.Clear();
                    double lastPx = 0;

                    while (x < columns && Differs(x))
                    {
                        // 차이 눈금에서는 선이 하나라 그 선과 0 선 사이를 칠합니다.
                        // 값 눈금에서는 이전 선과 이후 선 사이입니다.
                        double y1, y2;
                        if (Diffing)
                        {
                            y1 = ValueToY(_bandAfter[x] - _bandBefore[x], inner, vlo, vhi);
                            y2 = ValueToY(0, inner, vlo, vhi);
                        }
                        else
                        {
                            y1 = ValueToY(_bandBefore[x], inner, vlo, vhi) - sep;
                            y2 = ValueToY(_bandAfter[x], inner, vlo, vhi) + sep;
                        }
                        double t = Clamp(Math.Min(y1, y2), inner.Top, inner.Bottom);
                        double b = Clamp(Math.Max(y1, y2), inner.Top, inner.Bottom);
                        if (b - t < 1) b = t + 1;

                        lastPx = ColumnToX(inner, x);
                        _edgeTop.Add(new Point(lastPx, t));
                        _edgeBottom.Add(new Point(lastPx, b));
                        x++;
                    }

                    // 마지막 열의 오른쪽 변까지 덮습니다.
                    _edgeTop.Add(new Point(lastPx + width, _edgeTop[_edgeTop.Count - 1].Y));
                    _edgeBottom.Add(new Point(lastPx + width, _edgeBottom[_edgeBottom.Count - 1].Y));
                    _edgeBottom.Reverse();

                    ctx.BeginFigure(_edgeTop[0], true, true);
                    ctx.PolyLineTo(_edgeTop, false, false);
                    ctx.PolyLineTo(_edgeBottom, false, false);
                    any = true;
                }
            }

            _edgeTop.Clear();
            _edgeBottom.Clear();
            if (!any) return;

            geo.Freeze();
            dc.DrawGeometry(p.DiffBrush, null, geo);
        }

        /// <summary>
        /// 그 열에서 두 로그가 허용 오차를 넘어 벌어졌는지.
        /// 대시보드·히트맵과 같은 함수를 씁니다.
        /// </summary>
        private bool Differs(int x)
        {
            return ToleranceRule.IsOver(_bandBefore[x], _bandAfter[x],
                                        _absoluteTolerance, _relativePercent);
        }
    }
}
