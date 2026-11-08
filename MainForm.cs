using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CoastNotebook
{
    public class MainForm : Form
    {
        readonly string dataPath;
        Book book, undoBefore;
        string editing = "", selected = "", exportError = "";
        bool dirty, boatDirty, loading, busy, closing, filtering;
        DateTime promptedDate;
        readonly System.ComponentModel.IContainer components = new System.ComponentModel.Container();
        Panel editor;
        FlowLayoutPanel page;
        TableLayoutPanel targetCard;
        ContextMenuStrip toolsMenu, recordMenu;
        int peopleHeight, boatHeight;
        DateTimePicker date, boatTime, targetDay, boatDay;
        ComboBox camera, title, kind, harbor, vesselType;
        NumericUpDown quantity;
        TextBox azimuth, elevation, note, vesselName, vesselNumber;
        FlowLayoutPanel cart;
        List<Item> cartItems = new List<Item>();
        Button saveButton, cancelEdit, undoButton, peopleHeader, boatHeader;
        Label status, targetTitle;
        DataGridView peopleGrid, boatGrid;
        CheckBox peopleActive, peopleDone, peopleAll, boatActive, boatDone, boatAll;
        Panel peoplePanel, boatPanel;
        int cartRow, noteRow;
        bool peopleClosed, boatsClosed;
        Timer clock;
        ToolTip tips;
        Panel arrivalEditor;
        FlowLayoutPanel arrivalChoices;
        DateTimePicker arrivalTime, arrivalDay;
        TextBox arrivalManual;
        Button arrivalConfirm;
        string arrivalId = "";
        bool positioningArrival;
        class ArrivalSlot { public string Id; }
        const int ArrivalHeight = 94;

        public MainForm(string folder)
        {
            tips = new ToolTip(components); toolsMenu = new ContextMenuStrip(components); recordMenu = new ContextMenuStrip(components);
            dataPath = Path.Combine(folder, "Records.xml"); book = Data.Load(dataPath);
            if (book.Settings.Kinds == null) book.Settings.Kinds = new List<Kind>();
            if (!book.Settings.Configured)
            {
                book.Settings.CsvFolder = Path.Combine(folder, "CSV");
                book.Settings.PhotoFolder = Path.Combine(folder, "Photos");
                book.Settings.ShowSuccess = false;
                string[] names = { "낚시꾼", "산책", "채집/해루질", "텐트/모닥불", "수영/레저", "차량", "기타" };
                string[] units = { "명", "명", "명", "동", "명", "대", "건" };
                for (int i = 0; i < names.Length; i++) book.Settings.Kinds.Add(new Kind {
                    Id = Guid.NewGuid().ToString("N"), Name = names[i], Unit = units[i] });
            }
            Text = "COAST"; CoastStyle.Form(this);
            StartPosition = FormStartPosition.Manual;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Bounds = new Rectangle(area.Right - 570, area.Top, 570, area.Height);
            MinimumSize = new Size(420, Math.Min(600, area.Height));
            TableLayoutPanel root = new TableLayoutPanel(); root.Dock = DockStyle.Fill;
            root.ColumnCount = 1; root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowCount = 3; root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            Controls.Add(root);
            Label brand = new Label(); brand.Text = "COAST · " + book.Settings.Name;
            brand.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold); brand.TextAlign = ContentAlignment.MiddleLeft;
            brand.Name = "brand";
            Button settings = CoastStyle.Button("설정 ⋯", ShowTools);
            root.Controls.Add(CoastStyle.Row(new Control[] { brand, settings }, 78, 22), 0, 0);
            root.GetControlFromPosition(0, 0).Dock = DockStyle.Fill;
            page = new FlowLayoutPanel(); page.Dock = DockStyle.Fill; page.FlowDirection = FlowDirection.TopDown;
            page.WrapContents = false; page.AutoScroll = true; page.Padding = new Padding(1);
            editor = page; root.Controls.Add(page, 0, 1);
            TableLayoutPanel boatCard = BuildBoatCard(); targetCard = BuildTargetCard();
            peoplePanel = BuildList(false); boatPanel = BuildList(true);
            peopleHeight = boatHeight = Math.Max(145, (ClientSize.Height - 84 - 415) / 2);
            peoplePanel.Height = peopleHeight; boatPanel.Height = boatHeight;
            foreach (Control control in new Control[] { targetCard, peoplePanel, boatCard, boatPanel })
            { control.Dock = DockStyle.None; control.Margin = new Padding(5, 3, 5, 3); page.Controls.Add(control); }
            page.SizeChanged += delegate { SizePage(); };
            SizePage(); BuildArrivalEditor();
            status = new Label(); status.ForeColor = CoastStyle.Muted; status.TextAlign = ContentAlignment.MiddleLeft;
            undoButton = CoastStyle.Button("실행 취소", UndoLast); undoButton.Enabled = false;
            Control footer = CoastStyle.Row(new Control[] { status, undoButton }, 79, 21); footer.Dock = DockStyle.Fill;
            root.Controls.Add(footer, 0, 2);
            FormClosing += ConfirmClosing; Shown += Started;
            clock = new Timer(components); clock.Interval = 15000; clock.Tick += Tick;

            LoadKinds(); ResetEditor(); ResetBoat();
        }

        protected override void Dispose(bool disposing)
        { if (disposing) components.Dispose(); base.Dispose(disposing); }
        void SizePage()
        {
            int width = Math.Max(350, page.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (Control control in page.Controls)
            {
                control.Width = width;
                if (control is TableLayoutPanel) { control.MinimumSize = new Size(width, 0); control.MaximumSize = new Size(width, 0); }
            }
        }
        DateTimePicker DayInput()
        {
            DateTimePicker picker = new DateTimePicker(); picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "yyyy-MM-dd"; picker.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            picker.CalendarForeColor = Color.Black; picker.ForeColor = Color.Black; return picker;
        }
        static DateTime At(DateTimePicker day, DateTimePicker time) { return day.Value.Date.Add(time.Value.TimeOfDay); }
        static DateTime MinuteNow() { DateTime now = DateTime.Now; return now.Date.AddHours(now.Hour).AddMinutes(now.Minute); }
        ComboBox Choice()
        { ComboBox box = new ComboBox(); CoastStyle.InputControl(box); box.FlatStyle = FlatStyle.Flat; return box; }
        DateTimePicker TimeInput()
        { DateTimePicker picker = new DateTimePicker(); picker.Format = DateTimePickerFormat.Custom; picker.CustomFormat = "HH:mm"; picker.ShowUpDown = true; return picker; }
        Label Heading(string text)
        { Label label = new Label(); label.Text = text; label.TextAlign = ContentAlignment.MiddleLeft; label.Font = new Font(Font, FontStyle.Bold); return label; }
        TableLayoutPanel BuildTargetCard()
        {
            TableLayoutPanel card = CoastStyle.Card();
            targetTitle = Heading("식별표적 입력"); cancelEdit = CoastStyle.Button("수정 취소", NewEntry); cancelEdit.Visible = false;
            Button history = CoastStyle.Button("조회", delegate { OpenHistory(false); }); targetDay = DayInput();
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { targetTitle, history, targetDay, cancelEdit }, 31, 13, 34, 22), 38);
            date = TimeInput(); camera = Choice(); title = Choice();
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("시간", date),
                CoastStyle.Field("카메라", camera), CoastStyle.Field("지형지물 · 생략 가능", title) }, 26, 35, 39), 48);
            kind = Choice(); kind.DropDownStyle = ComboBoxStyle.DropDownList;
            azimuth = new TextBox(); elevation = new TextBox();
            tips.SetToolTip(azimuth, "방위각: 0~6400 정수, 빈칸이면 미입력");
            tips.SetToolTip(elevation, "고각: -180~180 정수, 빈칸이면 미입력");
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("식별유형", kind),
                CoastStyle.Field("P · 선택", azimuth), CoastStyle.Field("T · 선택", elevation) }, 62, 19, 19), 48);
            quantity = new NumericUpDown(); quantity.Maximum = 999; quantity.Minimum = 0; quantity.Value = 1;
            saveButton = CoastStyle.Button("식별", SaveEntry, true);
            Button add = CoastStyle.Button("함께 추가", AddItem);
            Button detail = CoastStyle.Button("＋ 비고", delegate { note.Visible = !note.Visible; targetCard.RowStyles[noteRow].Height = note.Visible ? 31 : 0; });
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("수량", quantity), saveButton, add, detail }, 18, 36, 25, 21), 45);
            cart = new FlowLayoutPanel(); cart.AutoScroll = true; cart.WrapContents = true;
            cart.Visible = false;
            cartRow = CoastStyle.Add(card, cart, 0);
            note = new TextBox(); note.Visible = false; CoastStyle.InputControl(note); noteRow = CoastStyle.Add(card, note, 0);
            tips.SetToolTip(note, "비고 · 특이사항 (선택)");
            camera.SelectedIndexChanged += delegate { if (!loading) LoadLocations(false); };
            camera.Leave += delegate { if (!loading) LoadLocations(true); };
            foreach (Control control in new Control[] { camera, title, azimuth, elevation, note }) control.TextChanged += MarkDirty;
            date.ValueChanged += MarkDirty; quantity.ValueChanged += MarkDirty;
            kind.SelectedIndexChanged += MarkDirty; targetDay.ValueChanged += MarkDirty;
            return card;
        }
        TableLayoutPanel BuildBoatCard()
        {
            TableLayoutPanel card = CoastStyle.Card();
            boatDay = DayInput(); Button history = CoastStyle.Button("조회", delegate { OpenHistory(true); });
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { Heading("선박 입출항"), history, boatDay }, 42, 14, 44), 38);
            boatTime = TimeInput(); harbor = Choice(); vesselType = Choice();
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("시간", boatTime),
                CoastStyle.Field("항구 · 북 → 남", harbor), CoastStyle.Field("선박유형", vesselType) }, 26, 35, 39), 48);
            vesselName = new TextBox(); vesselNumber = new TextBox();
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { CoastStyle.Field("선명 · 선택", vesselName),
                CoastStyle.Field("식별번호 · 선택", vesselNumber) }, 50, 50), 48);
            Button depart = CoastStyle.Button("출항", delegate { RegisterBoat(false); }); depart.BackColor = Color.FromArgb(34, 94, 145);
            Button arrive = CoastStyle.Button("입항", delegate { RegisterBoat(true); });
            CoastStyle.Add(card, CoastStyle.Row(new Control[] { depart, arrive }, 50, 50), 34);
            foreach (Control control in new Control[] { harbor, vesselType, vesselName, vesselNumber })
                control.TextChanged += delegate { if (!loading) boatDirty = true; };
            boatDay.ValueChanged += delegate { if (!loading) boatDirty = true; };
            boatTime.ValueChanged += delegate { if (!loading) boatDirty = true; };
            return card;
        }
        Panel BuildList(bool vessel)
        {
            Panel panel = new Panel(); panel.Margin = new Padding(5, 3, 5, 3);
            Button header = CoastStyle.Button(vessel ? "▾ 선박" : "▾ 식별표적", delegate {
                if (vessel) boatsClosed = !boatsClosed; else peopleClosed = !peopleClosed;
                UpdateCollapsed();
            }); header.Dock = DockStyle.Top; header.Height = 32; header.TextAlign = ContentAlignment.MiddleLeft;
            FlowLayoutPanel filters = new FlowLayoutPanel(); filters.Dock = DockStyle.Top; filters.Height = 28;
            CheckBox active = Filter(vessel ? "출항중" : "관리중", true);
            CheckBox done = Filter(vessel ? "입항/기존" : "철수", false);
            CheckBox all = Filter("전체", false);
            filters.Controls.Add(active); filters.Controls.Add(done); filters.Controls.Add(all);
            if (vessel) { boatActive = active; boatDone = done; boatAll = all; boatHeader = header; }
            else { peopleActive = active; peopleDone = done; peopleAll = all; peopleHeader = header; }
            EventHandler changed = delegate(object sender, EventArgs e) { NormalizeFilters((CheckBox)sender, active, done, all); };
            active.CheckedChanged += changed; done.CheckedChanged += changed; all.CheckedChanged += changed;
            DataGridView grid = CoastStyle.Grid();
            string[] names = vessel ? new string[] { "시간", "식별번호", "선박유형", "선명", "상태", "입항항" }
                : new string[] { "식별/상태", "지형지물 · P/T", "유형 / 수량", "철수시간", "처리", "" };
            for (int i = 0; i < names.Length; i++)
            {
                string key = vessel ? "c" + i : i == names.Length - 2 ? "action" : i == names.Length - 1 ? "menu" : "c" + i;
                grid.Columns.Add(key, names[i]); grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
                grid.Columns[i].FillWeight = key == "action" ? 45 : key == "menu" ? 26 : vessel ? 75 : i == 1 || i == 2 ? 120 : 70;
                grid.Columns[i].MinimumWidth = key == "menu" ? 25 : key == "action" ? 40 : 45;
            }
            grid.CellClick += GridClick; grid.CellMouseDown += GridMouseDown;
            grid.RowPrePaint += delegate(object sender, DataGridViewRowPrePaintEventArgs e) {
                string group = grid.Rows[e.RowIndex].Tag as string; if (group == null) return;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(36, 51, 66))) e.Graphics.FillRectangle(brush, e.RowBounds);
                Rectangle bounds = e.RowBounds; bounds.X += 6;
                TextRenderer.DrawText(e.Graphics, group, Font, bounds, Color.LightBlue,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
            };
            if (vessel)
            {
                boatGrid = grid; grid.Scroll += delegate { PositionArrivalEditor(); };
                grid.SizeChanged += delegate { PositionArrivalEditor(); };
                grid.ColumnWidthChanged += delegate { PositionArrivalEditor(); };
            }
            else peopleGrid = grid;
            Panel body = new Panel(); body.Dock = DockStyle.Fill; body.Name = "body";
            body.Controls.Add(grid); body.Controls.Add(filters); panel.Controls.Add(body); panel.Controls.Add(header);
            Panel grip = new Panel(); grip.Name = vessel ? "boatResize" : "peopleResize";
            grip.Dock = DockStyle.Bottom; grip.Height = 14; grip.Cursor = Cursors.HSplit; grip.BackColor = CoastStyle.Line;
            grip.Paint += delegate(object sender, PaintEventArgs e) { TextRenderer.DrawText(e.Graphics, "⋮ 끌어서 목록 높이 조절", Font, grip.ClientRectangle,
                CoastStyle.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); };
            int startY = 0, startHeight = 0;
            grip.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button != MouseButtons.Left) return;
                startY = grip.PointToScreen(e.Location).Y; startHeight = panel.Height; grip.Capture = true; };
            grip.MouseMove += delegate(object sender, MouseEventArgs e) { if (grip.Capture) ResizeList(vessel, startHeight + grip.PointToScreen(e.Location).Y - startY); };
            grip.MouseUp += delegate { grip.Capture = false; };
            panel.Controls.Add(grip); grip.BringToFront();
            return panel;
        }
        CheckBox Filter(string text, bool value)
        { CheckBox box = new CheckBox(); box.Text = text; box.Checked = value; box.AutoSize = true; box.Margin = new Padding(5, 4, 5, 1); return box; }
        void NormalizeFilters(CheckBox clicked, CheckBox active, CheckBox done, CheckBox all)
        {
            if (filtering || loading) return; filtering = true;
            if (clicked.Checked) { if (clicked == all) { active.Checked = false; done.Checked = false; } else all.Checked = false; }
            if ((!active.Checked && !done.Checked) || (active.Checked && done.Checked))
            { active.Checked = false; done.Checked = false; all.Checked = true; }
            filtering = false; RefreshRecords();
        }
        void ResizeList(bool vessel, int height)
        {
            height = Math.Max(110, Math.Min(1000, height));
            if (vessel) { boatHeight = height; if (!boatsClosed) boatPanel.Height = height; }
            else { peopleHeight = height; if (!peopleClosed) peoplePanel.Height = height; }
            PositionArrivalEditor();
        }
        void UpdateCollapsed()
        {
            peoplePanel.Controls["body"].Visible = !peopleClosed; boatPanel.Controls["body"].Visible = !boatsClosed;
            peoplePanel.Controls["peopleResize"].Visible = !peopleClosed; boatPanel.Controls["boatResize"].Visible = !boatsClosed;
            peoplePanel.Height = peopleClosed ? 35 : peopleHeight; boatPanel.Height = boatsClosed ? 35 : boatHeight;
            RefreshRecords();
        }
        void MarkDirty(object sender, EventArgs e) { if (!loading) dirty = true; }
        void Error(Exception ex) { MessageBox.Show(this, ex.Message, "작업 결과 확인"); }
        bool Discard() { return !dirty || MessageBox.Show(this, "저장하지 않은 표적 입력을 버립니까?", "입력 확인", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes; }
        void ShowTools(object sender, EventArgs e)
        {
            if (toolsMenu.Items.Count == 0)
            {
                toolsMenu.Items.Add("환경설정", null, Configure); toolsMenu.Items.Add("CSV 재시도", null, RetryCsv);
                toolsMenu.Items.Add("식별표적 CSV", null, delegate { OpenCsv(false); });
                toolsMenu.Items.Add("선박 CSV", null, delegate { OpenCsv(true); });
                toolsMenu.Items.Add("도움말 편집", null, EditHelp); toolsMenu.Items.Add("날짜 확인", null, delegate { EnsureDay(true); });
            }
            toolsMenu.Show((Control)sender, new Point(0, ((Control)sender).Height));
        }
        void Started(object sender, EventArgs e)
        {
            try
            {
                try { ConfigDialog.Validate(book.Settings); }
                catch { if (!ConfigureCore()) { closing = true; Close(); return; } }
                LoadKinds(); UpdateBrand(); RefreshRecords(); EnsureDay(true); SyncCsv(); clock.Start();
            }
            catch (Exception ex) { Error(ex); closing = true; Close(); }
        }
        void UpdateBrand()
        {
            Text = "COAST · " + book.Settings.Name;
            foreach (Control root in Controls) foreach (Control row in root.Controls)
                if (row is TableLayoutPanel) foreach (Control child in row.Controls)
                    if (child.Name == "brand") child.Text = "COAST · " + book.Settings.Name;
        }
        void FillChoices(ComboBox box, List<string> choices)
        {
            string text = box.Text; box.Items.Clear(); foreach (string choice in choices) box.Items.Add(choice);
            box.Text = text; if (box.Text.Length == 0 && box.Items.Count > 0) box.SelectedIndex = 0;
        }
        void LoadKinds()
        {
            bool wasLoading = loading; loading = true;
            List<string> cameras = new List<string>(); foreach (CameraSpec spec in Data.Cameras(book.Settings)) cameras.Add(spec.Name);
            FillChoices(camera, cameras); LoadLocations(true);
            KindChoice previous = kind.SelectedItem as KindChoice;
            kind.Items.Clear();
            foreach (Kind value in book.Settings.Kinds)
                foreach (string child in value.Choices()) kind.Items.Add(new KindChoice { Kind = value, SubName = value.Choices().Length > 1 ? child : "" });
            if (previous != null) for (int i = 0; i < kind.Items.Count; i++)
            { KindChoice choice = (KindChoice)kind.Items[i]; if (choice.Kind.Id == previous.Kind.Id && choice.SubName == previous.SubName) kind.SelectedIndex = i; }
            if (kind.SelectedIndex < 0 && kind.Items.Count > 0) kind.SelectedIndex = 0;
            FillChoices(vesselType, Data.Lines(book.Settings.VesselTypes));
            List<string> ports = Data.Lines(book.Settings.Harbors);
            foreach (Entry entry in book.Entries) if (entry.Category == "Vessel" && !ports.Contains(entry.Title)) ports.Add(entry.Title);
            FillChoices(harbor, ports); loading = wasLoading;
        }
        void LoadLocations(bool preserve)
        {
            bool wasLoading = loading; loading = true; string text = title.Text;
            List<string> places = Data.Lines(book.Settings.Places);
            foreach (CameraSpec spec in Data.Cameras(book.Settings)) if (spec.Name == camera.Text) { places = spec.Places; break; }
            title.Items.Clear(); title.Items.Add("지형지물 생략"); foreach (string place in places) title.Items.Add(place);
            if (preserve && text.Length > 0) title.Text = text;
            else title.SelectedIndex = title.Items.Count > 1 ? 1 : 0;
            loading = wasLoading;
        }
        Item ReadItem()
        {
            KindChoice choice = kind.SelectedItem as KindChoice;
            if (choice == null) throw new FormatException("식별유형을 선택하세요.");
            return new Item { KindId = choice.Kind.Id, Name = choice.Kind.Name, Unit = choice.Kind.Unit,
                SubName = choice.SubName, Count = (int)quantity.Value };
        }
        void AddItem(object sender, EventArgs e)
        {
            try
            {
                Item value = ReadItem(); Item found = cartItems.Find(delegate(Item item) { return item.KindId == value.KindId && item.SubName == value.SubName; });
                if (found == null) cartItems.Add(value);
                else { if (found.Count + value.Count > 999) throw new FormatException("같은 하위항목의 합산 수량은 999 이하여야 합니다."); found.Count += value.Count; }
                dirty = true; RenderCart();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void RenderCart()
        {
            cart.Controls.Clear();
            foreach (Item item in cartItems)
            {
                Item captured = item;
                Button chip = CoastStyle.Button(item.ToString() + " ×", delegate { cartItems.Remove(captured); dirty = true; RenderCart(); });
                chip.AutoSize = true; chip.Height = 28; chip.ForeColor = CoastStyle.Green;
                tips.SetToolTip(chip, item.Name + " / 클릭하면 제외"); cart.Controls.Add(chip);
            }
            cart.Visible = cartItems.Count > 0;
            if (targetCard != null) targetCard.RowStyles[cartRow].Height = cartItems.Count == 0 ? 0 : 56;
            Entry total = new Entry(); total.Items = cartItems; tips.SetToolTip(cart, "묶음 합계: " + total.Totals());
        }
        void NewEntry(object sender, EventArgs e) { if (Discard()) ResetEditor(); }
        void ResetEditor()
        {
            loading = true; editing = ""; targetDay.Value = DateTime.Today; date.Value = MinuteNow(); note.Text = ""; azimuth.Text = ""; elevation.Text = "";
            quantity.Value = 1; cartItems.Clear(); RenderCart(); saveButton.Text = "식별";
            targetTitle.Text = "식별표적 입력"; cancelEdit.Visible = false; note.Visible = false; targetCard.RowStyles[noteRow].Height = 0;
            TableLayoutPanel heading = (TableLayoutPanel)targetCard.GetControlFromPosition(0, 0);
            heading.ColumnStyles[3].SizeType = SizeType.Absolute; heading.ColumnStyles[3].Width = 0;
            loading = false; dirty = false;
        }
        void ResetBoat()
        { bool before = loading; loading = true; boatDay.Value = DateTime.Today; boatTime.Value = MinuteNow(); vesselName.Text = ""; vesselNumber.Text = ""; loading = before; boatDirty = false; }
        Entry Find(Book source, string id) { return source.Entries.Find(delegate(Entry entry) { return entry.Id == id; }); }
        void Persist(Book next) { Data.Save(dataPath, next); book = next; }
        bool StoreEntry(Entry value, string message)
        {
            try
            {
                Book next = Data.Clone(book); Entry old = Find(next, value.Id);
                if (old != null) { Data.Queue(next, old.Time); next.Entries.Remove(old); }
                next.Entries.Add(value); Data.Queue(next, value.Time);
                Book before = Data.Clone(book); Persist(next); undoBefore = before; undoButton.Enabled = true;
                selected = value.Id; if (value.Id == arrivalId) CloseArrival(); SyncCsv(); RefreshRecords(); status.Text = message + (exportError.Length == 0 ? " · CSV 정상" : " · CSV 갱신 대기"); return true;
            }
            catch (Exception ex) { status.Text = "원본 저장 실패 · 입력 유지"; Error(ex); return false; }
        }
        void SaveEntry(object sender, EventArgs e)
        {
            if (!EnsureDay(true)) return;
            try
            {
                if (At(targetDay, date) > DateTime.Now) throw new FormatException("미래 시각은 입력할 수 없습니다.");
                Entry original = Find(book, editing);
                if (String.IsNullOrWhiteSpace(camera.Text) && (original == null || original.Category != "Manual")) throw new FormatException("카메라를 선택하거나 입력하세요.");
                Entry value = original == null ? new Entry { Id = Guid.NewGuid().ToString("N"), Category = "Target", Section = book.Settings.Name }
                    : Find(Data.Clone(book), editing);
                value.Time = At(targetDay, date); value.Camera = camera.Text.Trim();
                value.Title = title.Text == "지형지물 생략" ? "" : title.Text.Trim(); value.Note = note.Text;
                value.Azimuth = Data.OptionalAngle(azimuth.Text, 0, 6400); value.Elevation = Data.OptionalAngle(elevation.Text, -180, 180);
                if (value.FinishedAt.HasValue && value.FinishedAt < value.Time) throw new FormatException("기록 시각이 종료 시각보다 늦습니다.");
                value.Items.Clear();
                if (cartItems.Count == 0) value.Items.Add(ReadItem());
                else foreach (Item item in cartItems) value.Items.Add(new Item { KindId = item.KindId, Name = item.Name, Unit = item.Unit, SubName = item.SubName, Count = item.Count });
                if (!StoreEntry(value, original == null ? "식별 기록 저장 완료" : "기록 수정 완료")) return;
                ResetEditor(); string extra = "";
                if (value.Photos.Count > 0 && MessageBox.Show(this, "저장되었습니다. 연결 사진의 이름도 갱신합니까?", "사진명 확인", MessageBoxButtons.YesNo) == DialogResult.Yes) extra = RenameCore(value.Id);
                if (book.Settings.CopyText) CopyReport(value);
                if (book.Settings.ShowSuccess || extra.Length > 0) MessageBox.Show(this, status.Text + extra, "저장 결과");
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void RegisterBoat(bool arrival)
        {
            if (!EnsureDay(true)) return;
            if (At(boatDay, boatTime) > DateTime.Now || String.IsNullOrWhiteSpace(harbor.Text) || String.IsNullOrWhiteSpace(vesselType.Text))
            { status.Text = "선박 시각, 항구와 선박유형을 확인하세요."; return; }
            Entry value = new Entry { Id = Guid.NewGuid().ToString("N"), Time = At(boatDay, boatTime), Title = harbor.Text.Trim(),
                Category = "Vessel", Section = book.Settings.Name, VesselType = vesselType.Text.Trim(), VesselName = vesselName.Text.Trim(),
                VesselNumber = vesselNumber.Text.Trim(), Movement = arrival ? "입항" : "출항", Complete = arrival,
                FinishedAt = arrival ? (DateTime?)At(boatDay, boatTime) : null, ArrivalHarbor = arrival ? harbor.Text.Trim() : "" };
            if (StoreEntry(value, arrival ? "단독 입항 기록 저장 완료" : "출항 기록 저장 완료")) { ResetBoat(); if (book.Settings.CopyText) CopyReport(value); }
        }
        void LoadEntry(Entry entry)
        {
            if (entry.Category == "Vessel") return;
            loading = true; editing = entry.Id; selected = entry.Id; targetDay.Value = entry.Time.Date; date.Value = entry.Time;
            camera.Text = entry.Camera; LoadLocations(false); title.Text = entry.Title.Length == 0 ? "지형지물 생략" : entry.Title;
            azimuth.Text = entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString(CultureInfo.InvariantCulture) : "";
            elevation.Text = entry.Elevation.HasValue ? entry.Elevation.Value.ToString(CultureInfo.InvariantCulture) : "";
            note.Text = entry.Note; cartItems.Clear();
            foreach (Item item in entry.Items) cartItems.Add(new Item { KindId = item.KindId, Name = item.Name, Unit = item.Unit, SubName = item.SubName, Count = item.Count });
            RenderCart(); note.Visible = entry.Note.Length > 0; targetCard.RowStyles[noteRow].Height = entry.Note.Length == 0 ? 0 : 31;
            saveButton.Text = "저장"; targetTitle.Text = "식별표적 수정"; cancelEdit.Visible = true;
            TableLayoutPanel heading = (TableLayoutPanel)targetCard.GetControlFromPosition(0, 0);
            heading.ColumnStyles[3].SizeType = SizeType.Percent; heading.ColumnStyles[3].Width = 22;
            loading = false; dirty = false;
        }
        void RefreshRecords()
        {
            if (peopleGrid == null || boatGrid == null) return;
            FillGrid(peopleGrid, false, peopleAll.Checked || peopleActive.Checked, peopleAll.Checked || peopleDone.Checked);
            FillGrid(boatGrid, true, boatAll.Checked || boatActive.Checked, boatAll.Checked || boatDone.Checked);
            int people = 0, boats = 0, arrivals = 0; SortedDictionary<string, int> counts = new SortedDictionary<string, int>();
            foreach (Entry entry in book.Entries)
            {
                if (entry.Time.Date != DateTime.Today) continue;
                if (entry.Category == "Vessel") { if (!entry.Complete) boats++; else if (!entry.IndependentMovement) arrivals++; }
                else if (!entry.Complete) { people++; foreach (Item item in entry.Items) { if (!counts.ContainsKey(item.Unit)) counts[item.Unit] = 0; counts[item.Unit] += item.Count; } }
            }
            string sum = ""; foreach (KeyValuePair<string, int> count in counts) sum += " " + count.Value + count.Key;
            peopleHeader.Text = (peopleClosed ? "▸" : "▾") + " 식별표적 · 관리중 " + people + "건   " + sum;
            boatHeader.Text = (boatsClosed ? "▸" : "▾") + " 선박 · 출항중 " + boats + "척   입항 " + arrivals + "건";
        }
        void FillGrid(DataGridView grid, bool vessel, bool active, bool done)
        {
            grid.SuspendLayout(); grid.Rows.Clear();
            List<Entry> entries = Data.SelectEntries(book, DateTime.Today, DateTime.Today, active, done, "");
            List<string> groups = new List<string>();
            if (vessel) groups.AddRange(Data.Lines(book.Settings.Harbors));
            else foreach (CameraSpec spec in Data.Cameras(book.Settings)) groups.Add(spec.Name);
            foreach (Entry entry in entries) if ((entry.Category == "Vessel") == vessel)
            { string group = vessel ? entry.Title : entry.Camera; if (!groups.Contains(group)) groups.Add(group); }
            foreach (string group in groups)
            {
                List<Entry> rows = entries.FindAll(delegate(Entry entry) { return (entry.Category == "Vessel") == vessel && (vessel ? entry.Title : entry.Camera) == group; });
                int groupRow = grid.Rows.Add(); DataGridViewRow heading = grid.Rows[groupRow];
                string groupLabel = "▾ " + (group.Length == 0 ? "기존·카메라 미기재" : group) + " · " + rows.Count + "건";
                // A full-row tooltip keeps long group names readable in narrow columns.
                foreach (DataGridViewCell cell in heading.Cells) cell.ToolTipText = group;
                heading.DefaultCellStyle.BackColor = Color.FromArgb(36, 51, 66); heading.DefaultCellStyle.ForeColor = Color.LightBlue;
                heading.DefaultCellStyle.SelectionBackColor = heading.DefaultCellStyle.BackColor; heading.Tag = groupLabel;
                foreach (Entry entry in rows)
                {
                    int index;
                    if (vessel) index = grid.Rows.Add(entry.Time.ToString("HH:mm") + (entry.FinishedAt.HasValue ? "\n입항 " + entry.FinishedAt.Value.ToString("HH:mm") : ""),
                        entry.VesselNumber, entry.VesselType, entry.VesselName.Length == 0 ? "미기재" : entry.VesselName, entry.StateText(), entry.ArrivalPlace());
                    else index = grid.Rows.Add(entry.Time.ToString("HH:mm") + "\n" + entry.StateText(),
                        (entry.Title.Length == 0 ? "지형지물 생략" : entry.Title) + (entry.Azimuth.HasValue ? "\nP " + entry.Azimuth : "")
                        + (entry.Elevation.HasValue ? " T " + entry.Elevation : ""), entry.Describe(),
                        entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("MM-dd HH:mm") : "—", "", "");
                    DataGridViewRow row = grid.Rows[index]; row.Tag = entry;
                    row.MinimumHeight = vessel && !entry.FinishedAt.HasValue ? 27 : 42;
                    foreach (DataGridViewCell cell in row.Cells) cell.ToolTipText = entry.Report() + "\n" + entry.Note;
                    if (entry.Complete) { row.DefaultCellStyle.ForeColor = CoastStyle.Muted; row.DefaultCellStyle.BackColor = Color.FromArgb(22, 30, 38); }
                    if (entry.Id == selected)
                    {
                        row.Selected = true;
                        if (!vessel && !entry.Complete && !entry.IndependentMovement) row.Cells["action"] = new DataGridViewButtonCell { Value = vessel ? "입항" : "철수", FlatStyle = FlatStyle.Flat };
                        if (!vessel) row.Cells["menu"] = new DataGridViewButtonCell { Value = "⋯", FlatStyle = FlatStyle.Flat };
                    }
                    else row.Selected = false;
                    if (vessel && entry.Id == arrivalId)
                    { int slot = grid.Rows.Add(); grid.Rows[slot].Tag = new ArrivalSlot { Id = entry.Id }; grid.Rows[slot].MinimumHeight = ArrivalHeight + 2; }
                }
            }
            grid.ResumeLayout(); if (vessel) PositionArrivalEditor();
        }
        void GridClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = (DataGridView)sender; if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            Entry entry = grid.Rows[e.RowIndex].Tag as Entry; if (entry == null) return;
            string column = grid.Columns[e.ColumnIndex].Name;
            if (entry.Id == selected && column == "action" && !entry.Complete) { FinishSelected(null, EventArgs.Empty); return; }
            if (entry.Id == selected && column == "menu") { ShowRecordMenu(grid, entry, grid.PointToClient(Cursor.Position)); return; }
            if (entry.Category != "Vessel" && entry.Id != editing)
            { if (!Discard()) return; LoadEntry(entry); }
            if (entry.Category == "Vessel" && arrivalId != entry.Id) CloseArrival();
            selected = entry.Id; RefreshRecords();
        }
        void GridMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            DataGridView grid = (DataGridView)sender; Entry entry = grid.Rows[e.RowIndex].Tag as Entry;
            if (entry != null) { selected = entry.Id; RefreshRecords(); ShowRecordMenu(grid, entry, grid.PointToClient(Cursor.Position)); }
        }
        void ShowRecordMenu(Control owner, Entry entry, Point at)
        {
            selected = entry.Id; recordMenu.Close(); recordMenu.Items.Clear(); ContextMenuStrip menu = recordMenu;
            if (!entry.Complete && !entry.IndependentMovement)
            {
                if (entry.Category == "Vessel")
                {
                    menu.Items.Add("입항 처리", null, delegate { selected = entry.Id; BeginArrival(entry.Id); });
                    menu.Items.Add("빠른 입항", null, delegate { selected = entry.Id; FinishSelected(null, EventArgs.Empty); });
                }
                else menu.Items.Add("철수 처리", null, FinishSelected);
            }
            menu.Items.Add(entry.Category == "Vessel" ? "선박 정보 수정" : "기록 수정", null, delegate {
                if (entry.Category == "Vessel") EditDetails(null, EventArgs.Empty);
                else if (entry.Id == editing || Discard()) { LoadEntry(Find(book, entry.Id)); RefreshRecords(); }
            });
            menu.Items.Add("날짜·상태·비고 수정", null, EditDetails);
            menu.Items.Add("사진할당", null, AttachPhotos); menu.Items.Add("사진명 갱신", null, RenamePhotos);
            menu.Items.Add("보고문 확인", null, delegate { using (TextDialog dialog = new TextDialog("보고문", Find(book, entry.Id).Report(), true)) dialog.ShowDialog(this); });
            menu.Items.Add("보고문 복사", null, delegate { CopyReport(Find(book, entry.Id)); });
            if (entry.Complete && (entry.Category != "Vessel" || entry.Movement == "출항")) menu.Items.Add("상태 되돌리기", null, RestoreSelected);
            menu.Items.Add("기록 삭제 (사진 보존)", null, DeleteEntry);
            menu.Show(owner, at);
        }
        void BuildArrivalEditor()
        {
            arrivalEditor = new Panel(); arrivalEditor.Visible = false; arrivalEditor.BackColor = CoastStyle.Panel;
            arrivalEditor.BorderStyle = BorderStyle.FixedSingle;
            arrivalDay = DayInput(); arrivalDay.Font = new Font(Font.FontFamily, 9F);
            Button cancel = CoastStyle.Button("취소", delegate { CloseArrival(); RefreshRecords(); });
            Control top = CoastStyle.Row(new Control[] { Heading("입항 처리"), arrivalDay, cancel }, 44, 36, 20);
            top.Dock = DockStyle.Top; top.Height = 34; arrivalEditor.Controls.Add(top);
            arrivalChoices = new FlowLayoutPanel(); arrivalChoices.Dock = DockStyle.Fill;
            arrivalChoices.WrapContents = false; arrivalChoices.AutoScroll = true;
            arrivalTime = TimeInput(); arrivalTime.Width = 78;
            arrivalManual = new TextBox(); CoastStyle.InputControl(arrivalManual); arrivalManual.Width = 105;
            tips.SetToolTip(arrivalManual, "입항할 항구를 직접 입력하세요.");
            arrivalConfirm = CoastStyle.Button("입항 확정", delegate { CompleteArrival(arrivalManual.Text); }); arrivalConfirm.Width = 85;
            arrivalEditor.Controls.Add(arrivalChoices); arrivalChoices.BringToFront();
            arrivalManual.Visible = false; arrivalConfirm.Visible = false;
            arrivalChoices.Controls.Add(arrivalTime); arrivalChoices.Controls.Add(arrivalManual); arrivalChoices.Controls.Add(arrivalConfirm);
            boatGrid.Controls.Add(arrivalEditor);
        }
        void BeginArrival(string id)
        {
            Entry entry = Find(book, id);
            if (entry == null || entry.Category != "Vessel" || entry.Complete || entry.IndependentMovement) return;
            arrivalId = id; selected = id; arrivalTime.Value = MinuteNow(); arrivalDay.Value = DateTime.Today;
            // Detach reusable controls without disposing them; they retain the entered draft during redraws.
            while (arrivalChoices.Controls.Count > 0)
            {
                Control control = arrivalChoices.Controls[0]; arrivalChoices.Controls.Remove(control);
                if (control != arrivalTime && control != arrivalManual && control != arrivalConfirm) control.Dispose();
            }
            arrivalManual.Text = ""; arrivalManual.Visible = false; arrivalConfirm.Visible = false;
            arrivalChoices.Controls.Add(arrivalTime);
            foreach (string port in Data.Lines(book.Settings.Harbors))
            {
                string captured = port;
                Button choose = CoastStyle.Button(port, delegate { CompleteArrival(captured); });
                choose.AutoSize = true; choose.MinimumSize = new Size(55, 29); arrivalChoices.Controls.Add(choose);
            }
            Button direct = CoastStyle.Button("직접입력", delegate {
                arrivalManual.Visible = true; arrivalConfirm.Visible = true; arrivalManual.Focus();
                arrivalChoices.ScrollControlIntoView(arrivalConfirm);
            }); direct.Width = 80; arrivalChoices.Controls.Add(direct);
            arrivalChoices.Controls.Add(arrivalManual); arrivalChoices.Controls.Add(arrivalConfirm);
            RefreshRecords();
            for (int i = 0; i < boatGrid.Rows.Count; i++)
                if (boatGrid.Rows[i].Tag is ArrivalSlot)
                {
                    try { boatGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 1); }
                    catch (InvalidOperationException) { }
                    break;
                }
            PositionArrivalEditor();
        }
        void CloseArrival()
        { arrivalId = ""; if (arrivalEditor != null) arrivalEditor.Visible = false; }
        void CompleteArrival(string port)
        {
            if (arrivalId.Length == 0 || !EnsureDay(true)) return;
            try
            {
                Entry entry = Find(Data.Clone(book), arrivalId); if (entry == null) return;
                Data.Arrive(entry, At(arrivalDay, arrivalTime), port);
                StoreEntry(entry, entry.ArrivalPlace() + " 입항 처리 완료");
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void PositionArrivalEditor()
        {
            if (positioningArrival || arrivalEditor == null) return;
            positioningArrival = true;
            try
            {
                int index = -1;
                foreach (DataGridViewRow row in boatGrid.Rows)
                { ArrivalSlot slot = row.Tag as ArrivalSlot; if (slot != null && slot.Id == arrivalId) { index = row.Index; break; } }
                if (index < 0) { arrivalEditor.Visible = false; return; }
                Rectangle rowArea = boatGrid.GetRowDisplayRectangle(index, false);
                int vertical = 0, horizontal = 0;
                foreach (Control control in boatGrid.Controls)
                {
                    if (control.Visible && control is VScrollBar) vertical = control.Width;
                    if (control.Visible && control is HScrollBar) horizontal = control.Height;
                }
                Rectangle bounds = new Rectangle(1, rowArea.Top + 1, Math.Max(50, boatGrid.ClientSize.Width - vertical - 2), ArrivalHeight);
                Rectangle visible = Rectangle.Intersect(bounds, new Rectangle(0, boatGrid.ColumnHeadersHeight,
                    Math.Max(0, boatGrid.ClientSize.Width - vertical), Math.Max(0, boatGrid.ClientSize.Height - boatGrid.ColumnHeadersHeight - horizontal)));
                if (visible.IsEmpty) { arrivalEditor.Visible = false; return; }
                arrivalEditor.Bounds = bounds;
                Region old = arrivalEditor.Region;
                visible.Offset(-bounds.X, -bounds.Y); arrivalEditor.Region = new Region(visible);
                if (old != null) old.Dispose();
                arrivalEditor.Visible = true; arrivalEditor.BringToFront();
            }
            finally { positioningArrival = false; }
        }
        bool CanChangeSelected()
        {
            if (selected == editing && dirty) { status.Text = "이 기록의 입력을 먼저 저장하거나 수정을 취소하세요."; return false; }
            return true;
        }
        void FinishSelected(object sender, EventArgs e)
        {
            if (!EnsureDay(true) || !CanChangeSelected()) return;
            try
            {
                Entry entry = Find(Data.Clone(book), selected); if (entry == null) return;
                if (entry.Category == "Vessel") Data.Arrive(entry, DateTime.Now, entry.Title); else Data.Finish(entry, DateTime.Now);
                if (StoreEntry(entry, entry.Category == "Vessel" ? "입항 시각 연결 완료" : "철수 처리 완료") && editing == entry.Id) ResetEditor();
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void RestoreSelected(object sender, EventArgs e)
        {
            if (!EnsureDay(true) || !CanChangeSelected()) return;
            try { Entry entry = Find(Data.Clone(book), selected); if (entry == null) return;
                Data.Reopen(entry); if (StoreEntry(entry, "상태를 되돌렸습니다.") && editing == entry.Id) ResetEditor(); }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void EditDetails(object sender, EventArgs e)
        {
            if (!CanChangeSelected()) return; Entry entry = Find(Data.Clone(book), selected); if (entry == null) return;
            using (RecordEditDialog dialog = new RecordEditDialog(entry, book.Settings))
                if (dialog.ShowDialog(this) == DialogResult.OK && StoreEntry(dialog.Value, "기록 수정 완료") && editing == entry.Id) ResetEditor();
        }
        void CopyReport(Entry entry)
        { try { Clipboard.SetText(entry.Report()); status.Text = "저장된 보고문을 복사했습니다."; } catch (Exception ex) { status.Text = "원본 보존 · 복사 실패: " + ex.Message; } }
        void UndoLast(object sender, EventArgs e)
        {
            if (undoBefore == null || !Discard()) return;
            try
            {
                Book next = Data.Clone(book); next.Entries = Data.Clone(undoBefore).Entries;
                foreach (Entry entry in book.Entries) Data.Queue(next, entry.Time);
                foreach (Entry entry in next.Entries) Data.Queue(next, entry.Time);
                Persist(next); undoBefore = null; undoButton.Enabled = false; selected = ""; CloseArrival(); ResetEditor(); SyncCsv(); RefreshRecords(); status.Text = "직전 기록 변경을 되돌렸습니다.";
            }
            catch (Exception ex) { Error(ex); }
        }
        void ClearUndo() { undoBefore = null; undoButton.Enabled = false; }
        void Configure(object sender, EventArgs e)
        {
            if (busy) return;
            try { if (ConfigureCore()) { LoadKinds(); UpdateBrand(); RefreshRecords(); SyncCsv(); } }
            catch (Exception ex) { Error(ex); }
        }
        bool ConfigureCore()
        {
            using (ConfigDialog dialog = new ConfigDialog(book.Settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                Book next = Data.Clone(book); next.Settings = dialog.Value;
                foreach (Entry entry in next.Entries) Data.Queue(next, entry.Time);
                Persist(next); ClearUndo(); return true;
            }
        }
        void OpenHistory(bool vessel)
        {
            using (HistoryDialog dialog = new HistoryDialog(book, vessel))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Entry entry = Find(book, dialog.SelectedId); if (entry == null) return;
                if (entry.Category == "Vessel") { selected = entry.Id; EditDetails(null, EventArgs.Empty); }
                else if (Discard()) { LoadEntry(entry); RefreshRecords(); }
            }
        }
        void DeleteEntry(object sender, EventArgs e)
        {
            if (!CanChangeSelected()) return; Entry entry = Find(book, selected); if (entry == null) return;
            if (MessageBox.Show(this, "기록을 삭제합니까?\n사진 파일은 보존합니다.", "삭제 확인", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                Book next = Data.Clone(book); next.Entries.Remove(Find(next, entry.Id)); Data.Queue(next, entry.Time);
                Book before = Data.Clone(book); Persist(next); undoBefore = before; undoButton.Enabled = true;
                if (editing == entry.Id) ResetEditor(); selected = ""; SyncCsv(); RefreshRecords(); status.Text = "기록 삭제 완료 · 사진 보존 · 실행 취소 가능";
            }
            catch (Exception ex) { Error(ex); }
        }
        void UpdatePhotoStatus(Entry entry)
        {
            int missing = 0, mismatch = 0;
            for (int i = 0; i < entry.Photos.Count; i++)
            {
                string path = entry.Photos[i];
                if (!File.Exists(path)) missing++;
                else if (!String.Equals(Path.GetFileName(path), Data.PhotoFileName(entry, entry.Photos.Count,
                    i + 1, Path.GetExtension(path)), StringComparison.OrdinalIgnoreCase)) mismatch++;
            }
            status.Text = "연결 사진 " + entry.Photos.Count + " / 없음 " + missing + " / 이름 갱신 필요 " + mismatch;
        }

        void SyncCsv()
        {
            try
            {
                foreach (string day in book.PendingDays) Data.ExportDay(book, Data.ParseDay(day));
                if (book.PendingDays.Count > 0)
                {
                    Book next = Data.Clone(book); next.PendingDays.Clear(); Persist(next);
                }
                exportError = ""; status.Text = "원본 정상 / CSV 갱신 대기 없음";
            }
            catch (Exception ex) { exportError = ex.Message; status.Text = "원본 보존 / CSV 갱신 대기 (재시도 가능)"; }
        }
        void RetryCsv(object sender, EventArgs e)
        {
            SyncCsv(); MessageBox.Show(this, exportError.Length == 0 ? "대기 중인 CSV 갱신을 완료했습니다." : exportError);
        }
        void OpenCsv(bool vessel)
        {
            try
            {
                DateTime day = DateTime.Today; Entry entry = Find(book, selected);
                if (entry != null && (entry.Category == "Vessel") == vessel) day = entry.Time.Date;
                Data.ExportDay(book, day); string original = Data.CsvDayPath(book, day, vessel);
                string copies = Path.Combine(Path.GetDirectoryName(dataPath), "ViewCopies"); Directory.CreateDirectory(copies);
                string copy = Path.Combine(copies, Data.Day(day) + (vessel ? "_선박_" : "_식별표적_") + "열람용_" + Guid.NewGuid().ToString("N") + ".csv");
                File.Copy(original, copy, false);
                try { Process.Start(new ProcessStartInfo(copy) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "사본은 저장했지만 열지 못했습니다.\r\n" + copy + "\r\n" + ex.Message); }
            }
            catch (Exception ex) { Error(ex); }
        }

        void AttachPhotos(object sender, EventArgs e)
        {
            Entry entry = Find(book, selected);
            if (entry == null || (dirty && editing == selected)) { MessageBox.Show(this, "사진을 연결할 기록을 먼저 저장하세요."); return; }
            using (PhotoAssignDialog dialog = new PhotoAssignDialog(entry))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ClearUndo();
                busy = true; int moved = 0, copied = 0, failed = 0; List<string> errors = new List<string>();
                try
                {
                    string folder = Path.Combine(book.Settings.PhotoFolder, Data.Day(entry.Time)); Directory.CreateDirectory(folder);
                    foreach (string source in dialog.FileNames)
                    {
                        string destination = ""; bool linked = false, owned = false;
                        try
                        {
                            foreach (Entry other in book.Entries)
                                foreach (string path in other.Photos)
                                    if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                                        throw new IOException("이미 연결된 사진입니다.");
                            int total = entry.Photos.Count + dialog.FileNames.Length;
                            int ordinal = Find(book, entry.Id).Photos.Count + 1;
                            destination = Data.PhotoDestination(entry, total, ordinal, folder,
                                Path.GetExtension(source).ToLowerInvariant(), null);
                            Data.CopyVerified(source, destination); owned = true;
                            Book next = Data.Clone(book); Find(next, entry.Id).Photos.Add(destination);
                            Data.Queue(next, entry.Time); Persist(next); linked = true;
                            // Recheck immediately before removing the source. On failure, keep both files.
                            if (Data.Hash(source) != Data.Hash(destination)) throw new IOException("원본이 변경되어 삭제하지 않았습니다.");
                            File.Delete(source); moved++;
                        }
                        catch (Exception ex)
                        {
                            if (linked) copied++; else failed++;
                            if (!linked && owned) { try { if (File.Exists(destination)) File.Delete(destination); } catch { } }
                            errors.Add(Path.GetFileName(source) + ": " + ex.Message);
                        }
                    }
                }
                catch (Exception ex) { failed = dialog.FileNames.Length - moved - copied; errors.Add(ex.Message); }
                finally { busy = false; }
                string renamed = RenameCore(entry.Id);
                UpdatePhotoStatus(Find(book, entry.Id)); SyncCsv();
                MessageBox.Show(this, "이동 " + moved + " / 연결·복사만 완료 " + copied + " / 실패 " + failed
                    + "\r\n" + String.Join("\r\n", errors.ToArray()) + renamed, "사진 처리 결과");
            }
        }
        void RenamePhotos(object sender, EventArgs e)
        {
            if (dirty && editing == selected) { MessageBox.Show(this, "기록을 먼저 저장하세요."); return; }
            if (Find(book, selected) == null) return;
            ClearUndo();
            MessageBox.Show(this, RenameCore(selected), "사진명 갱신 결과");
            UpdatePhotoStatus(Find(book, selected));
        }
        string RenameCore(string id)
        {
            ClearUndo();
            busy = true; int renamed = 0, missing = 0; List<string> errors = new List<string>();
            try
            {
                Entry entry = Find(book, id);
                for (int i = 0; i < entry.Photos.Count; i++)
                {
                    string source = Find(book, id).Photos[i];
                    if (!File.Exists(source)) { missing++; continue; }
                    string expected = Data.PhotoFileName(entry, entry.Photos.Count, i + 1, Path.GetExtension(source));
                    if (String.Equals(Path.GetFileName(source), expected, StringComparison.OrdinalIgnoreCase)) continue;
                    string destination = "";
                    bool moved = false;
                    try
                    {
                        destination = Data.PhotoDestination(entry, entry.Photos.Count, i + 1,
                            Path.GetDirectoryName(source), Path.GetExtension(source), source);
                        File.Move(source, destination); moved = true;
                        Book next = Data.Clone(book); Find(next, id).Photos[i] = destination; Persist(next); renamed++;
                    }
                    catch (Exception ex)
                    {
                        string rollback = "";
                        if (moved) { try { File.Move(destination, source); } catch (Exception failure) { rollback = " / 되돌리기 실패: " + destination + " / " + failure.Message; } }
                        errors.Add(Path.GetFileName(source) + ": " + ex.Message + rollback);
                    }
                }
            }
            catch (Exception ex) { errors.Add(ex.Message); }
            finally { busy = false; }
            return "\r\n이름 변경 " + renamed + " / 파일 없음 " + missing + " / 오류 " + errors.Count
                + (errors.Count == 0 ? "" : "\r\n" + String.Join("\r\n", errors.ToArray()));
        }

        void EditHelp(object sender, EventArgs e)
        {
            using (TextDialog dialog = new TextDialog("일반 도움말 편집", book.Help))
            {
                while (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try { Book next = Data.Clone(book); next.Help = dialog.Input.Text; Persist(next); break; }
                    catch (Exception ex) { Error(ex); }
                }
            }
        }
        bool EnsureDay(bool force)
        {
            DateTime today = DateTime.Now.Date;
            if (book.AcknowledgedDate == today) { editor.Enabled = true; return true; }
            if (!force && promptedDate == today) return false;
            promptedDate = today; editor.Enabled = false; busy = true;
            try
            {
                MessageBox.Show(this, "날짜가 " + Data.Day(today) + "입니다.\r\n"
                    + "기존 기록은 복제하지 않고 보존합니다. 입력 중인 내용과 일시는 그대로 유지됩니다. 날짜가 지난 입력은 시각과 날짜를 확인하세요.\r\n"
                    + "과거 기록은 [기록 조회]에서 조회하세요.", "날짜 확인");
                Book next = Data.Clone(book); next.AcknowledgedDate = today;
                next.AlarmAcknowledgements.RemoveAll(delegate(string key) { return !key.StartsWith(Data.Day(today) + "|", StringComparison.Ordinal); });
                Persist(next); editor.Enabled = true; RefreshRecords(); return true;
            }
            catch (Exception ex) { status.Text = "날짜 확인 저장 실패 / 입력 잠김 / 날짜 확인 버튼으로 재시도"; Error(ex); return false; }
            finally { busy = false; }
        }
        void Tick(object sender, EventArgs e)
        {
            if (busy || ContainsModal()) return;
            if (!EnsureDay(false)) return;
            try
            {
                DateTime now = DateTime.Now; List<string> due = new List<string>(); List<string> keys = new List<string>();
                foreach (string[] alarm in ConfigDialog.ParseAlarms(book.Settings.Alarms))
                {
                    string key = Data.Day(now) + "|" + alarm[0] + "|" + alarm[1];
                    DateTime at = now.Date.Add(TimeSpan.ParseExact(alarm[0], @"hh\:mm", CultureInfo.InvariantCulture));
                    if (now >= at && !book.AlarmAcknowledgements.Contains(key))
                    { due.Add(alarm[0] + " " + alarm[1]); keys.Add(key); }
                }
                if (due.Count == 0) return;
                busy = true;
                MessageBox.Show(this, "확인하지 않은 금일 일반 일정입니다.\r\n" + String.Join("\r\n", due.ToArray()), "일정 알림");
                Book next = Data.Clone(book); next.AlarmAcknowledgements.AddRange(keys); Persist(next);
            }
            catch (Exception ex) { Error(ex); }
            finally { busy = false; }
        }
        bool ContainsModal() { foreach (Form form in Application.OpenForms) if (form != this && form.Modal) return true; return false; }
        void ConfirmClosing(object sender, FormClosingEventArgs e)
        {
            if (busy) { e.Cancel = true; return; }
            if (!closing && !Discard()) { e.Cancel = true; return; }
            if (!closing && boatDirty && MessageBox.Show(this, "저장하지 않은 선박 입력을 버리고 종료합니까?", "입력 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true;
        }
    }
}
