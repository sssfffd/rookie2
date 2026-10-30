using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace LogScope.Common
{
    /// <summary>LLM 서버에 닿는지.</summary>
    public enum LlmServerState
    {
        /// <summary>아직 확인 안 함.</summary>
        Unknown,
        /// <summary>확인하는 중.</summary>
        Checking,
        /// <summary>닿음 — 분석 3 · 종합 분석 결과를 쓸 수 있음.</summary>
        Ok,
        /// <summary>안 닿음 (또는 주소가 없음).</summary>
        Fail,
    }

    /// <summary>
    /// <b>LLM 서버 연결 확인</b> (1.51). 설정의 "LLM 서버 주소" 로 닿는지 봅니다 — 닿을 때만 분석 3 과
    /// 메인 화면 종합 분석 결과를 쓸 수 있습니다 (LLM 이 안 되는 환경일 수 있어서).
    ///
    /// 확인 방법 (주소 모양에 따라):
    /// <code>
    ///   10.0.0.5                    → 핑 (ICMP). 그 PC 가 살아 있는지만 — 서버 프로그램이 떠 있는지는 모름
    ///   10.0.0.5:8000               → 그 포트로 TCP 접속. 서버 프로그램이 떠 있는지까지
    ///   http://10.0.0.5:8000/v1     → 주소의 호스트 · 포트(없으면 80/443)로 TCP 접속
    /// </code>
    /// 네트워크로 <b>데이터를 보내지 않습니다</b> — 닿는지만 보고 바로 끊습니다. 시간 한도 <see cref="TimeoutMs"/>.
    ///
    /// 언제 하나: 프로그램을 켤 때 한 번 (지금 설정의 주소로), 설정 창의 [연결 확인], 설정 창을 닫았는데 주소가
    /// 바뀌었을 때. 배경 스레드에서 돌고, 끝나면 <see cref="Changed"/> — 받는 쪽(창)이 화면 스레드로 넘깁니다.
    /// WPF 를 쓰지 않습니다.
    /// </summary>
    public static class LlmServer
    {
        /// <summary>확인 한 번의 시간 한도 (밀리초).</summary>
        public const int TimeoutMs = 3000;

        private static readonly object Gate = new object();
        private static int _seq;
        private static LlmServerState _state = LlmServerState.Unknown;
        private static string _detail = string.Empty;
        private static string _address = string.Empty;
        private static DateTime _checkedAt = DateTime.MinValue;

        public static LlmServerState State { get { lock (Gate) return _state; } }

        /// <summary>닿는지 — 분석 3 · 종합 분석 결과를 쓸 수 있는지.</summary>
        public static bool IsOk { get { return State == LlmServerState.Ok; } }

        /// <summary>마지막 확인의 한 줄 ("10.0.0.5:8000 — 연결됨 (12 ms)").</summary>
        public static string Detail { get { lock (Gate) return _detail; } }

        /// <summary>
        /// 안 닿을 때 화면에 덮는 큰 글 (확인 중이면 확인 중이라는 글). 닿으면 빈 글자.
        /// 분석 3 화면 · 메인 분석 3 칸 · 종합 분석 결과가 같은 글을 씁니다.
        /// </summary>
        public static string BlockText
        {
            get
            {
                LlmServerState s = State;
                if (s == LlmServerState.Ok) return string.Empty;
                return s == LlmServerState.Checking ? CommonTexts.LlmChecking : CommonTexts.LlmBlocked;
            }
        }

        /// <summary>덮는 글 아래 작은 글 — 무엇을 확인했고 왜 안 됐는지.</summary>
        public static string BlockDetail
        {
            get
            {
                LlmServerState s = State;
                if (s == LlmServerState.Ok || s == LlmServerState.Checking) return string.Empty;
                string d = Detail;
                return d.Length > 0 ? d : "아직 확인하지 않았습니다 (설정 → LLM 서버 → [연결 확인]).";
            }
        }

        /// <summary>마지막으로 확인한 주소.</summary>
        public static string Address { get { lock (Gate) return _address; } }

        public static DateTime CheckedAt { get { lock (Gate) return _checkedAt; } }

        /// <summary>상태가 바뀔 때 (확인 시작 · 끝). <b>배경 스레드에서 올 수 있습니다.</b></summary>
        public static event EventHandler Changed;

        /// <summary>
        /// 배경 스레드에서 확인을 시작합니다. 앞의 확인이 아직 돌고 있으면 그 결과는 버립니다 (마지막 것만 씀).
        /// </summary>
        public static void CheckAsync(string address)
        {
            string a = (address ?? string.Empty).Trim();
            int my;
            lock (Gate)
            {
                my = ++_seq;
                _state = LlmServerState.Checking;
                _address = a;
                _detail = "연결 확인 중";
            }
            Raise();

            var t = new Thread(delegate ()
            {
                bool ok;
                string d = Probe(a, TimeoutMs, out ok);
                lock (Gate)
                {
                    if (my != _seq) return;   // 그사이 새 확인이 시작됨
                    _state = ok ? LlmServerState.Ok : LlmServerState.Fail;
                    _detail = d;
                    _checkedAt = DateTime.Now;
                }
                Raise();
            });
            t.IsBackground = true;
            t.Name = "LogScope LLM 서버 확인";
            t.Start();
        }

        /// <summary>시험용 — 상태를 직접 둡니다.</summary>
        public static void SetForTest(LlmServerState state, string detail)
        {
            lock (Gate) { _seq++; _state = state; _detail = detail ?? string.Empty; }
            Raise();
        }

        private static void Raise()
        {
            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        // ================= 확인 한 번 (막힘 — 배경 스레드에서) =================

        /// <summary>
        /// 주소로 닿는지 봅니다. 결과 한 줄을 돌려주고 ok 에 닿았는지. 예외는 밖으로 내지 않습니다.
        /// </summary>
        public static string Probe(string address, int timeoutMs, out bool ok)
        {
            ok = false;
            string host;
            int port;
            string why;
            if (!TryParse(address, out host, out port, out why)) return why;

            var sw = Stopwatch.StartNew();
            try
            {
                if (port > 0)
                {
                    using (var c = new TcpClient())
                    {
                        IAsyncResult ar = c.BeginConnect(host, port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                            return host + ":" + port + " — " + (timeoutMs / 1000.0).ToString("0.#") + " 초 안에 응답 없음";
                        c.EndConnect(ar);
                        ok = c.Connected;
                        return host + ":" + port + (ok ? " — 연결됨 (" + sw.ElapsedMilliseconds + " ms)" : " — 연결 안 됨");
                    }
                }

                using (var p = new Ping())
                {
                    PingReply r = p.Send(host, timeoutMs);
                    ok = r != null && r.Status == IPStatus.Success;
                    return host + (ok ? " — 핑 응답 (" + r.RoundtripTime + " ms, 포트를 적으면 서버 프로그램까지 확인)"
                                      : " — 핑 응답 없음 (" + (r != null ? r.Status.ToString() : "?") + ")");
                }
            }
            catch (SocketException e) { return Target(host, port) + " — 연결 안 됨 (" + e.Message + ")"; }
            catch (PingException e) { return Target(host, port) + " — 핑 실패 (" + (e.InnerException ?? e).Message + ")"; }
            catch (InvalidOperationException e) { return Target(host, port) + " — 확인 실패 (" + e.Message + ")"; }
            catch (ArgumentException e) { return Target(host, port) + " — 주소가 이상함 (" + e.Message + ")"; }
        }

        private static string Target(string host, int port) { return port > 0 ? host + ":" + port : host; }

        /// <summary>
        /// 주소를 호스트와 포트로. 포트가 0 이면 핑. 실패하면 거짓과 까닭.
        /// <code>
        ///   "10.0.0.5"               → 10.0.0.5, 0 (핑)
        ///   "10.0.0.5:8000"          → 10.0.0.5, 8000
        ///   "http://srv/v1"          → srv, 80
        ///   "https://srv:8443/x"     → srv, 8443
        ///   "[fe80::1]:8000" · "::1" → IPv6 (대괄호에 포트, 없으면 핑)
        /// </code>
        /// </summary>
        public static bool TryParse(string address, out string host, out int port, out string why)
        {
            host = string.Empty;
            port = 0;
            why = string.Empty;
            string a = (address ?? string.Empty).Trim();
            if (a.Length == 0) { why = "LLM 서버 주소가 비어 있습니다 (설정 → LLM 서버)."; return false; }

            if (a.IndexOf("://", StringComparison.Ordinal) > 0)
            {
                Uri u;
                if (!Uri.TryCreate(a, UriKind.Absolute, out u) || string.IsNullOrEmpty(u.Host))
                {
                    why = "주소를 읽지 못했습니다: " + a;
                    return false;
                }
                host = u.Host.Trim('[', ']');
                port = u.Port > 0 ? u.Port : (u.Scheme == "https" ? 443 : 80);
                return true;
            }

            // [IPv6]:port
            if (a.StartsWith("[", StringComparison.Ordinal))
            {
                int close = a.IndexOf(']');
                if (close < 0) { why = "주소를 읽지 못했습니다: " + a; return false; }
                host = a.Substring(1, close - 1);
                string rest = a.Substring(close + 1);
                if (rest.StartsWith(":", StringComparison.Ordinal) && !int.TryParse(rest.Substring(1), out port))
                {
                    why = "포트를 읽지 못했습니다: " + a;
                    return false;
                }
                return host.Length > 0;
            }

            int colon = a.IndexOf(':');
            if (colon > 0 && colon == a.LastIndexOf(':'))   // 콜론 하나 = 호스트:포트 (여럿이면 IPv6)
            {
                host = a.Substring(0, colon);
                if (!int.TryParse(a.Substring(colon + 1), out port) || port <= 0 || port > 65535)
                {
                    why = "포트를 읽지 못했습니다: " + a;
                    return false;
                }
                return true;
            }

            host = a;
            return true;
        }
    }
}
