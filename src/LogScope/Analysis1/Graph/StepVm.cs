using System.Collections.Generic;
using System;
using LogScope.Analysis1.Compare;
using LogScope.Common;

namespace LogScope.Analysis1.Graph
{
    // 1.50: 단계 — StepVm (단계 고르기 · 단추) · StepBands (그래프 위 단계 띠).
    //       같은 기능의 파일을 하나로 합쳤습니다. 각 덩어리 앞의 ==== 줄에 전 파일 이름이 있습니다.

    /// <summary>
    /// 그래프의 <b>단계 고르기</b>. 단계 하나를 고르면 그 단계만 보이고,
    /// 두 로그는 <b>그 단계의 시작끼리</b> 맞춰집니다.
    ///
    /// 전에는 단계마다 손으로 맞춰야 했습니다 — 기준 IO 를 고르고, 값을
    /// 고르고, "몇 번째 사건" 을 세어 넣고, 구간을 끌어다 맞추고. 단계가
    /// 여섯이면 그걸 여섯 번 합니다. 단계를 한 번 읽어 두면 그 셈은 이미
    /// 다 되어 있습니다 (<see cref="StepPair.Shift"/>).
    ///
    /// <b>이 뷰모델은 고른 번호만 들고 있습니다.</b> 실제로 다시 견주고
    /// 구간을 맞추는 일은 창이 합니다 (시간 맞추기와 같은 방식).
    /// </summary>
    public sealed class StepVm : ObservableObject
    {
        private readonly AppState _state;

        public StepVm(AppState state)
        {
            _state = state;

            // [눈금 0 = 단계 시작] 단추를 1.15 에 뺐습니다. 예전에 켜 둔 것이
            // 설정에 남아 있으면 끌 단추가 없는데 눈금만 0 부터 세어집니다.
            if (_state.Settings.StepZeroAtStart) _state.Settings.StepZeroAtStart = false;
        }

        /// <summary>목록 맨 위. 단계를 안 고른 상태입니다 (글자는 Texts/GraphTexts.StepWhole).</summary>
        public const string AllItem = GraphTexts.StepWhole;

        // 목록과 "목록 번호 → 짝 번호" 표. 로그가 바뀔 때만 다시 짭니다
        // (ItemsSource 는 화면이 그려질 때마다 읽습니다).
        private List<string> _choices;
        private List<int> _map;

        private void Fill()
        {
            if (_choices != null) return;

            _choices = new List<string> { AllItem };
            _map = new List<int> { -1 };

            StepPairList l = _state.StepPairs();
            for (int i = 0; i < l.Count; i++)
            {
                // 짝이 없는 단계는 목록에 올리지 않습니다. 골라도 맞출 수가
                // 없어서 고르면 아무 일이 안 일어나는데, 그건 고장으로
                // 보입니다. 몇 개가 빠졌는지는 아래 Note 가 말해 줍니다.
                if (!l[i].HasBoth) continue;
                _choices.Add(l[i].Label);
                _map.Add(i);
            }
        }

        public IEnumerable<string> Choices { get { Fill(); return _choices; } }

        /// <summary>고를 수 있는 단계가 있는지 (양쪽에 다 있는 단계).</summary>
        public bool HasSteps { get { return _state.HasSteps; } }

        /// <summary>목록에서 고른 자리. 0 은 전체 구간입니다.</summary>
        public int PickIndex
        {
            get
            {
                Fill();
                int at = _map.IndexOf(_state.StepPick);
                return at >= 0 ? at : 0;
            }
            set
            {
                Fill();
                if (value < 0 || value >= _map.Count) return;
                int pair = _map[value];
                if (_state.StepPick == pair) return;
                _state.StepPick = pair;

                // 칸 목록도 달라집니다 — 나눠 보는 중에 하나를 고르면
                // 그 칸 하나만 그립니다.
                _bands = null;

                Raise("PickIndex"); Raise("Note"); Raise("Summary"); Raise("Picked");
                Fire();
            }
        }

        /// <summary>단계를 하나 고른 상태인지.</summary>
        public bool Picked { get { return _state.PickedStep != null; } }

        /// <summary>목록을 한 칸 뒤로 / 앞으로. 단계를 훑어볼 때 씁니다.</summary>
        public void Step(int delta)
        {
            Fill();
            if (_choices.Count <= 1) return;

            int at = PickIndex + delta;
            // 전체(0)를 지나 돌지 않습니다. 끝에서 멈춥니다 — 돌아가면
            // 마지막 단계에서 [▶] 를 누른 사람이 첫 단계를 보게 됩니다.
            if (at < 0) at = 0;
            if (at >= _choices.Count) at = _choices.Count - 1;
            PickIndex = at;
        }

        // ---- 단계별로 나눠 보기 ----
        //
        // 한 줄 시간으로 쭉 보는 대신, 단계마다 칸을 하나씩 두고 나란히
        // 놓습니다. 칸마다 밀기 값이 따로라서 <b>모든 단계가 한 화면에서
        // 저마다 맞춰진 채로</b> 보입니다.

        private StepBands _bands;

        /// <summary>참이면 가로가 단계 칸 여러 개가 됩니다.</summary>
        public bool SplitByStep
        {
            get { return _state.Settings.StepSplit; }
            set
            {
                if (_state.Settings.StepSplit == value) return;
                _state.Settings.StepSplit = value;
                _bands = null;

                // 나눠 보는 중에는 한 단계만 고르는 것이 뜻이 없습니다
                // (모든 칸이 다 보입니다). 고른 것을 풀어 둬야 밀기 값도
                // 칸이 정하는 대로 갑니다.
                // 고른 단계는 그대로 둡니다 — 나눠 보기를 켜면 그 단계
                // 칸 하나만, 끄면 그 단계 구간만 보입니다. 둘 다 "그
                // 단계를 보고 있다" 라서 끄고 켤 때 뜻이 안 바뀝니다.

                Raise("SplitByStep"); Raise("CanPick"); Raise("CanZero");
                Raise("CanPickNow"); Raise("CanZeroNow"); Raise("NoStepText");
                Raise("PickIndex"); Raise("Picked"); Raise("Note"); Raise("Summary");
                Fire();
            }
        }

        // ---- 칸 머리를 눌러 그 단계만 (1.16) ----

        /// <summary>
        /// 나눠 보는 중에 칸 머리(단계 이름)를 눌렀을 때. 그 단계만 봅니다.
        /// <b>이미 그 단계만 보고 있으면 모든 칸으로 돌아갑니다</b> — 머리를
        /// 한 번 더 누르는 것이 "나가기" 입니다 (목록에서 [전체 구간] 을
        /// 골라도 됩니다).
        /// </summary>
        public void ToggleBand(int pair)
        {
            Fill();
            if (_state.StepPick == pair) { PickIndex = 0; return; }
            int at = _map.IndexOf(pair);
            if (at > 0) PickIndex = at;
        }

        /// <summary>
        /// 그래프에 넘길 칸 목록. 나눠 보기를 껐으면 null 입니다 (평소대로
        /// 한 줄 시간).
        /// </summary>
        public StepBands Bands
        {
            get
            {
                if (!SplitByStep) return null;

                // 나눠 보는 중에 한 단계를 골랐으면 <b>그 칸 하나만</b>
                // 그립니다 — 여러 칸으로 훑다가 하나를 들여다보는 길입니다.
                // [전체 구간] 으로 돌리면 모든 칸이 돌아옵니다.
                if (_bands == null) _bands = StepBands.Build(_state.StepPairs(), _state.StepPick);
                return _bands;
            }
        }

        /// <summary>
        /// 단계를 하나 고를 수 있는지. <b>나눠 보는 중에도 고를 수
        /// 있습니다</b> (1.01) — 그러면 그 칸 하나만 그립니다.
        ///
        /// 0.93 ~ 1.00 에는 나눠 보는 중에 잠가 뒀습니다 ("모든 칸이 다
        /// 보이니 고를 것이 없다" 고 생각했습니다). 그런데 칸이 여섯이면
        /// 한 칸이 화면 폭의 육분의 일이라, <b>하나를 들여다볼 길이
        /// 없었습니다.</b>
        /// </summary>
        public bool CanPick { get { return true; } }

        /// <summary>지금 고르기 단추를 켤지 (단계가 있으면 켭니다).</summary>
        public bool CanPickNow { get { return HasSteps; } }

        /// <summary>지금 [시작을 0 으로] 를 켤지.</summary>
        public bool CanZeroNow { get { return HasSteps && CanZero; } }

        /// <summary>
        /// 단계가 없을 때 도구 줄에 적는 한 줄. 있으면 빈 글자라 사라집니다.
        ///
        /// <b>묶음을 숨기지 않고 이 글을 띄웁니다.</b> 0.91 에는 단계가
        /// 없으면 묶음째 숨겼는데, 그러면 이 기능이 있다는 것 자체를 알
        /// 수가 없습니다 — "단계 묶는 기능이 UI 에 없는 것 같다" 는 말을
        /// 들은 까닭입니다.
        /// </summary>
        public string NoStepText
        {
            get
            {
                if (_state.HasSteps) return string.Empty;
                if (!_state.HasBoth) return "두 로그를 열면 켜집니다";
                return "설정 [비교] 의 \"단계 IO\" 를 적으면 켜집니다";
            }
        }

        /// <summary>
        /// [시작을 0 으로] 를 쓸 수 있는지. 나눠 보는 중에는 칸마다 그
        /// 단계 시작에서 시작하므로 이미 그 모양입니다.
        /// </summary>
        public bool CanZero { get { return !SplitByStep; } }

        // ---- 가로축을 단계 시작부터 세기 ----

        /// <summary>
        /// 가로축 글자를 단계 시작부터 셉니다 (0, +1초, +2초 …).
        /// 단계를 바꿔 가며 봐도 눈금이 늘 0 에서 시작해서, 다른 단계끼리도
        /// "시작 몇 초 뒤" 를 바로 견줄 수 있습니다.
        ///
        /// 그림은 안 옮깁니다 — 글자만 바뀝니다.
        /// </summary>
        public bool ZeroAtStart
        {
            get { return _state.Settings.StepZeroAtStart; }
            set
            {
                if (_state.Settings.StepZeroAtStart == value) return;
                _state.Settings.StepZeroAtStart = value;
                Raise("ZeroAtStart"); Raise("Note");
                Fire();
            }
        }

        /// <summary>가로축 0 으로 삼을 시각. 안 쓰면 NaN.</summary>
        public double ZeroTime
        {
            get
            {
                // 나눠 보는 중에는 눈금이 단계 이름이라 0 자리가 없습니다.
                if (SplitByStep) return double.NaN;

                StepPair p = _state.PickedStep;
                if (p == null || !ZeroAtStart) return double.NaN;
                return p.BeforeStart;
            }
        }

        /// <summary>
        /// 고른 단계가 차지하는 구간. 그래프를 이 구간으로 맞춥니다.
        ///
        /// <b>두 로그 중 긴 쪽까지 덮습니다.</b> 이후 로그의 단계가 더
        /// 길면 짧은 쪽에 맞춰 잘리는데, 잘린 줄 모르면 "뒤가 왜 없지" 가
        /// 됩니다 (길이가 많이 다르면 Note 가 그 말도 합니다).
        /// </summary>
        public bool TryWindow(out double t0, out double t1)
        {
            // 셈은 StepWindow 한 곳입니다 (시험이 닿는 자리).
            return StepWindow.Of(_state.PickedStep, out t0, out t1);
        }

        /// <summary>도구 줄 글. 지금 무엇을 보고 있는지.</summary>
        public string Summary
        {
            get
            {
                if (SplitByStep)
                {
                    StepPair one = _state.PickedStep;
                    return one != null ? "칸 " + one.Label : "단계별로 나눠 보기";
                }
                StepPair p = _state.PickedStep;
                if (p == null) return "단계";
                return "단계 " + p.Label;
            }
        }

        /// <summary>
        /// 아래에 적는 안내. 고른 단계의 알아 둘 점, 그리고 <b>목록에 올리지
        /// 못한 단계</b>가 몇 개인지. 없으면 빈 글자라 줄이 사라집니다.
        /// </summary>
        public string Note
        {
            get
            {
                var parts = new List<string>();

                // ---- 칸으로 나눠 보는 중 ----
                if (SplitByStep)
                {
                    StepBands b = Bands;
                    if (b == null || b.IsEmpty) parts.Add("나눠 그릴 단계가 없습니다.");
                    else if (b.Count == 1 && _state.PickedStep != null)
                    {
                        parts.Add("단계 " + _state.PickedStep.Label + " 칸 하나만 보고 있습니다 —"
                                  + " [전체 구간] 으로 돌리면 모든 칸이 돌아옵니다.");
                        parts.Add("휠로 확대, Home 으로 되돌리기.");
                    }
                    else
                    {
                        parts.Add("단계 " + b.Count + " 칸으로 나눠 그립니다 — 칸마다 그 단계의"
                                  + " 시작끼리 맞췄습니다.");
                        if (b.Skipped > 0)
                            parts.Add("짝이 없는 단계 " + b.Skipped + " 개는 칸으로 안 만들었습니다.");

                        // 확대하는 법을 적어 둡니다. 칸마다 따로 확대된다는
                        // 것은 해 보지 않으면 알 수 없습니다.
                        parts.Add("휠을 칸 위에 올리면 그 칸만 확대됩니다 (끌기도 그 칸만)."
                                  + "  +/- 와 ←/→ 는 모든 칸을 같이, Home 은 되돌리기.");

                        // 어디까지 칸마다 맞춰 셈하는지 적어 둡니다.
                        // 히트맵은 0.96 부터 칸마다 그 단계로 셉니다
                        // ([가로를 단계로] 를 켠 경우). 대시보드와 점수는
                        // 아직 밀기 값 하나입니다 — 안 적어 두면 그래프에서
                        // 맞은 것이 그 수에도 반영된 줄 압니다.
                        parts.Add("히트맵도 [가로를 단계로] 를 켜면 칸마다 그 단계로 맞춰 셉니다."
                                  + "  대시보드와 점수는 아직 밀기 값 하나로 셉니다.");
                    }
                }
                else
                {
                    // ---- 한 단계를 골랐을 때 ----
                    StepPair p = _state.PickedStep;
                    if (p != null)
                    {
                        parts.Add("이후 로그를 " + NumberText.Plain(p.Shift)
                                  + " 만큼 밀어 단계 시작을 맞췄습니다.");
                        if (p.Note.Length > 0) parts.Add(p.Note);
                        if (ZeroAtStart) parts.Add("가로축 눈금은 이 단계 시작부터 셉니다.");

                        if (_state.UsesTriggerAlign)
                            parts.Add("사건 맞추기는 비켜섰습니다 — [전체 구간] 으로 돌리면 돌아옵니다.");
                    }
                    else if (HasSteps)
                    {
                        // ---- 단계는 있는데 아무것도 안 켠 상태 ----
                        //
                        // 이 줄이 이 묶음의 설명서입니다. 단추 이름만으로는
                        // 무엇을 하는 것인지 알 수 없다는 말을 들었습니다.
                        StepPairList l = _state.StepPairs();
                        parts.Add("단계 " + l.UsableCount + " 개를 읽었습니다.");
                        parts.Add("[단계마다 칸으로] 를 켜면 단계마다 칸으로 쪼개 나란히 견줍니다"
                                  + " (칸마다 그 단계 시작끼리 맞춤).");
                        parts.Add("목록에서 한 단계만 골라 그 구간만 볼 수도 있습니다.");
                    }
                }

                StepPairList all = _state.StepPairs();
                if (all.Note.Length > 0) parts.Add(all.Note);
                if (!SplitByStep && _state.PickedStep == null && all.Source.Length > 0)
                    parts.Add("단계는 " + all.Source + " 에서 왔습니다.");

                return string.Join("   ", parts.ToArray());
            }
        }

        /// <summary>창이 받습니다 — 다시 견주고 구간을 맞춰야 합니다.</summary>
        public event EventHandler Changed;

        private void Fire()
        {
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        /// <summary>로그가 바뀌었을 때. 목록을 다시 짭니다.</summary>
        public void Reload()
        {
            _choices = null;
            _map = null;
            _bands = null;

            Raise("Choices"); Raise("PickIndex"); Raise("HasSteps");
            Raise("Picked"); Raise("Note"); Raise("Summary"); Raise("ZeroAtStart");
            Raise("SplitByStep"); Raise("CanPick"); Raise("CanZero");
            Raise("CanPickNow"); Raise("CanZeroNow"); Raise("NoStepText");
        }
    }

    // ======================================================================
    //  StepBands.cs — 1.50 에 이 파일로 합침
    // ======================================================================

    /// <summary>
    /// 그래프 가로에 나란히 놓는 <b>단계 칸</b> 하나.
    ///
    /// 칸 하나가 단계 하나이고, 칸마다 <b>제 밀기 값</b>을 들고 있습니다.
    /// 그래서 한 화면에서 모든 단계가 저마다 맞춰진 채로 보입니다 — 단계마다
    /// 손으로 맞출 일이 없습니다.
    /// </summary>
    public sealed class StepBand
    {
        /// <summary>칸 위에 적는 글 ("가열", 같은 이름 둘째부터 "가열 (2)").</summary>
        public string Label = string.Empty;

        /// <summary>이 칸이 보여 주는 구간 (이전 로그 시간 기준).</summary>
        public double T0;
        public double T1;

        /// <summary>이 칸에서 이후 로그를 미는 값 (이후 시각 + Shift = 이전 시각).</summary>
        public double Shift;

        /// <summary>
        /// 이 칸이 어느 단계 짝인지 (StepPairList 의 번호). 칸 머리를 눌러
        /// 그 단계만 볼 때 씁니다 (1.16). 칸 번호와 다릅니다 — 짝이 없는
        /// 단계는 칸이 안 생겨서 번호가 밀립니다.
        /// </summary>
        public int Pair = -1;

        /// <summary>
        /// 두 로그의 그 단계 <b>끝</b> (이전 로그 시간 기준, 이후는 밀어 둔 값)
        /// (1.18). 두 로그 단계 길이가 다르면(10 초 / 12 초) 짧은 쪽은 제
        /// 끝에서 선을 끊습니다 — 안 끊으면 칸 끝(긴 쪽 끝)까지 <b>다음 단계
        /// 값</b>이 이어져 그려져서, 다음 단계가 이 단계처럼 보입니다.
        /// </summary>
        public double BeforeEnd;
        public double AfterEnd;

        public double Length { get { return T1 - T0; } }
    }

    /// <summary>
    /// <b>단계별로 나눠 보기</b>. 가로를 한 줄 시간으로 쭉 늘어놓는 대신,
    /// 단계마다 칸을 하나씩 두고 나란히 놓습니다.
    ///
    /// <code>
    ///   [1. 준비 ][ 2. 가열     ][3. 유지 ][ 4. 냉각   ]
    /// </code>
    ///
    /// 칸마다 밀기 값이 따로라서, 이전·이후 로그가 <b>그 단계 안에서</b>
    /// 맞춰집니다. 공정이 중간에 늘어지거나 당겨진 로그를 견줄 때 한 줄
    /// 시간으로는 뒤로 갈수록 어긋나는데, 단계로 끊으면 그 어긋남이 칸
    /// 안에서 끝납니다.
    ///
    /// <b>단계 사이의 빈틈은 칸이 없습니다.</b> 공정이 멈춘 구간은 볼 것이
    /// 없고, 자리를 주면 그만큼 볼 것이 줄어듭니다 (히트맵의 칸 자르기와
    /// 같은 규칙입니다).
    /// </summary>
    public sealed class StepBands
    {
        private readonly List<StepBand> _bands;

        /// <summary>칸으로 만들지 못한 단계 수 (짝이 없는 단계).</summary>
        public readonly int Skipped;

        private StepBands(List<StepBand> bands, int skipped)
        {
            _bands = bands;
            Skipped = skipped;
        }

        public int Count { get { return _bands.Count; } }
        public StepBand this[int i] { get { return _bands[i]; } }
        public bool IsEmpty { get { return _bands.Count == 0; } }

        /// <summary>모든 칸의 길이 합. 폭을 길이에 비례해 나누는 데 씁니다.</summary>
        public double TotalLength
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < _bands.Count; i++) sum += _bands[i].Length;
                return sum;
            }
        }

        /// <summary>
        /// 짝지어 둔 단계로 칸을 만듭니다. <b>양쪽에 다 있는 단계만</b>
        /// 칸이 됩니다 — 한쪽에만 있는 단계는 맞출 값이 없어서, 칸을 만들면
        /// 이전 로그만 덜렁 그려지고 그게 "이후가 없다" 로 읽힙니다.
        /// 몇 개를 뺐는지는 <see cref="Skipped"/> 가 들고 있습니다.
        /// </summary>
        public static StepBands Build(StepPairList pairs)
        {
            return Build(pairs, -1);
        }

        /// <summary>
        /// 칸을 만듭니다. <paramref name="only"/> 가 0 이상이면 <b>그 단계
        /// 하나만</b> 칸으로 만듭니다 — 여러 칸으로 나눠 보다가 하나를
        /// 들여다볼 때 씁니다 (칸 하나가 화면 폭을 다 씁니다).
        ///
        /// 하나만 볼 때도 <b>칸 모드 그대로</b>입니다: 그 단계의 밀기 값으로
        /// 맞추고, 눈금에 단계 이름이 적히고, 확대도 칸 안에서 됩니다.
        /// 그래서 "하나만 보기" 와 "여러 개 보기" 를 오가도 보이는 뜻이
        /// 달라지지 않습니다.
        /// </summary>
        public static StepBands Build(StepPairList pairs, int only)
        {
            var list = new List<StepBand>();
            int skipped = 0;
            if (pairs == null) return new StepBands(list, 0);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (only >= 0 && i != only) continue;

                StepPair p = pairs[i];
                if (!p.HasBoth || double.IsNaN(p.Shift)) { skipped++; continue; }

                double t0 = p.BeforeStart;
                double t1 = Math.Max(p.BeforeEnd, p.AfterEnd + p.Shift);
                if (!(t1 > t0)) { skipped++; continue; }

                var b = new StepBand();
                b.Label = p.Label;
                b.T0 = t0;
                b.T1 = t1;
                b.Shift = p.Shift;
                b.Pair = i;
                b.BeforeEnd = p.BeforeEnd;
                b.AfterEnd = p.AfterEnd + p.Shift;
                list.Add(b);
            }
            return new StepBands(list, skipped);
        }

        /// <summary>
        /// 칸들의 가로 폭을 나눕니다. <b>길이에 비례</b>해서 나눕니다 —
        /// 똑같은 폭으로 나누면 짧은 단계가 늘어나고 긴 단계가 눌려서,
        /// 같은 기울기가 칸마다 다른 기울기로 보입니다.
        ///
        /// 다만 너무 짧은 단계가 <b>선 한 줄</b>이 되어 버리면 그 단계는
        /// 아예 못 봅니다. 그래서 최소 폭을 보장하고, 모자란 만큼은 넉넉한
        /// 칸들에서 비례로 떼어 옵니다.
        ///
        /// 칸이 많아 최소 폭조차 안 되면 <b>똑같이 나눕니다</b> (그 경우엔
        /// 비례를 지킬 자리가 없습니다).
        /// </summary>
        /// <summary>
        /// 칸들의 왼쪽 자리와 폭을 한꺼번에 냅니다 — <b>칸 사이를 띄워서</b>.
        ///
        /// 금 한 줄만 긋는 것보다 띄우는 쪽이 낫습니다. 칸 사이는 시간이
        /// <b>건너뛴</b> 자리인데, 선 하나로만 가르면 두 칸의 선이 이어진
        /// 하나로 읽힙니다.
        ///
        /// 자리를 내는 곳이 여기 하나여야 합니다 — 그리는 쪽·눈금 글자·
        /// 마우스가 모두 같은 자리를 봐야 하기 때문입니다. 각자 셈하면
        /// 눈금은 2 번 칸을 가리키는데 눌러 보면 3 번 칸이 잡힙니다.
        /// </summary>
        /// <param name="left">첫 칸의 왼쪽 자리.</param>
        /// <param name="total">칸들이 다 들어갈 너비 (틈까지 포함).</param>
        /// <param name="gap">칸 사이의 틈.</param>
        public void Layout(double left, double total, double gap, double min,
                           out double[] xs, out double[] widths)
        {
            int n = _bands.Count;
            xs = new double[n];
            widths = new double[n];
            if (n == 0) return;

            if (gap < 0) gap = 0;

            // 틈을 먼저 떼어 내고 남은 너비를 나눕니다. 틈이 너비를 다
            // 먹으면 틈을 줄입니다 — 칸이 하나도 안 보이는 것보다 낫습니다.
            double gaps = gap * (n - 1);
            if (gaps >= total * 0.5) { gap = n > 1 ? total * 0.5 / (n - 1) : 0; gaps = gap * (n - 1); }

            widths = Widths(total - gaps, min);

            double at = left;
            for (int i = 0; i < n; i++)
            {
                xs[i] = at;
                at += widths[i] + gap;
            }
        }

        public double[] Widths(double total, double min)
        {
            int n = _bands.Count;
            var w = new double[n];
            if (n == 0) return w;

            if (!(total > 0)) return w;
            if (min < 0) min = 0;

            // 최소 폭조차 안 들어가면 똑같이 나눕니다.
            if (min * n >= total)
            {
                for (int i = 0; i < n; i++) w[i] = total / n;
                return w;
            }

            double sum = TotalLength;
            if (!(sum > 0))
            {
                for (int i = 0; i < n; i++) w[i] = total / n;
                return w;
            }

            // 1) 길이에 비례해 나눕니다.
            for (int i = 0; i < n; i++) w[i] = total * (_bands[i].Length / sum);

            // 2) 최소 폭에 못 미치는 칸을 올려 주고, 올려 준 만큼을
            //    남은 칸들에서 비례로 떼어 옵니다. 떼어 오다 또 최소 폭
            //    아래로 내려가는 칸이 생기므로 더 내려갈 칸이 없을 때까지
            //    돌립니다 (칸 수만큼이면 반드시 끝납니다).
            for (int round = 0; round < n; round++)
            {
                double need = 0, spare = 0;
                for (int i = 0; i < n; i++)
                {
                    if (w[i] < min) need += min - w[i];
                    else spare += w[i] - min;
                }
                if (need <= 0 || spare <= 0) break;

                double take = Math.Min(need, spare);
                double scale = take / spare;
                for (int i = 0; i < n; i++)
                {
                    if (w[i] < min) w[i] = min;
                    else w[i] -= (w[i] - min) * scale;
                }
            }

            // 3) 반올림 오차로 전체가 조금 모자라거나 넘칠 수 있습니다.
            //    마지막 칸에서 맞춥니다 — 칸 사이에 틈이 생기면 그 자리가
            //    "기록 없음" 으로 보입니다.
            double got = 0;
            for (int i = 0; i < n; i++) got += w[i];
            w[n - 1] += total - got;
            return w;
        }
    }
}
