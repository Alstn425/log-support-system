using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CoastNotebook
{
    public class Kind
    {
        public string Id = "";
        public string Name = "";
        public string Unit = "";
        public override string ToString() { return Name + " (" + Unit + ")"; }
        public string[] Choices()
        {
            string[] names = Name.Split('/');
            for (int i = 0; i < names.Length; i++) names[i] = names[i].Trim();
            return names;
        }
    }

    public class Item
    {
        public string KindId = "";
        public string Name = "";
        public string Unit = "";
        public string SubName = "";
        public int Count;
        public override string ToString() { return (SubName.Length == 0 ? Name : SubName) + " " + Count + Unit; }
    }

    public class Entry
    {
        public string Id = "";
        public DateTime Time;
        public string Title = "";
        public string Note = "";
        public string Section = "";
        public string Camera = "";
        public int? Azimuth;
        public int? Elevation;
        public string Category = "Manual";
        public DateTime? FinishedAt;
        public string VesselType = "";
        public string VesselName = "";
        public string Movement = "";
        public bool Complete;
        public List<Item> Items = new List<Item>();
        public List<string> Photos = new List<string>();
        public override string ToString()
        {
            return Time.ToString("MM-dd HH:mm") + " " + Title + " "
                + Describe() + " [" + StateText() + "]";
        }
        public string Describe()
        {
            if (Category == "Vessel") return VesselType + " " + VesselName + " " + Movement;
            List<string> parts = new List<string>();
            foreach (Item item in Items) parts.Add(item.ToString());
            return String.Join(", ", parts.ToArray());
        }
        public string StateText()
        {
            return Category == "Vessel" ? Movement : Category == "Target"
                ? (Complete ? "철수" : "식별") : (Complete ? "완료" : "진행");
        }
        public string CategoryText()
        {
            return Category == "Vessel" ? "선박" : Category == "Target" ? "특이표적" : "일반/기존";
        }
        public string Totals()
        {
            SortedDictionary<string, int> counts = new SortedDictionary<string, int>();
            Dictionary<string, Item> labels = new Dictionary<string, Item>();
            foreach (Item item in Items)
            {
                if (!counts.ContainsKey(item.KindId)) counts[item.KindId] = 0;
                counts[item.KindId] += item.Count; labels[item.KindId] = item;
            }
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                Item label = labels[pair.Key];
                parts.Add(label.Name + " " + pair.Value + label.Unit);
            }
            return String.Join(", ", parts.ToArray());
        }
    }

    public class Settings
    {
        public bool Configured;
        public string Name = "";
        public string CsvFolder = "";
        public string PhotoFolder = "";
        public bool CopyText;
        public bool ShowSuccess = true;
        public List<Kind> Kinds = new List<Kind>();
        public string Alarms = "";
        public string Cameras = "";
        public string Places = "";
        public string VesselTypes = "어선\r\n레저보트\r\n기타";
    }

    public class Book
    {
        public int Version = 2;
        public Settings Settings = new Settings();
        public List<Entry> Entries = new List<Entry>();
        public List<string> PendingDays = new List<string>();
        public string Help = "";
        public DateTime AcknowledgedDate;
        public List<string> AlarmAcknowledgements = new List<string>();
    }

    public static class Data
    {
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Book));

        public static Book Clone(Book book)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                WriteXml(memory, book);
                memory.Position = 0;
                return (Book)Serializer.Deserialize(memory);
            }
        }

        public static Book Load(string path)
        {
            if (!File.Exists(path)) return new Book();
            XmlReaderSettings options = new XmlReaderSettings();
            options.DtdProcessing = DtdProcessing.Prohibit;
            options.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(path, options))
            {
                Book book = (Book)Serializer.Deserialize(reader);
                Validate(book);
                return book;
            }
        }

        public static void Validate(Book book)
        {
            if (book == null || (book.Version != 1 && book.Version != 2) || book.Settings == null
                || book.Entries == null || book.PendingDays == null
                || book.AlarmAcknowledgements == null)
                throw new InvalidDataException("지원하지 않거나 손상된 원본입니다.");
            HashSet<string> ids = new HashSet<string>();
            foreach (Entry entry in book.Entries)
            {
                Guid parsedId;
                if (entry == null || !Guid.TryParseExact(entry.Id, "N", out parsedId)
                    || !ids.Add(entry.Id) || entry.Items == null || entry.Photos == null
                    || entry.Title == null || entry.Note == null || entry.Camera == null || entry.Section == null
                    || (entry.Azimuth.HasValue && (entry.Azimuth < 0 || entry.Azimuth > 6400))
                    || (entry.Elevation.HasValue && (entry.Elevation < -180 || entry.Elevation > 180))
                    || (entry.Category != "Manual" && entry.Category != "Target" && entry.Category != "Vessel")
                    || entry.VesselType == null || entry.VesselName == null || entry.Movement == null
                    || (entry.FinishedAt.HasValue && (!entry.Complete || entry.FinishedAt < entry.Time))
                    || entry.Time.Year < 1753 || entry.Time.Year > 9998)
                    throw new InvalidDataException("기록 ID 또는 필드가 올바르지 않습니다.");
                if (entry.Category == "Vessel" && (!entry.Complete || entry.Items.Count != 0
                    || (entry.Movement != "입항" && entry.Movement != "출항")
                    || String.IsNullOrWhiteSpace(entry.VesselType) || String.IsNullOrWhiteSpace(entry.VesselName)))
                    throw new InvalidDataException("선박의 선종, 선명/추적번호와 입출항을 확인하세요.");
                HashSet<string> kinds = new HashSet<string>();
                foreach (Item item in entry.Items)
                    if (item == null || String.IsNullOrWhiteSpace(item.KindId)
                        || item.SubName == null || !kinds.Add(item.KindId + "\0" + item.SubName)
                        || item.Name == null || item.Unit == null
                        || item.Count < 0 || item.Count > 999)
                        throw new InvalidDataException("기록의 항목 또는 수량이 올바르지 않습니다.");
            }
            foreach (string day in book.PendingDays) ParseDay(day);
        }

        public static void AtomicWrite(string path, Action<Stream> write)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temp,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        public static void Save(string path, Book book)
        {
            Validate(book);
            // Preserve the exact v1 original before the first format upgrade.
            if (File.Exists(path) && Load(path).Version == 1)
                File.Copy(path, path + ".v1-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                    + "-" + Guid.NewGuid().ToString("N") + ".bak", false);
            int previous = book.Version;
            try
            {
                if (book.Version == 1)
                    foreach (Entry entry in book.Entries) Queue(book, entry.Time);
                book.Version = 2;
                AtomicWrite(path, delegate(Stream stream) { WriteXml(stream, book); });
            }
            catch { book.Version = previous; throw; }
        }

        static void WriteXml(Stream stream, Book book)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false);
            settings.Indent = true;
            settings.NewLineHandling = NewLineHandling.Entitize;
            settings.CloseOutput = false;
            using (XmlWriter writer = XmlWriter.Create(stream, settings)) Serializer.Serialize(writer, book);
        }

        public static string Day(DateTime value) { return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static DateTime ParseDay(string value)
        {
            return DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        public static void Queue(Book book, DateTime day)
        {
            string text = Day(day);
            if (!book.PendingDays.Contains(text)) book.PendingDays.Add(text);
        }

        public static List<Entry> SelectEntries(Book book, DateTime from, DateTime through,
            bool active, bool done, string search)
        {
            if (from.Date > through.Date) throw new ArgumentException("조회 시작일이 종료일보다 늦습니다.");
            // Clearing both status boxes means all statuses, never an accidental empty list.
            if (!active && !done) { active = true; done = true; }
            List<Entry> result = new List<Entry>();
            foreach (Entry entry in book.Entries)
            {
                if (entry.Time.Date < from.Date || entry.Time.Date > through.Date) continue;
                if (entry.Complete ? !done : !active) continue;
                string text = entry.Title + " " + entry.Note + " " + entry.Describe()
                    + " " + entry.Camera + " " + entry.Section + " " + entry.Azimuth + " " + entry.Elevation
                    + " " + entry.StateText() + " " + entry.CategoryText();
                if (text.IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(entry);
            }
            result.Sort(delegate(Entry a, Entry b) {
                int order = b.Time.CompareTo(a.Time);
                return order != 0 ? order : String.CompareOrdinal(a.Id, b.Id);
            });
            return result;
        }

        public static string CsvCell(string value)
        {
            string text = value ?? "";
            string trimmed = text.TrimStart();
            int integer;
            bool safeInteger = Int32.TryParse(text, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out integer);
            if ((trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0)
                && !safeInteger
                || (text.Length > 0 && "\t\r\n".IndexOf(text[0]) >= 0))
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        static void CsvLine(StringBuilder text, List<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(CsvCell(fields[i]));
            }
            text.Append("\r\n");
        }

        public static string ExportDay(Book book, DateTime day)
        {
            string folder = Path.Combine(book.Settings.CsvFolder, day.ToString("yyyy"), Day(day));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Records.csv");
            // IDs identify columns even after a type is renamed or removed.
            SortedDictionary<string, string> kinds = new SortedDictionary<string, string>();
            foreach (Kind kind in book.Settings.Kinds) kinds[kind.Id] = kind.Name;
            foreach (Entry entry in book.Entries)
                foreach (Item item in entry.Items)
                    if (!kinds.ContainsKey(item.KindId)) kinds[item.KindId] = item.Name;

            SortedDictionary<string, SortedSet<string>> children = new SortedDictionary<string, SortedSet<string>>();
            foreach (Kind kind in book.Settings.Kinds)
            {
                string[] choices = kind.Choices();
                if (choices.Length < 2) continue;
                children[kind.Id] = new SortedSet<string>(choices, StringComparer.Ordinal);
            }
            foreach (Entry entry in book.Entries)
                foreach (Item item in entry.Items)
                    if (item.SubName.Length > 0)
                    {
                        if (!children.ContainsKey(item.KindId)) children[item.KindId] = new SortedSet<string>(StringComparer.Ordinal);
                        children[item.KindId].Add(item.SubName);
                    }
            foreach (SortedSet<string> choices in children.Values) choices.Add("");

            List<string> header = new List<string>(new string[] {
                "ID", "일시", "제목 / 장소", "상태", "내역", "비고", "사진수",
                "구간", "카메라", "방위각(0~6400)", "고각(-180~180)",
                "기록구분", "선종", "선명/추적번호", "입출항", "철수/완료일시" });
            foreach (KeyValuePair<string, string> kind in kinds)
            {
                header.Add(kind.Value + " [" + kind.Key + "] 합계");
                if (children.ContainsKey(kind.Key))
                    foreach (string child in children[kind.Key])
                        header.Add(kind.Value + " > " + (child.Length == 0 ? "미분류(기존 기록)" : child)
                            + " [" + kind.Key + "] 수량");
            }
            StringBuilder text = new StringBuilder();
            CsvLine(text, header);
            foreach (Entry entry in book.Entries)
            {
                if (entry.Time.Date != day.Date) continue;
                List<string> row = new List<string>(new string[] {
                    entry.Id, entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Title,
                    entry.StateText(), entry.Describe(), entry.Note,
                    entry.Photos.Count.ToString(CultureInfo.InvariantCulture), entry.Section, entry.Camera,
                    entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.Elevation.HasValue ? entry.Elevation.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.CategoryText(), entry.VesselType, entry.VesselName, entry.Movement,
                    entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("yyyy-MM-dd HH:mm") : "" });
                foreach (string kind in kinds.Keys)
                {
                    int count = 0;
                    foreach (Item item in entry.Items)
                        if (item.KindId == kind) count += item.Count;
                    row.Add(count.ToString(CultureInfo.InvariantCulture));
                    if (children.ContainsKey(kind))
                        foreach (string child in children[kind])
                        {
                            int subCount = 0;
                            foreach (Item item in entry.Items)
                                if (item.KindId == kind && item.SubName == child) subCount += item.Count;
                            row.Add(subCount.ToString(CultureInfo.InvariantCulture));
                        }
                }
                CsvLine(text, row);
            }
            byte[] bytes = new UTF8Encoding(true).GetBytes(text.ToString());
            AtomicWrite(path, delegate(Stream stream)
            {
                byte[] bom = new UTF8Encoding(true).GetPreamble();
                stream.Write(bom, 0, bom.Length);
                stream.Write(bytes, 0, bytes.Length);
            });
            return path;
        }

        public static string SafeName(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (char c in value)
                result.Append(c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '_' : c);
            string name = result.ToString().Trim().TrimEnd('.', ' ');
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd('.', ' ');
            return name.Length == 0 ? "기록" : name;
        }

        public static string PhotoStem(Entry entry)
        {
            return SafeName(entry.Time.ToString("yyyyMMdd_HHmm") + " "
                + entry.Title + " " + entry.Describe()) + "_" + entry.Id.Substring(0, 8);
        }

        public static string PhotoFileName(Entry entry, int count, int ordinal, string extension)
        {
            if (count < 1 || ordinal < 1 || ordinal > count) throw new ArgumentOutOfRangeException("ordinal");
            return PhotoStem(entry) + (count > 1 ? " (" + ordinal.ToString(CultureInfo.InvariantCulture) + ")" : "") + extension;
        }

        public static string PhotoDestination(Entry entry, int count, int ordinal, string folder,
            string extension, string currentPath)
        {
            string path = Path.Combine(folder, PhotoFileName(entry, count, ordinal, extension));
            if (currentPath != null && String.Equals(Path.GetFullPath(path), Path.GetFullPath(currentPath),
                StringComparison.OrdinalIgnoreCase)) return path;
            if (File.Exists(path) || Directory.Exists(path))
                throw new IOException("같은 이름의 파일 또는 폴더가 있습니다: " + path);
            return path;
        }

        public static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        public static void CopyVerified(string source, string destination)
        {
            bool created = false;
            try
            {
                using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    input.CopyTo(output);
                    output.Flush(true);
                }
                if (Hash(source) != Hash(destination)) throw new IOException("사진 복사 검증에 실패했습니다.");
            }
            catch
            {
                if (created) { try { File.Delete(destination); } catch { } }
                throw;
            }
        }
    }
}
