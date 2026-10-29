using System;
using System.Collections.Generic;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면 맨 위 <b>종합 분석 결과 (LLM)</b> 칸 (1.45).
    ///
    /// <code>
    ///   종합 분석 결과                                   [LLM 분석 실행] [취소]
    ///   ● 분석 1 로그 비교  완료   ● 분석 2 DB  안 함   ● 분석 3  완료      ← Checks
    ///   [■■■■■■□□□□] 서버에 보내는 중                                     ← 돌 때만
    ///   ┌ 결과 글 (고를 수 있음 · 길면 칸 안에서 굴림) ────────────────┐
    ///   └─────────────────────────────────────────────────────────────┘
    ///   2026-10-29 15:02 · 분석 1 · 3 결과로 분석                          ← FinishedText
    /// </code>
    /// 분석을 돌리는 것은 창(ShellWindow)이 하고, 이 뷰모델은 보여 줄 값만 듭니다 (WPF 없음 — 시험이 닿음).
    /// </summary>
    public sealed class LlmPanelVm : ObservableObject
    {
        public LlmPanelVm()
        {
            Checks = new List<LlmCheckVm> { new LlmCheckVm(1), new LlmCheckVm(2), new LlmCheckVm(3) };
        }

        // ---------------- 분석 셋의 체크 표시 ----------------

        /// <summary>분석 1 · 2 · 3 — 지금 결과가 있는지.</summary>
        public List<LlmCheckVm> Checks { get; private set; }

        /// <summary>체크 표시를 다시 적습니다 (MainVm.Refresh 가 부름).</summary>
        public void SetReady(int number, string title, bool ready, string note)
        {
            if (number < 1 || number > Checks.Count) return;
            Checks[number - 1].Set(title, ready, note);
            Raise("AnyReady"); Raise("CanRun");
        }

        public bool AnyReady
        {
            get { for (int i = 0; i < Checks.Count; i++) if (Checks[i].Ready) return true; return false; }
        }

        /// <summary>버튼을 누를 수 있는지 — 하나라도 결과가 있고 돌고 있지 않을 때.</summary>
        public bool CanRun { get { return AnyReady && !_running; } }

        // ---------------- 진행 ----------------

        private bool _running;
        private double _progress;
        private bool _unknown;
        private string _message = string.Empty;

        public bool IsRunning { get { return _running; } }

        /// <summary>진행바 값 0~100.</summary>
        public double Progress { get { return _progress; } }

        /// <summary>진행률을 모를 때 (왔다갔다 진행바).</summary>
        public bool ProgressUnknown { get { return _unknown; } }

        /// <summary>진행바 옆 글 ("서버에 보내는 중").</summary>
        public string Message { get { return _message; } }

        /// <summary>돌기 시작. 앞 결과 · 오류는 지웁니다.</summary>
        public void Start()
        {
            _running = true;
            _progress = 0;
            _unknown = true;
            _message = "시작하는 중";
            _error = string.Empty;
            RaiseRun();
            Raise("ErrorText"); Raise("HasError");
        }

        /// <summary>진행 알림. fraction 이 음수면 진행률을 모름.</summary>
        public void Report(string stage, double fraction)
        {
            if (!_running) return;
            _unknown = fraction < 0 || double.IsNaN(fraction);
            _progress = _unknown ? 0 : Math.Max(0, Math.Min(100, fraction * 100.0));
            if (!string.IsNullOrEmpty(stage)) _message = stage;
            Raise("Progress"); Raise("ProgressUnknown"); Raise("Message");
        }

        // ---------------- 결과 ----------------

        private string _result = string.Empty;
        private string _error = string.Empty;
        private string _finished = string.Empty;

        /// <summary>LLM 이 돌려준 종합 분석 결과 글.</summary>
        public string ResultText { get { return _result; } }
        public bool HasResult { get { return _result.Length > 0; } }

        public string ErrorText { get { return _error; } }
        public bool HasError { get { return _error.Length > 0; } }

        /// <summary>언제 · 무엇으로 낸 결과인지 — 그 뒤 분석이 바뀌었으면 옛 결과로 읽게.</summary>
        public string FinishedText { get { return _finished; } }

        /// <summary>결과도 오류도 없고 돌지도 않을 때의 안내.</summary>
        public bool ShowEmpty { get { return !_running && _result.Length == 0 && _error.Length == 0; } }

        /// <summary>결과를 받음.</summary>
        public void Finish(string result, string usedText, DateTime at)
        {
            _running = false;
            _result = result ?? string.Empty;
            if (_result.Length == 0) _result = "(LLM 이 빈 결과를 돌려주었습니다)";
            _finished = at.ToString("yyyy-MM-dd HH:mm:ss") + " · " + usedText + " 결과로 분석";
            _message = string.Empty;
            RaiseRun();
            Raise("ResultText"); Raise("HasResult"); Raise("FinishedText");
        }

        /// <summary>실패 — 앞 결과는 그대로 두고 오류만 적습니다.</summary>
        public void Fail(string error)
        {
            _running = false;
            _error = string.IsNullOrEmpty(error) ? "알 수 없는 오류" : error;
            _message = string.Empty;
            RaiseRun();
            Raise("ErrorText"); Raise("HasError");
        }

        /// <summary>취소됨 — 앞 결과는 그대로.</summary>
        public void Cancelled()
        {
            _running = false;
            _message = string.Empty;
            _error = "취소했습니다.";
            RaiseRun();
            Raise("ErrorText"); Raise("HasError");
        }

        private void RaiseRun()
        {
            Raise("IsRunning"); Raise("CanRun"); Raise("Progress"); Raise("ProgressUnknown");
            Raise("Message"); Raise("ShowEmpty");
        }

        /// <summary>"분석 1 · 3" — 결과가 있는 분석 이름들.</summary>
        public static string UsedText(bool a1, bool a2, bool a3)
        {
            var parts = new List<string>();
            if (a1) parts.Add("1");
            if (a2) parts.Add("2");
            if (a3) parts.Add("3");
            return parts.Count == 0 ? "분석 없음" : "분석 " + string.Join(" · ", parts.ToArray());
        }
    }

    /// <summary>체크 표시 하나 — "● 분석 1 로그 비교  완료".</summary>
    public sealed class LlmCheckVm : ObservableObject
    {
        public LlmCheckVm(int number) { Number = number; }

        public int Number { get; private set; }

        private string _title = string.Empty;
        private bool _ready;
        private string _note = string.Empty;

        /// <summary>"분석 1 · 로그 비교".</summary>
        public string Title { get { return _title; } }

        public bool Ready { get { return _ready; } }

        /// <summary>"완료" / "안 함".</summary>
        public string StateText { get { return _ready ? "완료" : "안 함"; } }

        /// <summary>마우스를 올리면 뜨는 설명 — 무엇을 보고 완료로 봤는지, 안 됐으면 무엇을 하면 되는지.</summary>
        public string Note { get { return _note; } }

        public void Set(string title, bool ready, string note)
        {
            _title = "분석 " + Number + " · " + (title ?? string.Empty);
            _ready = ready;
            _note = note ?? string.Empty;
            Raise("Title"); Raise("Ready"); Raise("StateText"); Raise("Note");
        }
    }
}
