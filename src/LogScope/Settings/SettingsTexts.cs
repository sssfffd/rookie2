using System;

namespace LogScope.Settings
{
    /// <summary>
    /// <b>설정 창</b> 의 글자 — 단추 이름 · 칸 이름 · 마우스를 올리면 뜨는 설명.
    ///
    /// <b>고치는 법</b>: 바꿀 글을 Ctrl+F 로 찾아 따옴표 안만 고치고 다시 빌드
    /// (build.bat). 이름(= 왼쪽)은 XAML 이 찾는 열쇠라 바꾸지 마세요 — 바꾸면
    /// scripts/check_sources.py 가 "x:Static 대상 없음" 으로 알려 줍니다.
    /// 줄바꿈은 \n 입니다.
    ///
    /// 화면 파일: src/LogScope/Settings/SettingsWindow.xaml
    /// </summary>
    public static class SettingsTexts
    {
        // ---- SettingsWindow ----

        /// <summary>창 제목</summary>
        public const string WindowTitle = "설정";
        /// <summary>단추 이름</summary>
        public const string Close = "닫기";

        // ---- 폴더 ----

        /// <summary>TabItem 머리글</summary>
        public const string TabHeader = "폴더";
        /// <summary>글</summary>
        public const string Note = "파일 열기 창이 처음 가리킬 폴더를 이전용 / 이후용으로 따로 정할 수 있습니다.\n비워 두면 마지막으로 연 파일이 있던 폴더에서 시작합니다.";
        /// <summary>글</summary>
        public const string BeforeFolderLabel = "이전 로그 기본 폴더";
        /// <summary>단추 이름</summary>
        public const string PickBeforeFolder = "폴더 고르기";
        /// <summary>글</summary>
        public const string AfterFolderLabel = "이후 로그 기본 폴더";
        /// <summary>단추 이름</summary>
        public const string PickAfterFolder = "폴더 고르기";
        /// <summary>글</summary>
        public const string BeforeFileLabel = "마지막으로 연 이전 로그";
        /// <summary>글</summary>
        public const string AfterFileLabel = "마지막으로 연 이후 로그";
        /// <summary>단추 이름</summary>
        public const string ForgetFiles = "기억된 파일 지우기";

        // ---- 표시 ----

        /// <summary>TabItem 머리글</summary>
        public const string TabHeader2 = "표시";
        /// <summary>글</summary>
        public const string ThemeIndexLabel = "테마";
        /// <summary>글</summary>
        public const string Note2 = "테마를 고르면 바로 바뀝니다. 프로그램을 다시 켜지 않아도 됩니다.";

        // ---- 비교 ----

        /// <summary>TabItem 머리글</summary>
        public const string TabHeader3 = "비교";
        /// <summary>글</summary>
        public const string RelativeTolerancePercentTextLabel = "허용 오차 (이전 값 대비 %)";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string RelativeTolerancePercentTextTip = "오차 = |이후 − 이전| / |이전| x 100. 기본 0.1%.\n화면에 적히는 퍼센트가 바로 이 값이라 그대로 견주면 됩니다.";
        /// <summary>글</summary>
        public const string Note3 = "기본 0.1";
        /// <summary>글</summary>
        public const string AbsoluteToleranceTextLabel = "허용 오차 (절대값)";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string AbsoluteToleranceTextTip = "값 그대로. 기본 0 (끔).\n이 값 이하로 벌어진 것은 퍼센트와 상관없이 같은 것으로 봅니다.\n0 언저리에서 퍼센트가 크게 튀는 것을 막는 데 씁니다.";
        /// <summary>글</summary>
        public const string Note4 = "기본 0 (끔)";
        /// <summary>단추 이름</summary>
        public const string ResetTolerance = "허용 오차를 기본값으로";
        /// <summary>글</summary>
        public const string Note5 = "위 두 값이 모든 IO 의 기본값입니다.";
        /// <summary>글</summary>
        public const string SortMetricIndexLabel = "목록 정렬 기준";
        /// <summary>글</summary>
        public const string AutoAlignLabel = "시간 맞추기";
        /// <summary>체크 이름</summary>
        public const string AutoAlign = "두 로그의 시작 시각을 자동으로 맞춤";
        /// <summary>글</summary>
        public const string ManualShiftTextLabel = "손으로 밀기";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string ManualShiftTextTip = "시간축 단위입니다. 시각 로그라면 밀리초.";
        /// <summary>글</summary>
        public const string StepIoTextLabel = "단계 IO";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string StepIoTextTip = "이 IO 의 값이 바뀌는 자리가 단계 경계입니다 (\"준비\" → \"가열\").\n글자 IO 면 그 글자가 단계 이름이고, 숫자면 값이 그대로 이름입니다.\n비우면 단계를 안 씁니다.";
        /// <summary>글</summary>
        public const string Note6 = "설정을 닫으면 바로 들어갑니다 (로그를 다시 읽지 않습니다).";

        // ---- IO 별 허용 오차 ----

        /// <summary>글</summary>
        public const string Heading = "IO 별 허용 오차";
        /// <summary>글</summary>
        public const string Note7 = "여기 적은 IO 만 따로 봅니다. 칸을 비워 두면 그 자리는 위의 기본값을 씁니다.\n이름은 느슨하게 맞춥니다 — 띄어쓰기나 _ - . 가 달라도 같은 IO 로 봅니다.";
        /// <summary>글</summary>
        public const string Label = "IO 이름";
        /// <summary>글</summary>
        public const string Label2 = "퍼센트 (%)";
        /// <summary>글</summary>
        public const string Label3 = "값 (절대)";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string PercentTextTip = "비워 두면 기본값을 씁니다.";
        /// <summary>입력 칸 마우스를 올리면 뜨는 설명</summary>
        public const string AbsoluteTextTip = "비워 두면 기본값을 씁니다.\n이 값 이하로 벌어진 것은 퍼센트와 상관없이 같은 것으로 봅니다.";
        /// <summary>단추 이름</summary>
        public const string RemoveTolerance = "지우기";
        /// <summary>단추 이름</summary>
        public const string AddTolerance = "IO 추가";
        /// <summary>글</summary>
        public const string Note8 = "오차는 그 순간의 값끼리 견줍니다.\n\n    오차(%) = |이후 − 이전| / |이전| x 100\n\n위 칸에 적는 퍼센트가 바로 이 값입니다. 대시보드의 [최대 오차 %] 칸,\n히트맵 칸에 적히는 퍼센트도 모두 같은 값이라 눈으로 바로 견줄 수 있습니다.\n어느 채널이든 같은 잣대입니다.\n\n이전 값이 0 이면 나눌 수가 없어 100% 로 봅니다 (0 → 0 은 0%).\n0 언저리에서 잘게 흔들리는 것까지 100% 로 잡히는 게 싫으면\n절대 오차를 함께 걸어 두세요.\n\n이 값은 대시보드 · 그래프 차이 영역 표시 · 히트맵이 모두 함께 씁니다.\n여기서 바꾼 값은 위 메뉴의 [비교 다시 하기] 를 눌러야 반영됩니다.";

        // ---- DB 도구 ----

        /// <summary>TabItem 머리글</summary>
        public const string TabHeader4 = "DB 도구 (선택)";
        /// <summary>글</summary>
        public const string MysqlDumpPathLabel = "mysqldump 경로";
        /// <summary>단추 이름</summary>
        public const string PickMysqlDump = "파일 고르기";
        /// <summary>글</summary>
        public const string DbDumpBeforeLabel = "이전 덤프 인수";
        /// <summary>글</summary>
        public const string DbDumpAfterLabel = "이후 덤프 인수";
        /// <summary>글</summary>
        public const string Ibd2SdiPathLabel = "ibd2sdi 경로";
        /// <summary>단추 이름</summary>
        public const string PickIbd2Sdi = "파일 고르기";
        /// <summary>글</summary>
        public const string MysqldumpFrmNote = "전부 비워 두어도 됩니다. 비워 두면 DB 화면은 폴더·파일만 읽습니다.\n\nmysqldump 는 돌고 있는 서버에 접속해서 뽑습니다 — 멈춘 데이터 폴더(.frm/.ibd)만으로는 뽑을 수 없습니다.\n덤프 인수에 암호를 적으면 작업 관리자에 그대로 보입니다. --login-path 나 --defaults-extra-file 을 쓰세요.\n여기 적은 실행 파일을 자식 프로세스로 띄웁니다. 남이 고쳐 쓸 수 있는 폴더를 가리키지 마세요.\n\nibd2sdi 는 멈춘 .ibd 에서 표와 열 이름만 꺼냅니다 (값은 못 꺼냅니다). MySQL 8.0 의 .ibd 에만 통합니다.";
        // ---- 정보 ----

        /// <summary>TabItem 머리글</summary>
        public const string TabHeader6 = "정보";

        // ---- LLM 서버 (1.51) ----

        /// <summary>탭 이름</summary>
        public const string TabHeaderLlm = "LLM 서버";
        /// <summary>칸 이름</summary>
        public const string LlmServerLabel = "서버 주소";
        /// <summary>단추 이름</summary>
        public const string LlmCheck = "연결 확인";
        /// <summary>단추 마우스를 올리면 뜨는 설명</summary>
        public const string LlmCheckTip = "적은 주소로 서버에 닿는지 봅니다 (데이터는 보내지 않음). 닿으면 분석 3 · 종합 분석 결과를 쓸 수 있습니다.";
        /// <summary>칸 이름</summary>
        public const string LlmStatusLabel = "상태";
        /// <summary>글</summary>
        public const string LlmServerNote = "LLM 서버에 닿을 때만 분석 3 과 메인 화면 종합 분석 결과를 쓸 수 있습니다.\n" +
            "주소 모양:\n" +
            "  10.0.0.5                 핑 — 그 PC 가 살아 있는지만\n" +
            "  10.0.0.5:8000            그 포트로 접속 — 서버 프로그램이 떠 있는지까지 (권장)\n" +
            "  http://10.0.0.5:8000/v1  주소의 호스트 · 포트로 접속\n" +
            "프로그램을 켤 때 이 주소로 한 번 저절로 확인합니다. 닿는지만 보고 데이터는 보내지 않습니다.\n" +
            "인증 토큰 · 비밀번호는 여기 적지 마세요.";
        /// <summary>글</summary>
        public const string LogScopeHeading = "LogScope — IO 로그 그래프 뷰어";
        /// <summary>글</summary>
        public const string Note10 = "설정 파일";
    }
}
