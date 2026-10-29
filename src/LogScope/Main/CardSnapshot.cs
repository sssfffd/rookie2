using System;
using System.Collections.Generic;
using System.Globalization;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// 메인 화면 분석 칸 하나의 <b>요약을 그대로 찍어 둔 것</b> (1.42) — History 에 저장하고,
    /// 줄을 누르면 칸에 다시 펼칩니다.
    ///
    /// 칸에 보이는 것만 담습니다: 크게 적는 점수나 낱말 · 옆의 개수 · 작은 글, 요약 줄
    /// (분석 2 · 3) 과 그룹 줄 (분석 1), 그리고 무엇과 무엇을 견줬는지 (이름만). 로그 값 자체는
    /// 담지 않습니다 — 기록 하나가 몇 KB 로 남아야 수백 개가 쌓여도 가볍습니다.
    ///
    /// WPF 를 쓰지 않습니다 (시험이 닿게).
    /// </summary>
    public sealed class CardSnapshot
    {
        public int Number;
        public string Title = string.Empty;

        public bool HasScore;
        public double Score;

        public string Word = string.Empty;        // PASS · FAIL 등. 비면 점수 칸
        public string WordGrade = string.Empty;
        public string WordNote = string.Empty;
        public string Count = string.Empty;       // "차이 발생 N 개" 의 N

        public string LineNote = string.Empty;    // 요약이 비었을 때의 글
        public List<LineSnapshot> Lines = new List<LineSnapshot>();
        public List<GroupSnapshot> Groups = new List<GroupSnapshot>();

        public string BeforeName = string.Empty;  // 견준 것 (파일 · 폴더 이름)
        public string AfterName = string.Empty;

        // ---- History 줄의 한 칸에 적는 짧은 글 ----

        /// <summary>크게 적는 글 — 낱말이 있으면 낱말, 아니면 점수, 둘 다 없으면 "-".</summary>
        public string HeadText
        {
            get
            {
                if (Word.Length > 0) return Word;
                if (HasScore) return Score.ToString("0.#", CultureInfo.InvariantCulture);
                return "-";
            }
        }

        /// <summary>크게 적는 글의 색.</summary>
        public string HeadGrade
        {
            get
            {
                if (Word.Length > 0) return WordGrade.Length > 0 ? WordGrade : ScoreBands.GradeNone;
                return HasScore ? ScoreBands.GradeOf(Score) : ScoreBands.GradeNone;
            }
        }

        /// <summary>"차이 발생 3 개". 개수가 없으면 빈 글자.</summary>
        public string CountText
        {
            get { return Count.Length > 0 ? "차이 발생 " + Count + "개" : string.Empty; }
        }

        /// <summary>요약 한 줄 — 첫 줄과 남은 줄 수, 또는 그룹 수.</summary>
        public string SummaryText
        {
            get
            {
                if (Lines.Count > 0)
                {
                    string first = Lines[0].Label;
                    if (Lines[0].Value.Length > 0) first += "  " + Lines[0].Value;
                    return Lines.Count == 1 ? first : first + "  외 " + (Lines.Count - 1).ToString("N0") + "줄";
                }
                if (Groups.Count > 0)
                {
                    int changed = 0;
                    for (int i = 0; i < Groups.Count; i++) if (Groups[i].ChangedCount > 0) changed++;
                    return "그룹 " + Groups.Count.ToString("N0") + "개 중 " + changed.ToString("N0") + "개 달라짐";
                }
                return LineNote;
            }
        }

        /// <summary>"이전 → 이후" 이름. 둘 다 없으면 빈 글자.</summary>
        public string InputsText
        {
            get
            {
                if (BeforeName.Length == 0 && AfterName.Length == 0) return string.Empty;
                return (BeforeName.Length > 0 ? BeforeName : "-") + "  →  " + (AfterName.Length > 0 ? AfterName : "-");
            }
        }

        // ---- 저장 ----

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["n"] = (double)Number;
            d["title"] = Title ?? string.Empty;
            d["hasScore"] = HasScore;
            d["score"] = Score;
            d["word"] = Word ?? string.Empty;
            d["wordGrade"] = WordGrade ?? string.Empty;
            d["wordNote"] = WordNote ?? string.Empty;
            d["count"] = Count ?? string.Empty;
            d["lineNote"] = LineNote ?? string.Empty;
            d["before"] = BeforeName ?? string.Empty;
            d["after"] = AfterName ?? string.Empty;

            var lines = new List<object>();
            for (int i = 0; i < Lines.Count; i++) lines.Add(Lines[i].ToJson());
            d["lines"] = lines;

            var groups = new List<object>();
            for (int i = 0; i < Groups.Count; i++) groups.Add(Groups[i].ToJson());
            d["groups"] = groups;
            return d;
        }

        public static CardSnapshot FromJson(object parsed)
        {
            var s = new CardSnapshot();
            Dictionary<string, object> d = Json.AsObject(parsed);
            if (d.Count == 0) return s;

            s.Number = Json.GetInt(d, "n", 0);
            s.Title = Json.GetString(d, "title", string.Empty);
            s.HasScore = Json.GetBool(d, "hasScore", false);
            s.Score = Json.GetDouble(d, "score", 0);
            s.Word = Json.GetString(d, "word", string.Empty);
            s.WordGrade = Json.GetString(d, "wordGrade", string.Empty);
            s.WordNote = Json.GetString(d, "wordNote", string.Empty);
            s.Count = Json.GetString(d, "count", string.Empty);
            s.LineNote = Json.GetString(d, "lineNote", string.Empty);
            s.BeforeName = Json.GetString(d, "before", string.Empty);
            s.AfterName = Json.GetString(d, "after", string.Empty);

            List<object> lines = Json.GetArray(d, "lines");
            for (int i = 0; i < lines.Count; i++) s.Lines.Add(LineSnapshot.FromJson(lines[i]));

            List<object> groups = Json.GetArray(d, "groups");
            for (int i = 0; i < groups.Count; i++) s.Groups.Add(GroupSnapshot.FromJson(groups[i]));
            return s;
        }
    }

    /// <summary>요약 줄 하나 (분석 2 · 3).</summary>
    public sealed class LineSnapshot
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public string Grade = string.Empty;
        public string Detail = string.Empty;
        public string Target = string.Empty;
        public bool ValueIsChange;
        public int Card = 2;

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["label"] = Label ?? string.Empty;
            d["value"] = Value ?? string.Empty;
            d["grade"] = Grade ?? string.Empty;
            d["detail"] = Detail ?? string.Empty;
            d["target"] = Target ?? string.Empty;
            d["change"] = ValueIsChange;
            d["card"] = (double)Card;
            return d;
        }

        public static LineSnapshot FromJson(object parsed)
        {
            var l = new LineSnapshot();
            Dictionary<string, object> d = Json.AsObject(parsed);
            l.Label = Json.GetString(d, "label", string.Empty);
            l.Value = Json.GetString(d, "value", string.Empty);
            l.Grade = Json.GetString(d, "grade", string.Empty);
            l.Detail = Json.GetString(d, "detail", string.Empty);
            l.Target = Json.GetString(d, "target", string.Empty);
            l.ValueIsChange = Json.GetBool(d, "change", false);
            l.Card = Json.GetInt(d, "card", 2);
            return l;
        }
    }

    /// <summary>그룹 줄 하나 (분석 1). 누르면 그 그룹을 그래프에서 보도록 IO 이름도 담습니다.</summary>
    public sealed class GroupSnapshot
    {
        public string Name = string.Empty;
        public List<string> Members = new List<string>();
        public int MemberCount;
        public int ChangedCount;
        public string Detail = string.Empty;

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            d["name"] = Name ?? string.Empty;
            var m = new List<object>();
            for (int i = 0; i < Members.Count; i++) m.Add(Members[i] ?? string.Empty);
            d["members"] = m;
            d["memberCount"] = (double)MemberCount;
            d["changedCount"] = (double)ChangedCount;
            d["detail"] = Detail ?? string.Empty;
            return d;
        }

        public static GroupSnapshot FromJson(object parsed)
        {
            var g = new GroupSnapshot();
            Dictionary<string, object> d = Json.AsObject(parsed);
            g.Name = Json.GetString(d, "name", string.Empty);
            List<object> m = Json.GetArray(d, "members");
            for (int i = 0; i < m.Count; i++)
            {
                string one = m[i] as string;
                if (one != null) g.Members.Add(one);
            }
            g.MemberCount = Json.GetInt(d, "memberCount", g.Members.Count);
            g.ChangedCount = Json.GetInt(d, "changedCount", 0);
            g.Detail = Json.GetString(d, "detail", string.Empty);
            return g;
        }
    }
}
