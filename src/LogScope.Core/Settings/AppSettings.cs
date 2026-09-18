using System;
using System.Collections.Generic;
using LogScope.Core.Compare;

namespace LogScope.Core.Settings
{
    /// <summary>
    /// 그룹 하나. 구성원을 채널 번호가 아니라 IO 이름으로 저장합니다.
    /// 그래야 다른 파일을 열어도 같은 이름의 IO 에 그대로 붙습니다.
    /// </summary>
    public sealed class GroupDef
    {
        public string Name = "그룹";
        public List<string> Members = new List<string>();
        public bool Expanded = true;

        public GroupDef() { }
        public GroupDef(string name) { Name = name; }

        public GroupDef Clone()
        {
            var g = new GroupDef(Name);
            g.Members = new List<string>(Members);
            g.Expanded = Expanded;
            return g;
        }
    }

    /// <summary>한 세트 = 이전 로그 + 이후 로그 한 쌍.</summary>
    public sealed class LogSet
    {
        public string Title = string.Empty;
        public string BeforePath = string.Empty;
        public string AfterPath = string.Empty;

        /// <summary>파일 열기 창이 처음 가리킬 폴더. 세트마다, 이전/이후 따로.</summary>
        public string BeforeFolder = string.Empty;
        public string AfterFolder = string.Empty;

        public string DisplayName(int index)
        {
            return string.IsNullOrEmpty(Title) ? (index + 1) + "번 세트" : (index + 1) + ". " + Title;
        }
    }

    public sealed class AppSettings
    {
        public const int SetCount = 6;
        public const int FileVersion = 1;

        public string Theme = "light";              // "light" 또는 "dark"
        public int ActiveSet;
        public LogSet[] Sets = NewSets();

        public List<GroupDef> Groups = new List<GroupDef>();

        // 비교 기본값
        //
        // 기준값은 두 가지 중 큰 쪽입니다 (ToleranceRule 참고).
        //   RelativeTolerancePercent : 채널 값 범위의 몇 %. 기본 0.1%.
        //   AbsoluteTolerance        : 값 그대로. 기본 0 (끔).
        public double RelativeTolerancePercent = ToleranceRule.DefaultPercent;
        public double AbsoluteTolerance;
        public DiffMetric SortMetric = DiffMetric.MaxAbs;
        public bool AutoAlign = true;
        public double ManualShift;
        public bool ShadeDifference = true;
        public bool SeparateTraces;                 // 겹칠 때 위아래로 조금 벌려 그리기

        // 그래프 기본값
        public bool LaneMode = true;                // true = 레인, false = 겹쳐보기
        public string ValueScaleMode = "raw";       // raw | normalized | delta
        public bool FitVisible;                     // 보이는 구간에 세로 배율 맞춤

        // 선택 기능 (파이썬 AI 모듈)
        public bool AiEnabled;
        public string PythonPath = string.Empty;
        public string AiScriptPath = string.Empty;

        // 창 상태
        public int WindowWidth = 1360;
        public int WindowHeight = 860;
        public bool WindowMaximized;

        private static LogSet[] NewSets()
        {
            var s = new LogSet[SetCount];
            for (int i = 0; i < SetCount; i++) s[i] = new LogSet();
            return s;
        }

        public LogSet ActiveLogSet
        {
            get
            {
                if (ActiveSet < 0 || ActiveSet >= SetCount) ActiveSet = 0;
                return Sets[ActiveSet];
            }
        }

        // ---------------- 직렬화 ----------------

        public Dictionary<string, object> ToJson()
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            root["fileVersion"] = (double)FileVersion;
            root["savedBy"] = BuildInfo.Product + " " + BuildInfo.Version;
            root["theme"] = Theme;
            root["activeSet"] = (double)ActiveSet;

            var sets = new List<object>();
            for (int i = 0; i < SetCount; i++)
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                d["title"] = Sets[i].Title ?? string.Empty;
                d["beforePath"] = Sets[i].BeforePath ?? string.Empty;
                d["afterPath"] = Sets[i].AfterPath ?? string.Empty;
                d["beforeFolder"] = Sets[i].BeforeFolder ?? string.Empty;
                d["afterFolder"] = Sets[i].AfterFolder ?? string.Empty;
                sets.Add(d);
            }
            root["sets"] = sets;

            var groups = new List<object>();
            for (int i = 0; i < Groups.Count; i++)
            {
                var g = new Dictionary<string, object>(StringComparer.Ordinal);
                g["name"] = Groups[i].Name ?? string.Empty;
                g["expanded"] = Groups[i].Expanded;
                var members = new List<object>();
                foreach (string m in Groups[i].Members) members.Add(m);
                g["members"] = members;
                groups.Add(g);
            }
            root["groups"] = groups;

            root["relativeTolerancePercent"] = RelativeTolerancePercent;
            root["absoluteTolerance"] = AbsoluteTolerance;
            root["sortMetric"] = (double)(int)SortMetric;
            root["autoAlign"] = AutoAlign;
            root["manualShift"] = ManualShift;
            root["shadeDifference"] = ShadeDifference;
            root["separateTraces"] = SeparateTraces;

            root["laneMode"] = LaneMode;
            root["valueScaleMode"] = ValueScaleMode ?? "raw";
            root["fitVisible"] = FitVisible;

            root["aiEnabled"] = AiEnabled;
            root["pythonPath"] = PythonPath ?? string.Empty;
            root["aiScriptPath"] = AiScriptPath ?? string.Empty;

            root["windowWidth"] = (double)WindowWidth;
            root["windowHeight"] = (double)WindowHeight;
            root["windowMaximized"] = WindowMaximized;
            return root;
        }

        public static AppSettings FromJson(object parsed)
        {
            var s = new AppSettings();
            Dictionary<string, object> root = Json.AsObject(parsed);
            if (root.Count == 0) return s;

            s.Theme = Json.GetString(root, "theme", s.Theme);
            s.ActiveSet = Json.GetInt(root, "activeSet", 0);
            if (s.ActiveSet < 0 || s.ActiveSet >= SetCount) s.ActiveSet = 0;

            List<object> sets = Json.GetArray(root, "sets");
            for (int i = 0; i < sets.Count && i < SetCount; i++)
            {
                Dictionary<string, object> d = Json.AsObject(sets[i]);
                s.Sets[i].Title = Json.GetString(d, "title", string.Empty);
                s.Sets[i].BeforePath = Json.GetString(d, "beforePath", string.Empty);
                s.Sets[i].AfterPath = Json.GetString(d, "afterPath", string.Empty);
                s.Sets[i].BeforeFolder = Json.GetString(d, "beforeFolder", string.Empty);
                s.Sets[i].AfterFolder = Json.GetString(d, "afterFolder", string.Empty);
            }

            List<object> groups = Json.GetArray(root, "groups");
            s.Groups.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                Dictionary<string, object> g = Json.AsObject(groups[i]);
                var def = new GroupDef(Json.GetString(g, "name", "그룹 " + (i + 1)));
                def.Expanded = Json.GetBool(g, "expanded", true);
                foreach (object m in Json.GetArray(g, "members"))
                {
                    string name = m as string;
                    if (!string.IsNullOrEmpty(name)) def.Members.Add(name);
                }
                s.Groups.Add(def);
            }

            s.RelativeTolerancePercent =
                Json.GetDouble(root, "relativeTolerancePercent", ToleranceRule.DefaultPercent);
            if (s.RelativeTolerancePercent < 0 || s.RelativeTolerancePercent > 100)
                s.RelativeTolerancePercent = ToleranceRule.DefaultPercent;

            // "tolerance" 는 예전 이름입니다. 옛 설정 파일도 그대로 열리게 둡니다.
            s.AbsoluteTolerance = Json.GetDouble(root, "absoluteTolerance",
                                                 Json.GetDouble(root, "tolerance", 0));
            if (s.AbsoluteTolerance < 0) s.AbsoluteTolerance = 0;
            int metric = Json.GetInt(root, "sortMetric", 0);
            if (metric < 0 || metric > 6) metric = 0;
            s.SortMetric = (DiffMetric)metric;
            s.AutoAlign = Json.GetBool(root, "autoAlign", true);
            s.ManualShift = Json.GetDouble(root, "manualShift", 0);
            s.ShadeDifference = Json.GetBool(root, "shadeDifference", true);
            s.SeparateTraces = Json.GetBool(root, "separateTraces", false);

            s.LaneMode = Json.GetBool(root, "laneMode", true);
            s.ValueScaleMode = Json.GetString(root, "valueScaleMode", "raw");
            s.FitVisible = Json.GetBool(root, "fitVisible", false);

            s.AiEnabled = Json.GetBool(root, "aiEnabled", false);
            s.PythonPath = Json.GetString(root, "pythonPath", string.Empty);
            s.AiScriptPath = Json.GetString(root, "aiScriptPath", string.Empty);

            s.WindowWidth = Json.GetInt(root, "windowWidth", 1360);
            s.WindowHeight = Json.GetInt(root, "windowHeight", 860);
            s.WindowMaximized = Json.GetBool(root, "windowMaximized", false);
            return s;
        }
    }
}
