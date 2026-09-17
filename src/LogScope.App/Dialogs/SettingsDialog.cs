using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using LogScope.App.Controls;
using LogScope.Core;
using LogScope.Core.Compare;
using LogScope.Core.Settings;

namespace LogScope.App.Dialogs
{
    /// <summary>
    /// 설정 창. 오른쪽 위 "설정" 버튼으로 엽니다.
    /// 로그 세트 6 개, 세트마다의 기본 폴더, 테마, 비교 기준, 그리고
    /// 프로그램 버전과 커밋 해시를 여기서 봅니다.
    /// </summary>
    public sealed class SettingsDialog : Form
    {
        private readonly AppSettings _s;

        private readonly ListBox _setList = new ListBox();
        private readonly TextBox _setTitle = new TextBox();
        private readonly TextBox _beforeFolder = new TextBox();
        private readonly TextBox _afterFolder = new TextBox();
        private readonly Label _beforeFile = new Label();
        private readonly Label _afterFile = new Label();

        private readonly ComboBox _theme = new ComboBox();
        private readonly TextBox _tolerance = new TextBox();
        private readonly ComboBox _metric = new ComboBox();
        private readonly CheckBox _autoAlign = new CheckBox();
        private readonly TextBox _shift = new TextBox();

        private readonly CheckBox _aiEnabled = new CheckBox();
        private readonly TextBox _python = new TextBox();
        private readonly TextBox _aiScript = new TextBox();

        private bool _loading;

        public SettingsDialog(AppSettings settings)
        {
            _s = settings;
            Theme t = Theme.Current;

            Text = "설정";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = t.Window;
            ForeColor = t.Text;
            Font = Theme.Ui;
            ClientSize = new Size(Dpi.S(720), Dpi.S(520));
            MinimumSize = new Size(Dpi.S(640), Dpi.S(460));

            var tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Theme.Ui;
            tabs.Padding = new Point(Dpi.S(12), Dpi.S(6));
            tabs.TabPages.Add(BuildSetsTab());
            tabs.TabPages.Add(BuildViewTab());
            tabs.TabPages.Add(BuildCompareTab());
            tabs.TabPages.Add(BuildAiTab());
            tabs.TabPages.Add(BuildAboutTab());

            var bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.Padding = new Padding(Dpi.S(10));
            bottom.BackColor = t.Panel;

            Button close = Buttons.MakePrimary("닫기", null);
            close.DialogResult = DialogResult.OK;
            bottom.Controls.Add(close);
            AcceptButton = close;

            Controls.Add(tabs);
            Controls.Add(bottom);

            LoadSets();
        }

        // ---------------- 로그 세트 ----------------

        private TabPage BuildSetsTab()
        {
            Theme t = Theme.Current;
            var page = NewPage("로그 세트");

            var help = new Label();
            help.Text = "한 세트 = 이전 로그 + 이후 로그 한 쌍입니다. 1번부터 6번까지 따로 기억합니다.\n"
                      + "세트마다 파일 열기 창이 처음 가리킬 폴더를 이전용/이후용으로 따로 정할 수 있습니다.";
            help.Dock = DockStyle.Top;
            help.Height = Dpi.S(40);
            help.ForeColor = t.Muted;

            _setList.Dock = DockStyle.Left;
            _setList.Width = Dpi.S(170);
            _setList.BorderStyle = BorderStyle.FixedSingle;
            _setList.BackColor = t.IsDark ? t.PanelAlt : Color.White;
            _setList.ForeColor = t.Text;
            _setList.IntegralHeight = false;
            _setList.SelectedIndexChanged += delegate { LoadSetDetail(); };

            var detail = new Panel();
            detail.Dock = DockStyle.Fill;
            detail.Padding = new Padding(Dpi.S(14), 0, 0, 0);

            int y = 0;
            y = AddRow(detail, y, "세트 이름", _setTitle, null);
            _setTitle.TextChanged += delegate
            {
                if (_loading || _setList.SelectedIndex < 0) return;
                _s.Sets[_setList.SelectedIndex].Title = _setTitle.Text;
                RefreshSetNames();
            };

            y = AddRow(detail, y, "이전 로그 기본 폴더", _beforeFolder,
                Buttons.Make("폴더 고르기", delegate { PickFolder(_beforeFolder, true); }));
            _beforeFolder.TextChanged += delegate
            {
                if (_loading || _setList.SelectedIndex < 0) return;
                _s.Sets[_setList.SelectedIndex].BeforeFolder = _beforeFolder.Text;
            };

            y = AddRow(detail, y, "이후 로그 기본 폴더", _afterFolder,
                Buttons.Make("폴더 고르기", delegate { PickFolder(_afterFolder, false); }));
            _afterFolder.TextChanged += delegate
            {
                if (_loading || _setList.SelectedIndex < 0) return;
                _s.Sets[_setList.SelectedIndex].AfterFolder = _afterFolder.Text;
            };

            y += Dpi.S(8);
            _beforeFile.AutoEllipsis = true;
            _afterFile.AutoEllipsis = true;
            y = AddInfo(detail, y, "마지막으로 연 이전 로그", _beforeFile);
            y = AddInfo(detail, y, "마지막으로 연 이후 로그", _afterFile);

            var clear = Buttons.Make("이 세트의 파일 기억 지우기", delegate
            {
                int i = _setList.SelectedIndex;
                if (i < 0) return;
                _s.Sets[i].BeforePath = string.Empty;
                _s.Sets[i].AfterPath = string.Empty;
                LoadSetDetail();
            });
            clear.Location = new Point(Dpi.S(14), y + Dpi.S(8));
            detail.Controls.Add(clear);

            // 도킹은 Controls 의 뒤쪽부터 자리를 잡습니다. 채움(Fill) 이
            // 맨 안쪽에 오도록 detail 을 먼저 넣고, 위 띠를 마지막에 넣습니다.
            page.Controls.Add(detail);
            page.Controls.Add(_setList);
            page.Controls.Add(help);
            return page;
        }

        private void PickFolder(TextBox target, bool before)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = before ? "이전 로그를 찾을 기본 폴더" : "이후 로그를 찾을 기본 폴더";
                dlg.ShowNewFolderButton = false;
                if (Directory.Exists(target.Text)) dlg.SelectedPath = target.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.SelectedPath;
            }
        }

        private void LoadSets()
        {
            _loading = true;
            _setList.Items.Clear();
            for (int i = 0; i < AppSettings.SetCount; i++) _setList.Items.Add(_s.Sets[i].DisplayName(i));
            _setList.SelectedIndex = _s.ActiveSet;
            _loading = false;
            LoadSetDetail();
        }

        private void RefreshSetNames()
        {
            int keep = _setList.SelectedIndex;
            _loading = true;
            for (int i = 0; i < AppSettings.SetCount; i++) _setList.Items[i] = _s.Sets[i].DisplayName(i);
            _setList.SelectedIndex = keep;
            _loading = false;
        }

        private void LoadSetDetail()
        {
            int i = _setList.SelectedIndex;
            if (i < 0) return;
            _loading = true;
            LogSet s = _s.Sets[i];
            _setTitle.Text = s.Title ?? string.Empty;
            _beforeFolder.Text = s.BeforeFolder ?? string.Empty;
            _afterFolder.Text = s.AfterFolder ?? string.Empty;
            _beforeFile.Text = string.IsNullOrEmpty(s.BeforePath) ? "(없음)" : s.BeforePath;
            _afterFile.Text = string.IsNullOrEmpty(s.AfterPath) ? "(없음)" : s.AfterPath;
            _loading = false;
        }

        // ---------------- 표시 ----------------

        private TabPage BuildViewTab()
        {
            var page = NewPage("표시");
            var body = new Panel();
            body.Dock = DockStyle.Fill;

            _theme.DropDownStyle = ComboBoxStyle.DropDownList;
            _theme.Items.AddRange(new object[] { "밝은 테마 (흰 바탕)", "어두운 테마" });
            _theme.SelectedIndex = string.Equals(_s.Theme, "dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _theme.SelectedIndexChanged += delegate
            {
                _s.Theme = _theme.SelectedIndex == 1 ? "dark" : "light";
            };

            int y = 0;
            y = AddRow(body, y, "테마", _theme, null);

            var note = new Label();
            note.Text = "테마는 프로그램을 다시 켜면 화면 전체에 적용됩니다.";
            note.ForeColor = Theme.Current.Muted;
            note.AutoSize = true;
            note.Location = new Point(Dpi.S(14), y + Dpi.S(6));
            body.Controls.Add(note);

            page.Controls.Add(body);
            return page;
        }

        // ---------------- 비교 ----------------

        private TabPage BuildCompareTab()
        {
            var page = NewPage("비교");
            var body = new Panel();
            body.Dock = DockStyle.Fill;

            _tolerance.Text = _s.Tolerance.ToString("0.######", CultureInfo.InvariantCulture);
            _tolerance.TextChanged += delegate
            {
                double v;
                if (double.TryParse(_tolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0)
                    _s.Tolerance = v;
            };

            _metric.DropDownStyle = ComboBoxStyle.DropDownList;
            _metric.Items.AddRange(new object[]
            {
                "최대 차이", "평균 차이", "RMS", "차이 면적", "차이 시간 비율", "차이 표본 수", "차이 구간 수"
            });
            _metric.SelectedIndex = (int)_s.SortMetric;
            _metric.SelectedIndexChanged += delegate { _s.SortMetric = (DiffMetric)_metric.SelectedIndex; };

            _autoAlign.Text = "두 로그의 시작 시각을 자동으로 맞춤";
            _autoAlign.Checked = _s.AutoAlign;
            _autoAlign.AutoSize = true;
            _autoAlign.CheckedChanged += delegate { _s.AutoAlign = _autoAlign.Checked; };

            _shift.Text = _s.ManualShift.ToString("0.######", CultureInfo.InvariantCulture);
            _shift.TextChanged += delegate
            {
                double v;
                if (double.TryParse(_shift.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    _s.ManualShift = v;
            };

            int y = 0;
            y = AddRow(body, y, "허용 오차", _tolerance, null);
            y = AddRow(body, y, "목록 정렬 기준", _metric, null);
            y = AddRow(body, y, "시간 맞추기", _autoAlign, null);
            y = AddRow(body, y, "손으로 밀기 (시간축 단위)", _shift, null);

            var note = new Label();
            note.Text = "차이 총합만 보면 +1 / -1 이 반복되는 채널이 크게 나옵니다.\n"
                      + "최대 / 평균 / RMS / 면적 / 시간 비율 / 표본 수 / 구간 수를 함께 보고 판단하세요.\n"
                      + "바뀐 값은 대시보드에서 '비교 다시 하기' 를 누르면 반영됩니다.";
            note.ForeColor = Theme.Current.Muted;
            note.AutoSize = true;
            note.Location = new Point(Dpi.S(14), y + Dpi.S(10));
            body.Controls.Add(note);

            page.Controls.Add(body);
            return page;
        }

        // ---------------- AI 모듈 ----------------

        private TabPage BuildAiTab()
        {
            var page = NewPage("AI 모듈 (선택)");
            var body = new Panel();
            body.Dock = DockStyle.Fill;

            _aiEnabled.Text = "파이썬 분석 모듈 사용";
            _aiEnabled.Checked = _s.AiEnabled;
            _aiEnabled.AutoSize = true;
            _aiEnabled.CheckedChanged += delegate { _s.AiEnabled = _aiEnabled.Checked; };

            _python.Text = _s.PythonPath ?? string.Empty;
            _python.TextChanged += delegate { _s.PythonPath = _python.Text; };

            _aiScript.Text = _s.AiScriptPath ?? string.Empty;
            _aiScript.TextChanged += delegate { _s.AiScriptPath = _aiScript.Text; };

            int y = 0;
            y = AddRow(body, y, "사용 여부", _aiEnabled, null);
            y = AddRow(body, y, "python.exe 경로", _python,
                Buttons.Make("파일 고르기", delegate { PickFile(_python, "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"); }));
            y = AddRow(body, y, "분석 스크립트 경로", _aiScript,
                Buttons.Make("파일 고르기", delegate { PickFile(_aiScript, "파이썬 (*.py)|*.py|모든 파일 (*.*)|*.*"); }));

            var note = new Label();
            note.Text = "기본은 꺼져 있습니다. 켜면 지정한 파이썬을 자식 프로세스로 띄워\n"
                      + "비교 결과 요약(채널 이름과 숫자)만 넘기고 소견 글을 받아 옵니다.\n"
                      + "로그 파일 자체나 파일 경로는 넘기지 않고, 네트워크로 내보내는 코드도 없습니다.\n"
                      + "스크립트가 무엇을 하는지는 그 파일을 직접 열어 확인하세요.\n"
                      + "남이 고쳐 쓸 수 있는 폴더의 파이썬을 지정하지 마세요.";
            note.ForeColor = Theme.Current.Muted;
            note.AutoSize = true;
            note.Location = new Point(Dpi.S(14), y + Dpi.S(10));
            body.Controls.Add(note);

            page.Controls.Add(body);
            return page;
        }

        private void PickFile(TextBox target, string filter)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = filter;
                if (File.Exists(target.Text)) dlg.FileName = target.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.FileName;
            }
        }

        // ---------------- 정보 ----------------

        private TabPage BuildAboutTab()
        {
            var page = NewPage("정보");
            var body = new Panel();
            body.Dock = DockStyle.Fill;

            var name = new Label();
            name.Text = "LogScope — IO 로그 그래프 뷰어";
            name.Font = Theme.UiBold;
            name.AutoSize = true;
            name.Location = new Point(Dpi.S(14), Dpi.S(10));
            body.Controls.Add(name);

            var version = new TextBox();
            version.ReadOnly = true;
            version.Multiline = true;
            version.BorderStyle = BorderStyle.FixedSingle;
            version.BackColor = Theme.Current.Panel;
            version.ForeColor = Theme.Current.Text;
            version.Font = Theme.Mono;
            version.Location = new Point(Dpi.S(14), Dpi.S(40));
            version.Size = new Size(Dpi.S(620), Dpi.S(130));
            version.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            version.Text =
                "버전       " + BuildInfo.Version + (BuildInfo.Modified ? "  (빌드 당시 소스 수정됨)" : "") + "\r\n" +
                "커밋       " + BuildInfo.Commit + "\r\n" +
                "빌드 시각  " + BuildInfo.BuiltAt + "\r\n" +
                ".NET       " + Environment.Version + "\r\n" +
                "실행 방식  " + (Environment.Is64BitProcess ? "64비트" : "32비트") + "\r\n" +
                "운영체제   " + Environment.OSVersion;
            body.Controls.Add(version);

            var pathLabel = new Label();
            pathLabel.Text = "설정 파일";
            pathLabel.AutoSize = true;
            pathLabel.ForeColor = Theme.Current.Muted;
            pathLabel.Location = new Point(Dpi.S(14), Dpi.S(182));
            body.Controls.Add(pathLabel);

            var pathBox = new TextBox();
            pathBox.ReadOnly = true;
            pathBox.BorderStyle = BorderStyle.FixedSingle;
            pathBox.BackColor = Theme.Current.Panel;
            pathBox.ForeColor = Theme.Current.Text;
            pathBox.Text = SettingsStore.ResolvePath();
            pathBox.Location = new Point(Dpi.S(14), Dpi.S(202));
            pathBox.Size = new Size(Dpi.S(620), Dpi.S(24));
            pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            body.Controls.Add(pathBox);

            page.Controls.Add(body);
            return page;
        }

        // ---------------- 자리 잡기 도우미 ----------------

        private static TabPage NewPage(string text)
        {
            var p = new TabPage(text);
            p.BackColor = Theme.Current.Window;
            p.ForeColor = Theme.Current.Text;
            p.Padding = new Padding(Dpi.S(14));
            p.UseVisualStyleBackColor = false;
            return p;
        }

        /// <summary>
        /// 설명 글 - 입력칸 - (버튼) 한 줄. 설명과 칸이 겹치지 않도록
        /// 너비를 고정으로 잡고, 입력칸만 창 너비에 따라 늘어나게 합니다.
        /// </summary>
        private static int AddRow(Control parent, int y, string caption, Control field, Control extra)
        {
            int labelW = Dpi.S(160);
            int rowH = Dpi.S(30);
            int extraW = extra != null ? Dpi.S(110) : 0;

            var l = new Label();
            l.Text = caption;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Bounds = new Rectangle(Dpi.S(14), y, labelW, rowH);
            l.ForeColor = Theme.Current.Muted;
            parent.Controls.Add(l);

            var cb = field as CheckBox;
            if (cb != null) cb.AutoSize = false;

            var tb = field as TextBox;
            if (tb != null)
            {
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.BackColor = Theme.Current.IsDark ? Theme.Current.PanelAlt : Color.White;
                tb.ForeColor = Theme.Current.Text;
            }

            field.Bounds = new Rectangle(Dpi.S(14) + labelW, y + Dpi.S(3),
                                         Dpi.S(360) - extraW, rowH - Dpi.S(6));
            field.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(field);

            if (extra != null)
            {
                extra.AutoSize = false;
                extra.Bounds = new Rectangle(field.Right + Dpi.S(6), y + Dpi.S(2),
                                             extraW - Dpi.S(10), rowH - Dpi.S(4));
                extra.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                parent.Controls.Add(extra);
            }
            return y + rowH + Dpi.S(6);
        }

        private static int AddInfo(Control parent, int y, string caption, Label value)
        {
            int labelW = Dpi.S(160);
            int rowH = Dpi.S(24);

            var l = new Label();
            l.Text = caption;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Bounds = new Rectangle(Dpi.S(14), y, labelW, rowH);
            l.ForeColor = Theme.Current.Muted;
            parent.Controls.Add(l);

            value.AutoSize = false;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.Bounds = new Rectangle(Dpi.S(14) + labelW, y, Dpi.S(360), rowH);
            value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            value.ForeColor = Theme.Current.Text;
            parent.Controls.Add(value);
            return y + rowH + Dpi.S(2);
        }
    }
}
