using System;

namespace LogScope.Core.Io
{
    /// <summary>
    /// 표를 한 줄씩 흘려보내는 원본. CSV 든 XLSX 든 이 모양으로 맞춰
    /// TableBuilder 가 같은 코드로 처리합니다.
    ///
    /// 중요한 점: 표 전체를 메모리에 올리지 않습니다. NextRow 는 내부 버퍼를
    /// 다시 쓰므로, 호출한 쪽이 줄을 보관하려면 직접 복사해야 합니다.
    /// </summary>
    public interface IRowSource : IDisposable
    {
        /// <summary>다음 줄을 읽습니다. 더 없으면 false.</summary>
        bool NextRow(out Cell[] cells, out int count);

        /// <summary>진행률 0.0 ~ 1.0. 알 수 없으면 음수.</summary>
        double Progress { get; }

        /// <summary>지금까지 읽은 줄 수.</summary>
        long RowsRead { get; }
    }

    /// <summary>읽는 도중 진행 상황을 알리고 취소를 받습니다.</summary>
    public sealed class LoadProgress
    {
        private readonly Action<string, double> _report;
        private volatile bool _cancelled;

        public LoadProgress(Action<string, double> report) { _report = report; }

        public void Cancel() { _cancelled = true; }
        public bool IsCancelled { get { return _cancelled; } }

        /// <summary>fraction 이 음수면 "진행률을 모름" 이라는 뜻입니다.</summary>
        public void Report(string stage, double fraction)
        {
            if (_report != null) _report(stage, fraction);
        }

        public void ThrowIfCancelled()
        {
            if (_cancelled) throw new OperationCanceledException();
        }
    }
}
