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
        //   RelativeTolerancePercent : 이전 값 대비 몇 %. 기본 0.1%.
        //   AbsoluteTolerance        : 값 그대로. 기본 0 (끔).
        public double RelativeTolerancePercent = ToleranceRule.DefaultPercent;
        public double AbsoluteTolerance;
        public DiffMetric SortMetric = DiffMetric.MaxAbs;

        /// <summary>목록 정렬 방향. 참이면 큰 값이 위로.</summary>
        public bool SortDescending = true;

        /// <summary>
        /// 차이량이 아니라 이름/구분으로 정렬 중이면 그 값.
        /// 0 = 차이량(SortMetric), 1 = IO 이름, 2 = 구분.
        /// </summary>
        public int SortColumn;

        /// <summary>
        /// 히트맵 칸에 차이를 퍼센트로 적을지. 거짓이면 값 그대로 적습니다.
        /// 기본은 값입니다. "몇 도 틀어졌나" 가 먼저 궁금한 경우가 많아서입니다.
        /// </summary>
        public bool HeatmapPercent;

        public bool AutoAlign = true;
        public double ManualShift;

        // 시간 맞추기 — 특정 IO 가 바뀌는 순간을 기준으로.
        //
        // 시작 시각끼리 맞추는 것(AutoAlign)은 기록을 시작한 시점이 같을 때만
        // 뜻이 있습니다. 설비가 기동한 순간처럼 로그 안의 사건으로 맞추려면
        // 기준이 될 IO 를 정해 둬야 합니다. IO 이름으로 저장하므로 다시 연
        // 로그에도 그대로 붙습니다.
        public string AlignIo = string.Empty;
        /// <summary>0 = 그 값이 될 때, 1 = 그 값에서 벗어날 때, 2 = 아무 변화.</summary>
        public int AlignEdge;
        /// <summary>몇 번째 변화로 맞출지. 1 이 첫 번째.</summary>
        public int AlignOccurrence = 1;
        /// <summary>
        /// 기준이 되는 값. 0/1 만이 아니라 그 IO 에 실제로 나온 값이면 무엇이든
        /// 됩니다 (0~5 로 움직이는 단계 신호면 5 도). NaN 이면 그 IO 에 맞게
        /// 알아서 고릅니다 — 1 이 있으면 1, 없으면 최댓값.
        /// </summary>
        public double AlignLevel = double.NaN;

        /// <summary>
        /// 가로축으로 쓸 IO 이름. 빈 글자면 로그의 시간 열을 씁니다.
        /// 역시 이름으로 저장합니다.
        /// </summary>
        public string AxisIo = string.Empty;
        // 이 둘은 <b>같이 켜지지 않습니다.</b> 벌려 놓고 그 사이를 칠하면
        // 칠해진 넓이가 "값 차이" 가 아니라 "값 차이 + 벌린 간격" 이 되어,
        // 눈으로 재는 넓이가 눈금과 안 맞습니다. 막는 곳은 화면 쪽(GraphVm)
        // 이고, 여기서는 옛 설정 파일이 둘 다 켜 둔 경우만 손봅니다.
        public bool ShadeDifference = true;         // 차이 영역 표시
        public bool SeparateTraces;                 // 파형 분리 보기

        // 그래프 기본값
        public bool LaneMode = true;                // true = 레인, false = 겹쳐보기
        /// <summary>
        /// 세로 눈금. raw = 값 그대로, delta = 이웃 표본과의 차이(차분).
        ///
        /// 예전에는 normalized(0–1 정규화)도 있었습니다. 없앴으므로 옛 설정
        /// 파일에서 그 값이 나오면 raw 로 되돌립니다 — 모르는 값을 그대로
        /// 들고 있으면 어느 단추도 안 켜진 채로 뜹니다.
        /// </summary>
        public string ValueScaleMode = "raw";       // raw | delta
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
            root["sortDescending"] = SortDescending;
            root["sortColumn"] = (double)SortColumn;
            root["heatmapPercent"] = HeatmapPercent;
            root["autoAlign"] = AutoAlign;
            root["alignIo"] = AlignIo;
            root["alignEdge"] = (double)AlignEdge;
            root["alignOccurrence"] = (double)AlignOccurrence;
            // NaN 은 JSON 에 담을 수 없어서 "알아서 고름" 은 아예 안 적습니다.
            if (!double.IsNaN(AlignLevel)) root["alignLevel"] = AlignLevel;
            root["axisIo"] = AxisIo;
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
            if (metric < 0 || metric > 7) metric = 0;
            s.SortMetric = (DiffMetric)metric;
            // 차이 면적은 화면에서 뺐습니다. 예전 설정에 남아 있으면
            // 아무 칸에도 삼각형이 붙지 않으므로 기본값으로 돌립니다.
            if (s.SortMetric == DiffMetric.Area) s.SortMetric = DiffMetric.MaxAbs;
            s.SortDescending = Json.GetBool(root, "sortDescending", true);
            s.SortColumn = Json.GetInt(root, "sortColumn", 0);
            // 3 은 예전의 "기준값" 칸이었습니다. 이제 기준은 채널마다 다르지
            // 않고 설정한 퍼센트 하나라, 그 칸이 없어졌습니다.
            if (s.SortColumn < 0 || s.SortColumn > 2) s.SortColumn = 0;
            s.HeatmapPercent = Json.GetBool(root, "heatmapPercent", false);
            s.AutoAlign = Json.GetBool(root, "autoAlign", true);
            s.AlignIo = Json.GetString(root, "alignIo", string.Empty);
            s.AlignEdge = Json.GetInt(root, "alignEdge", 0);
            if (s.AlignEdge < 0 || s.AlignEdge > 2) s.AlignEdge = 0;
            s.AlignOccurrence = Json.GetInt(root, "alignOccurrence", 1);
            if (s.AlignOccurrence < 1) s.AlignOccurrence = 1;
            s.AlignLevel = root.ContainsKey("alignLevel")
                ? Json.GetDouble(root, "alignLevel", double.NaN)
                : double.NaN;
            s.AxisIo = Json.GetString(root, "axisIo", string.Empty);
            s.ManualShift = Json.GetDouble(root, "manualShift", 0);
            s.ShadeDifference = Json.GetBool(root, "shadeDifference", true);
            s.SeparateTraces = Json.GetBool(root, "separateTraces", false);
            // 둘을 따로 켜던 시절의 설정 파일이 남아 있을 수 있습니다. 그대로
            // 두면 화면에서는 만들 수 없는 상태로 뜹니다. 차이 영역 쪽을 남깁니다.
            if (s.ShadeDifference && s.SeparateTraces) s.SeparateTraces = false;

            s.LaneMode = Json.GetBool(root, "laneMode", true);
            s.ValueScaleMode = Json.GetString(root, "valueScaleMode", "raw");
            if (s.ValueScaleMode != "delta") s.ValueScaleMode = "raw";
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
