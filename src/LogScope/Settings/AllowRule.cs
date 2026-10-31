using System;

namespace LogScope.Settings
{
    /// <summary>
    /// 분석 1 오차 목록에서 <b>"앞으로도 허용"</b> 을 고른 것 하나 (1.56).
    ///
    /// 다음 비교에서 같은 종류 · 같은 IO · 같은 단계의 오차가 <b>이번에 본 크기 이하</b>면
    /// 저절로 "허용됨" 이 됩니다. 더 크면 다시 오차로 뜹니다 — 한 번 허용했다고
    /// 그보다 큰 이상까지 숨기지 않게.
    ///
    /// 설정 파일(settings.json 의 "allowRules")에 적힙니다. 실행할 경로 같은 것은 없습니다.
    /// </summary>
    public sealed class AllowRule
    {
        /// <summary>오차 종류 (JudgeKind 의 이름: "Value" · "StepLength" · "IoMissing" · "IoExtra").</summary>
        public string Kind = string.Empty;

        /// <summary>IO 이름. 단계 길이 오차면 빈 글자.</summary>
        public string Io = string.Empty;

        /// <summary>단계 이름 (목록 글, 예: "가열 (2)"). 단계가 없는 로그면 빈 글자.</summary>
        public string Step = string.Empty;

        /// <summary>이 길이(시간축 단위 — 시각 로그면 ms)까지 허용. 값 오차만 씀.</summary>
        public double MaxDuration;

        /// <summary>이 크기까지 허용 — 값 오차면 범위를 벗어난 값, 단계 길이면 길이 차이(시간축 단위).</summary>
        public double MaxExcess;

        /// <summary>허용한 날짜 (목록에 보여 주기만).</summary>
        public DateTime CreatedAt = DateTime.Now;

        /// <summary>목록에 적는 한 줄.</summary>
        public string Describe(Func<double, string> duration, Func<double, string> value)
        {
            string where = (Io.Length > 0 ? Io : "단계 길이")
                         + (Step.Length > 0 ? " · " + Step : string.Empty);
            string size;
            if (Kind == "Value")
                size = (duration != null ? duration(MaxDuration) : MaxDuration.ToString()) + " · "
                     + (value != null ? value(MaxExcess) : MaxExcess.ToString()) + " 까지";
            else if (Kind == "StepLength")
                size = "±" + (duration != null ? duration(MaxExcess) : MaxExcess.ToString()) + " 까지";
            else
                size = Kind == "IoMissing" ? "이후 로그에 없어도" : "이후 로그에만 있어도";
            return where + " — " + size + "  (" + CreatedAt.ToString("yyyy-MM-dd") + ")";
        }
    }
}
