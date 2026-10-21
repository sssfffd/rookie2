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
    /// 그래프 판 — <b>마우스와 키보드</b>. 휠 확대, 끌어 이동, 오른쪽
    /// 끌기로 네모 확대, 커서 A·B 찍기, 방향키.
    ///
    /// 여기서는 그리지 않습니다 — 보이는 구간과 커서만 고치고
    /// InvalidateVisual 로 다시 그리게 합니다.
    /// </summary>
    public sealed partial class GraphCanvas
    {

        // ---------------- 마우스 ----------------

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_state == null) return;

            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);

            // 한 칸에 너무 많이 커지지 않게 아주 조금씩 바꿉니다. (요구사항 2번)
            double factor = Math.Pow(1.14, notches);
            Point at = e.GetPosition(this);

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) ZoomValueAt(at, factor);
            else if (StepMode)
            {
                // 커서를 올려 둔 <b>그 칸만</b> 확대합니다. 다른 칸은 그대로
                // 남아야 견줄 바탕이 됩니다.
                //
                // 칸 위가 아니면 (칸 사이의 틈 · 왼쪽 눈금 자리 · 아래 눈금
                // 띠) <b>모든 칸을 같이</b> 확대합니다. 전에는 아무 일도
                // 안 일어났는데, 틈이 10 px 이라 거기서 굴린 사람은 확대가
                // 안 되는 줄로 읽습니다.
                double left, width;
                int k = StepAt(at.X, PlotArea, out left, out width);
                if (k >= 0) ZoomStep(k, XToTime(at.X), factor);
                else ZoomAllSteps(factor);
            }
            else ZoomTimeAround(XToTime(at.X), factor);

            InvalidateVisual();
            RaiseViewChanged();
            e.Handled = true;
        }

        /// <summary>
        /// Shift + 휠. 커서가 가리키던 값이 그 자리에 그대로 남도록
        /// 가운데를 다시 잡습니다. (요구사항 2번)
        /// </summary>
        private void ZoomValueAt(Point at, double factor)
        {
            string key; Rect inner; IoRowVm[] ios;
            if (!LaneAt(at, out key, out inner, out ios)) return;

            double lo, hi;
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);
            if (!usable) return;      // 그릴 값이 없는 레인은 세로 위치를 적지 않습니다.

            LaneY ly = LaneFor(key);
            double span = (hi - lo) / ly.Zoom;
            double center = CenterOf(ly, lo, hi, true);

            double f = inner.Height > 0 ? (inner.Bottom - at.Y) / inner.Height : 0.5;
            double valueAt = center - span * 0.5 + f * span;

            ly.Zoom *= factor;
            ClampZooms();
            double newSpan = (hi - lo) / ly.Zoom;
            ly.Center = valueAt - (f - 0.5) * newSpan;
        }

        private bool LaneAt(Point at, out string key, out Rect inner, out IoRowVm[] ios)
        {
            key = OverlayLaneKey;
            inner = Rect.Empty;
            ios = new IoRowVm[0];

            Rect plot = PlotArea;
            if (!plot.Contains(at) || _channels.Count == 0) return false;

            if (!_laneMode)
            {
                inner = new Rect(plot.Left, plot.Top + HeaderH, plot.Width,
                                 Math.Max(8, plot.Height - HeaderH - 4));
                ios = _channels.ToArray();
                return true;
            }

            double laneH = Math.Max(MinLaneH, plot.Height / Math.Max(1, _channels.Count));
            double needed = laneH * _channels.Count;
            double scrollY = needed > plot.Height ? VerticalOffset : 0;

            int idx = (int)((at.Y + scrollY) / laneH);
            if (idx < 0 || idx >= _channels.Count) return false;

            double top = idx * laneH - scrollY;
            key = _channels[idx].Name;
            inner = new Rect(plot.Left, top + HeaderH, plot.Width, Math.Max(8, laneH - HeaderH - 4));
            ios = new[] { _channels[idx] };
            return true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (_state == null) return;

            _dragging = true;
            _moved = false;
            _dragPoint = e.GetPosition(this);
            _dragLast = _dragPoint;
            _dragT0 = _t0; _dragT1 = _t1;

            string key; Rect inner; IoRowVm[] ios;
            _dragLane = null;
            if (LaneAt(_dragPoint, out key, out inner, out ios))
            {
                double lo, hi;
                bool usable;
                BaseRange(ios, out lo, out hi, out usable);
                if (usable)
                {
                    _dragLane = key;
                    _dragCenter = CenterOf(LaneFor(key), lo, hi, true);
                }
            }

            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_band)
            {
                _bandTo = e.GetPosition(this);
                InvalidateVisual();
                return;
            }

            if (!_dragging) return;

            Point now = e.GetPosition(this);
            double dx = now.X - _dragPoint.X, dy = now.Y - _dragPoint.Y;
            if (!_moved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
            _moved = true;

            Rect plot = PlotArea;
            if (plot.Width > 0 && StepMode)
            {
                // 누른 칸만 옮깁니다. 끌기는 조금씩 여러 번 오므로 <b>이번에
                // 움직인 만큼</b>만 밀어야 합니다 (누를 때의 자리에서 센
                // 값을 쓰면 가속이 붙습니다).
                double left, width;
                int k = StepAt(_dragPoint.X, plot, out left, out width);
                if (k >= 0 && width > 0)
                {
                    double moved = e.GetPosition(this).X - _dragLast.X;
                    PanStep(k, -moved / width);
                }
            }
            else if (plot.Width > 0)
            {
                double span = _dragT1 - _dragT0;
                double shift = -dx * span / plot.Width;
                _t0 = _dragT0 + shift;
                _t1 = _dragT1 + shift;
            }

            _dragLast = e.GetPosition(this);

            if (_dragLane != null)
            {
                string key; Rect inner; IoRowVm[] ios;
                if (LaneAt(_dragPoint, out key, out inner, out ios) && inner.Height > 0)
                {
                    double lo, hi;
                    bool usable;
                    BaseRange(ios, out lo, out hi, out usable);
                    if (usable)
                    {
                        LaneY ly = LaneFor(key);
                        double vspan = (hi - lo) / ly.Zoom;
                        ly.Center = _dragCenter + dy * vspan / inner.Height;
                    }
                }
            }

            InvalidateVisual();
            RaiseViewChanged();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            if (_moved) return;

            // 끌지 않고 그냥 눌렀으면 커서를 놓습니다.
            double t = XToTime(e.GetPosition(this).X);
            // 칸 사이의 금을 누르면 가리키는 시각이 없습니다 (NaN).
            // 커서를 거기 두면 값이 전부 "-" 로 나오니 그냥 둡니다.
            if (double.IsNaN(t)) return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _cursorB = t; else _cursorA = t;
            InvalidateVisual();
            RaiseViewChanged();
        }

        // ---------------- 오른쪽 단추로 네모 확대 ----------------
        //
        // 휠로 확대하면 가운데를 기준으로 커지고 작아지는데, "이 구간만 크게"
        // 를 하려면 휠과 끌기를 번갈아 여러 번 해야 했습니다. 네모를 그리면
        // 그만큼 한 번에 맞춰 줍니다.
        //
        // 가로만 끌면 시간만 확대합니다. 세로로도 끌었으면 그 레인의
        // 값 범위까지 같이 맞춥니다 — 네모를 그렸는데 위아래가 그대로면
        // "영역만큼" 이라는 말과 어긋납니다.

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            Focus();
            if (_state == null || _channels.Count == 0) return;

            Point at = e.GetPosition(this);
            Rect plot = PlotArea;
            if (at.X < plot.Left || at.X > plot.Right || at.Y < plot.Top || at.Y > plot.Bottom) return;

            _band = true;
            _bandFrom = at;
            _bandTo = at;

            // 어느 레인에서 시작했는지 적어 둡니다. 세로 확대는 그 레인에만
            // 걸어야 합니다 — 레인을 넘나든 네모의 값 범위는 뜻이 없습니다.
            string key; Rect inner; IoRowVm[] ios;
            _bandLane = LaneAt(at, out key, out inner, out ios) ? key : null;

            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (!_band) return;

            _band = false;
            ReleaseMouseCapture();
            e.Handled = true;

            Rect plot = PlotArea;
            _bandTo = e.GetPosition(this);
            Rect r = BandRect(plot);

            // 손이 떨린 정도는 확대가 아닙니다. 그냥 누른 것으로 봅니다.
            if (r.Width < 6 && r.Height < 6) { InvalidateVisual(); return; }

            // 세로 먼저. 시간을 바꾸면 BaseRange(보이는 구간 맞춤)가 달라져
            // 값 범위 계산이 어긋납니다.
            if (r.Height >= 6) ZoomBandValue(r);
            if (r.Width >= 6)
            {
                if (StepMode) ZoomStepToBox(r);
                else ZoomBandTime(r);
            }

            InvalidateVisual();
            RaiseViewChanged();
        }

        /// <summary>
        /// 네모로 자른 구간으로 그 칸을 확대합니다. 네모가 칸 하나 안에
        /// 있을 때만 — 여러 칸에 걸쳐 그었으면 어느 칸을 확대할지 알 수
        /// 없으므로 아무 일도 하지 않습니다 (세로 확대는 그대로 걸립니다).
        /// </summary>
        private void ZoomStepToBox(Rect r)
        {
            Rect plot = PlotArea;
            double left, width;
            int k = StepAt(r.Left, plot, out left, out width);
            double rightLeft, rightWidth;
            int k2 = StepAt(r.Right, plot, out rightLeft, out rightWidth);
            if (k < 0 || k != k2) return;

            double a = XToTime(r.Left), b = XToTime(r.Right);
            if (double.IsNaN(a) || double.IsNaN(b) || !(b > a)) return;

            StepBand band = _stepBands[k];
            double min = band.Length * 1e-7;
            if (min <= 0) min = 1e-9;
            if (b - a < min) { double mid = (a + b) * 0.5; a = mid - min * 0.5; b = mid + min * 0.5; }

            EnsureStepViews();
            if (a < band.T0) a = band.T0;
            if (b > band.T1) b = band.T1;
            if (!(b > a)) return;

            _stepT0[k] = a;
            _stepT1[k] = b;
        }

        private void ZoomBandTime(Rect r)
        {
            double a = XToTime(r.Left), b = XToTime(r.Right);
            if (!(b > a)) return;

            // 너무 얇게 자르는 것은 막습니다 (휠 확대와 같은 한계).
            double full0, full1;
            _state.FullTimeRange(out full0, out full1);
            double minSpan = (full1 - full0) * 1e-7;
            if (minSpan <= 0) minSpan = 1e-9;
            if (b - a < minSpan)
            {
                double mid = (a + b) * 0.5;
                a = mid - minSpan * 0.5;
                b = mid + minSpan * 0.5;
            }

            _t0 = a;
            _t1 = b;
        }

        /// <summary>
        /// 네모의 위아래만큼 그 레인의 값 범위를 좁힙니다.
        ///
        /// 그리는 쪽과 <b>같은 식을 거꾸로</b> 풉니다 (FitLaneToVisible 과 같은
        /// 자리입니다) — 기준 범위를 Zoom 으로 나누고 Center 에 놓는 것이
        /// 그리는 규칙이므로, 원하는 vlo~vhi 에서 Zoom 과 Center 를 냅니다.
        /// </summary>
        private void ZoomBandValue(Rect r)
        {
            if (_bandLane == null) return;

            // [구간 맞춤] 이 켜져 있으면 값 범위는 보이는 시간 구간을 따라
            // 저절로 맞춰집니다. 거기에 네모 값을 적어 둬도 다음 그림에서
            // 덮이므로, 손대지 않습니다 — 시간만 좁혀도 위아래는 알아서
            // 그 구간에 맞습니다.
            if (_fitVisible) return;

            string key; Rect inner; IoRowVm[] ios;
            if (!LaneAt(_bandFrom, out key, out inner, out ios)) return;
            if (key != _bandLane || inner.Height <= 0) return;

            double lo, hi;
            bool usable;
            BaseRange(ios, out lo, out hi, out usable);
            if (!usable) return;

            double full = hi - lo;
            if (!(full > 0)) return;

            LaneY ly = LaneFor(key);
            double span = full / ly.Zoom;
            if (!(span > 0)) return;
            double center = CenterOf(ly, lo, hi, true);
            double vlo = center - span * 0.5, vhi = center + span * 0.5;

            // 네모가 레인 밖으로 나갔으면 그 레인 안으로 잘라 씁니다.
            double y0 = Clamp(r.Top, inner.Top, inner.Bottom);
            double y1 = Clamp(r.Bottom, inner.Top, inner.Bottom);
            if (y1 - y0 < 4) return;

            double vTop = YToValue(y0, inner, vlo, vhi);
            double vBottom = YToValue(y1, inner, vlo, vhi);
            if (!(vTop > vBottom)) return;

            ly.Center = (vTop + vBottom) * 0.5;
            ly.Zoom = full / (vTop - vBottom);
            ClampZooms();
        }

        /// <summary>ValueToY 의 거꾸로. 둘이 어긋나면 네모와 다른 자리로 확대됩니다.</summary>
        private static double YToValue(double y, Rect inner, double vlo, double vhi)
        {
            if (!(inner.Height > 0)) return vlo;
            return vlo + (inner.Bottom - y) * (vhi - vlo) / inner.Height;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double span = _t1 - _t0;

            // 단계별로 나눠 보는 중에는 가로로 움직이는 키를 안 받습니다
            // (휠·끌기와 같은 까닭). 세로 확대 키는 그대로입니다.
            switch (e.Key)
            {
                // 키보드는 <b>모든 칸을 같이</b> 움직입니다 — 단계마다
                // 같은 자리를 보게 해서 서로 견주려는 것입니다. 휠과 끌기는
                // 커서를 올려 둔 칸 하나만 움직입니다.
                case Key.Left:
                    if (StepMode) { PanAllSteps(-0.1); break; }
                    _t0 -= span * 0.1; _t1 -= span * 0.1; break;
                case Key.Right:
                    if (StepMode) { PanAllSteps(+0.1); break; }
                    _t0 += span * 0.1; _t1 += span * 0.1; break;
                case Key.Home: ResetTime(); e.Handled = true; return;
                case Key.OemPlus: case Key.Add: ZoomTime(1.3); e.Handled = true; return;
                case Key.OemMinus: case Key.Subtract: ZoomTime(1 / 1.3); e.Handled = true; return;
                default: return;
            }
            e.Handled = true;
            InvalidateVisual();
            RaiseViewChanged();
        }
    }
}
