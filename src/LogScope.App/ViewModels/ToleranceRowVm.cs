using System;
using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.Core.Compare;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 설정 창의 "IO 별 허용 오차" 한 줄.
    ///
    /// 두 칸(퍼센트 · 절대값)은 <b>비워 둘 수 있습니다.</b> 비우면 그 자리는
    /// 기본값을 씁니다. 0 과 빈 칸은 다릅니다 — 절대값 0 은 "절대 오차를 끈다",
    /// 퍼센트 0 은 "조금이라도 벌어지면 차이" 라는 뜻이라 둘 다 쓰는 값입니다.
    /// 그래서 안 정한 것은 NaN 으로 두고, 화면에서는 빈 칸으로 보입니다.
    /// </summary>
    public sealed class ToleranceRowVm : ObservableObject
    {
        private readonly ToleranceOverride _r;

        public ToleranceRowVm(ToleranceOverride rule)
        {
            _r = rule;
        }

        /// <summary>설정에 들어 있는 그 줄. 화면에서 고치면 여기가 바로 바뀝니다.</summary>
        public ToleranceOverride Rule { get { return _r; } }

        public string Io
        {
            get { return _r.Io ?? string.Empty; }
            set { _r.Io = (value ?? string.Empty).Trim(); Raise(); }
        }

        /// <summary>비어 있으면 기본값. 0~100 밖은 받지 않습니다.</summary>
        public string PercentText
        {
            get { return Text(_r.Percent, "0.####"); }
            set
            {
                double v;
                if (!Parse(value, out v)) { Raise(); return; }
                if (!double.IsNaN(v) && (v < 0 || v > 100)) { Raise(); return; }
                _r.Percent = v;
                Raise();
            }
        }

        /// <summary>비어 있으면 기본값. 음수는 받지 않습니다.</summary>
        public string AbsoluteText
        {
            get { return Text(_r.Absolute, "0.######"); }
            set
            {
                double v;
                if (!Parse(value, out v)) { Raise(); return; }
                if (!double.IsNaN(v) && v < 0) { Raise(); return; }
                _r.Absolute = v;
                Raise();
            }
        }

        private static string Text(double v, string format)
        {
            return double.IsNaN(v) ? string.Empty : v.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>빈 칸은 NaN(= 기본값). 숫자가 아니면 거짓 — 고치지 않고 되돌립니다.</summary>
        private static bool Parse(string text, out double value)
        {
            value = double.NaN;
            if (text == null) return true;

            string t = text.Trim();
            if (t.Length == 0) return true;

            double v;
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            if (double.IsNaN(v) || double.IsInfinity(v)) return false;

            value = v;
            return true;
        }
    }
}
