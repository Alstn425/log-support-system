using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CoastNotebook
{
    public static class CoastStyle
    {
        public static readonly Color Background = Color.FromArgb(16, 23, 30);
        public static readonly Color Panel = Color.FromArgb(25, 35, 45);
        public static readonly Color Input = Color.FromArgb(16, 26, 35);
        public static readonly Color Line = Color.FromArgb(48, 64, 79);
        public static readonly Color Text = Color.FromArgb(231, 237, 244);
        public static readonly Color Muted = Color.FromArgb(165, 179, 194);
        public static readonly Color Green = Color.FromArgb(72, 214, 172);
        public static void Form(Form form)
        { form.Font = new Font("맑은 고딕", 9F); form.BackColor = Background; form.ForeColor = Text; }
        public static void InputControl(Control control)
        { control.BackColor = Input; control.ForeColor = control is DateTimePicker ? Color.Black : Text; control.Margin = new Padding(3); }
        public static Button Button(string text, EventHandler handler, bool primary = false)
        {
            Button button = new Button(); button.Text = text; button.Height = 30;
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Line;
            button.BackColor = primary ? Color.FromArgb(24, 116, 94) : Color.FromArgb(37, 53, 68);
            button.ForeColor = Text; button.Click += handler; return button;
        }
        public static TableLayoutPanel Card()
        {
            TableLayoutPanel panel = new TableLayoutPanel(); panel.ColumnCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.AutoSize = true; panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Dock = DockStyle.Top; panel.Padding = new Padding(8); panel.BackColor = Panel;
            return panel;
        }
        public static int Add(TableLayoutPanel parent, Control control, int height)
        {
            int row = parent.RowCount++; parent.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill; control.Margin = Padding.Empty; parent.Controls.Add(control, 0, row);
            return row;
        }
        public static TableLayoutPanel Row(Control[] controls, params float[] weights)
        {
            TableLayoutPanel row = new TableLayoutPanel(); row.RowCount = 1; row.ColumnCount = controls.Length;
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < controls.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, weights[i]));
                controls[i].Dock = DockStyle.Fill; controls[i].Margin = new Padding(2);
                row.Controls.Add(controls[i], i, 0);
            }
            return row;
        }
        public static Control Field(string caption, Control input)
        {
            TableLayoutPanel field = new TableLayoutPanel(); field.ColumnCount = 1; field.RowCount = 2;
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 18)); field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label label = new Label(); label.Text = caption; label.ForeColor = Muted; label.Dock = DockStyle.Fill;
            label.Font = new Font("맑은 고딕", 8F); field.Controls.Add(label, 0, 0);
            InputControl(input); input.Dock = DockStyle.Fill; field.Controls.Add(input, 0, 1); return field;
        }
        public static DataGridView Grid()
        {
            DataGridView grid = new DataGridView(); grid.Dock = DockStyle.Fill; grid.ReadOnly = true;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = Background; grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Line; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Panel;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.DefaultCellStyle.BackColor = Background; grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 65, 63);
            grid.DefaultCellStyle.SelectionForeColor = Text; grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            return grid;
        }
    }

    public static class RecordTable
    {
        public static ListView Create(bool compact = false)
        {
            ListView list = new ListView();
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false;
            list.HideSelection = false; list.GridLines = true;
            list.BackColor = Color.FromArgb(43, 51, 58); list.ForeColor = Color.WhiteSmoke;
            list.ShowItemToolTips = true;
            list.Columns.Add(compact ? "시간" : "일시", compact ? 55 : 105);
            list.Columns.Add("제목 / 장소", compact ? 85 : 100);
            list.Columns.Add("내역", compact ? 140 : 155); list.Columns.Add("상태", 55);
            list.Columns.Add("사진", 40);
            list.Columns.Add("카메라", 100); list.Columns.Add("방위각", 65);
            list.Columns.Add("고각", 55); list.Columns.Add("구간", 65);
            list.Columns.Add("구분", 70); list.Columns.Add("철수/완료일시", 120);
            list.Columns.Add("선종", 80); list.Columns.Add("선명", 110); list.Columns.Add("식별번호", 100);
            return list;
        }
        public static void Fill(ListView list, List<Entry> entries, string selectedId, bool compact = false)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (Entry entry in entries)
                {
                    ListViewItem row = new ListViewItem(entry.Time.ToString(compact ? "HH:mm" : "MM-dd HH:mm"));
                    row.SubItems.Add(entry.Title); row.SubItems.Add(entry.Describe());
                    row.SubItems.Add(entry.StateText());
                    row.SubItems.Add(entry.Photos.Count.ToString(CultureInfo.InvariantCulture));
                    row.SubItems.Add(entry.Camera);
                    row.SubItems.Add(entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString(CultureInfo.InvariantCulture) : "");
                    row.SubItems.Add(entry.Elevation.HasValue ? entry.Elevation.Value.ToString(CultureInfo.InvariantCulture) : "");
                    row.SubItems.Add(entry.Section);
                    row.SubItems.Add(entry.CategoryText());
                    row.SubItems.Add(entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("MM-dd HH:mm") : "");
                    row.SubItems.Add(entry.VesselType); row.SubItems.Add(entry.VesselName); row.SubItems.Add(entry.VesselNumber);
                    row.ToolTipText = entry.Time.ToString("yyyy-MM-dd HH:mm") + " " + entry.CategoryText()
                        + "\r\n" + entry.Camera + " / " + entry.Title + "\r\n" + entry.Describe()
                        + "\r\n" + entry.StateText() + " / 방위각 " + entry.Azimuth + " / 고각 " + entry.Elevation
                        + "\r\n" + entry.Note;
                    row.Tag = entry; list.Items.Add(row);
                    if (entry.Id == selectedId) row.Selected = true;
                }
            }
            finally { list.EndUpdate(); }
        }
        public static Entry Selected(ListView list)
        {
            return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as Entry;
        }
        public static ListView CreateDomain(bool vessel)
        {
            ListView list = Create(); list.Columns.Clear();
            string[] titles = vessel ? new string[] { "출항/등록일시", "출항/등록항", "입항일시", "입항항", "선박유형", "선명", "식별번호", "상태", "비고", "사진", "구간" }
                : new string[] { "식별일시", "카메라", "지형지물", "유형·수량", "상태", "철수일시", "P", "T", "비고", "사진", "구간" };
            foreach (string title in titles) list.Columns.Add(title, title.Contains("일시") ? 125 : 100);
            return list;
        }
        public static void FillDomain(ListView list, IList<Entry> entries, bool vessel)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (Entry entry in entries)
                {
                    if ((entry.Category == "Vessel") != vessel) throw new ArgumentException("조회 종류가 다른 기록입니다.");
                    string finished = entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
                    string[] fields = vessel ? new string[] { entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Title, finished, entry.ArrivalPlace(),
                        entry.VesselType, entry.VesselName, entry.VesselNumber, entry.StateText(), entry.Note, entry.Photos.Count.ToString(), entry.Section }
                        : new string[] { entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Camera, entry.Title, entry.Describe(), entry.StateText(), finished,
                        entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString() : "", entry.Elevation.HasValue ? entry.Elevation.Value.ToString() : "", entry.Note,
                        entry.Photos.Count.ToString(), entry.Section };
                    ListViewItem row = new ListViewItem(fields); row.Tag = entry; list.Items.Add(row);
                }
            }
            finally { list.EndUpdate(); }
        }
    }

    public class HistoryDialog : Form
    {
        readonly Book book;
        readonly bool vessel;
        DateTimePicker from, through;
        CheckBox active, done;
        TextBox search;
        ListView records;
        Label count;
        List<Entry> current = new List<Entry>();
        public string SelectedId = "";

        public HistoryDialog(Book source, bool vessels)
        {
            book = source; vessel = vessels; Text = vessel ? "COAST · 선박 조회" : "COAST · 식별표적 조회"; Size = new Size(950, 560);
            MinimumSize = new Size(680, 380); StartPosition = FormStartPosition.CenterParent;
            CoastStyle.Form(this);
            TableLayoutPanel filters = CoastStyle.Card();
            from = new DateTimePicker(); from.Format = DateTimePickerFormat.Short; from.Value = DateTime.Today;
            through = new DateTimePicker(); through.Format = DateTimePickerFormat.Short;
            search = new TextBox();
            CoastStyle.Add(filters, CoastStyle.Row(new Control[] { CoastStyle.Field("시작일", from),
                CoastStyle.Field("종료일", through), CoastStyle.Field("검색어", search) }, 30, 30, 40), 50);
            FlowLayoutPanel buttons = new FlowLayoutPanel(); buttons.WrapContents = true;
            buttons.Controls.Add(CoastStyle.Button("조회", RefreshRows));
            buttons.Controls.Add(CoastStyle.Button("최근 1주", delegate { from.Value = DateTime.Today.AddDays(-6); through.Value = DateTime.Today; RefreshRows(null, EventArgs.Empty); }));
            buttons.Controls.Add(CoastStyle.Button("전체 기간", delegate {
                DateTime first = DateTime.Today, last = DateTime.Today;
                foreach (Entry entry in book.Entries) { if (entry.Time.Date < first) first = entry.Time.Date; if (entry.Time.Date > last) last = entry.Time.Date; }
                from.Value = first; through.Value = last; RefreshRows(null, EventArgs.Empty);
            }));
            buttons.Controls.Add(CoastStyle.Button("CSV 내보내기", Export));
            buttons.Controls.Add(CoastStyle.Button("유형별 집계", ShowStats));
            active = new CheckBox(); active.Text = vessel ? "출항중" : "관리중"; active.Checked = true; active.Width = 90;
            done = new CheckBox(); done.Text = vessel ? "입항/기존" : "철수"; done.Checked = true; done.Width = 100;
            buttons.Controls.Add(active); buttons.Controls.Add(done); CoastStyle.Add(filters, buttons, 65);
            count = new Label(); count.ForeColor = CoastStyle.Muted; CoastStyle.Add(filters, count, 23);
            records = RecordTable.CreateDomain(vessel); records.Dock = DockStyle.Fill;
            Button open = CoastStyle.Button("선택 기록 열기 (또는 두 번 클릭)", OpenSelected); open.Dock = DockStyle.Bottom;
            records.DoubleClick += OpenSelected;
            Controls.Add(records); Controls.Add(open); Controls.Add(filters);
            from.ValueChanged += RefreshRows; through.ValueChanged += RefreshRows;
            active.CheckedChanged += RefreshRows; done.CheckedChanged += RefreshRows;
            search.TextChanged += RefreshRows; RefreshRows(this, EventArgs.Empty);
        }
        void RefreshRows(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date)
            { current.Clear(); records.Items.Clear(); count.Text = "시작일이 종료일보다 늦습니다."; return; }
            current = Data.Domain(Data.SelectEntries(book, from.Value, through.Value, active.Checked, done.Checked, search.Text), vessel);
            RecordTable.FillDomain(records, current, vessel);
            count.Text = current.Count + "건 · 상태 모두 해제 시 전체 · " + (vessel ? "출항/등록" : "식별") + " 날짜 기준";
        }
        void Export(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date) { count.Text = "조회 기간을 확인하세요."; return; }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 파일|*.csv"; dialog.DefaultExt = "csv"; dialog.AddExtension = true;
                dialog.FileName = "COAST_" + (vessel ? "선박_" : "식별표적_") + DateTime.Today.ToString("yyyyMMdd") + "_조회.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { Data.ExportEntries(book, current, dialog.FileName, vessel); count.Text = "조회 결과 CSV 저장 완료: " + dialog.FileName; }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "CSV 저장 확인"); }
            }
        }
        void ShowStats(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date) { count.Text = "조회 기간을 확인하세요."; return; }
            using (TextDialog dialog = new TextDialog("유형별 집계", Data.Statistics(current, vessel), true)) dialog.ShowDialog(this);
        }
        void OpenSelected(object sender, EventArgs e)
        {
            Entry entry = RecordTable.Selected(records); if (entry == null) return;
            SelectedId = entry.Id; DialogResult = DialogResult.OK;
        }
    }

    public class PhotoAssignDialog : Form
    {
        readonly Entry entry;
        string[] files = new string[0];
        TextBox preview;
        Button assign;
        public string[] FileNames { get { return files; } }
        public PhotoAssignDialog(Entry selected)
        {
            entry = selected; Text = "사진할당"; Size = new Size(700, 430); StartPosition = FormStartPosition.CenterParent;
            CoastStyle.Form(this);
            Label notice = new Label(); notice.Text = "파일명 미리보기 · 할당 시 복사 검증과 연결 저장 후 원래 파일을 삭제합니다.";
            notice.Dock = DockStyle.Top; notice.Height = 30; notice.ForeColor = CoastStyle.Muted;
            Button choose = CoastStyle.Button("사진 선택…", Choose); choose.Dock = DockStyle.Top;
            preview = new TextBox(); preview.Multiline = true; preview.ReadOnly = true; preview.ScrollBars = ScrollBars.Both;
            preview.Dock = DockStyle.Fill; CoastStyle.InputControl(preview);
            assign = CoastStyle.Button("할당", delegate { DialogResult = DialogResult.OK; }, true);
            assign.Enabled = false; assign.Dock = DockStyle.Bottom;
            Controls.Add(preview); Controls.Add(choose); Controls.Add(notice); Controls.Add(assign);
        }
        void Choose(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true; dialog.Filter = "사진|*.jpg;*.jpeg;*.png;*.bmp";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                files = dialog.FileNames; preview.Text = Preview(entry, files); assign.Enabled = files.Length > 0;
            }
        }
        public static string Preview(Entry entry, string[] files)
        {
            List<string> lines = new List<string>(); int total = entry.Photos.Count + files.Length;
            for (int i = 0; i < files.Length; i++)
                lines.Add(Path.GetFileName(files[i]) + " → " + Data.PhotoFileName(entry, total,
                    entry.Photos.Count + i + 1, Path.GetExtension(files[i]).ToLowerInvariant()));
            if (entry.Photos.Count > 0) lines.Add("\r\n기존 연결 사진도 최종 장수에 맞게 번호·이름을 갱신합니다.");
            lines.Add("\r\n일부 파일 처리에 실패하면 최종 번호가 달라질 수 있습니다. 충돌 시 덮어쓰지 않습니다.");
            return String.Join("\r\n", lines.ToArray());
        }
    }

    public class RecordEditDialog : Form
    {
        public Entry Value;
        readonly Entry entry;
        DateTimePicker time, finished;
        ComboBox place, camera, vesselType, arrivalPort;
        TextBox name, number, note;
        CheckBox complete, hasFinished;
        public RecordEditDialog(Entry value, Settings settings)
        {
            entry = value; Text = value.Category == "Vessel" ? "선박 정보 수정" : "날짜·상태·비고 수정";
            CoastStyle.Form(this); Size = new Size(540, 610); StartPosition = FormStartPosition.CenterParent;
            Panel scroll = new Panel(); scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; Controls.Add(scroll);
            TableLayoutPanel card = CoastStyle.Card(); scroll.Controls.Add(card);
            time = new DateTimePicker(); time.Format = DateTimePickerFormat.Custom; time.CustomFormat = "yyyy-MM-dd HH:mm"; time.Value = entry.Time;
            CoastStyle.Add(card, CoastStyle.Field("기록 일시", time), 49);
            place = new ComboBox(); place.Text = entry.Title;
            foreach (string harbor in Data.Lines(settings.Harbors)) place.Items.Add(harbor);
            CoastStyle.Add(card, CoastStyle.Field(entry.Category == "Vessel" ? "출항/등록항" : "지형지물 · 생략 가능", place), 49);
            if (entry.Category == "Vessel")
            {
                vesselType = new ComboBox(); foreach (string type in Data.Lines(settings.VesselTypes)) vesselType.Items.Add(type); vesselType.Text = entry.VesselType;
                name = new TextBox(); name.Text = entry.VesselName; number = new TextBox(); number.Text = entry.VesselNumber;
                arrivalPort = new ComboBox(); foreach (string port in Data.Lines(settings.Harbors)) arrivalPort.Items.Add(port);
                arrivalPort.Text = entry.ArrivalPlace(); CoastStyle.Add(card, CoastStyle.Field("입항항 (직접 입력 가능)", arrivalPort), 49);
                CoastStyle.Add(card, CoastStyle.Field("선박유형", vesselType), 49);
                CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("선명 · 선택", name), CoastStyle.Field("식별번호 · 선택", number) }, 50, 50), 49);
            }
            else
            {
                camera = new ComboBox(); foreach (CameraSpec spec in Data.Cameras(settings)) camera.Items.Add(spec.Name); camera.Text = entry.Camera;
                CoastStyle.Add(card, CoastStyle.Field("카메라", camera), 49);
            }
            complete = new CheckBox(); complete.Text = entry.Category == "Vessel" ? "입항 완료" : "철수/완료"; complete.Checked = entry.Complete;
            hasFinished = new CheckBox(); hasFinished.Text = "처리 시각 기록"; hasFinished.Checked = entry.FinishedAt.HasValue;
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { complete, hasFinished }, 50, 50), 29);
            finished = new DateTimePicker(); finished.Format = DateTimePickerFormat.Custom; finished.CustomFormat = "yyyy-MM-dd HH:mm";
            finished.Value = entry.FinishedAt ?? DateTime.Now; CoastStyle.Add(card, CoastStyle.Field("철수/입항/완료 시각", finished), 49);
            note = new TextBox(); note.Multiline = true; note.Text = entry.Note; CoastStyle.Add(card, CoastStyle.Field("비고", note), 75);
            Button save = CoastStyle.Button("저장", Save, true); CoastStyle.Add(card, save, 35);
            complete.CheckedChanged += delegate { if (complete.Checked) { hasFinished.Checked = true; finished.Value = DateTime.Now; }
                else hasFinished.Checked = false; UpdateState(); };
            hasFinished.CheckedChanged += delegate { UpdateState(); };
            if (entry.Category == "Vessel" && entry.Movement == "입항") complete.Enabled = false;
            UpdateState();
        }
        void UpdateState() { hasFinished.Enabled = complete.Checked; finished.Enabled = complete.Checked && hasFinished.Checked; }
        void Save(object sender, EventArgs e)
        {
            try
            {
                if (time.Value > DateTime.Now || (complete.Checked && hasFinished.Checked && finished.Value > DateTime.Now))
                    throw new FormatException("미래 시각은 저장할 수 없습니다.");
                entry.Time = time.Value; entry.Title = place.Text.Trim(); entry.Note = note.Text; entry.Complete = complete.Checked;
                entry.FinishedAt = complete.Checked && hasFinished.Checked ? (DateTime?)finished.Value : null;
                if (entry.Category == "Vessel")
                {
                    if (String.IsNullOrWhiteSpace(entry.Title)) throw new FormatException("항구를 입력하세요.");
                    entry.VesselType = vesselType.Text.Trim(); entry.VesselName = name.Text.Trim(); entry.VesselNumber = number.Text.Trim();
                    entry.ArrivalHarbor = complete.Checked ? arrivalPort.Text.Trim() : "";
                    if (entry.Complete && !entry.IndependentMovement && entry.ArrivalHarbor.Length == 0) throw new FormatException("입항항을 입력하세요.");
                    if (entry.Movement == "출항" && (!entry.Complete || entry.FinishedAt.HasValue)) entry.IndependentMovement = false;
                    if (!entry.IndependentMovement && entry.Movement == "입항") entry.FinishedAt = entry.Time;
                }
                else entry.Camera = camera.Text.Trim();
                Book check = new Book(); check.Entries.Add(entry); Data.Validate(check);
                Value = entry; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "입력 확인"); }
        }
    }

    public class TextDialog : Form
    {
        public TextBox Input = new TextBox();
        public TextDialog(string title, string text, bool readOnly = false)
        {
            Text = title; Width = 560; Height = 470;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("맑은 고딕", 9F);
            Input.Multiline = true; Input.ScrollBars = ScrollBars.Both;
            Input.Dock = DockStyle.Fill; Input.Text = text; Input.ReadOnly = readOnly;
            CoastStyle.Form(this); CoastStyle.InputControl(Input);
            Button save = new Button();
            save.Text = readOnly ? "닫기" : "적용"; save.Dock = DockStyle.Bottom; save.Height = 36;
            save.DialogResult = DialogResult.OK;
            Controls.Add(Input); Controls.Add(save);
        }
    }

    public class WindowsMacroInput : IMacroInput
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, Input[] inputs, int size);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct InputData { [System.Runtime.InteropServices.FieldOffset(0)] public MouseInput Mouse; }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct Input { public uint Type; public InputData Data; }
        public bool Available { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }
        public bool LeftDown { get { return (GetAsyncKeyState(1) & 0x8000) != 0; } }
        public bool EscapeDown { get { return (GetAsyncKeyState(27) & 0x8000) != 0; } }
        public int X { get { return Cursor.Position.X; } }
        public int Y { get { return Cursor.Position.Y; } }
        public int DesktopLeft { get { return SystemInformation.VirtualScreen.Left; } }
        public int DesktopTop { get { return SystemInformation.VirtualScreen.Top; } }
        public int DesktopWidth { get { return SystemInformation.VirtualScreen.Width; } }
        public int DesktopHeight { get { return SystemInformation.VirtualScreen.Height; } }
        public bool Contains(int x, int y) { return SystemInformation.VirtualScreen.Contains(x, y); }
        public void Click(int x, int y)
        {
            Cursor.Position = new Point(x, y);
            Input[] events = new Input[] {
                new Input { Type = 0, Data = new InputData { Mouse = new MouseInput { Flags = 0x0002 } } },
                new Input { Type = 0, Data = new InputData { Mouse = new MouseInput { Flags = 0x0004 } } }
            };
            if (SendInput(2, events, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Input))) != 2)
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "매크로 클릭 전송에 실패했습니다.");
        }
    }
    public class MacroOverlay : Form
    {
        readonly MacroEngine engine;
        readonly Font caption = new Font("맑은 고딕", 16F, FontStyle.Bold), number = new Font("맑은 고딕", 10F, FontStyle.Bold);
        public MacroOverlay(MacroEngine source)
        {
            engine = source; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen; BackColor = Color.Black; Opacity = 0.23; TopMost = true; ShowInTaskbar = false;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams value = base.CreateParams; value.ExStyle |= 0x20 | 0x80000 | 0x08000000; return value; }
        }
        protected override void WndProc(ref Message m)
        { if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; } base.WndProc(ref m); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.DrawString("COAST · 클릭 녹화 중\n완료: ESC 또는 COAST의 중지 버튼", caption, Brushes.Yellow, 25, 25);
            if (engine.Profile == null) return;
            for (int i = 0; i < engine.Profile.Steps.Count; i++)
            {
                MacroStep step = engine.Profile.Steps[i]; int x = step.X - Left, y = step.Y - Top;
                e.Graphics.FillEllipse(Brushes.Red, x - 6, y - 6, 12, 12);
                e.Graphics.DrawString((i + 1).ToString(), number, Brushes.White, x + 9, y - 12);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { caption.Dispose(); number.Dispose(); } base.Dispose(disposing); }
    }
    public class MacroEditDialog : Form
    {
        public MacroProfile Value;
        readonly MacroProfile original;
        TextBox name;
        NumericUpDown delay;
        DataGridView steps;
        Label error;
        public MacroEditDialog(MacroProfile profile, int slot)
        {
            original = profile; Text = "매크로 " + (slot + 1) + " · 클릭과 지연 설정"; Size = new Size(690, 560);
            MinimumSize = new Size(580, 380); StartPosition = FormStartPosition.CenterScreen; CoastStyle.Form(this);
            TableLayoutPanel top = CoastStyle.Card(); name = new TextBox(); name.Text = profile.Name;
            delay = new NumericUpDown(); delay.Minimum = 0; delay.Maximum = 600000; delay.Value = profile.DefaultDelayMs;
            CoastStyle.Add(top, CoastStyle.Row(new Control[] { CoastStyle.Field("슬롯 이름", name), CoastStyle.Field("기본 클릭 후 지연(ms)", delay),
                CoastStyle.Button("전체에 적용", delegate { foreach (DataGridViewRow row in steps.Rows) if (!row.IsNewRow) row.Cells[2].Value = (int)delay.Value; }) }, 48, 32, 20), 55);
            Label hint = new Label(); hint.Text = "좌표는 화면 전체 기준입니다. 같은 해상도·화면 배치를 사용하세요. 지연은 각 클릭 후 다음 클릭까지의 대기입니다.";
            hint.ForeColor = CoastStyle.Muted; CoastStyle.Add(top, hint, 42);
            steps = CoastStyle.Grid(); steps.ReadOnly = false; steps.AllowUserToAddRows = true; steps.AllowUserToDeleteRows = true;
            steps.Columns.Add("x", "X 좌표"); steps.Columns.Add("y", "Y 좌표"); steps.Columns.Add("delay", "클릭 후 지연(ms)");
            foreach (MacroStep step in profile.Steps) steps.Rows.Add(step.X, step.Y, step.DelayMs);
            TableLayoutPanel footer = CoastStyle.Card(); footer.Dock = DockStyle.Bottom;
            error = new Label(); error.ForeColor = Color.Salmon; CoastStyle.Add(footer, error, 35);
            Button cancel = CoastStyle.Button("취소", delegate { DialogResult = DialogResult.Cancel; });
            CoastStyle.Add(footer, CoastStyle.Row(new Control[] { CoastStyle.Button("선택 클릭 삭제", delegate {
                foreach (DataGridViewRow row in steps.SelectedRows) if (!row.IsNewRow) steps.Rows.Remove(row);
            }), cancel, CoastStyle.Button("슬롯 저장", Save, true) }, 34, 26, 40), 36);
            Controls.Add(steps); Controls.Add(top); Controls.Add(footer); CancelButton = cancel;
        }
        void Save(object sender, EventArgs e)
        {
            try
            {
                steps.EndEdit(); MacroProfile profile = Data.CloneMacro(original); profile.Name = name.Text.Trim();
                profile.DefaultDelayMs = (int)delay.Value; profile.Steps.Clear();
                foreach (DataGridViewRow row in steps.Rows)
                {
                    if (row.IsNewRow) continue; int x, y, wait;
                    if (!Int32.TryParse(Convert.ToString(row.Cells[0].Value), out x) || !Int32.TryParse(Convert.ToString(row.Cells[1].Value), out y)
                        || !Int32.TryParse(Convert.ToString(row.Cells[2].Value), out wait)) throw new FormatException("좌표와 지연은 정수로 입력하세요.");
                    profile.Steps.Add(new MacroStep { X = x, Y = y, DelayMs = wait });
                }
                Data.ValidateMacro(profile); Value = profile; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }
    }

    public class TextListEditor : TableLayoutPanel
    {
        public readonly List<TextBox> Inputs = new List<TextBox>();
        readonly List<Control> rows = new List<Control>();
        readonly Button add;
        public TextListEditor(IEnumerable<string> values, string addText)
        {
            ColumnCount = 1; ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top;
            add = CoastStyle.Button(addText, delegate { AddValue(""); });
            foreach (string value in values) AddValue(value);
            Controls.Add(add, 0, rows.Count); Reflow();
        }
        public void AddValue(string value)
        {
            TextBox box = new TextBox(); box.Text = value; CoastStyle.InputControl(box);
            Button up = CoastStyle.Button("↑", delegate { MoveValue(box, -1); });
            Button down = CoastStyle.Button("↓", delegate { MoveValue(box, 1); });
            Button remove = CoastStyle.Button("×", delegate { Remove(box); });
            Control row = CoastStyle.Row(new Control[] { box, up, down, remove }, 76, 8, 8, 8);
            Inputs.Add(box); rows.Add(row); Controls.Add(row); Reflow();
        }
        public void MoveValue(TextBox box, int delta)
        {
            int i = Inputs.IndexOf(box), j = i + delta; if (i < 0 || j < 0 || j >= Inputs.Count) return;
            TextBox value = Inputs[i]; Inputs[i] = Inputs[j]; Inputs[j] = value;
            Control row = rows[i]; rows[i] = rows[j]; rows[j] = row; Reflow();
        }
        void Remove(TextBox box)
        {
            int i = Inputs.IndexOf(box); if (i < 0) return;
            Control row = rows[i]; rows.RemoveAt(i); Inputs.RemoveAt(i); Controls.Remove(row); row.Dispose(); Reflow();
        }
        void Reflow()
        {
            SuspendLayout(); RowStyles.Clear(); RowCount = rows.Count + 1;
            for (int i = 0; i < rows.Count; i++) { RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); SetRow(rows[i], i); rows[i].Dock = DockStyle.Fill; }
            RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            if (Controls.Contains(add)) { SetRow(add, rows.Count); add.Dock = DockStyle.Fill; }
            ResumeLayout(true);
        }
        public List<string> ReadValues()
        {
            List<string> result = new List<string>(); HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TextBox box in Inputs)
            {
                string value = box.Text.Trim();
                if (value.Length == 0 || value.IndexOfAny(new char[] { '\r', '\n' }) >= 0 || !unique.Add(value))
                    throw new FormatException("각 입력칸에 이름을 하나씩 입력하고 빈칸·중복 이름을 확인하세요.");
                result.Add(value);
            }
            return result;
        }
    }
    public class CameraCardEditor : TableLayoutPanel
    {
        public TextBox CameraName;
        public TextListEditor Places;
        public CameraCardEditor(CameraSpec camera, EventHandler remove)
        {
            ColumnCount = 1; ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Padding = new Padding(10);
            BackColor = CoastStyle.Panel; Margin = new Padding(5, 7, 5, 7);
            CameraName = new TextBox(); CameraName.Text = camera.Name;
            CoastStyle.Add(this, CoastStyle.Row(new Control[] { CoastStyle.Field("카메라 이름", CameraName),
                CoastStyle.Button("카메라 삭제", remove) }, 80, 20), 49);
            Label label = new Label(); label.Text = "이 카메라의 지형지물"; label.ForeColor = CoastStyle.Muted; CoastStyle.Add(this, label, 24);
            Places = new TextListEditor(camera.Places, "+ 지형지물 추가");
            RowCount++; RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(Places, 0, RowCount - 1);
        }
        public CameraSpec ReadValue() { return new CameraSpec { Name = CameraName.Text.Trim(), Places = Places.ReadValues() }; }
    }
    public class ConfigDialog : Form
    {
        public Settings Value;
        readonly Settings previous;
        TextBox name, csv, photos;
        CheckBox copy, success, solarEnabled;
        DateTimePicker solarDay, bmnt, sunrise, sunset, eent;
        NumericUpDown riseOffset, setOffset;
        Label solarPreview, error;
        TabControl tabs;
        TableLayoutPanel cameraCards;
        List<CameraCardEditor> cameras = new List<CameraCardEditor>();
        TextListEditor harbors, vesselTypes;
        DataGridView kinds;
        public ConfigDialog(Settings current)
        {
            previous = current; Text = "COAST · 환경설정"; Size = new Size(820, 730); MinimumSize = new Size(740, 520);
            StartPosition = FormStartPosition.CenterScreen; CoastStyle.Form(this);
            tabs = new TabControl(); tabs.Dock = DockStyle.Fill; tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(140, 30); tabs.SizeMode = TabSizeMode.Fixed;
            tabs.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                using (SolidBrush brush = new SolidBrush(CoastStyle.Panel)) e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, e.Bounds,
                    tabs.SelectedIndex == e.Index ? CoastStyle.Green : CoastStyle.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            Controls.Add(tabs);
            TableLayoutPanel basic = Page("기본");
            name = new TextBox(); name.Text = current.Name; CoastStyle.Add(basic, CoastStyle.Field("구간 이름", name), 55);
            csv = Folder(basic, "CSV 저장 폴더", current.CsvFolder); photos = Folder(basic, "사진 할당 폴더", current.PhotoFolder);
            copy = new CheckBox(); copy.Text = "저장 후 보고문 복사"; copy.Checked = current.CopyText; CoastStyle.Add(basic, copy, 32);
            success = new CheckBox(); success.Text = "저장 결과 팝업 표시"; success.Checked = current.ShowSuccess; CoastStyle.Add(basic, success, 32);
            Hint(basic, "원본 XML은 EXE 옆 Data 폴더에 있습니다. 사진·CSV는 위 폴더로 출력합니다.");
            TableLayoutPanel cameraPage = Page("카메라·지형지물");
            Hint(cameraPage, "카메라 이름과 그 카메라에 보이는 지형지물을 각각 입력하세요.");
            cameraCards = CoastStyle.Card(); AutoRow(cameraPage, cameraCards);
            foreach (CameraSpec spec in Data.Cameras(current)) AddCamera(spec);
            CoastStyle.Add(cameraPage, CoastStyle.Button("+ 카메라 추가", delegate { AddCamera(new CameraSpec()); }), 35);
            TableLayoutPanel portPage = Page("항구·선박유형");
            Hint(portPage, "항구 · 북 → 남 순서로 입력하고 ↑ ↓ 버튼으로 순서를 조절하세요.");
            harbors = new TextListEditor(Data.Lines(current.Harbors), "+ 항구 추가"); AutoRow(portPage, harbors);
            Hint(portPage, "선박유형"); vesselTypes = new TextListEditor(Data.Lines(current.VesselTypes), "+ 선박유형 추가"); AutoRow(portPage, vesselTypes);
            TableLayoutPanel typePage = Page("식별유형");
            Hint(typePage, "유형명과 단위를 별도 칸에 입력합니다. 채집/해루질처럼 /로 묶으면 기록 화면에서는 각각 선택합니다.");
            kinds = CoastStyle.Grid(); kinds.ReadOnly = false; kinds.AllowUserToAddRows = true; kinds.AllowUserToDeleteRows = true;
            kinds.EditMode = DataGridViewEditMode.EditOnEnter; kinds.Columns.Add("name", "유형명 (묶음은 /로 구분)"); kinds.Columns.Add("unit", "단위");
            kinds.Columns[0].FillWeight = 80; kinds.Columns[1].FillWeight = 20;
            foreach (Kind kind in current.Kinds) { int row = kinds.Rows.Add(kind.Name, kind.Unit); kinds.Rows[row].Tag = kind.Id; }
            CoastStyle.Add(typePage, kinds, 340);
            CoastStyle.Add(typePage, CoastStyle.Button("선택한 유형 삭제", delegate {
                foreach (DataGridViewRow row in kinds.SelectedRows) if (!row.IsNewRow) kinds.Rows.Remove(row);
            }), 34);
            BuildSolar(Page("기상·알림"), current.Solar);
            TableLayoutPanel footer = CoastStyle.Card(); footer.Dock = DockStyle.Bottom;
            error = new Label(); error.ForeColor = Color.Salmon; CoastStyle.Add(footer, error, 34);
            Button cancel = CoastStyle.Button("취소", delegate { DialogResult = DialogResult.Cancel; });
            CoastStyle.Add(footer, CoastStyle.Row(new Control[] { cancel, CoastStyle.Button("설정 저장", Save, true) }, 30, 70), 38);
            Controls.Add(footer); CancelButton = cancel;
        }
        TableLayoutPanel Page(string text)
        {
            TabPage page = new TabPage(text); page.BackColor = CoastStyle.Background; page.ForeColor = CoastStyle.Text; page.AutoScroll = true;
            tabs.TabPages.Add(page); TableLayoutPanel card = CoastStyle.Card(); page.Controls.Add(card); return card;
        }
        static void AutoRow(TableLayoutPanel parent, Control child)
        { int row = parent.RowCount++; parent.RowStyles.Add(new RowStyle(SizeType.AutoSize)); child.Dock = DockStyle.Top; parent.Controls.Add(child, 0, row); }
        static void Hint(TableLayoutPanel parent, string text)
        { Label label = new Label(); label.Text = text; label.ForeColor = CoastStyle.Muted; CoastStyle.Add(parent, label, 44); }
        TextBox Folder(TableLayoutPanel parent, string caption, string value)
        {
            TextBox box = new TextBox(); box.Text = value;
            Button browse = CoastStyle.Button("폴더 선택…", delegate {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                { if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text; if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath; }
            });
            CoastStyle.Add(parent, CoastStyle.Row(new Control[] { CoastStyle.Field(caption, box), browse }, 80, 20), 56); return box;
        }
        public void AddCamera(CameraSpec spec)
        {
            CameraCardEditor card = null;
            card = new CameraCardEditor(spec, delegate { cameras.Remove(card); cameraCards.Controls.Remove(card); card.Dispose(); ReflowCameras(); });
            cameras.Add(card); cameraCards.Controls.Add(card); ReflowCameras();
        }
        void ReflowCameras()
        {
            cameraCards.SuspendLayout(); cameraCards.RowStyles.Clear(); cameraCards.RowCount = cameras.Count;
            for (int i = 0; i < cameras.Count; i++) { cameraCards.RowStyles.Add(new RowStyle(SizeType.AutoSize)); cameraCards.SetRow(cameras[i], i); }
            cameraCards.ResumeLayout(true);
        }
        DateTimePicker SolarTime(string value)
        {
            DateTimePicker picker = new DateTimePicker(); picker.Format = DateTimePickerFormat.Custom; picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true; picker.ShowCheckBox = true; picker.Value = DateTime.Today;
            if (!String.IsNullOrEmpty(value)) picker.Value = DateTime.Today.Add(DateTime.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay);
            picker.Checked = !String.IsNullOrEmpty(value); picker.ValueChanged += SolarChanged; return picker;
        }
        void BuildSolar(TableLayoutPanel page, SolarSettings current)
        {
            Hint(page, "기상제원 입력 · 입력한 날짜의 시각만 사용합니다. 고정 시각 알람은 Windows 알람을 사용하세요.");
            solarDay = new DateTimePicker(); solarDay.Format = DateTimePickerFormat.Short; solarDay.Value = current.Date;
            CoastStyle.Add(page, CoastStyle.Field("적용 날짜", solarDay), 54);
            bmnt = SolarTime(current.Bmnt); sunrise = SolarTime(current.Sunrise); sunset = SolarTime(current.Sunset); eent = SolarTime(current.Eent);
            CoastStyle.Add(page, CoastStyle.Row(new Control[] { CoastStyle.Field("BMNT", bmnt), CoastStyle.Field("일출", sunrise),
                CoastStyle.Field("일몰", sunset), CoastStyle.Field("EENT", eent) }, 25, 25, 25, 25), 56);
            Hint(page, "시각을 모르면 체크를 해제합니다. BMNT·EENT는 참고 시각이며 교대 알람은 일출·일몰을 기준으로 합니다.");
            solarEnabled = new CheckBox(); solarEnabled.Text = "주야간 교대 알람 활성화"; solarEnabled.Checked = current.Enabled; CoastStyle.Add(page, solarEnabled, 32);
            riseOffset = new NumericUpDown(); riseOffset.Minimum = -720; riseOffset.Maximum = 720; riseOffset.Value = current.SunriseOffset;
            setOffset = new NumericUpDown(); setOffset.Minimum = -720; setOffset.Maximum = 720; setOffset.Value = current.SunsetOffset;
            CoastStyle.Add(page, CoastStyle.Row(new Control[] { CoastStyle.Field("일출 기준 ±분", riseOffset), CoastStyle.Field("일몰 기준 ±분", setOffset) }, 50, 50), 54);
            Hint(page, "음수: 기준 시각 전 / 양수: 기준 시각 후 · 프로그램 실행 중 알림이 동작합니다.");
            solarPreview = new Label(); solarPreview.ForeColor = CoastStyle.Green; CoastStyle.Add(page, solarPreview, 60);
            CoastStyle.Add(page, CoastStyle.Button("알람 시험", delegate { System.Media.SystemSounds.Exclamation.Play();
                MessageBox.Show(this, "주야간 교대 알람 시험입니다.", "알람 시험"); }), 35);
            solarDay.ValueChanged += SolarChanged; solarEnabled.CheckedChanged += SolarChanged;
            riseOffset.ValueChanged += SolarChanged; setOffset.ValueChanged += SolarChanged; SolarChanged(null, EventArgs.Empty);
        }
        static string TimeValue(DateTimePicker picker) { return picker.Checked ? picker.Value.ToString("HH:mm") : ""; }
        SolarSettings ReadSolar()
        { return new SolarSettings { Date = solarDay.Value.Date, Bmnt = TimeValue(bmnt), Sunrise = TimeValue(sunrise), Sunset = TimeValue(sunset), Eent = TimeValue(eent),
            Enabled = solarEnabled.Checked, SunriseOffset = (int)riseOffset.Value, SunsetOffset = (int)setOffset.Value }; }
        void SolarChanged(object sender, EventArgs e)
        {
            if (solarPreview == null) return;
            try
            {
                SolarSettings solar = ReadSolar(); List<string> lines = new List<string>();
                foreach (SolarAlarm alarm in Data.SolarAlarms(solar)) lines.Add(alarm.Title + " · " + alarm.At.ToString("MM-dd HH:mm"));
                solarPreview.Text = lines.Count == 0 ? "교대 알람 비활성화" : String.Join("\r\n", lines.ToArray());
            }
            catch (Exception ex) { solarPreview.Text = ex.Message; }
        }
        public static void Validate(Settings settings)
        {
            if (settings == null || !settings.Configured || String.IsNullOrWhiteSpace(settings.Name)
                || String.IsNullOrWhiteSpace(settings.CsvFolder) || !Path.IsPathRooted(settings.CsvFolder)
                || String.IsNullOrWhiteSpace(settings.PhotoFolder) || !Path.IsPathRooted(settings.PhotoFolder)
                || settings.Kinds == null || settings.Kinds.Count == 0 || settings.Kinds.Count > 50)
                throw new FormatException("구간명, 폴더와 1~50개의 유형을 확인하세요.");
            HashSet<string> ids = new HashSet<string>(), names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Kind kind in settings.Kinds)
            {
                if (kind == null || String.IsNullOrWhiteSpace(kind.Name) || String.IsNullOrWhiteSpace(kind.Id)
                    || !ids.Add(kind.Id) || !names.Add(kind.Name) || String.IsNullOrWhiteSpace(kind.Unit)) throw new FormatException("유형 이름·단위와 중복 여부를 확인하세요.");
                HashSet<string> children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string child in kind.Choices()) if (child.Length == 0 || !children.Add(child)) throw new FormatException("/ 사이의 유형은 비어 있거나 중복될 수 없습니다.");
            }
            Data.Cameras(settings); Data.ValidateSolar(settings.Solar);
            if (settings.Macros == null || settings.Macros.Length != 2) throw new FormatException("매크로 슬롯은 2개입니다.");
            foreach (MacroProfile macro in settings.Macros) Data.ValidateMacro(macro);
        }
        void Save(object sender, EventArgs e)
        {
            try
            {
                kinds.EndEdit(); Settings settings = Data.Clone(new Book { Settings = previous }).Settings;
                settings.Configured = true; settings.Name = name.Text.Trim(); settings.CsvFolder = csv.Text.Trim(); settings.PhotoFolder = photos.Text.Trim();
                settings.CopyText = copy.Checked; settings.ShowSuccess = success.Checked; settings.Alarms = "";
                settings.StructuredCameras = true; settings.CameraDefinitions.Clear();
                foreach (CameraCardEditor card in cameras) settings.CameraDefinitions.Add(card.ReadValue());
                settings.Cameras = ""; settings.Places = "";
                settings.Harbors = String.Join("\r\n", harbors.ReadValues().ToArray()); settings.VesselTypes = String.Join("\r\n", vesselTypes.ReadValues().ToArray());
                settings.Kinds.Clear();
                foreach (DataGridViewRow row in kinds.Rows)
                {
                    if (row.IsNewRow) continue;
                    string title = Convert.ToString(row.Cells[0].Value).Trim(), unit = Convert.ToString(row.Cells[1].Value).Trim();
                    Kind kind = new Kind { Id = Guid.NewGuid().ToString("N"), Name = title, Unit = unit };
                    foreach (Kind old in previous.Kinds) if (old.Name == title && old.Unit == unit) kind.Id = old.Id;
                    settings.Kinds.Add(kind);
                }
                settings.Solar = ReadSolar(); Validate(settings); Value = settings; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        }
    }

}
