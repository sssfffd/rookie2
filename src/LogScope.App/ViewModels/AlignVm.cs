using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Align;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 가로축과 시간 맞추기. <b>그래프 화면과 히트맵 화면이 같은 것 하나를
    /// 나눠 씁니다.</b>
    ///
    /// 두 화면이 각자 따로 들고 있으면 한쪽에서 맞춰 놓고 다른 쪽으로 가면
    /// 그 설정이 안 보이거나, 더 나쁘게는 서로 다른 값으로 그려집니다.
    /// 값은 어차피 설정(AppSettings) 한 군데에 있으니, 그걸 보는 뷰모델도
    /// 하나만 두는 편이 맞습니다.
    /// </summary>
    public sealed class AlignVm : ObservableObject
    {
        private readonly AppState _state;

        public AlignVm(AppState state) { _state = state; }

        private AppSettings S { get { return _state.Settings; } }

        // ---- 목록 갈무리 --------------------------------------------------
        //
        // 후보를 뽑으려면 채널을 <b>전부 훑어야</b> 합니다. 가로축 후보는
        // 오름차순인지 보려고, 맞추기 후보는 한 번이라도 바뀌는지 보려고요.
        // 채널 200 개에 표본 5 만이면 그것만으로 천만 번입니다.
        //
        // 그런데 ItemsSource 로 묶인 속성은 화면이 다시 그려질 때마다 불립니다.
        // 갈무리해 두지 않으면 콤보를 한 번 펼 때마다 로그를 통째로 훑습니다.
        // 로그가 바뀔 때만(Reload) 버립니다.
        private List<string> _axisCache;
        private List<string> _alignCache;
        private LevelSet _levelCache;

        private LevelSet Levels
        {
            get
            {
                if (_levelCache == null) _levelCache = _state.AlignLevels();
                return _levelCache;
            }
        }

        /// <summary>
        /// 축이나 맞추기가 바뀌면 불립니다. 로그를 다시 견주고 화면을 새로
        /// 그려야 해서, 그 일을 맡은 셸 창이 받습니다.
        /// </summary>
        public event EventHandler Changed;

        private void Fire()
        {
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---------------- 가로축 ----------------

        /// <summary>가로축 목록에서 "로그의 시간 열" 을 나타내는 줄.</summary>
        public const string OwnTimeItem = "(로그의 시간)";

        public IEnumerable<string> AxisChoices
        {
            get
            {
                if (_axisCache == null)
                {
                    _axisCache = new List<string> { OwnTimeItem };
                    _axisCache.AddRange(_state.AxisCandidates());
                }
                return _axisCache;
            }
        }

        public string AxisIo
        {
            get { return S.AxisIo.Length == 0 ? OwnTimeItem : S.AxisIo; }
            set
            {
                // null 은 고른 게 아닙니다. 목록을 다시 채우면 WPF 가 잠깐
                // SelectedItem 을 null 로 밀어 넣는데, 그걸 "안 씀" 으로
                // 받아들이면 목록을 새로 고칠 때마다 사용자의 선택이
                // 조용히 지워집니다.
                if (value == null) return;
                string v = value == OwnTimeItem ? string.Empty : value;
                if (S.AxisIo == v) return;
                S.AxisIo = v;
                Raise("AxisIo"); Raise("Summary");
                Fire();
            }
        }

        // ---------------- 시간 맞추기 ----------------

        /// <summary>목록에서 "고르지 않음" 을 나타내는 줄.</summary>
        public const string NoneItem = "(안 씀 — 시작 시각으로 맞춤)";

        /// <summary>
        /// 기준으로 삼을 수 있는 IO 들. 양쪽 로그에 다 있고 한쪽에서라도
        /// 값이 바뀌는 IO 만 올립니다.
        /// </summary>
        public IEnumerable<string> AlignChoices
        {
            get
            {
                if (_alignCache == null)
                {
                    _alignCache = new List<string> { NoneItem };
                    _alignCache.AddRange(_state.AlignCandidates());
                }
                return _alignCache;
            }
        }

        public string AlignIo
        {
            get { return S.AlignIo.Length == 0 ? NoneItem : S.AlignIo; }
            set
            {
                if (value == null) return;    // 위 AxisIo 와 같은 이유입니다.
                string v = value == NoneItem ? string.Empty : value;
                if (S.AlignIo == v) return;
                S.AlignIo = v;
                // IO 를 바꾸면 값 목록이 통째로 달라집니다. 예전 IO 의 값을
                // 들고 있으면 새 IO 에는 없는 값이라 맞추기가 실패합니다.
                S.AlignLevel = double.NaN;
                _levelCache = null;
                Raise("AlignIo"); Raise("Summary"); RaiseLevels();
                Fire();
            }
        }

        // ---- 기준이 되는 값 ----
        //
        // 0/1 만이 아닙니다. 그 IO 에 실제로 나온 값이면 무엇이든 고를 수
        // 있어서, 0~5 로 움직이는 단계 신호는 "5 가 되는 순간" 으로도
        // 맞출 수 있습니다.

        public IEnumerable<string> LevelChoices { get { return Levels.Names; } }

        public bool HasLevels { get { return !Levels.IsEmpty; } }

        public int LevelIndex
        {
            get
            {
                LevelSet ls = Levels;
                if (ls.IsEmpty) return -1;
                int i = ls.IndexOf(S.AlignLevel);
                return i >= 0 ? i : ls.IndexOf(ls.Fallback());
            }
            set
            {
                LevelSet ls = Levels;
                if (value < 0 || value >= ls.Values.Length) return;
                double v = ls.Values[value];
                if (S.AlignLevel == v) return;
                S.AlignLevel = v;
                Raise("LevelIndex");
                if (S.AlignIo.Length > 0) Fire();
            }
        }

        private void RaiseLevels()
        {
            Raise("LevelChoices"); Raise("LevelIndex"); Raise("HasLevels");
        }

        public IEnumerable<string> EdgeChoices
        {
            get { return new[] { "그 값이 될 때", "그 값에서 벗어날 때", "값이 바뀔 때" }; }
        }

        public int EdgeIndex
        {
            get { return S.AlignEdge; }
            set
            {
                if (value < 0 || value > 2 || S.AlignEdge == value) return;
                S.AlignEdge = value;
                Raise("EdgeIndex");
                if (S.AlignIo.Length > 0) Fire();
            }
        }

        /// <summary>몇 번째 사건으로 맞출지.</summary>
        public string OccurrenceText
        {
            get { return S.AlignOccurrence.ToString(CultureInfo.InvariantCulture); }
            set
            {
                int v;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return;
                if (v < 1) v = 1;
                if (S.AlignOccurrence == v) return;
                S.AlignOccurrence = v;
                Raise("OccurrenceText");
                if (S.AlignIo.Length > 0) Fire();
            }
        }

        /// <summary>맞추기를 풀고 시작 시각 자동 맞춤으로 돌아갑니다.</summary>
        public void ClearAlign()
        {
            if (S.AlignIo.Length == 0) return;
            _state.ClearTriggerAlign();
            S.AlignLevel = double.NaN;
            _levelCache = null;
            Raise("AlignIo"); Raise("Summary"); RaiseLevels();
            Fire();
        }

        // ---------------- 화면에 적는 글 ----------------

        /// <summary>
        /// 지금 무엇으로 맞춰져 있는지 한 줄로. 못 맞췄으면 그 이유입니다.
        /// 가로축에 딸린 주의도 여기 붙습니다.
        /// </summary>
        public string Note
        {
            get
            {
                string a = _state.AlignNote;
                string b = _state.AxisNote;
                if (a.Length == 0) return b;
                if (b.Length == 0) return a;
                return a + "   /   " + b;
            }
        }

        /// <summary>도구 줄 단추에 적는 짧은 상태. 폭이 변하지 않게 짧게 둡니다.</summary>
        public string Summary
        {
            get
            {
                if (S.AlignIo.Length > 0 && S.AxisIo.Length > 0) return "축·맞추기 켜짐";
                if (S.AlignIo.Length > 0) return "맞추기 켜짐";
                if (S.AxisIo.Length > 0) return "가로축 바꿈";
                return "가로축 · 시간 맞추기";
            }
        }

        /// <summary>로그나 맞추기 결과가 바뀌었을 때 화면을 새로 고칩니다.</summary>
        public void Reload()
        {
            // 로그가 바뀌었을 수 있으니 갈무리를 버립니다.
            _axisCache = null;
            _alignCache = null;
            _levelCache = null;

            Raise("AxisChoices"); Raise("AxisIo");
            Raise("AlignChoices"); Raise("AlignIo");
            Raise("EdgeIndex"); Raise("OccurrenceText");
            RaiseLevels();
            Raise("Note"); Raise("Summary");
        }
    }
}
