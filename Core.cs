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

    public class KindChoice
    {
        public Kind Kind;
        public string SubName = "";
        public override string ToString() { return SubName.Length == 0 ? Kind.Name : SubName; }
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
        public string VesselNumber = "";
        public string ArrivalHarbor = "";
        public bool IndependentMovement;
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
            if (Category == "Vessel") return (VesselType + " " + VesselName + " " + VesselNumber + " " + StateText()).Trim();
            List<string> parts = new List<string>();
            foreach (Item item in Items) parts.Add(item.ToString());
            return String.Join(", ", parts.ToArray());
        }
        public string StateText()
        {
            return Category == "Vessel" ? (IndependentMovement ? Movement + "(기존)" : Complete ? "입항" : "출항중") : Category == "Target"
                ? (Complete ? "철수" : "식별") : (Complete ? "완료" : "진행");
        }
        public string CategoryText()
        {
            return Category == "Vessel" ? "선박" : Category == "Target" ? "특이표적" : "일반/기존";
        }
        public string ArrivalPlace()
        {
            if (ArrivalHarbor.Length > 0) return ArrivalHarbor;
            return Category == "Vessel" && Complete && FinishedAt.HasValue && !IndependentMovement ? Title : "";
        }
        public string Report()
        {
            string text = Time.ToString("HHmm") + " " + (Category == "Vessel" ? "" : Camera + " ") + Title;
            if (Azimuth.HasValue) text += " P:" + Azimuth.Value;
            if (Elevation.HasValue) text += " T:" + Elevation.Value;
            text += " " + Describe();
            if (Category != "Vessel") text += " " + StateText();
            if (FinishedAt.HasValue) text += " / " + (Category == "Vessel" ? "입항 " : "종료 ")
                + FinishedAt.Value.ToString("MM-dd HH:mm") + (Category == "Vessel" ? " " + ArrivalPlace() : "");
            return text.Trim();
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

    public class SolarSettings
    {
        public DateTime Date = DateTime.Today;
        public string Bmnt = "", Sunrise = "", Sunset = "", Eent = "";
        public bool Enabled;
        public int SunriseOffset, SunsetOffset;
    }
    public class SolarAlarm
    {
        public string Key, Title;
        public DateTime At;
    }
    public class MacroStep
    {
        public int X, Y;
        public int DelayMs = 500;
    }
    public class MacroProfile
    {
        public string Name = "";
        public int DefaultDelayMs = 500;
        public int Left, Top, Width, Height;
        public List<MacroStep> Steps = new List<MacroStep>();
    }
    public interface IMacroInput
    {
        bool Available { get; }
        bool LeftDown { get; }
        bool EscapeDown { get; }
        int X { get; }
        int Y { get; }
        int DesktopLeft { get; }
        int DesktopTop { get; }
        int DesktopWidth { get; }
        int DesktopHeight { get; }
        bool Contains(int x, int y);
        void Click(int x, int y);
    }
    public enum MacroMode { Idle, Recording, Playing }
    public class MacroEngine
    {
        readonly IMacroInput input;
        bool wasDown;
        long lastClick, due;
        int index;
        public MacroProfile Profile;
        public MacroMode Mode { get; private set; }
        public bool RecordingFinished { get; private set; }
        public string Status = "";
        public Func<int, int, bool> AllowRecording;
        public bool Available { get { return input.Available; } }
        public bool EscapeHeld { get { return input.Available && input.EscapeDown; } }
        public MacroEngine(IMacroInput source) { input = source; }
        void RequireInput() { if (!input.Available) throw new PlatformNotSupportedException("실제 매크로 녹화·재생은 Windows에서 지원합니다."); }
        public void StartRecording(string name, int defaultDelay, long now)
        {
            RequireInput(); if (Mode != MacroMode.Idle) throw new InvalidOperationException("진행 중인 매크로를 먼저 중지하세요.");
            Profile = new MacroProfile { Name = name, DefaultDelayMs = defaultDelay, Left = input.DesktopLeft, Top = input.DesktopTop,
                Width = input.DesktopWidth, Height = input.DesktopHeight };
            Data.ValidateMacro(Profile); wasDown = input.LeftDown; lastClick = now; RecordingFinished = false;
            Mode = MacroMode.Recording; Status = "녹화 중 · ESC/중지로 완료";
        }
        public void StartPlaying(MacroProfile profile, long now)
        {
            RequireInput(); if (Mode != MacroMode.Idle) throw new InvalidOperationException("진행 중인 매크로를 먼저 중지하세요.");
            Data.ValidateMacro(profile); if (profile.Steps.Count == 0) throw new InvalidOperationException("먼저 이 슬롯에 클릭을 녹화하세요.");
            if (profile.Width > 0 && (profile.Left != input.DesktopLeft || profile.Top != input.DesktopTop
                || profile.Width != input.DesktopWidth || profile.Height != input.DesktopHeight))
                throw new InvalidOperationException("녹화 당시와 화면 해상도·배치가 다릅니다. 현재 화면에서 다시 녹화하세요.");
            foreach (MacroStep step in profile.Steps)
                if (!input.Contains(step.X, step.Y)) throw new InvalidOperationException("화면 밖의 클릭 좌표가 있습니다.");
            Profile = Data.CloneMacro(profile); index = 0; due = now; RecordingFinished = false;
            Mode = MacroMode.Playing; Status = "재생 중";
        }
        public void Tick(long now)
        {
            if (Mode == MacroMode.Idle) return;
            if (input.EscapeDown) { Stop(true); return; }
            if (Mode == MacroMode.Recording)
            {
                bool down = input.LeftDown;
                if (down && !wasDown && (AllowRecording == null || AllowRecording(input.X, input.Y)))
                {
                    if (Profile.Steps.Count >= 1000) { Stop(true); Status = "1000개 클릭 녹화를 완료했습니다."; return; }
                    if (Profile.Steps.Count > 0) Profile.Steps[Profile.Steps.Count - 1].DelayMs = (int)Math.Min(600000L, Math.Max(0, now - lastClick));
                    Profile.Steps.Add(new MacroStep { X = input.X, Y = input.Y, DelayMs = Profile.DefaultDelayMs }); lastClick = now;
                    Status = "녹화 중 · " + Profile.Steps.Count + "개 클릭 · ESC로 완료";
                }
                wasDown = down; return;
            }
            if (now < due) return;
            if (index >= Profile.Steps.Count) { Mode = MacroMode.Idle; Status = "매크로 재생 완료"; return; }
            MacroStep next = Profile.Steps[index]; input.Click(next.X, next.Y); index++;
            due = now + next.DelayMs; Status = "재생 중 · " + index + "/" + Profile.Steps.Count;
        }
        public void Stop(bool keepRecording)
        {
            RecordingFinished = Mode == MacroMode.Recording && keepRecording;
            Mode = MacroMode.Idle; Status = RecordingFinished ? "녹화 완료 · 저장할 내용을 확인하세요." : "매크로 중지";
        }
        public void ConsumeRecording() { RecordingFinished = false; }
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
        public bool StructuredCameras;
        public List<CameraSpec> CameraDefinitions = new List<CameraSpec>();
        public SolarSettings Solar = new SolarSettings();
        public MacroProfile[] Macros = new MacroProfile[] { new MacroProfile { Name = "매크로 1" }, new MacroProfile { Name = "매크로 2" } };
        public string Harbors = "";
        public string VesselTypes = "어선\r\n레저보트\r\n기타";
    }

    public class Book
    {
        public int Version = 5;
        public Settings Settings = new Settings();
        public List<Entry> Entries = new List<Entry>();
        public List<string> PendingDays = new List<string>();
        public string Help = "";
        public DateTime AcknowledgedDate;
        public List<string> AlarmAcknowledgements = new List<string>();
    }

    public class CameraSpec
    {
        public string Name = "";
        public List<string> Places = new List<string>();
    }

    public static class Data
    {
        public const int CurrentVersion = 5;
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Book));

        public static List<string> Lines(string text)
        {
            List<string> result = new List<string>();
            foreach (string raw in (text ?? "").Split('\n'))
            { string line = raw.Trim(); if (line.Length > 0 && !result.Contains(line)) result.Add(line); }
            return result;
        }

        public static List<CameraSpec> Cameras(Settings settings)
        {
            List<CameraSpec> result = new List<CameraSpec>();
            if (settings.StructuredCameras)
            {
                HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (settings.CameraDefinitions == null) throw new FormatException("카메라 설정을 확인하세요.");
                foreach (CameraSpec camera in settings.CameraDefinitions)
                {
                    if (camera == null || String.IsNullOrWhiteSpace(camera.Name) || !unique.Add(camera.Name.Trim()) || camera.Places == null)
                        throw new FormatException("카메라 이름은 비어 있거나 중복될 수 없습니다.");
                    CameraSpec copy = new CameraSpec(); copy.Name = camera.Name.Trim();
                    HashSet<string> places = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string place in camera.Places)
                    {
                        if (String.IsNullOrWhiteSpace(place) || !places.Add(place.Trim())) throw new FormatException("지형지물 이름은 비어 있거나 중복될 수 없습니다.");
                        copy.Places.Add(place.Trim());
                    }
                    result.Add(copy);
                }
                return result;
            }
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in Lines(settings.Cameras))
            {
                string[] parts = line.Split('|');
                if (parts.Length > 2 || parts[0].Trim().Length == 0 || !names.Add(parts[0].Trim()))
                    throw new FormatException("카메라는 한 줄에 카메라|장소1,장소2 형식이며 이름을 중복할 수 없습니다.");
                CameraSpec spec = new CameraSpec(); spec.Name = parts[0].Trim();
                string places = parts.Length == 2 ? parts[1].Replace(',', '\n') : settings.Places;
                spec.Places.AddRange(Lines(places)); result.Add(spec);
            }
            return result;
        }

        public static MacroProfile CloneMacro(MacroProfile source)
        {
            MacroProfile copy = new MacroProfile { Name = source.Name, DefaultDelayMs = source.DefaultDelayMs,
                Left = source.Left, Top = source.Top, Width = source.Width, Height = source.Height };
            foreach (MacroStep step in source.Steps) copy.Steps.Add(new MacroStep { X = step.X, Y = step.Y, DelayMs = step.DelayMs });
            return copy;
        }
        public static void ValidateMacro(MacroProfile profile)
        {
            if (profile == null || profile.Name == null || profile.Steps == null || profile.Steps.Count > 1000
                || profile.DefaultDelayMs < 0 || profile.DefaultDelayMs > 600000 || profile.Width < 0 || profile.Height < 0)
                throw new FormatException("매크로 이름·클릭 수·기본 지연을 확인하세요.");
            foreach (MacroStep step in profile.Steps)
                if (step == null || step.DelayMs < 0 || step.DelayMs > 600000) throw new FormatException("클릭 지연은 0~600000ms 범위입니다.");
        }
        public static void ValidateSolar(SolarSettings solar)
        {
            if (solar == null || solar.Date.Year < 1753 || solar.Date.Year > 9998 || Math.Abs((long)solar.SunriseOffset) > 720 || Math.Abs((long)solar.SunsetOffset) > 720)
                throw new FormatException("기상 적용 날짜와 ±분 범위(-720~720)를 확인하세요.");
            foreach (string text in new string[] { solar.Bmnt, solar.Sunrise, solar.Sunset, solar.Eent })
                if (text == null || (text.Length > 0 && !ValidTime(text))) throw new FormatException("기상 시각은 HH:mm 형식입니다.");
            if (solar.Enabled && (solar.Sunrise.Length == 0 || solar.Sunset.Length == 0))
                throw new FormatException("교대 알람을 사용하려면 일출·일몰 시각을 입력하세요.");
        }
        public static bool ValidTime(string value)
        { DateTime parsed; return DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed); }
        public static List<SolarAlarm> SolarAlarms(SolarSettings solar)
        {
            ValidateSolar(solar); List<SolarAlarm> result = new List<SolarAlarm>(); if (!solar.Enabled) return result;
            string[] times = { solar.Sunrise, solar.Sunset }; string[] names = { "일출 · 주간 전환", "일몰 · 야간 전환" };
            int[] offsets = { solar.SunriseOffset, solar.SunsetOffset };
            for (int i = 0; i < 2; i++)
            {
                DateTime at = solar.Date.Date.Add(DateTime.ParseExact(times[i], "HH:mm", CultureInfo.InvariantCulture).TimeOfDay).AddMinutes(offsets[i]);
                result.Add(new SolarAlarm { Title = names[i], At = at, Key = Day(at) + "|solar|" + Day(solar.Date) + "|" + i + "|" + times[i] + "|" + offsets[i] });
            }
            return result;
        }
        public static List<SolarAlarm> DueSolar(Book book, DateTime now)
        {
            List<SolarAlarm> due = new List<SolarAlarm>();
            foreach (SolarAlarm alarm in SolarAlarms(book.Settings.Solar))
                if (alarm.At.Date == now.Date && now >= alarm.At && !book.AlarmAcknowledgements.Contains(alarm.Key)) due.Add(alarm);
            return due;
        }

        public static int? OptionalAngle(string text, int min, int max)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            int number;
            if (!Int32.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number)
                || number < min || number > max)
                throw new FormatException("각도는 " + min + "~" + max + "의 정수 또는 빈칸이어야 합니다.");
            return number;
        }

        public static void Finish(Entry entry, DateTime at)
        {
            if (entry.Complete || entry.IndependentMovement) throw new InvalidOperationException("현재 기록은 종료 처리할 수 없습니다.");
            if (at < entry.Time || at > DateTime.Now) throw new ArgumentException("처리 시각은 기록 시각 이후, 현재 시각 이하여야 합니다.");
            entry.Complete = true; entry.FinishedAt = at;
        }

        public static void Arrive(Entry entry, DateTime at, string harbor)
        {
            if (entry.Category != "Vessel" || entry.Movement != "출항") throw new InvalidOperationException("출항 기록을 선택하세요.");
            if (String.IsNullOrWhiteSpace(harbor)) throw new ArgumentException("입항할 항구를 선택하거나 입력하세요.");
            Finish(entry, at); entry.ArrivalHarbor = harbor.Trim();
        }

        public static List<Entry> Domain(IList<Entry> entries, bool vessel)
        {
            List<Entry> result = new List<Entry>();
            foreach (Entry entry in entries) if ((entry.Category == "Vessel") == vessel) result.Add(entry);
            return result;
        }

        public static void Reopen(Entry entry)
        {
            if (entry.Category == "Vessel" && entry.Movement != "출항")
                throw new InvalidOperationException("단독 입항 기록에 출항 이력을 만들 수 없습니다.");
            entry.Complete = false; entry.FinishedAt = null; entry.ArrivalHarbor = ""; entry.IndependentMovement = false;
        }

        public static string Statistics(IList<Entry> entries, bool vessel = false)
        {
            if (vessel)
            {
                SortedDictionary<string, int> types = new SortedDictionary<string, int>();
                int active = 0, arrived = 0;
                foreach (Entry entry in entries)
                {
                    if (entry.Category != "Vessel") continue;
                    if (!types.ContainsKey(entry.VesselType)) types[entry.VesselType] = 0;
                    types[entry.VesselType]++;
                    if (!entry.Complete) active++; else if (!entry.IndependentMovement) arrived++;
                }
                StringBuilder text = new StringBuilder("선박 기록 집계\r\n");
                foreach (KeyValuePair<string, int> type in types) text.AppendLine(type.Key + ": " + type.Value + "건");
                text.AppendLine("출항중 " + active + "건 / 입항 완료 " + arrived + "건");
                text.AppendLine("선택 기간에 등록된 기록의 상태 기준이며 고유 선박 수가 아닙니다.");
                return text.ToString();
            }
            SortedDictionary<string, int> groups = new SortedDictionary<string, int>();
            SortedDictionary<string, int> children = new SortedDictionary<string, int>();
            foreach (Entry entry in entries)
                foreach (Item item in entry.Items)
                {
                    string group = item.Name + " (" + item.Unit + ")";
                    string child = item.Name + " > " + (item.SubName.Length == 0 ? "미분류/단일항목" : item.SubName) + " (" + item.Unit + ")";
                    if (!groups.ContainsKey(group)) groups[group] = 0;
                    if (!children.ContainsKey(child)) children[child] = 0;
                    groups[group] += item.Count; children[child] += item.Count;
                }
            StringBuilder report = new StringBuilder("묶음 유형 합산\r\n");
            foreach (KeyValuePair<string, int> group in groups) report.AppendLine(group.Key + ": " + group.Value);
            report.AppendLine("\r\n세부유형별 집계");
            foreach (KeyValuePair<string, int> child in children) report.AppendLine(child.Key + ": " + child.Value);
            report.AppendLine("\r\n등록된 수량의 합계입니다. 고유 인원수나 실제 방문자 수를 의미하지 않습니다.");
            return report.ToString();
        }

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
                NormalizeLegacy(book); Validate(book);
                return book;
            }
        }

        static void NormalizeLegacy(Book book)
        {
            if (book != null && book.Version < 3 && book.Entries != null)
                foreach (Entry entry in book.Entries)
                    if (entry != null && entry.Category == "Vessel") entry.IndependentMovement = true;
        }

        public static void Validate(Book book)
        {
            if (book == null || (book.Version < 1 || book.Version > CurrentVersion) || book.Settings == null
                || book.Entries == null || book.PendingDays == null
                || book.AlarmAcknowledgements == null)
                throw new InvalidDataException("지원하지 않거나 손상된 원본입니다.");
            if (book.Settings.Macros == null || book.Settings.Macros.Length != 2) throw new InvalidDataException("매크로 슬롯은 2개여야 합니다.");
            foreach (MacroProfile macro in book.Settings.Macros) ValidateMacro(macro);
            ValidateSolar(book.Settings.Solar);
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
                    || entry.VesselType == null || entry.VesselName == null || entry.VesselNumber == null || entry.ArrivalHarbor == null || entry.Movement == null
                    || (entry.FinishedAt.HasValue && (!entry.Complete || entry.FinishedAt < entry.Time))
                    || entry.Time.Year < 1753 || entry.Time.Year > 9998)
                    throw new InvalidDataException("기록 ID 또는 필드가 올바르지 않습니다.");
                if (entry.Category == "Vessel" && (entry.Items.Count != 0
                    || (entry.Movement != "입항" && entry.Movement != "출항")
                    || String.IsNullOrWhiteSpace(entry.VesselType)
                    || (entry.IndependentMovement && !entry.Complete)
                    || (entry.Movement == "입항" && !entry.Complete)
                    || (book.Version >= 3 && !entry.IndependentMovement && entry.Complete && !entry.FinishedAt.HasValue)))
                    throw new InvalidDataException("선박의 선종, 입출항 상태와 입항 시각을 확인하세요.");
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
            // Preserve the exact older-format original before upgrading.
            if (File.Exists(path))
            {
                Book original = Load(path);
                if (original.Version < CurrentVersion)
                    File.Copy(path, path + ".v" + original.Version + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                        + "-" + Guid.NewGuid().ToString("N") + ".bak", false);
            }
            int previous = book.Version;
            try
            {
                if (book.Version < CurrentVersion)
                    foreach (Entry entry in book.Entries) Queue(book, entry.Time);
                book.Version = CurrentVersion;
                Validate(book);
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
                    + " " + entry.StateText() + " " + entry.CategoryText() + " " + entry.VesselNumber + " " + entry.ArrivalPlace();
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

        public static string CsvDayPath(Book book, DateTime day, bool vessel)
        {
            return Path.Combine(book.Settings.CsvFolder, day.ToString("yyyy"), Day(day), vessel ? "Vessels.csv" : "Targets.csv");
        }
        public static string ExportDay(Book book, DateTime day)
        {
            string target = CsvDayPath(book, day, false); Directory.CreateDirectory(Path.GetDirectoryName(target));
            List<Entry> entries = SelectEntries(book, day, day, true, true, "");
            ExportEntries(book, Domain(entries, false), target, false);
            ExportEntries(book, Domain(entries, true), CsvDayPath(book, day, true), true);
            return target;
        }

        public static void ExportEntries(Book book, IList<Entry> entries, string path, bool? vessel = null)
        {
            if (!String.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("출력 파일의 확장자는 .csv여야 합니다.");
            if (vessel.HasValue)
                foreach (Entry entry in entries)
                    if ((entry.Category == "Vessel") != vessel.Value) throw new ArgumentException("다른 종류의 기록을 같은 CSV에 출력할 수 없습니다.");
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
                "기록구분", "선종", "선명", "등록구분(입출항)", "철수/입항/완료일시", "식별번호", "기존독립기록" });
            if (vessel == false) header = new List<string>(new string[] {
                "ID", "일시", "구간", "카메라", "지형지물", "방위각(0~6400)", "고각(-180~180)", "상태", "철수일시", "내역", "비고", "사진수" });
            if (vessel == true) header = new List<string>(new string[] {
                "ID", "출항/등록일시", "등록구분", "출항/등록항", "입항일시", "입항항", "선종", "선명", "식별번호", "상태", "비고", "사진수", "구간", "기존독립기록" });
            if (vessel != true)
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
            foreach (Entry entry in entries)
            {
                List<string> row = new List<string>(new string[] {
                    entry.Id, entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Title,
                    entry.StateText(), entry.Describe(), entry.Note,
                    entry.Photos.Count.ToString(CultureInfo.InvariantCulture), entry.Section, entry.Camera,
                    entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.Elevation.HasValue ? entry.Elevation.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.CategoryText(), entry.VesselType, entry.VesselName, entry.Movement,
                    entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("yyyy-MM-dd HH:mm") : "", entry.VesselNumber,
                    entry.IndependentMovement ? "예" : "" });
                if (vessel == false) row = new List<string>(new string[] {
                    entry.Id, entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Section, entry.Camera, entry.Title,
                    entry.Azimuth.HasValue ? entry.Azimuth.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.Elevation.HasValue ? entry.Elevation.Value.ToString(CultureInfo.InvariantCulture) : "",
                    entry.StateText(), entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("yyyy-MM-dd HH:mm") : "",
                    entry.Describe(), entry.Note, entry.Photos.Count.ToString(CultureInfo.InvariantCulture) });
                if (vessel == true) row = new List<string>(new string[] {
                    entry.Id, entry.Time.ToString("yyyy-MM-dd HH:mm"), entry.Movement, entry.Title,
                    entry.FinishedAt.HasValue ? entry.FinishedAt.Value.ToString("yyyy-MM-dd HH:mm") : "", entry.ArrivalPlace(),
                    entry.VesselType, entry.VesselName, entry.VesselNumber, entry.StateText(), entry.Note,
                    entry.Photos.Count.ToString(CultureInfo.InvariantCulture), entry.Section, entry.IndependentMovement ? "예" : "" });
                if (vessel != true)
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
