using System;
using System.Collections.Generic;

namespace LogScope.Common
{
    /// <summary>
    /// <b>분석 3 ↔ 메인 화면</b>을 잇는 자리 (1.29).
    ///
    /// 분석 3 은 Analysis3 폴더를 통째로 갈아 끼우는 화면이라, 창이나 메인 화면이
    /// 그 안의 클래스를 직접 알면 안 됩니다 (이름이 바뀌면 빌드가 깨짐). 그래서
    /// 둘 사이에 이 <b>게시판</b>을 하나 둡니다. 둘 다 여기만 봅니다.
    ///
    ///   메인 화면 분석 3 칸 ──(이전·이후 자리)──▶ Analysis3Link ──PathsChanged──▶ 분석 3 화면
    ///   메인 화면 분석 3 칸 ◀──ResultChanged──── Analysis3Link ◀──Publish(결과)── 분석 3 화면
    ///
    /// 분석 3 쪽에서 쓰는 것:
    ///   <see cref="BeforePath"/> · <see cref="AfterPath"/> · <see cref="HasBoth"/> — 지금 넣어 둔 자리
    ///   <see cref="PathsChanged"/> — 사람이 자리를 바꿨을 때 (둘 다 차면 바로 돌리면 됨)
    ///   <see cref="SetPath"/> — 분석 3 화면 안에서 파일을 골랐을 때 (메인 칸에도 보임)
    ///   <see cref="Publish"/> — 결과를 메인 화면 칸에 올림 · <see cref="ClearResult"/>
    ///   <see cref="OpenRequested"/> — 메인 칸의 요약 줄을 눌렀을 때 (그 줄의 Target)
    ///
    /// WPF 를 쓰지 않습니다 (시험이 닿게). <see cref="Publish"/> 는 배경 작업 안에서
    /// 불러도 됩니다 — 화면에 옮기는 일은 창(ShellWindow)이 화면 스레드로 넘겨서 합니다.
    /// </summary>
    public static class Analysis3Link
    {
        private static string _before = string.Empty;
        private static string _after = string.Empty;
        private static Analysis3Result _result;

        /// <summary>이전 자리 (파일이나 폴더). 안 넣었으면 빈 글자.</summary>
        public static string BeforePath { get { return _before; } }

        /// <summary>이후 자리 (파일이나 폴더). 안 넣었으면 빈 글자.</summary>
        public static string AfterPath { get { return _after; } }

        /// <summary>이전 · 이후가 다 찼는지.</summary>
        public static bool HasBoth { get { return _before.Length > 0 && _after.Length > 0; } }

        /// <summary>
        /// 사람이 자리를 바꿨을 때 (메인 칸에 놓거나 골랐을 때, 또는 분석 3 화면이
        /// <see cref="SetPath"/> 를 불렀을 때). 같은 값으로 다시 적으면 울리지 않습니다.
        ///
        /// 프로그램을 켤 때 지난번 자리를 되살리는 것(<see cref="Restore"/>)은 울리지
        /// 않습니다 — 켜자마자 저절로 돌지 않게. 켤 때의 자리는 위 두 값을 읽으면 됩니다.
        /// </summary>
        public static event EventHandler PathsChanged;

        /// <summary>결과가 올라오거나 지워졌을 때. 메인 화면이 듣습니다.</summary>
        public static event EventHandler ResultChanged;

        /// <summary>메인 칸의 요약 줄을 눌렀을 때 — 그 줄의 Target 이 실려 옵니다.</summary>
        public static event EventHandler<Analysis3OpenEventArgs> OpenRequested;

        /// <summary>한쪽 자리를 바꿉니다. 바뀌었으면 <see cref="PathsChanged"/>.</summary>
        public static void SetPath(bool before, string path)
        {
            string v = (path ?? string.Empty).Trim();
            if (before) SetPaths(v, _after);
            else SetPaths(_before, v);
        }

        /// <summary>두 자리를 한꺼번에 바꿉니다. 바뀌었으면 <see cref="PathsChanged"/> 한 번.</summary>
        public static void SetPaths(string before, string after)
        {
            string b = (before ?? string.Empty).Trim();
            string a = (after ?? string.Empty).Trim();
            if (b == _before && a == _after) return;
            _before = b;
            _after = a;
            EventHandler h = PathsChanged;
            if (h != null) h(null, EventArgs.Empty);
        }

        /// <summary>켤 때 설정에 남은 자리를 되살립니다. <b>알리지 않습니다.</b></summary>
        public static void Restore(string before, string after)
        {
            _before = (before ?? string.Empty).Trim();
            _after = (after ?? string.Empty).Trim();
        }

        /// <summary>지금 메인 칸에 올라가 있는 결과. 없으면 null.</summary>
        public static Analysis3Result Result { get { return _result; } }

        /// <summary>결과를 메인 화면 분석 3 칸에 올립니다. null 이면 지웁니다.</summary>
        public static void Publish(Analysis3Result result)
        {
            _result = result;
            EventHandler h = ResultChanged;
            if (h != null) h(null, EventArgs.Empty);
        }

        /// <summary>메인 칸의 결과를 지웁니다 ("아직 결과가 없습니다" 로).</summary>
        public static void ClearResult()
        {
            Publish(null);
        }

        /// <summary>메인 칸의 줄을 눌렀다고 분석 3 화면에 알립니다 (창이 부릅니다).</summary>
        public static void RequestOpen(string target)
        {
            EventHandler<Analysis3OpenEventArgs> h = OpenRequested;
            if (h != null) h(null, new Analysis3OpenEventArgs(target));
        }
    }

    /// <summary>메인 칸에서 누른 줄의 Target.</summary>
    public sealed class Analysis3OpenEventArgs : EventArgs
    {
        public readonly string Target;
        public Analysis3OpenEventArgs(string target) { Target = target ?? string.Empty; }
    }

    /// <summary>칸 · 줄의 색. None 은 회색.</summary>
    public enum Analysis3Grade
    {
        None,
        Good,
        Warn,
        Bad,
    }

    /// <summary>
    /// 메인 화면 분석 3 칸에 올리는 결과.
    ///
    /// 크게 적는 자리에 <b>낱말</b>(분석 2 처럼 PASS · WARNING · FAIL)이나 <b>점수</b>
    /// (분석 1 처럼 0~100) 중 하나를 씁니다. 둘 다 있으면 낱말이 이깁니다.
    ///
    ///   [FAIL]  차이 발생 [3] 개        ← Verdict · Count
    ///   ┌ 요약 ───────────────────┐
    ///   │ ● 항목 A        1 → 2   │    ← Lines (누르면 OpenRequested)
    ///   │ ● 항목 B        OK      │
    ///   └─────────────────────────┘
    ///   줄이 없으면 그 자리에 Note
    /// </summary>
    public sealed class Analysis3Result
    {
        /// <summary>크게 적는 낱말 (PASS · WARNING · FAIL 등). 비우면 점수를 씁니다.</summary>
        public string Verdict = string.Empty;

        /// <summary>점수 0~100. 쓰지 않으면 NaN 그대로 둡니다. 색은 분석 1 과 같은 기준.</summary>
        public double Score = double.NaN;

        /// <summary>
        /// 낱말 색. None 이면 낱말로 고릅니다 — PASS 초록 · WARNING/WARN 노랑 · FAIL 빨강,
        /// 그 밖의 낱말은 회색. 점수만 쓸 때는 보지 않습니다.
        /// </summary>
        public Analysis3Grade Grade = Analysis3Grade.None;

        /// <summary>낱말/점수 오른쪽 "차이 발생 N 개" 의 N. 비우면 그 자리를 숨깁니다.</summary>
        public string Count = string.Empty;

        /// <summary>낱말 오른쪽 작은 글 (낱말을 쓸 때만 보임). 비워도 됩니다.</summary>
        public string Note = string.Empty;

        /// <summary>요약 칸에 한 줄씩. 비우면 <see cref="EmptyText"/> 를 적습니다.</summary>
        public List<Analysis3Line> Lines = new List<Analysis3Line>();

        /// <summary>요약 줄이 없을 때 그 자리 글. 비우면 "적을 것이 없습니다."</summary>
        public string EmptyText = string.Empty;
    }

    /// <summary>요약 칸의 한 줄.</summary>
    public sealed class Analysis3Line
    {
        /// <summary>왼쪽 이름.</summary>
        public string Label = string.Empty;

        /// <summary>오른쪽 값 ("1 → 2" 등). 비워도 됩니다.</summary>
        public string Value = string.Empty;

        /// <summary>왼쪽 점 색.</summary>
        public Analysis3Grade Grade = Analysis3Grade.None;

        /// <summary>줄 아래 작은 글. 비워도 됩니다.</summary>
        public string Detail = string.Empty;

        /// <summary>
        /// 누르면 분석 3 화면으로 넘어가며 <see cref="Analysis3Link.OpenRequested"/> 로 실어 보낼 것.
        /// 비우면 누를 수 없는 줄입니다.
        /// </summary>
        public string Target = string.Empty;

        /// <summary>참이면 값을 빨간 글씨로 (값이 달라졌다는 뜻일 때).</summary>
        public bool ValueIsChange;

        public Analysis3Line() { }

        public Analysis3Line(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public Analysis3Line(string label, string value, Analysis3Grade grade)
            : this(label, value)
        {
            Grade = grade;
        }
    }
}
