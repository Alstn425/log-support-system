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
    }

    public class HistoryDialog : Form
    {
        readonly Book book;
        DateTimePicker from, through;
        CheckBox active, done;
        TextBox search;
        ListView records;
        Label count;
        List<Entry> current = new List<Entry>();
        public string SelectedId = "";

        public HistoryDialog(Book source)
        {
            book = source; Text = "COAST · 기록 조회"; Size = new Size(950, 560);
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
            active = new CheckBox(); active.Text = "관리중/출항중"; active.Checked = true; active.Width = 125;
            done = new CheckBox(); done.Text = "철수/입항/기존"; done.Checked = true; done.Width = 130;
            buttons.Controls.Add(active); buttons.Controls.Add(done); CoastStyle.Add(filters, buttons, 65);
            count = new Label(); count.ForeColor = CoastStyle.Muted; CoastStyle.Add(filters, count, 23);
            records = RecordTable.Create(); records.Dock = DockStyle.Fill;
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
            current = Data.SelectEntries(book, from.Value, through.Value, active.Checked, done.Checked, search.Text);
            RecordTable.Fill(records, current, "");
            count.Text = current.Count + "건 · 상태 모두 해제 시 전체 · 출항/식별 날짜 기준";
        }
        void Export(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date) { count.Text = "조회 기간을 확인하세요."; return; }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 파일|*.csv"; dialog.DefaultExt = "csv"; dialog.AddExtension = true;
                dialog.FileName = "COAST_" + DateTime.Today.ToString("yyyyMMdd") + "_조회.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { Data.ExportEntries(book, current, dialog.FileName); count.Text = "조회 결과 CSV 저장 완료: " + dialog.FileName; }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "CSV 저장 확인"); }
            }
        }
        void ShowStats(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date) { count.Text = "조회 기간을 확인하세요."; return; }
            using (TextDialog dialog = new TextDialog("유형별 집계", Data.Statistics(current), true)) dialog.ShowDialog(this);
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
        ComboBox place, camera, vesselType;
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
            CoastStyle.Add(card, CoastStyle.Field(entry.Category == "Vessel" ? "항구" : "지형지물 · 생략 가능", place), 49);
            if (entry.Category == "Vessel")
            {
                vesselType = new ComboBox(); foreach (string type in Data.Lines(settings.VesselTypes)) vesselType.Items.Add(type); vesselType.Text = entry.VesselType;
                name = new TextBox(); name.Text = entry.VesselName; number = new TextBox(); number.Text = entry.VesselNumber;
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

    public class ConfigDialog : Form
    {
        public Settings Value;
        TextBox name, csv, photos, kinds, alarms, cameras, places, vesselTypes, harbors;
        CheckBox copy, success;
        Settings previous;

        public ConfigDialog(Settings current)
        {
            previous = current; Text = "COAST · 환경설정"; Width = 760; Height = 740;
            MinimumSize = new Size(720, 500); StartPosition = FormStartPosition.CenterParent; CoastStyle.Form(this);
            Panel scroll = new Panel(); scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; Controls.Add(scroll);
            TableLayoutPanel grid = new TableLayoutPanel(); grid.ColumnCount = 2; grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.AutoSize = true; grid.Dock = DockStyle.Top; grid.Padding = new Padding(10); scroll.Controls.Add(grid);
            FlowLayoutPanel left = SettingsColumn(), right = SettingsColumn(); grid.Controls.Add(left, 0, 0); grid.Controls.Add(right, 1, 0);
            name = Field(left, "구간 명 (예: 4구간)", current.Name, 25);
            csv = FolderField(left, "CSV 출력 폴더", current.CsvFolder);
            photos = FolderField(left, "사진 할당 폴더", current.PhotoFolder);
            List<string> lines = new List<string>(); foreach (Kind kind in current.Kinds) lines.Add(kind.Name + "|" + kind.Unit);
            kinds = Field(left, "식별유형: 이름|단위 (/로 하위항목 구분)", String.Join("\r\n", lines.ToArray()), 150);
            vesselTypes = Field(left, "선박유형: 한 줄에 하나", current.VesselTypes, 90);
            cameras = Field(right, "카메라|지형지물1,지형지물2 (표시 순서 유지)", current.Cameras, 130);
            places = Field(right, "공통 지형지물 (이전 설정 호환, 한 줄에 하나)", current.Places, 55);
            harbors = Field(right, "항구: 북 → 남 순서로 한 줄에 하나", current.Harbors, 75);
            alarms = Field(right, "알림 일정: HH:mm|업무명 (빈칸이면 없음)", current.Alarms, 100);
            FlowLayoutPanel footer = SettingsColumn(); footer.Width = 680; grid.Controls.Add(footer, 0, 1); grid.SetColumnSpan(footer, 2);
            copy = new CheckBox(); copy.Text = "저장 후 보고문 복사"; copy.Checked = current.CopyText; copy.Width = 330; footer.Controls.Add(copy);
            success = new CheckBox(); success.Text = "저장 결과 팝업 표시"; success.Checked = current.ShowSuccess; success.Width = 330; footer.Controls.Add(success);
            Label notice = new Label(); notice.Width = 680; notice.Height = 57; notice.ForeColor = CoastStyle.Muted;
            notice.Text = "사진: 한 장은 번호 없이, 여러 장은 (1), (2)로 표시합니다. 할당 창에서 실제 파일명을 미리 볼 수 있습니다.\r\n"
                + "유형 이름·단위를 바꾸면 새 유형으로 등록되며 과거 기록은 유지됩니다.\r\nCSV와 사진은 별도 출력 폴더이며 원본 XML은 프로그램의 Data 폴더에 있습니다.";
            footer.Controls.Add(notice); Button save = CoastStyle.Button("설정 저장", Save, true); save.Width = 680; save.Height = 36; footer.Controls.Add(save);
        }
        FlowLayoutPanel SettingsColumn()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel(); panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false; panel.AutoSize = true; panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Width = 340; panel.Dock = DockStyle.Top; return panel;
        }

        TextBox Field(Control panel, string label, string text, int height)
        {
            Label title = new Label(); title.Text = label; title.Width = 330; title.Height = 32;
            panel.Controls.Add(title);
            TextBox box = new TextBox(); box.Text = text; box.Width = 330;
            box.Multiline = height > 30; box.Height = height;
            if (box.Multiline) box.ScrollBars = ScrollBars.Vertical;
            CoastStyle.InputControl(box); panel.Controls.Add(box); return box;
        }

        TextBox FolderField(Control panel, string label, string text)
        {
            Label title = new Label(); title.Text = label; title.Width = 330; title.Height = 32;
            panel.Controls.Add(title);
            FlowLayoutPanel row = new FlowLayoutPanel(); row.Width = 330; row.Height = 36;
            row.WrapContents = false; panel.Controls.Add(row);
            TextBox box = new TextBox(); box.Width = 230; box.Text = text; CoastStyle.InputControl(box); row.Controls.Add(box);
            Button browse = new Button(); browse.Text = "찾아보기"; browse.Width = 85; browse.Height = 27;
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = label + "를 선택하세요.";
                    dialog.ShowNewFolderButton = true;
                    if (Directory.Exists(box.Text.Trim())) dialog.SelectedPath = box.Text.Trim();
                    if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
                }
            };
            row.Controls.Add(browse); return box;
        }

        public static List<string[]> ParseAlarms(string text)
        {
            List<string[]> result = new List<string[]>();
            HashSet<string> unique = new HashSet<string>();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim(); if (line.Length == 0) continue;
                string[] parts = line.Split(new char[] { '|' }, 2);
                DateTime parsed;
                if (parts.Length != 2 || parts[1].Trim().Length == 0
                    || !DateTime.TryParseExact(parts[0].Trim(), "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out parsed) || !unique.Add(line))
                    throw new FormatException("일정 형식은 HH:mm|일정 이름 입니다. 같은 줄을 중복하지 마세요.");
                result.Add(new string[] { parts[0].Trim(), parts[1].Trim() });
            }
            return result;
        }

        public static void Validate(Settings settings)
        {
            if (settings == null || !settings.Configured || String.IsNullOrWhiteSpace(settings.Name)
                || String.IsNullOrWhiteSpace(settings.CsvFolder) || !Path.IsPathRooted(settings.CsvFolder)
                || String.IsNullOrWhiteSpace(settings.PhotoFolder) || !Path.IsPathRooted(settings.PhotoFolder)
                || settings.Kinds == null || settings.Kinds.Count == 0 || settings.Kinds.Count > 50)
                throw new FormatException("이름, 출력 폴더와 1~50개의 항목을 설정하세요.");
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Kind kind in settings.Kinds)
            {
                if (kind == null || String.IsNullOrWhiteSpace(kind.Name) || String.IsNullOrWhiteSpace(kind.Id)
                    || !ids.Add(kind.Id) || !names.Add(kind.Name) || String.IsNullOrWhiteSpace(kind.Unit))
                    throw new FormatException("유형 이름, 단위와 ID를 확인하세요. 유형 이름은 중복할 수 없습니다.");
                HashSet<string> children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string child in kind.Choices())
                    if (child.Length == 0 || !children.Add(child))
                        throw new FormatException("/ 사이의 하위항목은 비어 있거나 중복될 수 없습니다.");
            }
            ParseAlarms(settings.Alarms); Data.Cameras(settings);
        }

        void Save(object sender, EventArgs e)
        {
            try
            {
                Settings settings = new Settings(); settings.Configured = true;
                settings.Name = name.Text.Trim(); settings.CsvFolder = csv.Text.Trim();
                settings.PhotoFolder = photos.Text.Trim(); settings.CopyText = copy.Checked;
                settings.ShowSuccess = success.Checked; settings.Alarms = alarms.Text;
                settings.Cameras = cameras.Text; settings.Places = places.Text; settings.Harbors = harbors.Text;
                settings.VesselTypes = vesselTypes.Text;
                foreach (string raw in kinds.Text.Split('\n'))
                {
                    string line = raw.Trim(); if (line.Length == 0) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length != 2 || parts[0].Trim().Length == 0 || parts[1].Trim().Length == 0)
                        throw new FormatException("항목은 한 줄에 이름|단위로 입력하세요.");
                    Kind kind = new Kind(); kind.Name = parts[0].Trim(); kind.Unit = parts[1].Trim();
                    kind.Id = Guid.NewGuid().ToString("N");
                    foreach (Kind old in previous.Kinds)
                        if (old.Name == kind.Name && old.Unit == kind.Unit) kind.Id = old.Id;
                    settings.Kinds.Add(kind);
                }
                Validate(settings);
                Value = settings; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "설정 확인"); }
        }
    }
}
