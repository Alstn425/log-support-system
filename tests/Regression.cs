using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
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
            Field<ComboBox>(form, "kind").SelectedIndex = 1;
            Field<ComboBox>(form, "subkind").SelectedItem = "채집";
            Field<NumericUpDown>(form, "quantity").Value = 2; Call(form, "AddItem", null, EventArgs.Empty);
            Field<ComboBox>(form, "subkind").SelectedItem = "해루질";
            Field<NumericUpDown>(form, "quantity").Value = 3; Call(form, "AddItem", null, EventArgs.Empty);
            Field<ComboBox>(form, "subkind").SelectedItem = "채집";
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
                Check(config.Value.Cameras == Current(form).Settings.Cameras && config.Value.Harbors == "북항\n남항"
                    && config.Value.Kinds[0].Id == "kind", "settings preserve camera associations, harbor order and kind IDs");
                config.Close();
            }
            using (HistoryDialog history = new HistoryDialog(Current(form)))
            {
                history.Show(form); Application.DoEvents();
                Field<DateTimePicker>(history, "from").Value = DateTime.Today.AddDays(-6);
                Check(Field<ListView>(history, "records").Items.Count == 5, "history includes prior day and both domains");
                history.Close();
            }
            form.Close();
        }
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
        Check(Data.Load(xml).Version == 3 && !Data.Load(xml).Entries[0].IndependentMovement
            && Directory.GetFiles(root, "v2.xml.v2-*.bak").Length == 1, "explicit legacy reopen survives v2 upgrade and backup");
        Check(PhotoAssignDialog.Preview(vessel, new string[] { "a.jpg", "b.png" }).Contains(" (2).png")
            && PhotoAssignDialog.Preview(vessel, new string[] { "a.jpg" }).Contains(Data.PhotoFileName(vessel, 1, 1, ".jpg")),
            "photo preview follows actual one/multiple-file naming");
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
