using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Core.Model;

namespace LogScope.Core.Io
{
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
}
