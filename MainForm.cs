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
        Book book;
        string editing = "";
        bool dirty, loading, busy, closing;
        DateTime promptedDate;
        string exportError = "";
        FlowLayoutPanel editor, targetPanel, vesselPanel, completionPanel;
        DateTimePicker date, finishTime;
        TextBox note, search;
        ComboBox title, camera;
        NumericUpDown azimuth, elevation;
        CheckBox hasAzimuth, hasElevation;
        ComboBox kind, subkind, category, vesselType, movement;
        TextBox vesselName;
        CheckBox hasFinishedAt;
        Label totals;
        NumericUpDown quantity;
        ListBox cart;
        ListView records;
        Button saveButton;
        Label listTitle;
        CheckBox complete, showActive, showDone;
        Label status, photoStatus;
        Timer clock;

        public MainForm(string folder)
        {
            dataPath = Path.Combine(folder, "Records.xml");
            book = Data.Load(dataPath);
            if (book.Settings.Kinds == null) book.Settings.Kinds = new List<Kind>();
            if (!book.Settings.Configured)
            {
                book.Settings.CsvFolder = Path.Combine(folder, "CSV");
                book.Settings.PhotoFolder = Path.Combine(folder, "Photos");
                book.Settings.Kinds.Add(new Kind { Id = Guid.NewGuid().ToString("N"), Name = "낚시꾼", Unit = "명" });
            }
            Text = "COAST 기록장";
            Font = new Font("맑은 고딕", 9F);
            BackColor = Color.FromArgb(28, 34, 39); ForeColor = Color.WhiteSmoke;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Bounds = new Rectangle(area.Right - 420, area.Top, 420, area.Height);
            MinimumSize = new Size(360, Math.Min(600, area.Height));
            SplitContainer split = new SplitContainer(); split.Orientation = Orientation.Horizontal;
            split.Size = ClientSize; split.Dock = DockStyle.Fill;
            split.Panel1MinSize = 100; split.Panel2MinSize = 150;
            split.SplitterDistance = Math.Max(100, ClientSize.Height - 320);
            Controls.Add(split);
            FlowLayoutPanel page = new FlowLayoutPanel();
            page.Dock = DockStyle.Fill; page.FlowDirection = FlowDirection.TopDown;
            page.WrapContents = false; page.AutoScroll = true; split.Panel1.Controls.Add(page);
            FlowLayoutPanel tools = Row(page, 78);
            Button(tools, "설정", Configure); Button(tools, "CSV 재시도", RetryCsv);
            Button(tools, "열람용 CSV", OpenCsv); Button(tools, "도움말", EditHelp);
            Button(tools, "날짜 확인", delegate { EnsureDay(true); });
            editor = new FlowLayoutPanel(); editor.FlowDirection = FlowDirection.TopDown;
            editor.WrapContents = false; editor.Width = 358; editor.AutoSize = true;
            editor.AutoSizeMode = AutoSizeMode.GrowAndShrink; page.Controls.Add(editor);
            Label(editor, "기록 구분");
            category = new ComboBox(); category.Width = 340; category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.Items.AddRange(new object[] { "특이표적", "선박 입출항", "일반/기존 기록" });
            editor.Controls.Add(category);
            Label(editor, "기록 일시 (식별 또는 입출항 시각)");
            date = new DateTimePicker(); date.Format = DateTimePickerFormat.Custom;
            date.CustomFormat = "yyyy-MM-dd HH:mm"; date.Width = 340; editor.Controls.Add(date);
            Label(editor, "카메라 / 장소·제목 (직접 입력 또는 설정 목록 선택)");
            FlowLayoutPanel locationRow = Row(editor, 32);
            camera = new ComboBox(); camera.Width = 164; locationRow.Controls.Add(camera);
            title = new ComboBox(); title.Width = 164; title.MaxLength = 120; locationRow.Controls.Add(title);
            FlowLayoutPanel angles = Row(editor, 32);
            hasAzimuth = Check(angles, "방위각", false); hasAzimuth.Width = 65;
            azimuth = new NumericUpDown(); azimuth.Width = 90; azimuth.Maximum = 6400;
            angles.Controls.Add(azimuth);
            hasElevation = Check(angles, "고각", false); hasElevation.Width = 55;
            elevation = new NumericUpDown(); elevation.Width = 90;
            elevation.Minimum = -180; elevation.Maximum = 180; angles.Controls.Add(elevation);
            hasAzimuth.CheckedChanged += delegate { azimuth.Enabled = hasAzimuth.Checked; };
            hasElevation.CheckedChanged += delegate { elevation.Enabled = hasElevation.Checked; };
            Label(editor, "방위각 0~6400 / 고각 -180~180 (정수, 체크 해제=미입력)");
            targetPanel = Panel(editor);
            Label(targetPanel, "특이표적 묶음 / 하위항목 선택");
            FlowLayoutPanel itemRow = Row(targetPanel, 32);
            kind = new ComboBox(); kind.Width = 165; kind.DropDownStyle = ComboBoxStyle.DropDownList;
            itemRow.Controls.Add(kind);
            subkind = new ComboBox(); subkind.Width = 165; subkind.DropDownStyle = ComboBoxStyle.DropDownList;
            itemRow.Controls.Add(subkind); kind.SelectedIndexChanged += GroupChanged;
            FlowLayoutPanel quantityRow = Row(targetPanel, 36);
            quantity = new NumericUpDown(); quantity.Width = 90;
            quantity.Minimum = 0; quantity.Maximum = 999; quantity.Value = 1; quantityRow.Controls.Add(quantity);
            Button(quantityRow, "+ 추가", AddItem, 105);
            Label(targetPanel, "같은 하위항목 합산: 0~999 / 묶음은 하위항목 합계");
            cart = new ListBox(); cart.Width = 340; cart.Height = 66; targetPanel.Controls.Add(cart);
            totals = Label(targetPanel, "묶음 합계 없음"); totals.Height = 45;
            FlowLayoutPanel cartButtons = Row(targetPanel, 36);
            Button(cartButtons, "항목 삭제", delegate {
                if (cart.SelectedIndex >= 0) { cart.Items.RemoveAt(cart.SelectedIndex); dirty = true; UpdateTotals(); }
            });
            vesselPanel = Panel(editor);
            Label(vesselPanel, "선종 / 입출항 (각각 독립된 기록)");
            FlowLayoutPanel vesselRow = Row(vesselPanel, 32);
            vesselType = new ComboBox(); vesselType.Width = 165; vesselRow.Controls.Add(vesselType);
            movement = new ComboBox(); movement.Width = 165; movement.DropDownStyle = ComboBoxStyle.DropDownList;
            movement.Items.AddRange(new object[] { "출항", "입항" }); vesselRow.Controls.Add(movement);
            Label(vesselPanel, "선명 / 추적번호"); vesselName = TextBox(vesselPanel, false, 25);
            completionPanel = Panel(editor);
            FlowLayoutPanel completionRow = Row(completionPanel, 32);
            complete = Check(completionRow, "철수", false); complete.Width = 80;
            hasFinishedAt = Check(completionRow, "종료 시각 기록", false); hasFinishedAt.Width = 150;
            finishTime = new DateTimePicker(); finishTime.Format = DateTimePickerFormat.Custom;
            finishTime.CustomFormat = "yyyy-MM-dd HH:mm"; finishTime.Width = 340; completionPanel.Controls.Add(finishTime);
            complete.CheckedChanged += CompletionChanged; hasFinishedAt.CheckedChanged += CompletionChanged;
            category.SelectedIndexChanged += CategoryChanged;
            Label(editor, "비고 (여러 줄 입력 가능)"); note = TextBox(editor, true, 54);
            FlowLayoutPanel editButtons = Row(editor, 78);
            Button(editButtons, "새 입력", NewEntry);
            saveButton = Button(editButtons, "등록", SaveEntry, 108);
            Button(editButtons, "영구 삭제", DeleteEntry); Button(editButtons, "사진할당", AttachPhotos);
            Button(editButtons, "사진명 갱신", RenamePhotos);
            photoStatus = Label(editor, "사진 없음");
            FlowLayoutPanel viewTools = new FlowLayoutPanel(); viewTools.Dock = DockStyle.Top;
            viewTools.FlowDirection = FlowDirection.TopDown; viewTools.WrapContents = false; viewTools.Height = 142;
            status = Label(viewTools, "준비 중"); status.Height = 36;
            FlowLayoutPanel filters = Row(viewTools, 30);
            showActive = Check(filters, "식별/진행", true); showDone = Check(filters, "종료/입출항", true);
            Button(filters, "기간 조회", OpenHistory, 105);
            listTitle = Label(viewTools, "금일 기록 / 검색: 장소·항목·비고·카메라");
            search = TextBox(viewTools, false, 25);
            records = RecordTable.Create(true); records.Dock = DockStyle.Fill;
            split.Panel2.Controls.Add(records); split.Panel2.Controls.Add(viewTools);
            records.SelectedIndexChanged += SelectEntry;
            showActive.CheckedChanged += FilterChanged; showDone.CheckedChanged += FilterChanged;
            search.TextChanged += FilterChanged;
            title.TextChanged += MarkDirty; note.TextChanged += MarkDirty;
            camera.TextChanged += MarkDirty; azimuth.ValueChanged += MarkDirty;
            elevation.ValueChanged += MarkDirty; hasAzimuth.CheckedChanged += MarkDirty;
            hasElevation.CheckedChanged += MarkDirty;
            vesselType.TextChanged += MarkDirty; vesselName.TextChanged += MarkDirty;
            movement.SelectedIndexChanged += MarkDirty; finishTime.ValueChanged += MarkDirty;
            hasFinishedAt.CheckedChanged += MarkDirty;
            date.ValueChanged += MarkDirty; complete.CheckedChanged += MarkDirty;
            FormClosing += ConfirmClosing;
            Shown += Started;
            clock = new Timer(); clock.Interval = 15000; clock.Tick += Tick;
            FormClosed += delegate { clock.Dispose(); };
            ResetEditor();
        }

        FlowLayoutPanel Panel(Control parent)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel(); panel.Width = 350;
            panel.FlowDirection = FlowDirection.TopDown; panel.WrapContents = false;
            panel.AutoSize = true; panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Margin = Padding.Empty; parent.Controls.Add(panel); return panel;
        }
        void CategoryChanged(object sender, EventArgs e)
        {
            bool vessel = category.SelectedIndex == 1;
            targetPanel.Visible = !vessel; vesselPanel.Visible = vessel; completionPanel.Visible = !vessel;
            complete.Text = category.SelectedIndex == 0 ? "철수" : "완료";
            MarkDirty(sender, e);
        }
        void CompletionChanged(object sender, EventArgs e)
        {
            if (!loading && sender == complete)
            { hasFinishedAt.Checked = complete.Checked; if (complete.Checked) finishTime.Value = DateTime.Now; }
            hasFinishedAt.Enabled = complete.Checked;
            finishTime.Enabled = complete.Checked && hasFinishedAt.Checked;
            MarkDirty(sender, e);
        }
        void GroupChanged(object sender, EventArgs e)
        {
            subkind.Items.Clear(); Kind selected = kind.SelectedItem as Kind;
            if (selected == null) return;
            string[] choices = selected.Choices();
            if (choices.Length > 1)
                foreach (string choice in choices) subkind.Items.Add(choice);
            else subkind.Items.Add("하위항목 없음");
            subkind.SelectedIndex = 0; subkind.Enabled = choices.Length > 1;
        }
        void UpdateTotals()
        {
            Entry entry = new Entry(); foreach (Item item in cart.Items) entry.Items.Add(item);
            totals.Text = cart.Items.Count == 0 ? "묶음 합계 없음" : "묶음 합계: " + entry.Totals();
        }
        FlowLayoutPanel Row(Control parent, int height)
        {
            FlowLayoutPanel row = new FlowLayoutPanel(); row.Width = 350; row.Height = height;
            parent.Controls.Add(row); return row;
        }
        Label Label(Control parent, string text)
        {
            Label label = new Label(); label.Text = text; label.Width = 340; label.Height = 23;
            parent.Controls.Add(label); return label;
        }
        TextBox TextBox(Control parent, bool multiline, int height)
        {
            TextBox box = new TextBox(); box.Width = 340; box.Multiline = multiline; box.Height = height;
            box.BackColor = Color.FromArgb(43, 51, 58); box.ForeColor = Color.WhiteSmoke;
            if (multiline) box.ScrollBars = ScrollBars.Vertical;
            parent.Controls.Add(box); return box;
        }
        void Button(Control parent, string text, EventHandler handler) { Button(parent, text, handler, 108); }
        Button Button(Control parent, string text, EventHandler handler, int width)
        {
            Button button = new Button(); button.Text = text; button.Width = width; button.Height = 30;
            button.BackColor = Color.FromArgb(31, 84, 85); button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat; button.Click += handler; parent.Controls.Add(button);
            return button;
        }
        CheckBox Check(Control parent, string text, bool check)
        {
            CheckBox box = new CheckBox(); box.Text = text; box.Checked = check; box.Width = 105;
            parent.Controls.Add(box); return box;
        }
        void MarkDirty(object sender, EventArgs e) { if (!loading) dirty = true; }
        void Error(Exception ex) { MessageBox.Show(this, ex.Message, "작업 결과 확인"); }
        bool Discard()
        {
            return !dirty || MessageBox.Show(this, "저장하지 않은 입력을 버립니까?", "입력 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        void Started(object sender, EventArgs e)
        {
            try
            {
                try { ConfigDialog.Validate(book.Settings); }
                catch { if (!ConfigureCore()) { closing = true; Close(); return; } }
                Text = "COAST - " + book.Settings.Name;
                LoadKinds(); RefreshRecords(); EnsureDay(true); SyncCsv(); clock.Start();
            }
            catch (Exception ex) { Error(ex); closing = true; Close(); }
        }
        void LoadKinds()
        {
            LoadChoices(camera, book.Settings.Cameras); LoadChoices(title, book.Settings.Places);
            LoadChoices(vesselType, book.Settings.VesselTypes);
            kind.Items.Clear();
            foreach (Kind value in book.Settings.Kinds) kind.Items.Add(value);
            if (kind.Items.Count > 0) kind.SelectedIndex = 0;
        }
        void LoadChoices(ComboBox box, string choices)
        {
            string value = box.Text; box.Items.Clear();
            foreach (string line in (choices ?? "").Split('\n'))
                if (line.Trim().Length > 0 && !box.Items.Contains(line.Trim())) box.Items.Add(line.Trim());
            box.Text = value;
        }
        void Persist(Book next)
        {
            Data.Save(dataPath, next); book = next;
        }
        void Configure(object sender, EventArgs e)
        {
            if (busy) return;
            try { if (ConfigureCore()) { LoadKinds(); SyncCsv(); } }
            catch (Exception ex) { Error(ex); }
        }
        bool ConfigureCore()
        {
            using (ConfigDialog dialog = new ConfigDialog(book.Settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                Book next = Data.Clone(book); next.Settings = dialog.Value;
                foreach (Entry entry in next.Entries) Data.Queue(next, entry.Time);
                Persist(next); Text = "COAST - " + book.Settings.Name; return true;
            }
        }

        void AddItem(object sender, EventArgs e)
        {
            Kind selected = kind.SelectedItem as Kind; if (selected == null) return;
            string child = selected.Choices().Length > 1 ? (string)subkind.SelectedItem : "";
            int count = (int)quantity.Value;
            foreach (Item item in cart.Items)
                if (item.KindId == selected.Id && item.SubName == child)
                {
                    if (item.Count + count > 999) { MessageBox.Show(this, "합산 수량은 999 이하여야 합니다."); return; }
                    item.Count += count;
                    int index = cart.Items.IndexOf(item); cart.Items[index] = item;
                    dirty = true; UpdateTotals(); return;
                }
            cart.Items.Add(new Item { KindId = selected.Id, Name = selected.Name, Unit = selected.Unit, SubName = child, Count = count });
            dirty = true; UpdateTotals();
        }
        void NewEntry(object sender, EventArgs e) { if (Discard()) ResetEditor(); }
        void ResetEditor()
        {
            loading = true; editing = ""; date.Value = DateTime.Now; title.Text = ""; note.Text = "";
            camera.Text = ""; hasAzimuth.Checked = false; hasElevation.Checked = false;
            azimuth.Enabled = false; elevation.Enabled = false; azimuth.Value = 0; elevation.Value = 0;
            complete.Checked = false; hasFinishedAt.Checked = false; finishTime.Value = DateTime.Now;
            CompletionChanged(null, EventArgs.Empty);
            cart.Items.Clear(); UpdateTotals(); category.Enabled = true; category.SelectedIndex = 0;
            CategoryChanged(null, EventArgs.Empty); vesselName.Text = ""; vesselType.Text = ""; movement.SelectedIndex = 0;
            foreach (ListViewItem row in records.Items) row.Selected = false;
            saveButton.Text = "등록";
            photoStatus.Text = "사진 없음"; loading = false; dirty = false;
        }
        Entry Find(Book source, string id) { return source.Entries.Find(delegate(Entry entry) { return entry.Id == id; }); }
        void SelectEntry(object sender, EventArgs e)
        {
            if (loading) return;
            Entry selected = RecordTable.Selected(records); if (selected == null || selected.Id == editing) return;
            if (!Discard()) { RefreshRecords(); return; }
            LoadEntry(selected);
        }
        void LoadEntry(Entry selected)
        {
            loading = true; editing = selected.Id; date.Value = selected.Time; title.Text = selected.Title;
            note.Text = selected.Note; camera.Text = selected.Camera;
            hasAzimuth.Checked = selected.Azimuth.HasValue; azimuth.Value = selected.Azimuth ?? 0;
            hasElevation.Checked = selected.Elevation.HasValue; elevation.Value = selected.Elevation ?? 0;
            category.SelectedIndex = selected.Category == "Target" ? 0 : selected.Category == "Vessel" ? 1 : 2;
            category.Enabled = false; CategoryChanged(null, EventArgs.Empty);
            complete.Checked = selected.Complete; hasFinishedAt.Checked = selected.FinishedAt.HasValue;
            finishTime.Value = selected.FinishedAt ?? DateTime.Now; CompletionChanged(null, EventArgs.Empty);
            vesselType.Text = selected.VesselType; vesselName.Text = selected.VesselName;
            movement.SelectedItem = selected.Movement; cart.Items.Clear();
            foreach (Item item in selected.Items)
                cart.Items.Add(new Item { KindId = item.KindId, Name = item.Name, Unit = item.Unit, SubName = item.SubName, Count = item.Count });
            saveButton.Text = "저장"; UpdateTotals();
            UpdatePhotoStatus(selected); loading = false; dirty = false;
        }
        void OpenHistory(object sender, EventArgs e)
        {
            using (HistoryDialog dialog = new HistoryDialog(book))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !Discard()) return;
                Entry entry = Find(book, dialog.SelectedId);
                if (entry != null) { LoadEntry(entry); RefreshRecords(); }
            }
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
            photoStatus.Text = "연결 사진 " + entry.Photos.Count + " / 없음 " + missing + " / 이름 갱신 필요 " + mismatch;
        }

        void SaveEntry(object sender, EventArgs e)
        {
            if (!EnsureDay(true)) return;
            if (date.Value > DateTime.Now) { MessageBox.Show(this, "미래 시각은 입력할 수 없습니다."); return; }
            bool vessel = category.SelectedIndex == 1;
            if (String.IsNullOrWhiteSpace(title.Text) || (!vessel && cart.Items.Count == 0))
            { MessageBox.Show(this, "장소·제목과 하나 이상의 항목을 입력하세요."); return; }
            if (vessel && (String.IsNullOrWhiteSpace(vesselType.Text) || String.IsNullOrWhiteSpace(vesselName.Text)
                || movement.SelectedIndex < 0))
            { MessageBox.Show(this, "선종, 선명/추적번호와 입출항을 입력하세요."); return; }
            if (!vessel && complete.Checked && hasFinishedAt.Checked
                && (finishTime.Value < date.Value || finishTime.Value > DateTime.Now))
            { MessageBox.Show(this, "종료 시각은 기록 시각 이후, 현재 시각 이하여야 합니다."); return; }
            Entry value = new Entry(); value.Id = editing.Length == 0 ? Guid.NewGuid().ToString("N") : editing;
            value.Time = date.Value; value.Title = title.Text.Trim(); value.Note = note.Text; value.Complete = vessel || complete.Checked;
            value.Category = vessel ? "Vessel" : category.SelectedIndex == 0 ? "Target" : "Manual";
            value.FinishedAt = !vessel && complete.Checked && hasFinishedAt.Checked ? (DateTime?)finishTime.Value : null;
            if (vessel) { value.VesselType = vesselType.Text.Trim(); value.VesselName = vesselName.Text.Trim(); value.Movement = movement.Text; }
            value.Camera = camera.Text.Trim();
            value.Azimuth = hasAzimuth.Checked ? (int?)azimuth.Value : null;
            value.Elevation = hasElevation.Checked ? (int?)elevation.Value : null;
            Entry original = Find(book, editing);
            value.Section = original != null ? original.Section : book.Settings.Name;
            if (!vessel) foreach (Item item in cart.Items) value.Items.Add(new Item {
                KindId = item.KindId, Name = item.Name, Unit = item.Unit, SubName = item.SubName, Count = item.Count });
            try
            {
                Book next = Data.Clone(book); Entry old = Find(next, value.Id);
                if (old != null)
                {
                    value.Photos = new List<string>(old.Photos); Data.Queue(next, old.Time); next.Entries.Remove(old);
                }
                next.Entries.Add(value); Data.Queue(next, value.Time); Persist(next);
            }
            catch (Exception ex) { status.Text = "원본 저장 실패 / 입력 유지"; Error(ex); return; }
            editing = value.Id; category.Enabled = false; saveButton.Text = "저장"; dirty = false; SyncCsv();
            string extra = "";
            if (value.Photos.Count > 0 && MessageBox.Show(this,
                "기록은 저장되었습니다. 연결 사진의 이름도 지금 갱신합니까?", "사진명 확인",
                MessageBoxButtons.YesNo) == DialogResult.Yes) extra = RenameCore(value.Id);
            bool copied = false;
            if (book.Settings.CopyText)
            {
                try { Clipboard.SetText(value.Time.ToString("yyyy-MM-dd HH:mm") + " " + value.Title + " " + value.Describe()); copied = true; }
                catch (Exception ex) { extra += "\r\n클립보드 복사 실패: " + ex.Message; }
            }
            RefreshRecords();
            Entry saved = Find(book, editing); if (saved != null) UpdatePhotoStatus(saved);
            string message = "원본 저장 완료" + (exportError.Length == 0 ? " / CSV 갱신 완료" : " / CSV 갱신 대기")
                + (copied ? " / 문장 복사 완료" : "") + extra;
            status.Text = message;
            if (book.Settings.ShowSuccess || extra.Length > 0 || exportError.Length > 0)
                MessageBox.Show(this, message + (exportError.Length == 0 ? "" : "\r\n" + exportError), "저장 결과");
        }

        void DeleteEntry(object sender, EventArgs e)
        {
            Entry selected = Find(book, editing); if (selected == null) { MessageBox.Show(this, "기록을 먼저 선택하세요."); return; }
            if (MessageBox.Show(this, "기록을 영구 삭제합니까? 복원할 수 없습니다.\r\n연결된 사진 파일은 남깁니다.", "삭제 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                Book next = Data.Clone(book); Entry old = Find(next, editing); Data.Queue(next, old.Time);
                next.Entries.Remove(old); Persist(next); ResetEditor(); RefreshRecords(); SyncCsv();
            }
            catch (Exception ex) { Error(ex); }
        }
        void FilterChanged(object sender, EventArgs e) { if (records != null) RefreshRecords(); }
        void RefreshRecords()
        {
            loading = true;
            try
            {
                List<Entry> entries = Data.SelectEntries(book, DateTime.Today, DateTime.Today,
                    showActive.Checked, showDone.Checked, search.Text);
                RecordTable.Fill(records, entries, editing, true);
                listTitle.Text = "금일 기록 " + Data.Day(DateTime.Today) + " / " + entries.Count + "건";
            }
            finally { loading = false; }
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
        void OpenCsv(object sender, EventArgs e)
        {
            try
            {
                DateTime day = DateTime.Today; Entry entry = Find(book, editing);
                if (entry != null) day = entry.Time.Date;
                string original = Data.ExportDay(book, day);
                string copies = Path.Combine(Path.GetDirectoryName(dataPath), "ViewCopies"); Directory.CreateDirectory(copies);
                string copy = Path.Combine(copies, Data.Day(day) + "_열람용_" + Guid.NewGuid().ToString("N") + ".csv");
                File.Copy(original, copy, false);
                try { Process.Start(new ProcessStartInfo(copy) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "사본은 저장했지만 열지 못했습니다.\r\n" + copy + "\r\n" + ex.Message); }
            }
            catch (Exception ex) { Error(ex); }
        }

        void AttachPhotos(object sender, EventArgs e)
        {
            Entry entry = Find(book, editing);
            if (entry == null || dirty) { MessageBox.Show(this, "사진을 연결할 기록을 먼저 저장하세요."); return; }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true; dialog.Filter = "사진|*.jpg;*.jpeg;*.png;*.bmp";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
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
            if (dirty) { MessageBox.Show(this, "기록을 먼저 저장하세요."); return; }
            if (Find(book, editing) == null) return;
            MessageBox.Show(this, RenameCore(editing), "사진명 갱신 결과");
            UpdatePhotoStatus(Find(book, editing));
        }
        string RenameCore(string id)
        {
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
                    + "기존 기록은 복제하지 않고 보존합니다. 입력 중인 내용과 일시는 그대로 유지됩니다.\r\n"
                    + "과거 기록은 [기간 조회]에서 조회하세요.", "날짜 확인");
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
            if (!closing && !Discard()) e.Cancel = true;
        }
    }
}
