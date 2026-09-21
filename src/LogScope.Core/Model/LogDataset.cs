using System;
using System.Collections.Generic;

namespace LogScope.Core.Model
{
    /// <summary>
    /// 로그 파일 하나를 읽어 만든 결과. 시간축 한 개와 채널 여러 개입니다.
    /// 표를 통째로 복사하거나 전치해서 들고 있지 않고, 채널마다 값 배열
    /// 하나씩만 가집니다.
    /// </summary>
    public sealed class LogDataset
    {
        /// <summary>오름차순으로 정리된 시간축. 길이 = SampleCount.</summary>
        public double[] Times = new double[0];
        public TimeKind TimeKind = TimeKind.Index;
        public string TimeUnit = string.Empty;

        public List<Channel> Channels = new List<Channel>();

        /// <summary>읽으면서 남긴 알림 (시간축 대체, 건너뛴 줄 등).</summary>
        public List<string> Notes = new List<string>();

        /// <summary>실제로 사용한 배치. Auto 를 돌려주는 일은 없습니다.</summary>
        public Orientation Orientation = Orientation.Cols;

        /// <summary>표가 시작한 자리. 엑셀과 같은 1 기반 번호입니다.</summary>
        public int FirstRow = 1;
        public int FirstCol = 1;

        // ---- 가로축 바꿔 끼우기 --------------------------------------
        //
        // 보통은 로그의 시간 열을 가로축으로 씁니다. 그런데 시간과 나란히
        // 가는 다른 IO(경과 시간 레지스터, 스캔 번호, 별도 타임스탬프 열 …)를
        // 가로축으로 놓고 보고 싶을 때가 있습니다.
        //
        // 그럴 때 Times 배열을 그 채널의 값으로 <b>갈아 끼웁니다.</b> 아래
        // 모든 함수(이진 탐색, 값 읽기, 눈금 글자)와 그리는 쪽, 견주는 쪽이
        // 전부 Times 만 보므로 그 밖에는 손댈 곳이 없습니다.
        //
        // 대신 조건이 하나 있습니다 — <b>값이 뒤로 가면 안 됩니다.</b>
        // 이진 탐색과 접기가 오름차순을 전제하기 때문입니다. 뒤로 가는
        // 채널은 SetAxisChannel 이 거부하고 이유를 돌려줍니다.
        private double[] _ownTimes;
        private TimeKind _ownKind;
        private string _ownUnit;

        /// <summary>지금 가로축으로 쓰는 IO 이름. 빈 글자면 로그의 시간 열입니다.</summary>
        public string AxisChannel = string.Empty;

        public bool UsesOwnTime { get { return AxisChannel.Length == 0; } }

        /// <summary>
        /// 가로축을 이 IO 의 값으로 바꿉니다. 바꿀 수 없으면 거짓을 돌려주고
        /// problem 에 이유를 담습니다. 원래 시간축은 그대로 보관합니다.
        /// </summary>
        public bool SetAxisChannel(string name, out string problem)
        {
            problem = string.Empty;

            int ch = FindChannel(name);
            if (ch < 0) { problem = "\"" + name + "\" 를 찾지 못했습니다."; return false; }

            Channel c = Channels[ch];
            if (c.Kind == ChannelKind.State)
            {
                problem = "\"" + name + "\" 는 상태(문자열) 채널이라 가로축으로 쓸 수 없습니다.";
                return false;
            }

            float[] v = c.Values;
            int n = Math.Min(v.Length, SampleCount);
            if (n < 2) { problem = "표본이 너무 적습니다."; return false; }

            var axis = new double[n];
            double prev = double.NegativeInfinity;
            int filled = 0;

            for (int i = 0; i < n; i++)
            {
                double x = v[i];
                if (double.IsNaN(x))
                {
                    // 값이 빈 자리는 직전 값을 끌고 갑니다. 그래야 오름차순이
                    // 깨지지 않고, 그 구간은 어차피 그릴 값이 없습니다.
                    if (filled == 0) { problem = "\"" + name + "\" 의 앞부분이 비어 있습니다."; return false; }
                    axis[i] = prev;
                    continue;
                }
                if (x < prev)
                {
                    problem = "\"" + name + "\" 는 값이 뒤로 갑니다 ("
                            + NumberText.Plain(prev) + " → " + NumberText.Plain(x)
                            + "). 가로축은 늘어나기만 해야 합니다.";
                    return false;
                }
                axis[i] = x;
                prev = x;
                filled++;
            }

            if (axis[n - 1] <= axis[0])
            {
                problem = "\"" + name + "\" 는 처음부터 끝까지 값이 그대로입니다.";
                return false;
            }

            if (_ownTimes == null) { _ownTimes = Times; _ownKind = TimeKind; _ownUnit = TimeUnit; }
            Times = axis;
            TimeKind = TimeKind.Number;
            TimeUnit = c.Unit;
            AxisChannel = c.Name;
            return true;
        }

        /// <summary>가로축을 로그의 시간 열로 되돌립니다.</summary>
        public void ClearAxisChannel()
        {
            if (_ownTimes == null) return;
            Times = _ownTimes;
            TimeKind = _ownKind;
            TimeUnit = _ownUnit ?? string.Empty;
            AxisChannel = string.Empty;
        }

        /// <summary>
        /// 가로축으로 쓸 수 있는 IO 인지. 목록을 추리는 데 씁니다.
        /// 값이 뒤로 가지 않고 실제로 늘어나야 합니다.
        /// </summary>
        public bool CanBeAxis(int channel)
        {
            if (channel < 0 || channel >= ChannelCount) return false;
            Channel c = Channels[channel];
            if (c.Kind != ChannelKind.Analog) return false;

            float[] v = c.Values;
            int n = Math.Min(v.Length, SampleCount);
            if (n < 2) return false;

            double prev = double.NegativeInfinity, first = double.NaN;
            for (int i = 0; i < n; i++)
            {
                double x = v[i];
                if (double.IsNaN(x)) continue;
                if (x < prev) return false;
                if (double.IsNaN(first)) first = x;
                prev = x;
            }
            return !double.IsNaN(first) && prev > first;
        }

        public string SourcePath = string.Empty;
        public long SourceBytes;
        public double LoadSeconds;

        public int SampleCount { get { return Times.Length; } }
        public int ChannelCount { get { return Channels.Count; } }

        // 이름 -> 인덱스. 채널이 수천 개여도 비교가 빨라집니다.
        private Dictionary<string, int> _byName;
        private Dictionary<string, int> _byLooseName;

        public void AddNote(string note)
        {
            if (!string.IsNullOrEmpty(note) && !Notes.Contains(note)) Notes.Add(note);
        }

        public string NotesText { get { return string.Join("  ", Notes.ToArray()); } }

        private void EnsureIndex()
        {
            if (_byName != null) return;
            var exact = new Dictionary<string, int>(Channels.Count, StringComparer.Ordinal);
            var loose = new Dictionary<string, int>(Channels.Count, StringComparer.Ordinal);
            for (int i = 0; i < Channels.Count; i++)
            {
                string n = Channels[i].Name;
                if (!exact.ContainsKey(n)) exact[n] = i;
                string l = LooseKey(n);
                if (!loose.ContainsKey(l)) loose[l] = i;
            }
            _byName = exact;
            _byLooseName = loose;
        }

        /// <summary>공백과 대소문자를 지운 이름. 두 로그의 이름이 살짝 달라도 붙습니다.</summary>
        public static string LooseKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsWhiteSpace(c) || c == '_' || c == '-' || c == '.') continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>이름으로 채널을 찾습니다. 정확히 같은 이름을 먼저 봅니다. 없으면 -1.</summary>
        public int FindChannel(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            EnsureIndex();
            int i;
            if (_byName.TryGetValue(name, out i)) return i;
            if (_byLooseName.TryGetValue(LooseKey(name), out i)) return i;
            return -1;
        }

        /// <summary>t 에 가장 가까운 샘플 번호. 표본이 없으면 -1.</summary>
        public int IndexAt(double t)
        {
            int n = Times.Length;
            if (n == 0) return -1;
            int lo = 0, hi = n - 1;
            if (t <= Times[0]) return 0;
            if (t >= Times[n - 1]) return n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Times[mid] <= t) lo = mid; else hi = mid;
            }
            return (t - Times[lo]) <= (Times[hi] - t) ? lo : hi;
        }

        /// <summary>t 이하인 마지막 샘플 번호. t 가 첫 표본보다 앞이면 -1.</summary>
        public int IndexAtOrBefore(double t)
        {
            int n = Times.Length;
            if (n == 0 || t < Times[0]) return -1;
            if (t >= Times[n - 1]) return n - 1;
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Times[mid] <= t) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// 임의의 시각 t 에서의 값. 두 로그의 시간 격자가 달라도 한쪽에 맞춰
        /// 읽어 올 수 있습니다. 아날로그는 앞뒤를 선형 보간하고, 디지털과
        /// 상태는 직전 값을 유지합니다. 기록 구간 밖이면 NaN — 없는 값을
        /// 지어내지 않습니다.
        /// </summary>
        public double SampleAt(int ch, double t)
        {
            if (ch < 0 || ch >= Channels.Count) return double.NaN;
            int n = Times.Length;
            if (n == 0) return double.NaN;
            if (t < Times[0] || t > Times[n - 1]) return double.NaN;

            Channel c = Channels[ch];
            int i = IndexAtOrBefore(t);
            if (i < 0) return double.NaN;
            if (i >= n - 1) return c.Values[n - 1];

            float a = c.Values[i];
            if (c.IsStepped) return a;

            float b = c.Values[i + 1];
            if (float.IsNaN(a)) return double.NaN;
            if (float.IsNaN(b)) return a;
            double t0 = Times[i], t1 = Times[i + 1];
            if (t1 <= t0) return a;
            double f = (t - t0) / (t1 - t0);
            return a + (b - a) * f;
        }

        /// <summary>[t0,t1] 안에서 값이 바뀐 횟수.</summary>
        public int EdgeCount(int ch, double t0, double t1)
        {
            if (ch < 0 || ch >= Channels.Count) return 0;
            Channel c = Channels[ch];
            int i0 = Math.Max(0, IndexAtOrBefore(t0));
            int count = 0;
            float prev = float.NaN;
            bool have = false;
            for (int i = i0; i < Times.Length && Times[i] <= t1; i++)
            {
                float v = c.Values[i];
                if (float.IsNaN(v)) { have = false; continue; }
                if (have && v != prev) count++;
                prev = v; have = true;
            }
            return count;
        }

        public double TimeStart { get { return Times.Length > 0 ? Times[0] : 0.0; } }
        public double TimeEnd { get { return Times.Length > 0 ? Times[Times.Length - 1] : 1.0; } }

        /// <summary>시간축 값을 눈금 글자로 바꿉니다.</summary>
        public string FormatTime(double t)
        {
            switch (TimeKind)
            {
                case TimeKind.ClockMs:
                    {
                        if (double.IsNaN(t) || double.IsInfinity(t)) return "-";
                        double ms = t;
                        bool neg = ms < 0;
                        if (neg) ms = -ms;
                        long total = (long)Math.Floor(ms);
                        int msec = (int)(total % 1000);
                        long sec = total / 1000;
                        int s = (int)(sec % 60);
                        long min = sec / 60;
                        int m = (int)(min % 60);
                        long h = min / 60;
                        string text = msec != 0
                            ? string.Format("{0:00}:{1:00}:{2:00}.{3:000}", h, m, s, msec)
                            : string.Format("{0:00}:{1:00}:{2:00}", h, m, s);
                        return neg ? "-" + text : text;
                    }
                case TimeKind.DateMs:
                    {
                        try
                        {
                            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(t);
                            return dt.ToString("MM-dd HH:mm:ss");
                        }
                        catch (ArgumentOutOfRangeException) { return "-"; }
                    }
                case TimeKind.Index:
                    return "#" + ((long)Math.Round(t)).ToString();
                default:
                    {
                        string u = string.IsNullOrEmpty(TimeUnit) ? "" : " " + TimeUnit;
                        double a = Math.Abs(t);
                        if (a != 0 && (a < 0.001 || a >= 1e7)) return t.ToString("G6") + u;
                        return t.ToString("0.###") + u;
                    }
            }
        }
    }
}
