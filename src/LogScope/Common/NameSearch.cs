using System;

namespace LogScope.Common
{
    /// <summary>
    /// <b>IO 이름 검색 규칙 한 곳</b> (1.17). 그래프 IO 목록 · 히트맵 · 분석 2
    /// 목록의 검색 칸이 모두 이것을 씁니다 — 화면마다 규칙이 다르면 같은 글을
    /// 쳤는데 한 화면에서만 나오는 IO 가 생깁니다.
    ///
    ///   - 이름에 그 글자가 <b>들어 있으면</b> 맞음 (앞부분일 필요 없음)
    ///   - 대소문자 안 가림 ("temp" 로 TEMP_MAX 가 나옴)
    ///   - 앞뒤 빈칸은 무시, 빈 검색은 모두 맞음
    /// </summary>
    public static class NameSearch
    {
        public static bool Matches(string name, string find)
        {
            if (find == null) return true;
            string f = find.Trim();
            if (f.Length == 0) return true;
            return name != null && name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>거를 글이 있는지 (빈칸만 있으면 없음).</summary>
        public static bool IsActive(string find)
        {
            return find != null && find.Trim().Length > 0;
        }
    }
}
