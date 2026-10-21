using System;

namespace LogScope.Core.Model
{
    /// <summary>
    /// <b>단계 기능의 글자를 고치는 곳</b> (1.15). 여기만 고치고 다시 빌드하면
    /// 그래프 도구 줄의 단추 이름과, 단계마다 붙는 이름이 바뀝니다.
    ///
    /// <code>
    ///   [정렬] [단계마다] [전체 구간]          ← Caption · Each · Whole
    ///
    ///   단계 이름 = LabelBefore + (단계 IO 값) + LabelAfter
    ///     LabelBefore = ""   LabelAfter = "단계"   →  "1단계"  "2단계"
    ///     LabelBefore = "S"  LabelAfter = ""       →  "S1"     "S2"
    /// </code>
    ///
    /// 단계 이름은 그래프 칸 머리 · 커서 정보 · 히트맵 [가로를 단계로] 칸
    /// 머리에 똑같이 쓰입니다 (만드는 곳이 <see cref="StepTable.Label"/> 하나).
    /// 같은 이름이 두 번째부터 붙는 " (2)" 는 LabelAfter 뒤에 붙습니다
    /// ("1단계 (2)").
    ///
    /// 단계를 <b>짝짓는</b> 데는 이 글자를 쓰지 않습니다 — 짝은 단계 IO 값
    /// 그대로 봅니다. 그래서 여기를 고쳐도 짝이 바뀌지 않습니다.
    /// </summary>
    public static class StepText
    {
        /// <summary>도구 줄 묶음의 맨 왼쪽 이름.</summary>
        public const string Caption = "정렬";

        /// <summary>단계마다 칸을 나눠 나란히 보는 단추.</summary>
        public const string Each = "단계마다";

        /// <summary>나누지 않고 시간 한 줄로 보는 단추.</summary>
        public const string Whole = "전체 구간";

        /// <summary>단계 이름 앞에 붙일 글. 비워 두면 안 붙습니다.</summary>
        public const string LabelBefore = "";

        /// <summary>단계 이름 뒤에 붙일 글. 비워 두면 안 붙습니다.</summary>
        public const string LabelAfter = "";
    }
}
