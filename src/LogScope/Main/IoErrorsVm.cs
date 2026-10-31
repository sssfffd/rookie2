using System;
using System.Collections.Generic;
using LogScope.Analysis1.Judge;
using LogScope.Common;

namespace LogScope.Main
{
    /// <summary>
    /// IO 를 눌렀을 때 뜨는 작은 창(IoErrorsWindow, 1.62) 의 값 — 그 IO 의 구간별 오차와 허용.
    /// WPF 를 쓰지 않습니다 (시험이 닿음). 허용은 MainVm.Allow 로 넘기고 (메인 칸 · 설정 저장이 따라감),
    /// 목록만 다시 읽습니다.
    /// </summary>
    public sealed class IoErrorsVm : ObservableObject
    {
        private readonly MainVm _main;
        private readonly Func<List<JudgeItem>> _source;

        public IoErrorsVm(MainVm main, string title, Func<List<JudgeItem>> source)
        {
            _main = main;
            _source = source;
            Title = title ?? string.Empty;
            Rows = new List<ErrorRowVm>();
            Refresh();
        }

        /// <summary>창 제목 · 머리 — IO 이름 (또는 "공정 단계").</summary>
        public string Title { get; private set; }

        /// <summary>"오차 2 · 허용됨 1".</summary>
        public string Summary { get; private set; }

        public List<ErrorRowVm> Rows { get; private set; }
        public bool NoRows { get { return Rows.Count == 0; } }

        public void Refresh()
        {
            var rows = new List<ErrorRowVm>();
            List<JudgeItem> items = _source != null ? _source() : null;
            int open = 0;
            if (items != null)
            {
                foreach (JudgeItem it in items)
                {
                    rows.Add(new ErrorRowVm(it, _main.ItemText(it)));
                    if (!it.IsAllowed) open++;
                }
            }
            Rows = rows;
            Summary = rows.Count == 0 ? "오차 없음"
                    : "오차 " + open.ToString("N0") + (open < rows.Count ? " · 허용됨 " + (rows.Count - open).ToString("N0") : string.Empty);
            Raise("Rows"); Raise("NoRows"); Raise("Summary");
        }

        /// <summary>줄의 [이번만 허용] · [앞으로도 허용] · [허용 풀기].</summary>
        public void Allow(ErrorRowVm row, AllowChoice choice)
        {
            if (row == null) return;
            _main.Allow(row.Item, choice);
            Refresh();
        }
    }
}
