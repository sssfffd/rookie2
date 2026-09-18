using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using LogScope.App.Infrastructure;
using LogScope.App.Services;
using LogScope.Core.Compare;
using LogScope.Core.Settings;

namespace LogScope.App.ViewModels
{
    /// <summary>차이가 난 IO 목록의 한 줄.</summary>
    public sealed class DiffRowVm
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string MaxAbs { get; set; }
        public string MeanAbs { get; set; }
        public string Rms { get; set; }
        public string Area { get; set; }
        public string TimeRatio { get; set; }
        public string Segments { get; set; }
    }

    /// <summary>
    /// 목록의 제목 칸 하나. 누르면 그 기준으로 정렬되고, 지금 정렬 중인
    /// 칸에는 삼각형이 붙습니다 (내림차순이면 아래, 오름차순이면 위).
    /// </summary>
    public sealed class ColumnHeaderVm : ObservableObject
    {
        /// <summary>0 = 차이량(Metric), 1 = IO 이름, 2 = 구분.</summary>
        public int Kind { get; private set; }

        public DiffMetric Metric { get; private set; }
        public string Text { get; private set; }

        private ColumnHeaderVm(int kind, DiffMetric metric, string text)
        {
            Kind = kind; Metric = metric; Text = text;
        }

        public static ColumnHeaderVm ForMetric(DiffMetric m, string text)
        {
            return new ColumnHeaderVm(0, m, text);
        }

        public static ColumnHeaderVm ForName(string text)
        {
            return new ColumnHeaderVm(1, DiffMetric.MaxAbs, text);
        }

        public static ColumnHeaderVm ForKind(string text)
        {
            return new ColumnHeaderVm(2, DiffMetric.MaxAbs, text);
        }

        private bool _isSorted;
        public bool IsSorted
        {
            get { return _isSorted; }
            set { Set(ref _isSorted, value); }
        }

        private bool _descending = true;
        public bool Descending
        {
            get { return _descending; }
            set { Set(ref _descending, value); }
        }
    }

    /// <summary>
    /// 대시보드. 프로그램을 켜면 먼저 나오는 화면입니다.
    /// 이전/이후를 견준 결과를 네모 칸으로 요약하고, 차이가 난 IO 목록과
    /// 한쪽에만 있는 IO 목록을 보여 줍니다.
    /// </summary>
    public sealed class DashboardVm : ObservableObject
    {
        private readonly AppState _state;

        public DashboardVm(AppState state)
        {
            _state = state;
            BuildHeaders();
            Refresh();
        }

        // ---- 네모 칸 ----

        private string _beforeCount = "-";
        public string BeforeCount { get { return _beforeCount; } private set { Set(ref _beforeCount, value); } }

        private string _beforeNote = string.Empty;
        public string BeforeNote { get { return _beforeNote; } private set { Set(ref _beforeNote, value); } }

        private bool _beforeNoteBad;
        public bool BeforeNoteBad { get { return _beforeNoteBad; } private set { Set(ref _beforeNoteBad, value); } }

        private string _afterCount = "-";
        public string AfterCount { get { return _afterCount; } private set { Set(ref _afterCount, value); } }

        private string _afterNote = string.Empty;
        public string AfterNote { get { return _afterNote; } private set { Set(ref _afterNote, value); } }

        private bool _afterNoteGood;
        public bool AfterNoteGood { get { return _afterNoteGood; } private set { Set(ref _afterNoteGood, value); } }

        private string _changedCount = "-";
        public string ChangedCount { get { return _changedCount; } private set { Set(ref _changedCount, value); } }

        private string _changedNote = string.Empty;
        public string ChangedNote { get { return _changedNote; } private set { Set(ref _changedNote, value); } }

        private string _sampleCount = "-";
        public string SampleCount { get { return _sampleCount; } private set { Set(ref _sampleCount, value); } }

        private string _sampleNote = string.Empty;
        public string SampleNote { get { return _sampleNote; } private set { Set(ref _sampleNote, value); } }

        private string _files = "로그를 열어 주세요.";
        public string Files { get { return _files; } private set { Set(ref _files, value); } }

        private string _warning = string.Empty;
        public string Warning { get { return _warning; } private set { Set(ref _warning, value); } }

        // ---- 목록 ----

        private ObservableCollection<DiffRowVm> _changed = new ObservableCollection<DiffRowVm>();
        public ObservableCollection<DiffRowVm> Changed
        {
            get { return _changed; }
            private set { Set(ref _changed, value); }
        }

        private ObservableCollection<string> _onlyBefore = new ObservableCollection<string>();
        public ObservableCollection<string> OnlyBefore
        {
            get { return _onlyBefore; }
            private set { Set(ref _onlyBefore, value); }
        }

        private ObservableCollection<string> _onlyAfter = new ObservableCollection<string>();
        public ObservableCollection<string> OnlyAfter
        {
            get { return _onlyAfter; }
            private set { Set(ref _onlyAfter, value); }
        }

        public string ChangedEmptyText
        {
            get
            {
                return _state.Comparison == null
                    ? "이전 로그와 이후 로그를 모두 열면 여기에 차이가 나옵니다."
                    : "허용 오차 안에서 차이가 난 IO 가 없습니다.";
            }
        }

        public bool ChangedIsEmpty { get { return Changed.Count == 0; } }

        // ---- 정렬 기준 ----

        public static readonly string[] MetricNames =
        {
            "최대 차이", "평균 차이", "RMS", "차이 면적", "차이 시간 비율", "차이 표본 수", "차이 구간 수"
        };

        public IEnumerable<string> Metrics { get { return MetricNames; } }

        // 목록 제목 칸들. XAML 이 이 객체를 GridViewColumn.Header 로 씁니다.
        public ColumnHeaderVm ColName { get; private set; }
        public ColumnHeaderVm ColKind { get; private set; }
        public ColumnHeaderVm ColMax { get; private set; }
        public ColumnHeaderVm ColMean { get; private set; }
        public ColumnHeaderVm ColRms { get; private set; }
        public ColumnHeaderVm ColArea { get; private set; }
        public ColumnHeaderVm ColTime { get; private set; }
        public ColumnHeaderVm ColSegments { get; private set; }

        private List<ColumnHeaderVm> _headers;

        private void BuildHeaders()
        {
            ColName = ColumnHeaderVm.ForName("IO 이름");
            ColKind = ColumnHeaderVm.ForKind("구분");
            ColMax = ColumnHeaderVm.ForMetric(DiffMetric.MaxAbs, "최대 차이");
            ColMean = ColumnHeaderVm.ForMetric(DiffMetric.MeanAbs, "평균 차이");
            ColRms = ColumnHeaderVm.ForMetric(DiffMetric.Rms, "RMS");
            ColArea = ColumnHeaderVm.ForMetric(DiffMetric.Area, "차이 면적");
            ColTime = ColumnHeaderVm.ForMetric(DiffMetric.TimeRatio, "차이 시간");
            ColSegments = ColumnHeaderVm.ForMetric(DiffMetric.SegmentCount, "구간 수");

            _headers = new List<ColumnHeaderVm>
            {
                ColName, ColKind, ColMax, ColMean, ColRms, ColArea, ColTime, ColSegments
            };
            MarkSorted();
        }

        /// <summary>지금 정렬 중인 칸에만 삼각형이 붙도록 표시를 맞춥니다.</summary>
        private void MarkSorted()
        {
            AppSettings s = _state.Settings;
            foreach (ColumnHeaderVm h in _headers)
            {
                bool on = h.Kind == s.SortColumn
                          && (h.Kind != 0 || h.Metric == s.SortMetric);
                h.IsSorted = on;
                h.Descending = s.SortDescending;
            }
        }

        /// <summary>
        /// 제목 칸을 눌렀을 때. 같은 칸을 다시 누르면 방향이 뒤집힙니다.
        /// 차이량은 큰 값이 위로 오는 게 보통이라 내림차순으로 시작하고,
        /// 이름은 가나다순이 자연스러우니 오름차순으로 시작합니다.
        /// </summary>
        public void SortBy(ColumnHeaderVm header)
        {
            if (header == null) return;
            AppSettings s = _state.Settings;

            bool same = header.Kind == s.SortColumn
                        && (header.Kind != 0 || header.Metric == s.SortMetric);

            if (same) s.SortDescending = !s.SortDescending;
            else
            {
                s.SortColumn = header.Kind;
                if (header.Kind == 0) s.SortMetric = header.Metric;
                s.SortDescending = header.Kind == 0;   // 차이량은 큰 값부터
            }

            ApplySort();
            MarkSorted();
            FillChanged();
        }

        private void ApplySort()
        {
            if (_state.Comparison == null) return;
            AppSettings s = _state.Settings;
            switch (s.SortColumn)
            {
                case 1: DiffEngine.SortByName(_state.Comparison.Items, !s.SortDescending); break;
                case 2: DiffEngine.SortByKind(_state.Comparison.Items, !s.SortDescending); break;
                default: DiffEngine.SortByMetric(_state.Comparison.Items, s.SortMetric, s.SortDescending); break;
            }
        }

        // ---- 채우기 ----

        public void Refresh()
        {
            CompareResult c = _state.Comparison;

            string bf = string.IsNullOrEmpty(_state.BeforePath) ? "(없음)" : Path.GetFileName(_state.BeforePath);
            string af = string.IsNullOrEmpty(_state.AfterPath) ? "(없음)" : Path.GetFileName(_state.AfterPath);
            Files = "이전: " + bf + "        이후: " + af;

            int beforeChannels = _state.Before != null ? _state.Before.ChannelCount : 0;
            int afterChannels = _state.After != null ? _state.After.ChannelCount : 0;
            int onlyB = c != null ? c.OnlyBefore.Count : 0;
            int onlyA = c != null ? c.OnlyAfter.Count : 0;

            BeforeCount = beforeChannels.ToString("N0") + "개";
            BeforeNote = onlyB > 0 ? "이름이 이후에 없는 IO: " + onlyB + "개" : "이름이 이후에 없는 IO: 없음";
            BeforeNoteBad = onlyB > 0;

            AfterCount = afterChannels.ToString("N0") + "개";
            AfterNote = onlyA > 0 ? "이름이 이전에 없는 IO: " + onlyA + "개" : "이름이 이전에 없는 IO: 없음";
            AfterNoteGood = onlyA > 0;

            int changed = c != null ? c.ChangedCount : 0;
            int common = c != null ? c.CommonCount : 0;
            ChangedCount = changed.ToString("N0") + "개";
            ChangedNote = "양쪽에 다 있는 IO " + common + "개 중"
                        + "   /   허용 오차 값 범위의 "
                        + _state.Settings.RelativeTolerancePercent.ToString("0.####") + "%"
                        + (_state.Settings.AbsoluteTolerance > 0
                            ? " 또는 절대 " + _state.Settings.AbsoluteTolerance.ToString("0.######")
                              + " 중 큰 쪽"
                            : "");

            string sb = _state.Before != null ? _state.Before.SampleCount.ToString("N0") : "-";
            string sa = _state.After != null ? _state.After.SampleCount.ToString("N0") : "-";
            SampleCount = sb + " / " + sa;
            SampleNote = LoadSummary();

            Warning = c != null ? c.Warning : string.Empty;

            ApplySort();
            MarkSorted();
            FillChanged();
            FillOnly();
        }

        private string LoadSummary()
        {
            var parts = new List<string>();
            if (_state.Before != null) parts.Add("이전 " + _state.Before.LoadSeconds.ToString("0.0") + "초");
            if (_state.After != null) parts.Add("이후 " + _state.After.LoadSeconds.ToString("0.0") + "초");
            return parts.Count > 0 ? "여는 데 걸린 시간: " + string.Join(", ", parts.ToArray()) : "로그 없음";
        }

        private void FillChanged()
        {
            var rows = new ObservableCollection<DiffRowVm>();
            CompareResult c = _state.Comparison;
            if (c != null)
            {
                foreach (ChannelDiff d in c.Items)
                {
                    if (!d.Changed) continue;
                    rows.Add(new DiffRowVm
                    {
                        Name = d.Name,
                        Kind = d.ByName ? "상태 다름" : "값 다름",
                        MaxAbs = d.Format(DiffMetric.MaxAbs),
                        MeanAbs = d.Format(DiffMetric.MeanAbs),
                        Rms = d.Format(DiffMetric.Rms),
                        Area = d.Format(DiffMetric.Area),
                        TimeRatio = d.Format(DiffMetric.TimeRatio),
                        Segments = d.Format(DiffMetric.SegmentCount),
                    });
                }
            }
            Changed = rows;
            Raise("ChangedIsEmpty");
            Raise("ChangedEmptyText");
        }

        private void FillOnly()
        {
            var before = new ObservableCollection<string>();
            var after = new ObservableCollection<string>();
            CompareResult c = _state.Comparison;
            if (c != null)
            {
                foreach (ChannelDiff d in c.OnlyBefore) before.Add(d.Name);
                foreach (ChannelDiff d in c.OnlyAfter) after.Add(d.Name);
            }
            OnlyBefore = before;
            OnlyAfter = after;
        }
    }
}
