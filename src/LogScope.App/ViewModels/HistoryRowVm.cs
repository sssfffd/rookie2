using System.Globalization;
using LogScope.App.Infrastructure;
using LogScope.Core.History;

namespace LogScope.App.ViewModels
{
    /// <summary>
    /// 저장된 분석 한 건을 목록에 한 줄로. <b>지금 결과와의 차이</b>를 같이
    /// 적습니다 — 지난 수를 나란히 늘어놓기만 하면 머릿속으로 빼야 합니다.
    /// </summary>
    public sealed class HistoryRowVm : ObservableObject
    {
        public AnalysisRecord Record { get; private set; }

        public HistoryRowVm(AnalysisRecord record)
        {
            Record = record;
        }

        public string Id { get { return Record.Id; } }
        public string Name { get { return Record.DisplayName; } }
        public string BasisText { get { return Record.BasisText; } }

        /// <summary>
        /// 무엇을 견줬는지. 파일 <b>이름 전체</b>를 적습니다 — 발생시간만
        /// 적으면 같은 날 두 번 딴 로그를 가릴 수가 없습니다.
        /// </summary>
        public string LogsText
        {
            get
            {
                string b = Record.BeforeName.Length > 0 ? Record.BeforeName : "-";
                string a = Record.AfterName.Length > 0 ? Record.AfterName : "-";
                return b + "   →   " + a;
            }
        }

        /// <summary>풍선 도움말에 띄울 전체 경로.</summary>
        public string PathsText
        {
            get
            {
                string b = Record.BeforePath.Length > 0 ? Record.BeforePath : Record.BeforeName;
                string a = Record.AfterPath.Length > 0 ? Record.AfterPath : Record.AfterName;
                return "이전  " + b + "\n이후  " + a;
            }
        }

        /// <summary>분석 셋의 점수를 한 줄로. "100 / ?? / ??".</summary>
        public string ScoreText { get { return Record.AllScoresText; } }

        public string CountsText
        {
            get
            {
                return "견준 " + Record.ComparedCount.ToString("N0")
                     + " · 달라진 " + Record.ChangedCount.ToString("N0")
                     + " · 한쪽에만 " + Record.OneSidedCount.ToString("N0");
            }
        }

        // ---- 지금 결과와의 차이 -------------------------------------------

        private bool _live;          // 지금 견준 결과가 있는가
        private bool _same;          // 같은 기준으로 나온 수인가
        private int _dChanged;
        private double _dScore;
        private bool _dScoreOk;

        /// <summary>지금 결과를 넣어 차이를 다시 셉니다.</summary>
        public void Against(bool live, bool sameBasis,
                            bool hasScore, double score, int changedCount)
        {
            _live = live;
            _same = sameBasis;
            _dChanged = changedCount - Record.ChangedCount;
            // 점수 차이는 <b>분석 1</b> 것만 봅니다. 목록 한 줄에 셋을 다
            // 빼서 적으면 읽히지 않고, 지금 점수가 있는 것도 분석 1 뿐입니다.
            _dScoreOk = hasScore && Record.HasScores[0];
            _dScore = _dScoreOk ? score - Record.Scores[0] : 0;

            Raise("DeltaText"); Raise("DeltaKind"); Raise("HasDelta"); Raise("BasisWarning");
        }

        public bool HasDelta { get { return _live; } }

        /// <summary>
        /// 기준이 다르면 수를 나란히 놓아도 뜻이 없습니다. 조용히 빼서
        /// 보여 주면 "달라진 IO 가 줄었다" 로 읽히는데, 실은 허용 오차를
        /// 넓혔을 뿐일 수 있습니다.
        /// </summary>
        public string BasisWarning
        {
            get { return _live && !_same ? "기준이 달라 그대로 견줄 수 없습니다" : string.Empty; }
        }

        public string DeltaText
        {
            get
            {
                if (!_live) return string.Empty;
                if (!_same) return "기준 다름";

                string s = "달라진 IO " + Sign(_dChanged) + "개";
                if (_dScoreOk && _dScore != 0) s += "   점수 " + SignD(_dScore);
                return s;
            }
        }

        /// <summary>
        /// 색을 가르는 값. <b>달라진 IO 가 늘면 나쁨</b>입니다 — 지난번보다
        /// 어긋난 곳이 많아졌다는 뜻이니까요.
        /// </summary>
        public string DeltaKind
        {
            get
            {
                if (!_live || !_same) return "none";
                if (_dChanged > 0) return "bad";
                if (_dChanged < 0) return "good";
                return "same";
            }
        }

        private static string Sign(int v)
        {
            if (v > 0) return "+" + v.ToString("N0", CultureInfo.InvariantCulture);
            if (v < 0) return v.ToString("N0", CultureInfo.InvariantCulture);
            return "±0";
        }

        private static string SignD(double v)
        {
            if (v > 0) return "+" + v.ToString("0.#", CultureInfo.InvariantCulture);
            if (v < 0) return v.ToString("0.#", CultureInfo.InvariantCulture);
            return "±0";
        }
    }
}
