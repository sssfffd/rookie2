using System;
using System.Collections.Generic;
using LogScope.Core.Model;
using LogScope.Logs.Align;

using LogScope.App.Common;
namespace LogScope.App.Logs
{
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

        public StepVm(AppState state) { _state = state; }

        /// <summary>목록 맨 위. 단계를 안 고른 상태입니다.</summary>
        public const string AllItem = "(전체 구간)";

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
                if (value) _state.StepPick = -1;

                Raise("SplitByStep"); Raise("CanPick"); Raise("CanZero");
                Raise("CanPickNow"); Raise("CanZeroNow"); Raise("NoStepText");
                Raise("PickIndex"); Raise("Picked"); Raise("Note"); Raise("Summary");
                Fire();
            }
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
                if (_bands == null) _bands = StepBands.Build(_state.StepPairs());
                return _bands;
            }
        }

        /// <summary>단계를 하나 고를 수 있는지 (나눠 보는 중에는 못 고릅니다).</summary>
        public bool CanPick { get { return !SplitByStep; } }

        /// <summary>지금 고르기 단추를 켤지 (단계가 있고, 나눠 보기가 꺼져 있고).</summary>
        public bool CanPickNow { get { return HasSteps && CanPick; } }

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
                if (SplitByStep) return "단계별로 나눠 보기";
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

                StepPair p = _state.PickedStep;
                if (p != null)
                {
                    parts.Add("이후 로그를 " + NumberText.Plain(p.Shift)
                              + " 만큼 밀어 단계 시작을 맞췄습니다.");
                    if (p.Note.Length > 0) parts.Add(p.Note);
                    if (ZeroAtStart) parts.Add("가로축은 이 단계 시작부터 셉니다.");

                    // 사건 맞추기가 걸려 있으면 그건 비켜섭니다 (단계가
                    // 이깁니다). 말 안 하면 팝업에 "맞추기 켜짐" 이 떠
                    // 있는데 실제로는 안 먹는 상태가 됩니다.
                    if (_state.UsesTriggerAlign)
                        parts.Add("사건 맞추기는 비켜섰습니다 — [전체 구간] 으로 돌리면 돌아옵니다.");
                }

                if (SplitByStep)
                {
                    StepBands b = Bands;
                    if (b == null || b.IsEmpty) parts.Add("나눠 그릴 단계가 없습니다.");
                    else
                    {
                        parts.Add("단계 " + b.Count + " 칸으로 나눠 그립니다 — 칸마다 그 단계의"
                                  + " 시작끼리 맞췄습니다.");
                        if (b.Skipped > 0)
                            parts.Add("짝이 없는 단계 " + b.Skipped + " 개는 칸으로 안 만들었습니다.");

                        // 그래프만 칸으로 나눠 그립니다. 견준 수는 밀기 값
                        // 하나로 센 것이라, 그 말을 안 해 두면 그래프에서
                        // 맞은 것이 수에도 반영된 줄 압니다.
                        parts.Add("견준 수(대시보드 · 히트맵 · 점수)는 밀기 값 하나로 센 것입니다.");
                    }
                    parts.Add("가로로 확대·이동은 잠깁니다 (칸의 구간은 그 단계로 정해져 있습니다).");
                }

                StepPairList l = _state.StepPairs();
                if (l.Note.Length > 0) parts.Add(l.Note);
                if (p == null && l.Source.Length > 0) parts.Add("단계는 " + l.Source + " 에서 왔습니다.");

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
}
