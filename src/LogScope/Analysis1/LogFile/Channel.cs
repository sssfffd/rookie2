using System.Collections.Generic;
using System.Globalization;
using System;
using LogScope.Common;

namespace LogScope.Analysis1.LogFile
{
    // 1.50: IO 채널 — Channel · ChannelKind · ChannelAccum (읽으며 쌓기) · FloatBuffer.
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>
    /// IO 채널 하나. 값은 float 로 담습니다. 채널 200 x 표본 5만 이면
    /// double 이면 80 MB, float 이면 40 MB 입니다. 로그 값은 소수점 몇 자리
    /// 수준이라 float 의 유효자리(약 7 자리)로 충분합니다.
    /// 비어 있는 칸은 NaN 이며 "여기서 선이 끊긴다"는 뜻입니다.
    /// </summary>
    public sealed class Channel
    {
        public string Name;
        public ChannelKind Kind;
        public float[] Values;

        /// <summary>Kind == State 일 때만 채워집니다. Values 는 이 배열의 인덱스.</summary>
        public string[] States;

        /// <summary>
        /// 단위 ("bar", "℃", "mm" 등). 못 찾으면 빈 글자.
        /// 엑셀 머리 행 아래의 단위 줄에서 가져오고, 없으면 이름 끝의
        /// 괄호에서 찾습니다 ("압력 PT-01 [bar]").
        /// 세로 눈금 옆에 적는 데 씁니다.
        /// </summary>
        public string Unit = string.Empty;

        /// <summary>NaN 을 뺀 최소/최대. 값이 하나도 없으면 둘 다 NaN.</summary>
        public double Min;
        public double Max;

        public Channel(string name, int sampleCount)
        {
            Name = name ?? string.Empty;
            Kind = ChannelKind.Analog;
            Unit = string.Empty;
            Values = new float[sampleCount];
            States = null;
            Min = double.NaN;
            Max = double.NaN;
        }

        public int SampleCount { get { return Values.Length; } }

        /// <summary>Min/Max 를 값 배열에서 다시 계산합니다.</summary>
        public void RecomputeRange()
        {
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            float[] v = Values;
            for (int i = 0; i < v.Length; i++)
            {
                float x = v[i];
                if (float.IsNaN(x)) continue;
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }
            if (lo > hi) { Min = double.NaN; Max = double.NaN; }
            else { Min = lo; Max = hi; }
        }

        /// <summary>계단으로 그려야 하는 채널인지 (디지털/상태).</summary>
        public bool IsStepped { get { return Kind != ChannelKind.Analog; } }

        /// <summary>
        /// 표본 하나를 글자로. <b>값이 없으면 빈 글자</b>입니다.
        ///
        /// <see cref="FormatValue"/> 와 다른 점은 거기뿐인데, 그 차이가
        /// 중요합니다. 단계를 뽑을 때 "값 없음" 은 <b>단계 밖</b>이라는
        /// 뜻이라, "-" 가 돌아오면 그게 단계 이름이 되어 버립니다.
        /// </summary>
        public string TextAt(int i)
        {
            if (Values == null || i < 0 || i >= Values.Length) return string.Empty;
            float v = Values[i];
            if (float.IsNaN(v)) return string.Empty;

            if (Kind == ChannelKind.State && States != null)
            {
                int k = (int)Math.Round(v);
                return k >= 0 && k < States.Length ? (States[k] ?? string.Empty) : string.Empty;
            }
            if (Kind == ChannelKind.Digital) return v >= 0.5f ? "1" : "0";
            return NumberText.Plain(v);
        }

        /// <summary>커서 자리에 보여줄 글자. 상태 채널이면 상태 이름을 돌려줍니다.</summary>
        public string FormatValue(double v)
        {
            if (double.IsNaN(v)) return "-";
            if (Kind == ChannelKind.State && States != null)
            {
                int i = (int)Math.Round(v);
                if (i >= 0 && i < States.Length) return States[i];
                return "?";
            }
            if (Kind == ChannelKind.Digital) return v >= 0.5 ? "1" : "0";
            double a = Math.Abs(v);
            if (a != 0 && (a < 0.001 || a >= 1e7)) return v.ToString("G6");
            return v.ToString("0.######");
        }
    }

    // ======================================================================
    //  ChannelKind.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>채널을 어떻게 그리고 어떻게 비교할지를 가르는 구분.</summary>
    public enum ChannelKind
    {
        /// <summary>0/1, TRUE/FALSE, ON/OFF, HIGH/LOW. 계단으로 그린다.</summary>
        Digital = 0,
        /// <summary>실수 값. 점을 이어서 그린다.</summary>
        Analog = 1,
        /// <summary>문자열 상태. 값은 States 배열의 인덱스이고 계단으로 그린다.</summary>
        State = 2,
    }

    // ======================================================================
    //  ChannelAccum.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 채널 하나를 읽어 나가는 동안의 상태. 값을 담으면서 동시에
    /// "이 채널은 디지털인가 아날로그인가 상태인가" 를 판단합니다.
    /// 다 읽고 나서 표를 한 번 더 훑지 않기 위해서입니다.
    /// </summary>
    public sealed class ChannelAccum
    {
        public string Name;

        /// <summary>단위. 단위 줄에서 읽었거나 이름 끝 괄호에서 꺼낸 값.</summary>
        public string Unit = string.Empty;
        private FloatBuffer _values;

        private bool _sawNumber;
        private bool _onlyZeroOne = true;
        private bool _sawBool;

        // 상태(문자열) 채널로 바뀐 뒤에만 채워집니다.
        private Dictionary<string, int> _stateMap;
        private List<string> _states;
        private readonly int _maxStates;
        private bool _stateOverflow;

        public ChannelAccum(string name, int capacity, int maxStates)
        {
            Name = name;
            _values = new FloatBuffer(capacity);
            _maxStates = maxStates;
        }

        public int Count { get { return _values.Count; } }
        public bool IsState { get { return _stateMap != null; } }

        public void AddEmpty() { _values.Add(float.NaN); }

        public void Add(Cell c)
        {
            switch (c.Kind)
            {
                case CellKind.Empty:
                    _values.Add(float.NaN);
                    return;

                case CellKind.Bool:
                    _sawBool = true;
                    AddNumber(c.Num);
                    return;

                case CellKind.Number:
                case CellKind.DateSerial:
                    AddNumber(c.Num);
                    return;

                default:
                    {
                        string s = c.Text;
                        double d;
                        if (ValueParse.TryNumber(s, out d)) { AddNumber(d); return; }
                        bool b;
                        if (ValueParse.TryBool(s, out b)) { _sawBool = true; AddNumber(b ? 1 : 0); return; }
                        AddState(s);
                        return;
                    }
            }
        }

        private void AddNumber(double d)
        {
            if (_stateMap != null)
            {
                // 이미 상태 채널이 되었으면 숫자도 상태 이름으로 넣습니다.
                AddState(d.ToString("0.######", CultureInfo.InvariantCulture));
                return;
            }
            _sawNumber = true;
            if (d != 0.0 && d != 1.0) _onlyZeroOne = false;
            _values.Add((float)d);
        }

        private void AddState(string s)
        {
            if (_stateMap == null) ConvertToState();

            int idx;
            if (!_stateMap.TryGetValue(s, out idx))
            {
                if (_maxStates > 0 && _states.Count >= _maxStates)
                {
                    _stateOverflow = true;
                    _values.Add(float.NaN);
                    return;
                }
                idx = _states.Count;
                _states.Add(s);
                _stateMap[s] = idx;
            }
            _values.Add(idx);
        }

        /// <summary>
        /// 숫자로 담아 둔 값들을 상태 표로 옮깁니다. 채널 중간에 처음으로
        /// 글자 값이 나왔을 때 한 번만 일어납니다.
        /// </summary>
        private void ConvertToState()
        {
            _stateMap = new Dictionary<string, int>(StringComparer.Ordinal);
            _states = new List<string>();

            float[] old = _values.ToArray();
            int n = _values.Count;
            var remap = new Dictionary<float, int>();
            var rebuilt = new FloatBuffer(Math.Max(4, n));
            for (int i = 0; i < n; i++)
            {
                float v = old[i];
                if (float.IsNaN(v)) { rebuilt.Add(float.NaN); continue; }
                int idx;
                if (!remap.TryGetValue(v, out idx))
                {
                    string text = ((double)v).ToString("0.######", CultureInfo.InvariantCulture);
                    if (!_stateMap.TryGetValue(text, out idx))
                    {
                        idx = _states.Count;
                        _states.Add(text);
                        _stateMap[text] = idx;
                    }
                    remap[v] = idx;
                }
                rebuilt.Add(idx);
            }
            _values = rebuilt;
        }

        public void PadTo(int n) { _values.PadTo(n); }

        public bool StateOverflow { get { return _stateOverflow; } }

        public Channel Finish(int sampleCount)
        {
            FloatBuffer b = _values;
            b.PadTo(sampleCount);

            var ch = new Channel(Name, 0);
            ch.Unit = Unit ?? string.Empty;
            ch.Values = b.ToArray();
            if (ch.Values.Length > sampleCount)
            {
                float[] cut = new float[sampleCount];
                Array.Copy(ch.Values, cut, sampleCount);
                ch.Values = cut;
            }

            if (_stateMap != null)
            {
                ch.Kind = ChannelKind.State;
                ch.States = _states.ToArray();
            }
            else if (_sawNumber && _onlyZeroOne)
            {
                ch.Kind = ChannelKind.Digital;
            }
            else if (_sawBool && !_sawNumber)
            {
                ch.Kind = ChannelKind.Digital;
            }
            else
            {
                ch.Kind = ChannelKind.Analog;
            }

            ch.RecomputeRange();
            return ch;
        }
    }

    // ======================================================================
    //  FloatBuffer.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 길이를 미리 모르는 채로 값을 이어 담는 배열. List&lt;float&gt; 대신
    /// 쓰는 이유는 다 담은 뒤 내부 배열을 그대로 넘겨받아 복사를 한 번 줄이기
    /// 위해서입니다. 채널 200 개 x 표본 5 만 이면 이 복사 한 번이 40 MB 입니다.
    /// </summary>
    public sealed class FloatBuffer
    {
        private float[] _items;
        private int _count;

        public FloatBuffer(int capacity)
        {
            _items = new float[capacity < 4 ? 4 : capacity];
        }

        public int Count { get { return _count; } }

        public void Add(float v)
        {
            if (_count == _items.Length) Grow(_count + 1);
            _items[_count++] = v;
        }

        /// <summary>index 까지 NaN 으로 채운 뒤 값을 넣습니다 (건너뛴 칸 = 끊김).</summary>
        public void SetAt(int index, float v)
        {
            if (index >= _items.Length) Grow(index + 1);
            while (_count < index) _items[_count++] = float.NaN;
            _items[index] = v;
            if (index >= _count) _count = index + 1;
        }

        public void PadTo(int length)
        {
            if (length <= _count) return;
            if (length > _items.Length) Grow(length);
            while (_count < length) _items[_count++] = float.NaN;
        }

        private void Grow(int need)
        {
            int cap = _items.Length;
            while (cap < need) cap = cap < 1024 ? cap * 2 : cap + (cap >> 1);
            Array.Resize(ref _items, cap);
        }

        /// <summary>정확히 Count 길이인 배열. 이미 꼭 맞으면 내부 배열을 그대로 돌려줍니다.</summary>
        public float[] ToArray()
        {
            if (_count == _items.Length) return _items;
            float[] r = new float[_count];
            Array.Copy(_items, r, _count);
            return r;
        }
    }
}
