using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
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
        Check(Data.Load(path).Version == 2 && Data.Load(path).Entries[0].Id == "11111111111111111111111111111111",
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
    static void UiTests()
    {
        Application.EnableVisualStyles();
        string folder = Path.Combine(root, "UI"); Directory.CreateDirectory(folder);
        Book b = MakeBook(); b.Entries.Add(Make(DateTime.Now.AddMinutes(-2), false));
        b.Settings.Kinds.Add(new Kind { Id = "group", Name = "채집/해루질", Unit = "명" });
        b.Entries.Add(Make(DateTime.Today.AddDays(-1), false));
        Data.Save(Path.Combine(folder, "Records.xml"), b);
        using (MainForm form = new MainForm(folder))
        {
            form.Show(); Application.DoEvents();
            Check(Field<Button>(form, "saveButton").Text == "등록", "new entry uses register button");
            Check(Field<ListView>(form, "records").Items.Count == 1, "main view is today only");
            Call(form, "LoadEntry", b.Entries[0]);
            Check(Field<Button>(form, "saveButton").Text == "저장", "selected record uses save button");
            Field<ListBox>(form, "cart").Items.Clear();
            Check(Data.Load(Path.Combine(folder, "Records.xml")).Entries[0].Items.Count == 1,
                "editor cart does not mutate saved record");
            Call(form, "ResetEditor");
            Field<ComboBox>(form, "title").Text = "사동해변";
            Field<ComboBox>(form, "camera").Text = "사동해강안";
            Field<DateTimePicker>(form, "date").Value = DateTime.Now.AddMinutes(-1);
            Field<CheckBox>(form, "hasAzimuth").Checked = true;
            Field<NumericUpDown>(form, "azimuth").Value = 3426;
            Field<CheckBox>(form, "hasElevation").Checked = true;
            Field<NumericUpDown>(form, "elevation").Value = -121;
            Call(form, "AddItem", null, EventArgs.Empty); Call(form, "SaveEntry", null, EventArgs.Empty);
            Book saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries.Count == 3 && saved.Entries[2].Azimuth == 3426
                && saved.Entries[2].Elevation == -121 && saved.Entries[2].Section == "4구간", "UI register persists camera/angles/section");
            Field<ComboBox>(form, "title").Text = "수정한 장소";
            Call(form, "SaveEntry", null, EventArgs.Empty);
            saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries.Count == 3 && saved.Entries[2].Title == "수정한 장소", "UI save updates without duplicate");
            Field<ComboBox>(form, "kind").SelectedIndex = 1;
            Field<ComboBox>(form, "subkind").SelectedItem = "채집";
            Field<NumericUpDown>(form, "quantity").Value = 2; Call(form, "AddItem", null, EventArgs.Empty);
            Field<ComboBox>(form, "subkind").SelectedItem = "해루질";
            Field<NumericUpDown>(form, "quantity").Value = 3; Call(form, "AddItem", null, EventArgs.Empty);
            Field<ComboBox>(form, "subkind").SelectedItem = "채집";
            Field<NumericUpDown>(form, "quantity").Value = 4; Call(form, "AddItem", null, EventArgs.Empty);
            Call(form, "SaveEntry", null, EventArgs.Empty);
            saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries[2].Items.Count == 3 && saved.Entries[2].Items[1].Count == 6
                && saved.Entries[2].Items[2].Count == 3, "UI combines same child but keeps different children");
            Check(Field<Label>(form, "totals").Text.Contains("채집/해루질 9명"), "UI shows parent total");
            Field<CheckBox>(form, "complete").Checked = true; Call(form, "SaveEntry", null, EventArgs.Empty);
            saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries[2].Category == "Target" && saved.Entries[2].Complete
                && saved.Entries[2].FinishedAt.HasValue && saved.Entries[2].StateText() == "철수", "UI target withdrawal time persisted");
            for (int i = 0; i < 2; i++)
            {
                Call(form, "ResetEditor"); Field<ComboBox>(form, "category").SelectedIndex = 1;
                Field<ComboBox>(form, "title").Text = "테스트항";
                Field<DateTimePicker>(form, "date").Value = DateTime.Now.AddMinutes(-1);
                Field<ComboBox>(form, "vesselType").Text = "어선";
                Field<TextBox>(form, "vesselName").Text = "테스트선박";
                Field<ComboBox>(form, "movement").SelectedIndex = i;
                Call(form, "SaveEntry", null, EventArgs.Empty);
            }
            saved = Data.Load(Path.Combine(folder, "Records.xml"));
            Check(saved.Entries.Count == 5 && saved.Entries[3].Movement == "출항" && saved.Entries[4].Movement == "입항"
                && saved.Entries[3].Id != saved.Entries[4].Id && saved.Entries[4].Items.Count == 0,
                "UI departure and arrival are independent vessel events");
            Field<CheckBox>(form, "showActive").Checked = false;
            Field<CheckBox>(form, "showDone").Checked = false;
            Check(Field<ListView>(form, "records").Items.Count == 4, "UI cleared filters do not blank list");
            using (HistoryDialog history = new HistoryDialog(saved))
            {
                history.Show(form); Application.DoEvents();
                Check(Field<ListView>(history, "records").Items.Count == 5, "period popup includes prior day");
                history.Close();
            }
            form.Close();
        }
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
        for (int i = 0; i < expected.Length; i++) correct &= rows[1][16 + i] == expected[i];
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
            Title = "테스트항", Category = "Vessel", Complete = true, VesselType = "어선", VesselName = "테스트선박", Movement = "입항" };
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
