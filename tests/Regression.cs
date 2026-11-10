using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Drawing;
using CoastNotebook;

class Regression
{
    static int passed;
    static string root;
    static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }
    static void Reject(Action action, string name)
    {
        bool failed = false; try { action(); } catch { failed = true; }
        Check(failed, name);
    }
    static Entry Make(DateTime time, bool done)
    {
        Entry e = new Entry { Id = Guid.NewGuid().ToString("N"), Time = time,
            Title = "테스트해변", Complete = done };
        e.Items.Add(new Item { KindId = "kind", Name = "낚시꾼", Unit = "명", Count = 4 });
        return e;
    }
    static Book MakeBook()
    {
        Book b = new Book(); b.Settings.Configured = true; b.Settings.Name = "4구간";
        b.Settings.CsvFolder = Path.Combine(root, "CSV"); b.Settings.PhotoFolder = Path.Combine(root, "Photos");
        b.Settings.Kinds.Add(new Kind { Id = "kind", Name = "낚시꾼", Unit = "명" });
        b.Settings.ShowSuccess = false; b.AcknowledgedDate = DateTime.Today;
        return b;
    }
    static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
    static void Call(object target, string name, params object[] args)
    {
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
    [STAThread]
    static int Main(string[] args)
    {
        root = Path.Combine(Path.GetTempPath(), "coast-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CoreTests(args[0]);
            DomainTests();
            MockDomainTests();
            ReviewDomainTests();
            SettingsAndMacroTests();
            if (args.Length > 1 && args[1] == "--ui") UiTests();
            Console.WriteLine("TOTAL PASSED: " + passed); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
    static void CoreTests(string legacy)
    {
        string path = Path.Combine(root, "Records.xml"); File.Copy(legacy, path);
        byte[] original = File.ReadAllBytes(path); Book old = Data.Load(path);
        Check(old.Version == 1 && old.Entries[0].Items[0].Count == 4, "read legacy XML");
        Check(!old.Entries[0].Azimuth.HasValue && !old.Entries[0].Elevation.HasValue
            && old.Entries[0].Camera == "", "legacy unknown fields remain empty");
        Data.Save(path, old);
        Check(Data.Load(path).Version == Data.CurrentVersion && Data.Load(path).Entries[0].Id == "11111111111111111111111111111111",
            "upgrade roundtrip keeps ID and records");
        string[] backups = Directory.GetFiles(root, "Records.xml.v1-*.bak");
        Check(backups.Length == 1 && Convert.ToBase64String(File.ReadAllBytes(backups[0]))
            == Convert.ToBase64String(original), "exact legacy backup before upgrade");
        Check(old.PendingDays.Contains("2026-11-06"), "upgrade queues existing CSV dates");
        Data.Save(path, old);
        Check(Directory.GetFiles(root, "Records.xml.v1-*.bak").Length == 1, "upgrade backup not repeated");
        Book b = MakeBook(); Entry active = Make(DateTime.Today.AddHours(1), false);
        active.Camera = "사동해강안"; active.Azimuth = 3426; active.Elevation = -121; active.Section = "4구간";
        b.Entries.Add(active); b.Entries.Add(Make(DateTime.Today.AddHours(2), true));
        b.Entries.Add(Make(DateTime.Today.AddDays(-1), false));
        Data.Save(path, b); Book read = Data.Load(path);
        Check(read.Entries[0].Azimuth == 3426 && read.Entries[0].Elevation == -121
            && read.Entries[0].Camera == "사동해강안", "camera and angles roundtrip");
        active.Azimuth = 6400; active.Elevation = 180; Data.Validate(b);
        active.Azimuth = 0; active.Elevation = -180; Data.Validate(b);
        Check(true, "angle endpoints accepted");
        active.Azimuth = 6401; Reject(delegate { Data.Validate(b); }, "azimuth upper bound");
        active.Azimuth = -1; Reject(delegate { Data.Validate(b); }, "azimuth lower bound");
        active.Azimuth = 3426; active.Elevation = -181;
        Reject(delegate { Data.Validate(b); }, "elevation lower bound");
        active.Elevation = 181; Reject(delegate { Data.Validate(b); }, "elevation upper bound");
        active.Elevation = -121;
        Check(Data.SelectEntries(b, DateTime.Today, DateTime.Today, false, false, "").Count == 2,
            "both status filters off shows all today's records");
        Check(Data.SelectEntries(b, DateTime.Today, DateTime.Today, true, false, "").Count == 1,
            "active filter and today boundary");
        Check(Data.SelectEntries(b, DateTime.Today.AddDays(-1).AddHours(23), DateTime.Today, true, true, "").Count == 3,
            "inclusive date range");
        Check(Data.SelectEntries(b, DateTime.Today, DateTime.Today, true, true, "사동해강안").Count == 1,
            "search camera");
        Reject(delegate { Data.SelectEntries(b, DateTime.Today, DateTime.Today.AddDays(-1), true, true, ""); },
            "reject reversed period");
        string csv = Data.ExportDay(b, DateTime.Today); byte[] bytes = File.ReadAllBytes(csv);
        string text = File.ReadAllText(csv);
        Check(bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191, "UTF-8 CSV BOM");
        Check(text.Contains("\"방위각(0~6400)\"") && text.Contains("\"3426\",\"-121\""),
            "separate camera/angle CSV columns with numeric negative elevation");
        Check(Data.CsvCell("=1+1") == "\"'=1+1\"" && Data.CsvCell("-1+1") == "\"'-1+1\""
            && Data.CsvCell("\t=1") == "\"'\t=1\"", "CSV formula protection");
        Check(Data.CsvCell("a,\"b\"\r\nc") == "\"a,\"\"b\"\"\r\nc\"", "CSV quoting");
        DateTime previous = active.Time; active.Time = DateTime.Today.AddDays(-2);
        Data.Queue(b, previous); Data.Queue(b, active.Time);
        Check(b.PendingDays.Count == 2, "old and new CSV dates queued");
        string before = Data.ExportDay(b, previous), after = Data.ExportDay(b, active.Time);
        Check(!File.ReadAllText(before).Contains(active.Id) && File.ReadAllText(after).Contains(active.Id),
            "date edit removes old CSV row and adds new CSV row");
        byte[] saved = File.ReadAllBytes(path);
        Reject(delegate { Data.AtomicWrite(path, delegate(Stream stream) { stream.WriteByte(0); throw new IOException("test"); }); },
            "simulated interrupted write");
        Check(Convert.ToBase64String(saved) == Convert.ToBase64String(File.ReadAllBytes(path))
            && Directory.GetFiles(root, "*.tmp").Length == 0, "failed atomic write preserves original and removes temp");
        string source = Path.Combine(root, "source.jpg"), dest = Path.Combine(root, "dest.jpg");
        File.WriteAllText(source, "photo-content"); Data.CopyVerified(source, dest);
        Check(Data.Hash(source) == Data.Hash(dest) && File.Exists(source), "verified photo copy retains source");
        Reject(delegate { Data.CopyVerified(source, dest); }, "photo collision rejected");
        Check(File.ReadAllText(dest) == "photo-content", "collision does not delete existing file");
        Check(!Data.PhotoFileName(active, 1, 1, ".jpg").Contains("(1)")
            && Data.PhotoFileName(active, 2, 2, ".jpg").Contains(" (2).jpg"), "photo numbering");
        Book invalid = Data.Clone(b); invalid.Version = 999;
        Reject(delegate { Data.Validate(invalid); }, "unknown schema rejected");
        invalid = Data.Clone(b); invalid.Entries[0].Items[0].Count = 1000;
        Reject(delegate { Data.Validate(invalid); }, "invalid count rejected");
        string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a folder");
        b.Settings.CsvFolder = blocked; Data.Save(path, b);
        Reject(delegate { Data.ExportDay(b, active.Time); }, "CSV folder error reported");
        Check(Data.Load(path).PendingDays.Count == 2, "CSV failure retains saved source and pending dates");
    }
    static List<Entry> GridEntries(MainForm form, bool vessel)
    {
        List<Entry> result = new List<Entry>();
        foreach (DataGridViewRow row in Field<DataGridView>(form, vessel ? "boatGrid" : "peopleGrid").Rows)
            if (row.Tag is Entry) result.Add((Entry)row.Tag);
        return result;
    }
    static Book Current(MainForm form) { return Field<Book>(form, "book"); }
    static void Select(MainForm form, string id)
    { form.GetType().GetField("selected", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, id); }
    static Button FindButton(Control parent, string text)
    {
        foreach (Control child in parent.Controls)
        {
            Button button = child as Button; if (button != null && button.Text == text) return button;
            Button nested = FindButton(child, text); if (nested != null) return nested;
        }
        return null;
    }
    static void GridAction(MainForm form, bool vessel, string id)
    {
        DataGridView grid = Field<DataGridView>(form, vessel ? "boatGrid" : "peopleGrid");
        if (vessel)
        {
            Entry entry = Current(form).Entries.Find(delegate(Entry value) { return value.Id == id; });
            MenuAction(form, entry, "빠른 입항"); return;
        }
        foreach (DataGridViewRow row in grid.Rows)
        {
            Entry entry = row.Tag as Entry; if (entry == null || entry.Id != id) continue;
            typeof(DataGridView).GetMethod("OnCellClick", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(grid, new object[] { new DataGridViewCellEventArgs(grid.Columns["action"].Index, row.Index) });
            return;
        }
        throw new Exception("Expected action row missing");
    }
    static void UiTests()
    {
        Application.EnableVisualStyles(); string folder = Path.Combine(root, "UI"); Directory.CreateDirectory(folder);
        Book b = MakeBook(); b.Entries.Add(Make(DateTime.Now.AddMinutes(-2), false));
        b.Settings.Cameras = "카메라 A|암석지대,북방파제\n카메라 B|해변";
        b.Settings.Harbors = "북항\n남항"; b.Settings.Kinds.Add(new Kind { Id = "group", Name = "채집/해루질", Unit = "명" });
        b.Entries.Add(Make(DateTime.Today.AddDays(-1), false));
        string path = Path.Combine(folder, "Records.xml"); Data.Save(path, b);
        using (MainForm form = new MainForm(folder))
        {
            form.Show(); Application.DoEvents();
            Check(form.Width == 570 && Field<Button>(form, "saveButton").Text == "식별", "mock width and identify button");
            Check(GridEntries(form, false).Count == 1 && GridEntries(form, true).Count == 0, "separate target and vessel lists, today only");
            Field<ComboBox>(form, "camera").SelectedItem = "카메라 B";
            Check(Field<ComboBox>(form, "title").Items.Contains("해변") && !Field<ComboBox>(form, "title").Items.Contains("암석지대"), "camera-specific landmark selection");
            Field<ComboBox>(form, "camera").SelectedItem = "카메라 A";
            Field<ComboBox>(form, "title").SelectedIndex = 0;
            Field<DateTimePicker>(form, "date").Value = DateTime.Now.AddMinutes(-1);
            Field<TextBox>(form, "azimuth").Text = "3426"; Field<TextBox>(form, "elevation").Text = "-121";
            // No cart click is required: identify uses the current item directly.
            Field<Button>(form, "saveButton").PerformClick();
            Check(Current(form).Entries.Count == 3 && Current(form).Entries[2].Title == ""
                && Current(form).Entries[2].Azimuth == 3426 && Current(form).Entries[2].Elevation == -121,
                "one-click identify supports omitted landmark and optional P/T");
            string id = Current(form).Entries[2].Id;
            Check(Field<Button>(form, "saveButton").Text == "식별" && Field<List<Item>>(form, "cartItems").Count == 0,
                "successful identify returns to fresh input");
            FindButton(form, "함께 추가").PerformClick();
            Check(Field<List<Item>>(form, "cartItems").Count == 1, "add-together button creates a chip");
            ((Button)Field<FlowLayoutPanel>(form, "cart").Controls[0]).PerformClick();
            Check(Field<List<Item>>(form, "cartItems").Count == 0, "chip click removes only that item");
            FindButton(form, "＋ 비고").PerformClick();
            Check(Field<TextBox>(form, "note").Visible, "notes expand on click");
            FindButton(form, "＋ 비고").PerformClick();
            Call(form, "LoadEntry", Current(form).Entries[2]);
            Check(Field<Button>(form, "saveButton").Text == "저장", "selected target switches to save");
            SelectKind(form, "채집");
            Field<NumericUpDown>(form, "quantity").Value = 2; Call(form, "AddItem", null, EventArgs.Empty);
            SelectKind(form, "해루질");
            Field<NumericUpDown>(form, "quantity").Value = 3; Call(form, "AddItem", null, EventArgs.Empty);
            SelectKind(form, "채집");
            Field<NumericUpDown>(form, "quantity").Value = 4; Call(form, "AddItem", null, EventArgs.Empty);
            Check(Data.Load(path).Entries[2].Items.Count == 1, "cart editing does not mutate saved source");
            Call(form, "SaveEntry", null, EventArgs.Empty);
            Entry edited = Current(form).Entries.Find(delegate(Entry e) { return e.Id == id; });
            Check(Current(form).Entries.Count == 3 && edited.Items.Count == 3 && edited.Items[1].Count == 6
                && edited.Items[2].Count == 3, "save keeps ID and combines each child separately");
            Select(form, id); GridAction(form, false, id);
            Check(Current(form).Entries[2].Complete && Current(form).Entries[2].FinishedAt.HasValue
                && GridEntries(form, false).Count == 1, "row withdrawal updates time and active filter");
            Field<CheckBox>(form, "peopleDone").Checked = true;
            Check(Field<CheckBox>(form, "peopleAll").Checked && !Field<CheckBox>(form, "peopleActive").Checked,
                "both status choices normalize to all");
            Field<CheckBox>(form, "peopleAll").Checked = false;
            Check(Field<CheckBox>(form, "peopleAll").Checked && GridEntries(form, false).Count == 2,
                "last unchecked status falls back to all");
            // Undo withdrawal preserves changes to unrelated book metadata made afterward.
            Current(form).Help = "new help";
            Field<Button>(form, "undoButton").PerformClick();
            Check(!Current(form).Entries.Find(delegate(Entry e) { return e.Id == id; }).Complete
                && Current(form).Help == "new help", "undo restores records and preserves latest metadata");
            Field<ComboBox>(form, "harbor").SelectedItem = "북항";
            Field<ComboBox>(form, "harbor").Text = "북항";
            Field<ComboBox>(form, "vesselType").Text = "어선";
            Field<DateTimePicker>(form, "boatTime").Value = DateTime.Now.AddMinutes(-1);
            FindButton(form, "출항").PerformClick();
            Entry departure = Current(form).Entries[3]; string departureId = departure.Id;
            Check(departure.VesselName == "" && departure.VesselNumber == "" && !departure.Complete
                && GridEntries(form, true).Count == 1, "unnamed departure is accepted and shown as outbound");
            Select(form, departureId); GridAction(form, true, departureId);
            Entry arrived = Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; });
            Check(Current(form).Entries.Count == 4 && arrived.Complete && arrived.FinishedAt.HasValue
                && GridEntries(form, true).Count == 0 && arrived.Movement == "출항", "arrival links to original departure without new row");
            Field<CheckBox>(form, "boatDone").Checked = true;
            Check(GridEntries(form, true).Count == 1 && GridEntries(form, false).Count == 2, "vessel and target filters are independent");
            Call(form, "UndoLast", null, EventArgs.Empty);
            Check(!Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; }).Complete,
                "undo linked arrival restores outbound state");
            Field<DateTimePicker>(form, "boatTime").Value = DateTime.Now.AddMinutes(-1);
            FindButton(form, "입항").PerformClick();
            Check(Current(form).Entries.Count == 5 && Current(form).Entries[4].Movement == "입항"
                && Current(form).Entries[4].FinishedAt.HasValue, "standalone arrival remains available");
            Entry standalone = Current(form).Entries[4];
            using (RecordEditDialog edit = new RecordEditDialog(FindCopy(Current(form), standalone.Id), Current(form).Settings))
            {
                Field<TextBox>(edit, "name").Text = "수정 선박"; Field<TextBox>(edit, "number").Text = "ID-12";
                Call(edit, "Save", null, EventArgs.Empty);
                Check(edit.DialogResult == DialogResult.OK && edit.Value.VesselNumber == "ID-12",
                    "vessel details dialog supports separate name and number");
            }
            Field<Button>(form, "peopleHeader").PerformClick(); Field<Button>(form, "boatHeader").PerformClick();
            Check(!Field<Panel>(form, "peoplePanel").Controls["body"].Visible && !Field<Panel>(form, "boatPanel").Controls["body"].Visible,
                "both lists can collapse");
            Field<Button>(form, "peopleHeader").PerformClick(); Field<Button>(form, "boatHeader").PerformClick();
            Check(Field<Panel>(form, "peoplePanel").Controls["body"].Visible && Field<Panel>(form, "boatPanel").Controls["body"].Visible,
                "collapsed lists reopen");
            using (ConfigDialog config = new ConfigDialog(Current(form).Settings))
            {
                config.Show(form); Application.DoEvents();
                Call(config, "Save", null, EventArgs.Empty);
                Check(config.Value != null && Data.Cameras(config.Value)[0].Name == "카메라 A" && Data.Lines(config.Value.Harbors)[0] == "북항"
                    && config.Value.Kinds[0].Id == "kind", "settings preserve camera associations, harbor order and kind IDs");
                config.Close();
            }
            ReviewUiTests(form, path, departureId);
            NewUiTests(form, departureId);
            using (HistoryDialog history = new HistoryDialog(Current(form), false))
            {
                history.Show(form); Application.DoEvents();
                Field<DateTimePicker>(history, "from").Value = DateTime.Today.AddDays(-6);
                Check(Field<ListView>(history, "records").Items.Count == 3, "target history includes prior day and excludes vessels");
                history.Close();
            }
            using (HistoryDialog history = new HistoryDialog(Current(form), true))
            {
                history.Show(form); Application.DoEvents();
                Field<DateTimePicker>(history, "from").Value = DateTime.Today.AddDays(-6);
                Check(Field<ListView>(history, "records").Items.Count == 2
                    && !history.Text.Contains("식별표적"), "vessel history excludes targets");
                bool hasPort = false, hasCamera = false;
                foreach (ColumnHeader column in Field<ListView>(history, "records").Columns)
                { hasPort |= column.Text == "입항항"; hasCamera |= column.Text == "카메라"; }
                Check(hasPort && !hasCamera, "vessel query uses vessel columns");
                history.Close();
            }
            Call(form, "ResetEditor");
            form.Close();
        }
    }
    static void SelectKind(MainForm form, string name)
    {
        ComboBox box = Field<ComboBox>(form, "kind");
        for (int i = 0; i < box.Items.Count; i++) if (box.Items[i].ToString() == name) { box.SelectedIndex = i; return; }
        throw new Exception("Kind not found: " + name);
    }
    static void MenuAction(MainForm form, Entry entry, string action)
    {
        Control owner = Field<DataGridView>(form, entry.Category == "Vessel" ? "boatGrid" : "peopleGrid");
        Call(form, "ShowRecordMenu", owner, entry, new Point(5, 5));
        ContextMenuStrip menu = Field<ContextMenuStrip>(form, "recordMenu"); menu.Close();
        foreach (ToolStripItem item in menu.Items) if (item.Text == action) { item.PerformClick(); Application.DoEvents(); return; }
        throw new Exception("Menu action missing: " + action);
    }
    static void ReviewUiTests(MainForm form, string path, string departureId)
    {
        FlowLayoutPanel page = Field<FlowLayoutPanel>(form, "page");
        Check(page.Controls[0] == Field<TableLayoutPanel>(form, "targetCard")
            && page.Controls[1] == Field<Panel>(form, "peoplePanel") && page.Controls[3] == Field<Panel>(form, "boatPanel"),
            "page order is target input/list then vessel input/list");
        Check(Field<DateTimePicker>(form, "date").CustomFormat == "HH:mm"
            && Field<DateTimePicker>(form, "boatTime").CustomFormat == "HH:mm"
            && Field<DateTimePicker>(form, "targetDay").Font.Size >= 12, "large separate dates and time-only inputs");
        Check(form.GetType().GetField("subkind", BindingFlags.NonPublic | BindingFlags.Instance) == null,
            "no separate subtype control");
        SelectKind(form, "해루질"); Check(Field<ComboBox>(form, "kind").SelectedItem is KindChoice,
            "slash children appear directly in the main type dropdown");
        Call(form, "ResetEditor");
        int height = Field<Panel>(form, "peoplePanel").Height;
        Panel grip = (Panel)Field<Panel>(form, "peoplePanel").Controls["peopleResize"];
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip,
            new object[] { new MouseEventArgs(MouseButtons.Left, 1, 2, 2, 0) });
        typeof(Control).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip,
            new object[] { new MouseEventArgs(MouseButtons.Left, 0, 2, 32, 0) });
        typeof(Control).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grip,
            new object[] { new MouseEventArgs(MouseButtons.Left, 1, 2, 32, 0) });
        Check(Field<Panel>(form, "peoplePanel").Height == height + 30, "drag grip changes target list height");
        Call(form, "ResizeList", false, 150); Call(form, "ResizeList", true, 340);
        Check(Field<Panel>(form, "boatPanel").Height == 340 && Field<Panel>(form, "peoplePanel").Height == 150,
            "list heights can be adjusted independently");
        DataGridView grid = Field<DataGridView>(form, "boatGrid");
        Check(!grid.Columns.Contains("action") && !grid.Columns.Contains("menu") && grid.Columns.Count == 6,
            "vessel grid has no processing column");
        Button settings = FindButton(form, "설정 ⋯");
        for (int i = 0; i < 12; i++)
        {
            settings.PerformClick(); Application.DoEvents(); Field<ContextMenuStrip>(form, "toolsMenu").Close();
        }
        Check(!Field<ContextMenuStrip>(form, "toolsMenu").IsDisposed, "tools menu survives repeated open/close");
        Entry departure = FindCopy(Current(form), departureId); departure.Time = DateTime.Now.AddHours(-1);
        Check((bool)form.GetType().GetMethod("StoreEntry", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(form, new object[] { departure, "test departure time" }), "prepare older departure");
        for (int i = 0; i < 12; i++)
        {
            Call(form, "ShowRecordMenu", grid, departure, new Point(5, 5));
            Field<ContextMenuStrip>(form, "recordMenu").Close(); Application.DoEvents();
        }
        Check(!Field<ContextMenuStrip>(form, "recordMenu").IsDisposed, "record context menu survives repeated close");
        MenuAction(form, departure, "입항 처리");
        Check(Field<string>(form, "arrivalId") == departureId, "context action expands arrival controls for selected vessel");
        Field<DateTimePicker>(form, "arrivalDay").Value = departure.Time.Date;
        Field<DateTimePicker>(form, "arrivalTime").Value = departure.Time.AddMinutes(-1);
        Call(form, "CompleteArrival", "남항");
        Check(!Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; }).Complete
            && Field<string>(form, "arrivalId") == departureId, "invalid arrival time keeps draft and saved vessel unchanged");
        Field<DateTimePicker>(form, "arrivalTime").Value = DateTime.Now.AddMinutes(-10);
        FindButton(Field<FlowLayoutPanel>(form, "arrivalChoices"), "남항").PerformClick();
        Entry arrived = Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; });
        Check(arrived.Complete && arrived.Title == "북항" && arrived.ArrivalPlace() == "남항"
            && arrived.FinishedAt.Value < DateTime.Now.AddMinutes(-5), "preset arrival stores another harbor and earlier time without changing departure");
        Check(Data.Load(path).Entries.Find(delegate(Entry e) { return e.Id == departureId; }).ArrivalHarbor == "남항",
            "arrival harbor is persisted");
        Call(form, "UndoLast", null, EventArgs.Empty); departure = FindCopy(Current(form), departureId);
        MenuAction(form, departure, "입항 처리");
        FindButton(Field<FlowLayoutPanel>(form, "arrivalChoices"), "직접입력").PerformClick();
        Field<TextBox>(form, "arrivalManual").Text = "외부 포구";
        Field<DateTimePicker>(form, "arrivalTime").Value = DateTime.Now.AddMinutes(10);
        Field<Button>(form, "arrivalConfirm").PerformClick();
        Check(Field<TextBox>(form, "arrivalManual").Text == "외부 포구"
            && !Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; }).Complete,
            "future arrival rejected while manual port input is retained");
        Field<DateTimePicker>(form, "arrivalTime").Value = DateTime.Now.AddMinutes(-12);
        Field<Button>(form, "arrivalConfirm").PerformClick();
        arrived = Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; });
        Check(arrived.ArrivalPlace() == "외부 포구" && arrived.Title == "북항", "unconfigured port can be entered inline");
        Select(form, departureId); Call(form, "RestoreSelected", null, EventArgs.Empty);
        departure = FindCopy(Current(form), departureId); MenuAction(form, departure, "빠른 입항");
        arrived = Current(form).Entries.Find(delegate(Entry e) { return e.Id == departureId; });
        Check(arrived.ArrivalPlace() == "북항" && arrived.FinishedAt.Value > DateTime.Now.AddMinutes(-1),
            "quick arrival uses current time and original departure harbor");
        Call(form, "ResetEditor");
        Field<DateTimePicker>(form, "targetDay").Value = DateTime.Today.AddDays(-3);
        Field<DateTimePicker>(form, "date").Value = DateTime.Today.AddHours(10);
        Check(Field<DateTimePicker>(form, "boatDay").Value.Date == DateTime.Today, "target date does not change vessel draft date");
        Call(form, "ResetEditor");
    }

    static Entry FindCopy(Book b, string id) { return Data.Clone(b).Entries.Find(delegate(Entry e) { return e.Id == id; }); }

    static void MockDomainTests()
    {
        Check(Data.OptionalAngle("", 0, 6400) == null && Data.OptionalAngle("0", 0, 6400) == 0,
            "empty angle and measured zero remain distinct");
        Reject(delegate { Data.OptionalAngle("1.5", 0, 6400); }, "fractional angle rejected");
        Reject(delegate { Data.OptionalAngle("6401", 0, 6400); }, "text angle range checked");
        Settings settings = MakeBook().Settings; settings.Cameras = "A|one,two\nB|three";
        List<CameraSpec> cameras = Data.Cameras(settings);
        Check(cameras[0].Name == "A" && cameras[0].Places[1] == "two" && cameras[1].Places[0] == "three", "camera map order and associations");
        settings.Cameras = "A"; settings.Places = "shared";
        Check(Data.Cameras(settings)[0].Places[0] == "shared", "old camera format uses common places");
        settings.Cameras = "A|one\nA|two";
        Reject(delegate { Data.Cameras(settings); }, "duplicate camera names rejected");
        Book b = MakeBook(); Entry vessel = new Entry { Id = Guid.NewGuid().ToString("N"), Category = "Vessel", Time = DateTime.Now.AddHours(-1),
            Title = "북항", Movement = "출항", VesselType = "어선", VesselNumber = "BOAT-12" };
        b.Entries.Add(vessel); Data.Validate(b); Data.Finish(vessel, DateTime.Now);
        Check(vessel.Complete && vessel.FinishedAt.HasValue && vessel.StateText() == "입항" && vessel.Movement == "출항", "departure retains original event and links arrival timestamp");
        Data.Reopen(vessel); Check(!vessel.Complete && !vessel.FinishedAt.HasValue && vessel.StateText() == "출항중", "arrival state can be reopened");
        Reject(delegate { Data.Finish(vessel, vessel.Time.AddMinutes(-1)); }, "arrival before departure rejected");
        Reject(delegate { Data.Finish(vessel, DateTime.Now.AddMinutes(5)); }, "future arrival rejected");
        Entry alone = new Entry { Id = Guid.NewGuid().ToString("N"), Category = "Vessel", Time = DateTime.Today, FinishedAt = DateTime.Today,
            Complete = true, Title = "남항", Movement = "입항", VesselType = "레저보트" };
        b.Entries.Add(alone); Data.Validate(b);
        Reject(delegate { Data.Reopen(alone); }, "standalone arrival cannot invent departure");
        string path = Path.Combine(root, "query.csv"); Data.ExportEntries(b, new List<Entry> { vessel }, path);
        Check(ReadCsv(File.ReadAllText(path)).Count == 2 && File.ReadAllText(path).Contains("BOAT-12") && !File.ReadAllText(path).Contains(alone.Id),
            "query CSV contains only selected entries and vessel number");
        Reject(delegate { Data.ExportEntries(b, b.Entries, Path.Combine(root, "Records.xml")); }, "CSV export cannot overwrite an XML source");
        // Simulate the exact old v2 semantics: Complete meant an independent event had been recorded.
        Book old = MakeBook(); old.Version = 2; Entry oldVessel = new Entry { Id = Guid.NewGuid().ToString("N"), Category = "Vessel", Time = DateTime.Today,
            Title = "기존항", Movement = "출항", VesselType = "어선", VesselName = "기존선박", Complete = true };
        old.Entries.Add(oldVessel); string xml = Path.Combine(root, "v2.xml");
        using (FileStream stream = File.Create(xml)) new XmlSerializer(typeof(Book)).Serialize(stream, old);
        Book imported = Data.Load(xml);
        Check(imported.Entries[0].IndependentMovement && imported.Entries[0].StateText() == "출항(기존)" && imported.Entries[0].Complete,
            "legacy independent departure does not fabricate current outbound status");
        Data.Reopen(imported.Entries[0]); Data.Save(xml, imported);
        Check(Data.Load(xml).Version == Data.CurrentVersion && !Data.Load(xml).Entries[0].IndependentMovement
            && Directory.GetFiles(root, "v2.xml.v2-*.bak").Length == 1, "explicit legacy reopen survives v2 upgrade and backup");
        Check(PhotoAssignDialog.Preview(vessel, new string[] { "a.jpg", "b.png" }).Contains(" (2).png")
            && PhotoAssignDialog.Preview(vessel, new string[] { "a.jpg" }).Contains(Data.PhotoFileName(vessel, 1, 1, ".jpg")),
            "photo preview follows actual one/multiple-file naming");
    }

    static void ReviewDomainTests()
    {
        Book b = MakeBook(); Entry target = Make(DateTime.Today, false); target.Camera = "ONLY_TARGET"; b.Entries.Add(target);
        Entry ship = new Entry { Id = Guid.NewGuid().ToString("N"), Time = DateTime.Today.AddHours(1), Title = "A항", Category = "Vessel",
            Movement = "출항", VesselType = "어선", VesselNumber = "ONLY_VESSEL" }; b.Entries.Add(ship);
        DateTime at = DateTime.Today.AddHours(2); Data.Arrive(ship, at, "B항");
        Check(ship.Title == "A항" && ship.ArrivalHarbor == "B항" && ship.FinishedAt == at && ship.Report().Contains("B항"),
            "arrival keeps independent departure/arrival ports and report");
        string xml = Path.Combine(root, "arrival.xml"); Data.Save(xml, b);
        Check(Data.Load(xml).Entries[1].ArrivalHarbor == "B항", "arrival port roundtrip");
        string targets = Data.ExportDay(b, DateTime.Today); string ships = Data.CsvDayPath(b, DateTime.Today, true);
        string tc = File.ReadAllText(targets), sc = File.ReadAllText(ships);
        Check(Path.GetFileName(targets) == "Targets.csv" && Path.GetFileName(ships) == "Vessels.csv"
            && tc.Contains("ONLY_TARGET") && !tc.Contains("ONLY_VESSEL") && sc.Contains("ONLY_VESSEL") && !sc.Contains("ONLY_TARGET"),
            "daily CSV files are physically separate by domain");
        Check(!tc.Contains("선종") && !sc.Contains("방위각") && sc.Contains("입항항") && sc.Contains("B항"),
            "each CSV uses only its own domain columns");
        Reject(delegate { Data.ExportEntries(b, b.Entries, Path.Combine(root, "mixed.csv"), true); }, "mixed records refused by vessel export");
        Data.Reopen(ship); Check(ship.ArrivalHarbor == "" && ship.ArrivalPlace() == "", "reopening clears arrival port");
        Reject(delegate { Data.Arrive(ship, at, " "); }, "blank arrival harbor rejected");
        Check(!ship.Complete && !ship.FinishedAt.HasValue, "invalid arrival does not mutate entry");
        b.Version = 3; ship.Complete = true; ship.FinishedAt = at; ship.ArrivalHarbor = "";
        string legacy = Path.Combine(root, "v3.xml"); using (FileStream f = File.Create(legacy)) new XmlSerializer(typeof(Book)).Serialize(f, b);
        Book old = Data.Load(legacy); Check(old.Entries[1].ArrivalPlace() == "A항", "old same-harbor arrival remains readable");
        Data.Save(legacy, old); Check(Data.Load(legacy).Version == Data.CurrentVersion && Directory.GetFiles(root, "v3.xml.v3-*.bak").Length == 1,
            "v3 source backed up before arrival-port upgrade");
    }

    class FakeMacroInput : IMacroInput
    {
        public bool Available { get; set; }
        public bool LeftDown { get; set; }
        public bool EscapeDown { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int DesktopLeft { get; set; }
        public int DesktopTop { get; set; }
        public int DesktopWidth { get; set; }
        public int DesktopHeight { get; set; }
        public List<Point> Clicks = new List<Point>();
        public bool FailClick;
        public FakeMacroInput() { Available = true; DesktopLeft = -200; DesktopWidth = 1200; DesktopHeight = 800; }
        public bool Contains(int x, int y) { return new Rectangle(DesktopLeft, DesktopTop, DesktopWidth, DesktopHeight).Contains(x, y); }
        public void Click(int x, int y) { if (FailClick) throw new IOException("simulated input failure"); Clicks.Add(new Point(x, y)); }
    }
    static void SettingsAndMacroTests()
    {
        Book b = MakeBook(); SolarSettings solar = b.Settings.Solar;
        solar.Date = DateTime.Today; solar.Bmnt = "05:00"; solar.Sunrise = "06:00"; solar.Sunset = "18:00"; solar.Eent = "19:00";
        solar.Enabled = true; solar.SunriseOffset = -10; solar.SunsetOffset = 20;
        Check(Data.DueSolar(b, DateTime.Today.AddHours(5).AddMinutes(49)).Count == 0, "solar alarm waits until configured offset");
        List<SolarAlarm> due = Data.DueSolar(b, DateTime.Today.AddHours(5).AddMinutes(50));
        Check(due.Count == 1 && due[0].At.Hour == 5 && due[0].At.Minute == 50, "sunrise offset produces due alarm");
        b.AlarmAcknowledgements.Add(due[0].Key);
        Check(Data.DueSolar(b, DateTime.Today.AddHours(7)).Count == 0, "acknowledged solar alarm does not repeat");
        Check(Data.DueSolar(b, DateTime.Today.AddHours(18).AddMinutes(20)).Count == 1, "sunset alarm has independent acknowledgment");
        solar.Date = DateTime.Today.AddDays(-1);
        Check(Data.DueSolar(b, DateTime.Today.AddHours(20)).Count == 0, "previous day solar times are not reused as today's times");
        solar.Sunset = "23:30"; solar.SunsetOffset = 60;
        Check(Data.DueSolar(b, DateTime.Today.AddHours(1)).Count == 1, "solar offset crossing midnight uses actual alarm date");
        solar.SunriseOffset = 721; Reject(delegate { Data.ValidateSolar(solar); }, "solar offset bounds validated");
        solar.SunriseOffset = 0; solar.Sunrise = "25:00"; Reject(delegate { Data.ValidateSolar(solar); }, "invalid solar clock rejected");
        solar.Enabled = false; solar.Sunrise = "06:00";
        Check(Data.SolarAlarms(solar).Count == 0 && solar.Bmnt == "05:00" && solar.Eent == "19:00", "disabled alarms retain all four reference times");
        b.Settings.StructuredCameras = true; b.Settings.Cameras = "obsolete|not used"; b.Settings.Places = "obsolete shared";
        b.Settings.CameraDefinitions.Add(new CameraSpec { Name = "카메라 A|동측", Places = new List<string> { "방파제, 남단", "해변" } });
        Check(Data.Cameras(b.Settings)[0].Places[0] == "방파제, 남단", "structured cameras do not require delimiter syntax");
        FakeMacroInput input = new FakeMacroInput(); MacroEngine engine = new MacroEngine(input);
        input.LeftDown = true; engine.StartRecording("슬롯 1", 500, 0); engine.Tick(10);
        Check(engine.Profile.Steps.Count == 0, "held record-start click is not recorded");
        input.LeftDown = false; engine.Tick(20); input.X = -100; input.Y = 100; input.LeftDown = true; engine.Tick(100); engine.Tick(120);
        Check(engine.Profile.Steps.Count == 1 && engine.Profile.Steps[0].X == -100, "one point per press with multi-monitor negative coordinates");
        input.LeftDown = false; engine.Tick(140); input.X = 200; input.Y = 300; input.LeftDown = true; engine.Tick(400);
        input.EscapeDown = true; engine.Tick(420);
        Check(engine.Mode == MacroMode.Idle && engine.RecordingFinished && engine.EscapeHeld && engine.Profile.Steps[0].DelayMs == 300,
            "ESC ends recording and measured click interval is retained");
        input.EscapeDown = false; input.LeftDown = false;
        MacroProfile recorded = Data.CloneMacro(engine.Profile); engine.ConsumeRecording();
        b.Settings.Macros[0] = recorded;
        b.Settings.Macros[1] = new MacroProfile { Name = "슬롯 2", Steps = new List<MacroStep> { new MacroStep { X = 500, Y = 100, DelayMs = 0 } } };
        string path = Path.Combine(root, "macro-settings.xml"); Data.Save(path, b); Book read = Data.Load(path);
        Check(read.Settings.Macros.Length == 2 && read.Settings.Macros[0].Steps.Count == 2 && read.Settings.Macros[1].Steps.Count == 1,
            "both macro slots survive XML roundtrip without collection duplication");
        Check(Data.Cameras(read.Settings)[0].Name == "카메라 A|동측" && Data.Cameras(read.Settings)[0].Places[0] == "방파제, 남단",
            "camera names and landmarks survive structured XML roundtrip");
        MacroProfile copy = Data.CloneMacro(recorded); copy.Steps[0].X = 999;
        Check(recorded.Steps[0].X == -100, "macro editor copy cannot mutate stored slot");
        engine.StartPlaying(recorded, 0); engine.Tick(0); engine.Tick(299);
        Check(input.Clicks.Count == 1, "playback waits between clicks");
        engine.Tick(300); Check(input.Clicks.Count == 2 && input.Clicks[1] == new Point(200, 300), "playback executes recorded click order at due time");
        engine.Tick(799); Check(engine.Mode == MacroMode.Playing, "last configured delay is respected"); engine.Tick(800);
        Check(engine.Mode == MacroMode.Idle, "playback completes without blocking thread sleeps");
        input.Clicks.Clear(); engine.StartPlaying(b.Settings.Macros[1], 1000); engine.Tick(1000); engine.Tick(1001);
        Check(input.Clicks.Count == 1 && input.Clicks[0].X == 500, "second slot plays independently");
        engine.StartPlaying(recorded, 0); engine.Tick(0); input.EscapeDown = true; engine.Tick(10); input.EscapeDown = false; engine.Tick(10000);
        Check(engine.Mode == MacroMode.Idle && input.Clicks.Count == 2, "ESC cancels playback before later clicks");
        input.DesktopWidth = 800; Reject(delegate { engine.StartPlaying(recorded, 0); }, "changed desktop layout rejected before playback"); input.DesktopWidth = 1200;
        MacroProfile invalid = new MacroProfile { Steps = new List<MacroStep> { new MacroStep { X = 99999, Y = 0 } } };
        Reject(delegate { engine.StartPlaying(invalid, 0); }, "offscreen macro point rejected");
        invalid.Steps[0].DelayMs = -1; Reject(delegate { Data.ValidateMacro(invalid); }, "negative delay rejected");
        input.FailClick = true; engine.StartPlaying(recorded, 0); Reject(delegate { engine.Tick(0); }, "input injection failure is surfaced"); engine.Stop(false);
        input.FailClick = false; input.Available = false; Reject(delegate { engine.StartRecording("", 500, 0); }, "unsupported native recording fails clearly");
    }

    static void NewUiTests(MainForm form, string departureId)
    {
        Call(form, "ResetEditor"); Call(form, "ResetBoat");
        DateTime now = DateTime.Today.AddHours(12).AddMinutes(20);
        Call(form, "AdvanceClock", now);
        Check(Field<DateTimePicker>(form, "date").Value.Hour == 12 && Field<DateTimePicker>(form, "date").Value.Minute == 20,
            "untouched new-entry time follows live clock");
        Field<DateTimePicker>(form, "date").Value = now.AddHours(-1); Call(form, "AdvanceClock", now.AddMinutes(5));
        Check(Field<DateTimePicker>(form, "date").Value.Hour == 11, "manual time is preserved during live refresh");
        Call(form, "SetCurrentTime", false, now.AddMinutes(5));
        Check(Field<DateTimePicker>(form, "date").Value.Minute == 25, "current-time action replaces manually edited time");
        Call(form, "ResetEditor"); Call(form, "ResetBoat");
        Field<TextBox>(form, "note").Text = "draft survives midnight"; int count = Current(form).Entries.Count;
        Call(form, "AdvanceClock", DateTime.Today.AddDays(1));
        Check(Field<DateTimePicker>(form, "targetDay").Value.Date == DateTime.Today.AddDays(1)
            && Field<DateTimePicker>(form, "boatDay").Value.Date == DateTime.Today.AddDays(1)
            && Field<TextBox>(form, "note").Text == "draft survives midnight" && Current(form).Entries.Count == count,
            "midnight advances new dates without cloning records or losing draft");
        Call(form, "ResetEditor"); Field<DateTimePicker>(form, "targetDay").Value = DateTime.Today.AddDays(-3);
        Call(form, "AdvanceClock", DateTime.Today.AddDays(1));
        Check(Field<DateTimePicker>(form, "targetDay").Value.Date == DateTime.Today.AddDays(-3), "explicit historical date stays pinned");
        Call(form, "ResetEditor"); Call(form, "ResetBoat");
        using (ConfigDialog config = new ConfigDialog(Current(form).Settings))
        {
            config.Show(form); Application.DoEvents();
            Check(Field<TabControl>(config, "tabs").TabPages.Count == 5, "settings are separated into five clear tabs");
            Check(config.GetType().GetField("places", BindingFlags.NonPublic | BindingFlags.Instance) == null, "no common-landmark input in settings");
            List<CameraCardEditor> cards = Field<List<CameraCardEditor>>(config, "cameras");
            cards[0].Places.AddValue("새 지형지물, 동측");
            TextListEditor ports = Field<TextListEditor>(config, "harbors"); ports.MoveValue(ports.Inputs[0], 1);
            Call(config, "Save", null, EventArgs.Empty);
            Check(config.Value != null && config.Value.StructuredCameras && Data.Cameras(config.Value)[0].Places.Contains("새 지형지물, 동측")
                && Data.Lines(config.Value.Harbors)[0] == "남항", "camera cards and reordered harbors save without text delimiters");
            config.Close();
        }
        DataGridView grid = Field<DataGridView>(form, "boatGrid");
        DataGridViewRow numberRow = null;
        foreach (DataGridViewRow row in grid.Rows) if (row.Tag is Entry && ((Entry)row.Tag).Id == departureId) numberRow = row;
        grid.CurrentCell = numberRow.Cells["c1"]; grid.BeginEdit(true); Application.DoEvents();
        TextBox input = grid.EditingControl as TextBox;
        Check(input != null, "vessel number cell enters text editing directly"); input.Text = "RADAR-18";
        Call(form, "RefreshRecords"); Check(grid.IsCurrentCellInEditMode && ((TextBox)grid.EditingControl).Text == "RADAR-18",
            "list refresh preserves active number edit");
        grid.EndEdit(); Application.DoEvents();
        Entry saved = Current(form).Entries.Find(delegate(Entry entry) { return entry.Id == departureId; });
        Check(saved.VesselNumber == "RADAR-18", "inline vessel number persists without details dialog");
        numberRow = null; foreach (DataGridViewRow row in grid.Rows) if (row.Tag is Entry && ((Entry)row.Tag).Id == departureId) numberRow = row;
        grid.CurrentCell = numberRow.Cells["c1"]; grid.BeginEdit(true); Application.DoEvents(); input = (TextBox)grid.EditingControl; input.Text = "cancel this";
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new object[] { new KeyEventArgs(Keys.Escape) });
        Application.DoEvents();
        Check(Current(form).Entries.Find(delegate(Entry entry) { return entry.Id == departureId; }).VesselNumber == "RADAR-18", "ESC cancels inline number edit");
        numberRow = null; foreach (DataGridViewRow row in grid.Rows) if (row.Tag is Entry && ((Entry)row.Tag).Id == departureId) numberRow = row;
        grid.CurrentCell = numberRow.Cells["c1"]; grid.BeginEdit(true); Application.DoEvents(); ((TextBox)grid.EditingControl).Text = "RADAR-19";
        FormClosingEventArgs close = new FormClosingEventArgs(CloseReason.UserClosing, false);
        Call(form, "ConfirmClosing", null, close); Application.DoEvents();
        Check(!close.Cancel && Current(form).Entries.Find(delegate(Entry entry) { return entry.Id == departureId; }).VesselNumber == "RADAR-19",
            "closing commits an active vessel number edit before leaving");
        using (MacroEditDialog dialog = new MacroEditDialog(new MacroProfile { Name = "slot", Steps = new List<MacroStep> {
            new MacroStep { X = 10, Y = 20, DelayMs = 100 }, new MacroStep { X = 30, Y = 40, DelayMs = 200 } } }, 0))
        {
            dialog.Show(form); Application.DoEvents(); Field<NumericUpDown>(dialog, "delay").Value = 750;
            FindButton(dialog, "전체에 적용").PerformClick(); Call(dialog, "Save", null, EventArgs.Empty);
            Check(dialog.Value != null && dialog.Value.Steps[0].DelayMs == 750 && dialog.Value.Steps[1].DelayMs == 750,
                "macro delay editor applies delay to each click"); dialog.Close();
        }
        Check(Field<Button>(form, "macroOne").Visible && Field<Button>(form, "macroTwo").Visible,
            "both macro playback buttons are always visible");
        Call(form, "ResetEditor"); Call(form, "ResetBoat");
    }

    static List<string[]> ReadCsv(string text)
    {
        List<string[]> rows = new List<string[]>(); List<string> row = new List<string>();
        StringBuilder cell = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (!quoted && (c == ',' || c == '\n'))
            {
                row.Add(cell.ToString().TrimEnd('\r')); cell.Length = 0;
                if (c == '\n') { rows.Add(row.ToArray()); row.Clear(); }
            }
            else cell.Append(c);
        }
        return rows;
    }
    static void DomainTests()
    {
        Book b = MakeBook(); b.Settings.Kinds.Clear();
        b.Settings.Kinds.Add(new Kind { Id = "group", Name = "채집/해루질", Unit = "명" });
        Entry e = Make(DateTime.Today, false); e.Category = "Target"; e.Items.Clear();
        e.Items.Add(new Item { KindId = "group", Name = "채집/해루질", SubName = "채집", Unit = "명", Count = 2 });
        e.Items.Add(new Item { KindId = "group", Name = "채집/해루질", SubName = "해루질", Unit = "명", Count = 3 });
        e.Items.Add(new Item { KindId = "group", Name = "채집/해루질", Unit = "명", Count = 4 }); b.Entries.Add(e);
        Data.Validate(b); Check(e.Totals() == "채집/해루질 9명", "parent total includes known and legacy unclassified children");
        string xml = Path.Combine(root, "children.xml"); Data.Save(xml, b); Book loaded = Data.Load(xml);
        Check(loaded.Entries[0].Items[1].SubName == "해루질", "child choices roundtrip");
        List<string[]> rows = ReadCsv(File.ReadAllText(Data.ExportDay(b, DateTime.Today)));
        string[] expected = { "9", "4", "2", "3" };
        bool correct = rows.Count == 2 && rows[0].Length == rows[1].Length;
        string[] columns = { "채집/해루질 [group] 합계", "채집/해루질 > 미분류(기존 기록) [group] 수량",
            "채집/해루질 > 채집 [group] 수량", "채집/해루질 > 해루질 [group] 수량" };
        for (int i = 0; i < expected.Length; i++) { int column = Array.IndexOf(rows[0], columns[i]); correct &= column >= 0 && rows[1][column] == expected[i]; }
        Check(correct, "CSV parent sum and classified/unclassified child columns agree");
        b.Settings.Kinds.Clear(); b.Settings.Kinds.Add(new Kind { Id = "new", Name = "차량", Unit = "대" });
        string csv = File.ReadAllText(Data.ExportDay(b, DateTime.Today));
        Check(csv.Contains("채집/해루질 > 해루질 [group] 수량"), "deleted configuration does not remove historical child columns");
        Book bad = Data.Clone(b); bad.Entries[0].Items.Add(bad.Entries[0].Items[0]);
        Reject(delegate { Data.Validate(bad); }, "duplicate same child rejected");
        bad = Data.Clone(b); bad.Entries[0].FinishedAt = DateTime.Today.AddMinutes(-1); bad.Entries[0].Complete = true;
        Reject(delegate { Data.Validate(bad); }, "withdrawal before identification rejected");
        e.Complete = true; e.FinishedAt = e.Time.AddHours(1); Data.Save(xml, b);
        Check(Data.Load(xml).Entries[0].FinishedAt == e.Time.AddHours(1) && e.StateText() == "철수", "target completion timestamp roundtrip");
        Entry vessel = new Entry { Id = Guid.NewGuid().ToString("N"), Time = DateTime.Today,
            Title = "테스트항", Category = "Vessel", Complete = true, VesselType = "어선", VesselName = "테스트선박", Movement = "입항", FinishedAt = DateTime.Today };
        b.Entries.Add(vessel); Data.Save(xml, b);
        Check(Data.Load(xml).Entries[1].StateText() == "입항" && vessel.Describe().Contains("테스트선박"), "vessel event roundtrip");
        Check(Data.SelectEntries(b, DateTime.Today, DateTime.Today, true, true, "선박").Count == 1,
            "vessel category search");
        vessel.Movement = "자동매칭"; Reject(delegate { Data.Validate(b); }, "unsupported vessel state rejected");
        Settings settings = MakeBook().Settings; settings.Kinds[0].Name = "채집//해루질";
        Reject(delegate { ConfigDialog.Validate(settings); }, "empty slash choice rejected");
        settings.Kinds[0].Name = "채집/채집";
        Reject(delegate { ConfigDialog.Validate(settings); }, "duplicate slash choice rejected");
    }
}
