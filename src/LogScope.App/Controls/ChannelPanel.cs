using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LogScope.App.Dialogs;
using LogScope.Core.Model;
using LogScope.Core.Settings;

namespace LogScope.App.Controls
{
    /// <summary>
    /// 왼쪽의 IO 목록 + 그룹 패널.
    ///
    /// 목록은 직접 그립니다. IO 가 수천 개여도 보이는 줄만 그리기 때문에
    /// 화면을 굴려도 느려지지 않습니다. 검색창과 콤보는 진짜 컨트롤이라
    /// 그래프 위에서 마우스를 움직여도 다시 그려지지 않습니다 (깜빡임 없음).
    /// </summary>
    public sealed class ChannelPanel : UserControl
    {
        private readonly AppState _state;
        private readonly TextBox _search = new TextBox();
        private readonly ComboBox _kind = new ComboBox();
        private readonly ComboBox _scope = new ComboBox();
        private readonly Label _count = new Label();
        private readonly Label _groupLabel = new Label();
        private readonly ListSurface _list;
        private readonly FlowBar _topBar = new FlowBar();
        private readonly FlowBar _groupBar = new FlowBar();

        private int _activeGroup = -1;

        public event EventHandler SelectionChanged;
        public event EventHandler GroupsChanged;

        public ChannelPanel(AppState state)
        {
            _state = state;
            DoubleBuffered = true;
            BackColor = Theme.Current.Panel;

            _list = new ListSurface(this, state);
            _list.Dock = DockStyle.Fill;

            BuildTopBar();
            BuildGroupBar();

            Controls.Add(_list);
            Controls.Add(_groupBar);
            Controls.Add(_topBar);
            _groupBar.Dock = DockStyle.Bottom;
        }

        private ChannelView View { get { return _state.View; } }

        // ---------------- 위쪽: 검색과 필터 ----------------

        private void BuildTopBar()
        {
            Theme t = Theme.Current;

            var title = new Label();
            title.Text = "IO 목록";
            title.Font = Theme.UiBold;
            title.ForeColor = t.Text;
            title.AutoSize = true;
            title.Margin = new Padding(Dpi.S(2), Dpi.S(6), Dpi.S(8), Dpi.S(2));
            _topBar.Controls.Add(title);

            _count.Text = "0 / 0 선택";
            _count.AutoSize = true;
            _count.ForeColor = t.Muted;
            _count.Margin = new Padding(Dpi.S(2), Dpi.S(7), Dpi.S(2), Dpi.S(2));
            _topBar.Controls.Add(_count);
            _topBar.AddBreak();

            _search.Width = Dpi.S(150);
            _search.BorderStyle = BorderStyle.FixedSingle;
            _search.BackColor = t.IsDark ? t.PanelAlt : Color.White;
            _search.ForeColor = t.Text;
            _search.Margin = new Padding(Dpi.S(2), Dpi.S(3), Dpi.S(4), Dpi.S(3));
            _search.TextChanged += delegate
            {
                View.Search = _search.Text.Trim();
                View.RebuildRows();
                _list.ResetScrollIfNeeded();
                _list.Invalidate();
                UpdateCount();
            };
            var searchLabel = new Label();
            searchLabel.Text = "이름 검색";
            searchLabel.AutoSize = true;
            searchLabel.ForeColor = t.Muted;
            searchLabel.Margin = new Padding(Dpi.S(2), Dpi.S(7), Dpi.S(2), Dpi.S(2));
            _topBar.Controls.Add(searchLabel);
            _topBar.Controls.Add(_search);

            Button clear = Buttons.Make("검색 지우기", delegate { _search.Text = string.Empty; });
            _topBar.Controls.Add(clear);
            _topBar.AddBreak();

            _kind.DropDownStyle = ComboBoxStyle.DropDownList;
            _kind.Items.AddRange(new object[] { "전체 타입", "디지털", "아날로그", "상태" });
            _kind.SelectedIndex = 0;
            _kind.Width = Dpi.S(104);
            _kind.Margin = new Padding(Dpi.S(2), Dpi.S(3), Dpi.S(4), Dpi.S(3));
            _kind.SelectedIndexChanged += delegate
            {
                View.Kind = (KindFilter)_kind.SelectedIndex;
                View.RebuildRows(); _list.ResetScrollIfNeeded(); _list.Invalidate(); UpdateCount();
            };
            _topBar.Controls.Add(_kind);

            _scope.DropDownStyle = ComboBoxStyle.DropDownList;
            _scope.Items.AddRange(new object[] { "모든 IO", "고른 IO만", "달라진 IO만" });
            _scope.SelectedIndex = 0;
            _scope.Width = Dpi.S(112);
            _scope.Margin = new Padding(Dpi.S(2), Dpi.S(3), Dpi.S(4), Dpi.S(3));
            _scope.SelectedIndexChanged += delegate
            {
                View.SelectedOnly = _scope.SelectedIndex == 1;
                View.ChangedOnly = _scope.SelectedIndex == 2;
                View.RebuildRows(); _list.ResetScrollIfNeeded(); _list.Invalidate(); UpdateCount();
            };
            _topBar.Controls.Add(_scope);
            _topBar.AddBreak();

            _topBar.Controls.Add(Buttons.Make("전체 선택", delegate
            {
                View.SelectAllVisible(true); AfterSelectionChange();
            }));
            _topBar.Controls.Add(Buttons.Make("전체 해제", delegate
            {
                View.ClearSelection(); AfterSelectionChange();
            }));
        }

        // ---------------- 아래쪽: 그룹 관리 ----------------

        private void BuildGroupBar()
        {
            Theme t = Theme.Current;
            _groupBar.BackColor = t.PanelAlt;

            var title = new Label();
            title.Text = "그룹";
            title.Font = Theme.UiBold;
            title.ForeColor = t.Text;
            title.AutoSize = true;
            title.Margin = new Padding(Dpi.S(2), Dpi.S(6), Dpi.S(6), Dpi.S(2));
            _groupBar.Controls.Add(title);

            _groupLabel.Text = "고른 그룹 없음";
            _groupLabel.AutoSize = true;
            _groupLabel.ForeColor = t.Muted;
            _groupLabel.Margin = new Padding(Dpi.S(2), Dpi.S(7), Dpi.S(2), Dpi.S(2));
            _groupBar.Controls.Add(_groupLabel);
            _groupBar.AddBreak();

            _groupBar.Controls.Add(Buttons.Make("새 그룹 만들기", delegate
            {
                string name;
                if (!TextInputDialog.Ask(this, "새 그룹", "그룹 이름을 적어 주세요.",
                        "그룹 " + (_state.Settings.Groups.Count + 1), out name)) return;
                _state.AddGroup(name);
                _activeGroup = _state.Settings.Groups.Count - 1;
                AfterGroupChange();
            }));

            _groupBar.Controls.Add(Buttons.Make("이름 바꾸기", delegate
            {
                if (!HasActiveGroup()) return;
                GroupDef g = _state.Settings.Groups[_activeGroup];
                string name;
                if (!TextInputDialog.Ask(this, "그룹 이름", "새 이름을 적어 주세요.", g.Name, out name)) return;
                g.Name = name;
                AfterGroupChange();
            }));
            _groupBar.AddBreak();

            _groupBar.Controls.Add(Buttons.Make("위로 옮기기", delegate
            {
                if (!HasActiveGroup()) return;
                _state.MoveGroup(_activeGroup, -1);
                _activeGroup--;
                AfterGroupChange();
            }));
            _groupBar.Controls.Add(Buttons.Make("아래로 옮기기", delegate
            {
                if (!HasActiveGroup()) return;
                _state.MoveGroup(_activeGroup, +1);
                _activeGroup++;
                AfterGroupChange();
            }));
            _groupBar.AddBreak();

            _groupBar.Controls.Add(Buttons.Make("고른 IO를 이 그룹에 담기", delegate
            {
                if (!HasActiveGroup()) return;
                _state.PutInGroup(View.SelectedNames(), _activeGroup);
                AfterGroupChange();
            }));
            _groupBar.Controls.Add(Buttons.Make("그룹 삭제", delegate
            {
                if (!HasActiveGroup()) return;
                GroupDef g = _state.Settings.Groups[_activeGroup];
                if (MessageBox.Show(this, "'" + g.Name + "' 그룹을 지웁니다. IO 자체는 지워지지 않고 그룹만 없어집니다.",
                        "그룹 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                _state.RemoveGroup(_activeGroup);
                _activeGroup = -1;
                AfterGroupChange();
            }));
        }

        private bool HasActiveGroup()
        {
            if (_activeGroup >= 0 && _activeGroup < _state.Settings.Groups.Count) return true;
            MessageBox.Show(this, "먼저 목록에서 그룹 이름 줄을 한 번 눌러 그룹을 골라 주세요.",
                "그룹을 고르지 않았습니다", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        // ---------------- 갱신 ----------------

        public void RefreshAll()
        {
            View.RebuildRows();
            _list.ResetScrollIfNeeded();
            _list.Invalidate();
            UpdateCount();
            UpdateGroupLabel();
        }

        private void AfterSelectionChange()
        {
            View.RebuildRows();
            _list.Invalidate();
            UpdateCount();
            var h = SelectionChanged; if (h != null) h(this, EventArgs.Empty);
        }

        private void AfterGroupChange()
        {
            View.RebuildRows();
            _list.ResetScrollIfNeeded();
            _list.Invalidate();
            UpdateGroupLabel();
            var h = GroupsChanged; if (h != null) h(this, EventArgs.Empty);
        }

        private void UpdateCount()
        {
            _count.Text = View.SelectedCount + " / " + View.Count + " 선택";
        }

        private void UpdateGroupLabel()
        {
            _groupLabel.Text = (_activeGroup >= 0 && _activeGroup < _state.Settings.Groups.Count)
                ? "고른 그룹: " + _state.Settings.Groups[_activeGroup].Name
                : "고른 그룹 없음";
        }

        /// <summary>대시보드에서 IO 를 눌러 넘어왔을 때 그 줄로 굴려 줍니다.</summary>
        public void RevealIo(string name)
        {
            int io = View.IndexOf(name);
            if (io < 0) return;
            for (int r = 0; r < View.Rows.Count; r++)
            {
                if (View.Rows[r].Kind != RowKind.Io || View.Rows[r].Io != io) continue;
                _list.ScrollToRow(r);
                _list.Invalidate();
                return;
            }
        }

        internal void SetActiveGroup(int g)
        {
            _activeGroup = g;
            UpdateGroupLabel();
        }

        internal void NotifySelectionChanged() { AfterSelectionChange(); }
        internal void NotifyGroupsChanged() { AfterGroupChange(); }

        // ================= 목록 그리기 =================

        private sealed class ListSurface : Control
        {
            private readonly ChannelPanel _owner;
            private readonly AppState _state;
            private readonly VScrollBar _sb = new VScrollBar();
            private int _top;                 // 맨 위에 보이는 줄 번호
            private int _lastClickedRow = -1;
            private int _hoverRow = -1;
            private Point _dragStart;
            private bool _mayDrag;
            private int _dropGroup = -2;

            public ListSurface(ChannelPanel owner, AppState state)
            {
                _owner = owner;
                _state = state;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                         | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Current.Window;
                AllowDrop = true;

                _sb.Dock = DockStyle.Right;
                _sb.SmallChange = 1;
                _sb.ValueChanged += delegate { _top = _sb.Value; Invalidate(); };
                Controls.Add(_sb);
            }

            private ChannelView View { get { return _state.View; } }
            private int RowHeight { get { return Dpi.S(23); } }
            private int VisibleRows { get { return Math.Max(1, ClientSize.Height / RowHeight); } }

            public void ResetScrollIfNeeded()
            {
                int max = Math.Max(0, View.Rows.Count - VisibleRows);
                if (_top > max) _top = max;
                if (_top < 0) _top = 0;
                SyncScrollBar();
            }

            private void SyncScrollBar()
            {
                int rows = View.Rows.Count;
                int page = VisibleRows;
                _sb.Minimum = 0;
                _sb.Maximum = Math.Max(0, rows - 1);
                _sb.LargeChange = Math.Max(1, page);
                _sb.Enabled = rows > page;
                int v = Math.Min(_top, Math.Max(0, rows - page));
                if (v < 0) v = 0;
                if (v > _sb.Maximum) v = _sb.Maximum;
                if (_sb.Value != v) _sb.Value = v;
                _top = v;
            }

            public void ScrollToRow(int row)
            {
                int page = VisibleRows;
                if (row < _top) _top = row;
                else if (row >= _top + page) _top = row - page + 1;
                SyncScrollBar();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                ResetScrollIfNeeded();
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines <= 0) lines = 3;
                _top -= Math.Sign(e.Delta) * lines;
                ResetScrollIfNeeded();
                Invalidate();
            }

            private int RowAt(int y)
            {
                int r = _top + y / RowHeight;
                return (r >= 0 && r < View.Rows.Count) ? r : -1;
            }

            // ---- 그리기 ----

            protected override void OnPaint(PaintEventArgs e)
            {
                Theme t = Theme.Current;
                Graphics g = e.Graphics;
                g.Clear(t.Window);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                List<ViewRow> rows = View.Rows;
                if (rows.Count == 0)
                {
                    TextRenderer.DrawText(g, "로그를 열면 IO 목록이 여기에 나옵니다.",
                        Theme.Ui, new Rectangle(Dpi.S(12), Dpi.S(16), ClientSize.Width - Dpi.S(24), Dpi.S(40)),
                        t.Muted, TextFormatFlags.WordBreak);
                    return;
                }

                int w = ClientSize.Width - _sb.Width;
                int rowH = RowHeight;
                int last = Math.Min(rows.Count, _top + VisibleRows + 1);

                using (var selBrush = new SolidBrush(t.AccentSoft))
                using (var borderPen = new Pen(t.Border))
                {
                    for (int r = _top; r < last; r++)
                    {
                        int y = (r - _top) * rowH;
                        var rect = new Rectangle(0, y, w, rowH);
                        if (rows[r].Kind == RowKind.GroupHeader)
                            DrawGroupRow(g, rect, rows[r], borderPen, t);
                        else
                            DrawIoRow(g, rect, rows[r], selBrush, t, r);
                    }
                }
            }

            private void DrawGroupRow(Graphics g, Rectangle rect, ViewRow row, Pen border, Theme t)
            {
                bool isDropTarget = _dropGroup == row.Group;
                using (var b = new SolidBrush(isDropTarget ? t.AccentSoft : t.PanelAlt))
                    g.FillRectangle(b, rect);
                g.DrawLine(border, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);

                bool ungrouped = row.Group < 0;
                GroupDef def = ungrouped ? null : _state.Settings.Groups[row.Group];
                string name = ungrouped ? "그룹에 없는 IO" : def.Name;
                bool expanded = ungrouped || def.Expanded;

                int x = Dpi.S(6);

                // 펼침 표시 (작은 삼각형). 줄 아무 데나 누르면 접히고 펴집니다.
                DrawTriangle(g, new Rectangle(x, rect.Y + rect.Height / 2 - Dpi.S(4), Dpi.S(9), Dpi.S(9)),
                             expanded, t.Muted);
                x += Dpi.S(14);

                // 그룹 전체 선택 체크박스
                var box = new Rectangle(x, rect.Y + (rect.Height - Dpi.S(13)) / 2, Dpi.S(13), Dpi.S(13));
                DrawCheck(g, box, View.GroupFullySelected(row.Group), t);
                x += Dpi.S(19);

                int members = CountMembers(row.Group);
                bool active = !ungrouped && _owner._activeGroup == row.Group;
                Color fc = active ? t.Accent : t.Text;

                var textRect = new Rectangle(x, rect.Y, rect.Width - x - Dpi.S(50), rect.Height);
                TextRenderer.DrawText(g, name, Theme.UiBold, textRect, fc,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                TextRenderer.DrawText(g, members + "개",
                    Theme.Small, new Rectangle(rect.Right - Dpi.S(48), rect.Y, Dpi.S(44), rect.Height),
                    t.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            }

            private int CountMembers(int group)
            {
                int n = 0;
                foreach (ViewRow r in View.Rows) if (r.Kind == RowKind.Io && r.Group == group) n++;
                return n;
            }

            private void DrawIoRow(Graphics g, Rectangle rect, ViewRow row, SolidBrush sel, Theme t, int rowIndex)
            {
                IoRef io = View.All[row.Io];
                bool selected = View.IsSelected(row.Io);

                if (selected) g.FillRectangle(sel, rect);
                else if (rowIndex == _hoverRow)
                {
                    using (var b = new SolidBrush(t.IsDark ? t.PanelAlt : t.Panel)) g.FillRectangle(b, rect);
                }

                int x = Dpi.S(22);
                var box = new Rectangle(x, rect.Y + (rect.Height - Dpi.S(13)) / 2, Dpi.S(13), Dpi.S(13));
                DrawCheck(g, box, selected, t);
                x += Dpi.S(19);

                // 존재 차이 표시: 한쪽에만 있는 IO 는 글자로 알려 줍니다.
                string suffix = string.Empty;
                Color nameColor = t.Text;
                if (!io.InAfter && io.InBefore && _state.After != null) { suffix = "  (이전에만)"; nameColor = t.Bad; }
                else if (!io.InBefore && io.InAfter && _state.Before != null) { suffix = "  (이후에만)"; nameColor = t.Good; }
                else if (View.ChangedNames.Contains(io.Name)) nameColor = t.Warn;

                int tagW = Dpi.S(36);
                var textRect = new Rectangle(x, rect.Y, rect.Width - x - tagW - Dpi.S(6), rect.Height);
                TextRenderer.DrawText(g, io.Name + suffix, Theme.Ui, textRect, nameColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                TextRenderer.DrawText(g, KindTag(io.Kind), Theme.Small,
                    new Rectangle(rect.Right - tagW - Dpi.S(4), rect.Y, tagW, rect.Height), t.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            }

            private static string KindTag(ChannelKind k)
            {
                switch (k)
                {
                    case ChannelKind.Digital: return "디지털";
                    case ChannelKind.State: return "상태";
                    default: return "아날로그";
                }
            }

            private static void DrawCheck(Graphics g, Rectangle box, bool on, Theme t)
            {
                using (var b = new SolidBrush(on ? t.Accent : (t.IsDark ? t.PanelAlt : Color.White)))
                    g.FillRectangle(b, box);
                using (var p = new Pen(on ? t.Accent : t.Border))
                    g.DrawRectangle(p, box);
                if (!on) return;
                using (var p = new Pen(Color.White, Math.Max(1.6f, Dpi.F(1.6f))))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawLines(p, new[]
                    {
                        new PointF(box.Left + box.Width * 0.22f, box.Top + box.Height * 0.52f),
                        new PointF(box.Left + box.Width * 0.44f, box.Top + box.Height * 0.74f),
                        new PointF(box.Left + box.Width * 0.80f, box.Top + box.Height * 0.28f),
                    });
                    g.SmoothingMode = SmoothingMode.Default;
                }
            }

            private static void DrawTriangle(Graphics g, Rectangle r, bool down, Color c)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] pts = down
                    ? new[] { new PointF(r.Left, r.Top + r.Height * 0.25f),
                              new PointF(r.Right, r.Top + r.Height * 0.25f),
                              new PointF(r.Left + r.Width / 2f, r.Bottom) }
                    : new[] { new PointF(r.Left + r.Width * 0.25f, r.Top),
                              new PointF(r.Right, r.Top + r.Height / 2f),
                              new PointF(r.Left + r.Width * 0.25f, r.Bottom) };
                using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
                g.SmoothingMode = SmoothingMode.Default;
            }

            // ---- 마우스 ----

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int r = RowAt(e.Y);
                if (r != _hoverRow) { _hoverRow = r; Invalidate(); }

                if (_mayDrag && (e.Button & MouseButtons.Left) != 0)
                {
                    int dx = Math.Abs(e.X - _dragStart.X), dy = Math.Abs(e.Y - _dragStart.Y);
                    if (dx > SystemInformation.DragSize.Width || dy > SystemInformation.DragSize.Height)
                    {
                        _mayDrag = false;
                        List<string> names = View.SelectedNames();
                        if (names.Count > 0) DoDragDrop(new DragPayload(names), DragDropEffects.Move);
                    }
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_hoverRow != -1) { _hoverRow = -1; Invalidate(); }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                int r = RowAt(e.Y);
                if (r < 0) return;
                ViewRow row = View.Rows[r];

                if (e.Button == MouseButtons.Right)
                {
                    if (row.Kind == RowKind.GroupHeader && row.Group >= 0) _owner.SetActiveGroup(row.Group);
                    ShowMenu(row, e.Location);
                    return;
                }
                if (e.Button != MouseButtons.Left) return;

                int checkX = row.Kind == RowKind.GroupHeader ? Dpi.S(20) : Dpi.S(22);
                bool onCheck = e.X >= checkX - Dpi.S(4) && e.X <= checkX + Dpi.S(17);

                if (row.Kind == RowKind.GroupHeader)
                {
                    if (row.Group >= 0) _owner.SetActiveGroup(row.Group);
                    if (onCheck)
                    {
                        bool all = View.GroupFullySelected(row.Group);
                        View.SelectGroup(row.Group, !all);
                        _owner.NotifySelectionChanged();
                    }
                    else if (row.Group >= 0)
                    {
                        GroupDef g = _state.Settings.Groups[row.Group];
                        g.Expanded = !g.Expanded;
                        _owner.NotifyGroupsChanged();
                    }
                    Invalidate();
                    return;
                }

                // IO 줄
                if ((ModifierKeys & Keys.Shift) != 0 && _lastClickedRow >= 0)
                {
                    View.SelectRangeByRow(_lastClickedRow, r);   // 보이는 순서대로
                }
                else
                {
                    // 같은 IO 를 다시 누를 때만 꺼집니다. 다른 IO 를 눌러도
                    // 이미 켜 둔 것은 그대로 남습니다.
                    View.Toggle(row.Io);
                    _lastClickedRow = r;
                }
                _mayDrag = true;
                _dragStart = e.Location;
                _owner.NotifySelectionChanged();
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _mayDrag = false;
            }

            // ---- 끌어다 놓기 ----

            protected override void OnDragEnter(DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(typeof(DragPayload)) ? DragDropEffects.Move : DragDropEffects.None;
            }

            protected override void OnDragOver(DragEventArgs e)
            {
                e.Effect = e.Data.GetDataPresent(typeof(DragPayload)) ? DragDropEffects.Move : DragDropEffects.None;
                Point p = PointToClient(new Point(e.X, e.Y));
                int r = RowAt(p.Y);
                int g = -2;
                if (r >= 0) g = View.Rows[r].Group;
                if (g != _dropGroup) { _dropGroup = g; Invalidate(); }
            }

            protected override void OnDragLeave(EventArgs e)
            {
                base.OnDragLeave(e);
                if (_dropGroup != -2) { _dropGroup = -2; Invalidate(); }
            }

            protected override void OnDragDrop(DragEventArgs e)
            {
                _dropGroup = -2;
                var payload = e.Data.GetData(typeof(DragPayload)) as DragPayload;
                if (payload == null) return;
                Point p = PointToClient(new Point(e.X, e.Y));
                int r = RowAt(p.Y);
                if (r < 0) return;
                int group = View.Rows[r].Group;
                if (group < 0) _state.RemoveFromGroups(payload.Names);
                else _state.PutInGroup(payload.Names, group);
                _owner.NotifyGroupsChanged();
            }

            private sealed class DragPayload
            {
                public readonly List<string> Names;
                public DragPayload(List<string> names) { Names = names; }
            }

            // ---- 오른쪽 단추 메뉴 ----

            private void ShowMenu(ViewRow row, Point at)
            {
                var menu = new ContextMenuStrip();
                menu.Font = Theme.Ui;

                if (row.Kind == RowKind.Io)
                {
                    IoRef io = View.All[row.Io];
                    menu.Items.Add("이 IO만 남기고 나머지 해제", null, delegate
                    {
                        View.ClearSelection();
                        View.SetSelected(row.Io, true);
                        _owner.NotifySelectionChanged();
                    });
                    menu.Items.Add("그룹에서 빼기", null, delegate
                    {
                        _state.RemoveFromGroups(new[] { io.Name });
                        _owner.NotifyGroupsChanged();
                    });
                    menu.Items.Add(new ToolStripSeparator());

                    var into = new ToolStripMenuItem("이 IO를 그룹에 담기");
                    for (int g = 0; g < _state.Settings.Groups.Count; g++)
                    {
                        int captured = g;
                        into.DropDownItems.Add(_state.Settings.Groups[g].Name, null, delegate
                        {
                            _state.PutInGroup(new[] { io.Name }, captured);
                            _owner.NotifyGroupsChanged();
                        });
                    }
                    into.Enabled = into.DropDownItems.Count > 0;
                    menu.Items.Add(into);
                }
                else
                {
                    menu.Items.Add("이 그룹 전체 선택", null, delegate
                    {
                        View.SelectGroup(row.Group, true); _owner.NotifySelectionChanged();
                    });
                    menu.Items.Add("이 그룹 전체 해제", null, delegate
                    {
                        View.SelectGroup(row.Group, false); _owner.NotifySelectionChanged();
                    });
                    if (row.Group >= 0)
                    {
                        menu.Items.Add(new ToolStripSeparator());
                        menu.Items.Add("이름 바꾸기", null, delegate
                        {
                            GroupDef g = _state.Settings.Groups[row.Group];
                            string name;
                            if (!TextInputDialog.Ask(this, "그룹 이름", "새 이름을 적어 주세요.", g.Name, out name)) return;
                            g.Name = name;
                            _owner.NotifyGroupsChanged();
                        });
                        menu.Items.Add("위로 옮기기", null, delegate
                        {
                            _state.MoveGroup(row.Group, -1); _owner.SetActiveGroup(row.Group - 1);
                            _owner.NotifyGroupsChanged();
                        });
                        menu.Items.Add("아래로 옮기기", null, delegate
                        {
                            _state.MoveGroup(row.Group, +1); _owner.SetActiveGroup(row.Group + 1);
                            _owner.NotifyGroupsChanged();
                        });
                        menu.Items.Add("고른 IO를 이 그룹에 담기", null, delegate
                        {
                            _state.PutInGroup(View.SelectedNames(), row.Group);
                            _owner.NotifyGroupsChanged();
                        });
                        menu.Items.Add("그룹 삭제", null, delegate
                        {
                            _state.RemoveGroup(row.Group);
                            _owner.SetActiveGroup(-1);
                            _owner.NotifyGroupsChanged();
                        });
                    }
                }
                menu.Show(this, at);
            }
        }
    }
}
