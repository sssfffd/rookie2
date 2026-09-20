using System;

namespace LogScope.Core.Io
{
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
