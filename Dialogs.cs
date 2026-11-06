using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CoastNotebook
{
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
            list.Columns.Add("선종", 80); list.Columns.Add("선명/추적번호", 110);
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
                    row.SubItems.Add(entry.VesselType); row.SubItems.Add(entry.VesselName);
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
        public string SelectedId = "";

        public HistoryDialog(Book source)
        {
            book = source; Text = "COAST 기간 조회";
            Size = new Size(850, 500); MinimumSize = new Size(650, 350);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("맑은 고딕", 9F);
            FlowLayoutPanel filters = new FlowLayoutPanel(); filters.Dock = DockStyle.Top;
            filters.Height = 72;
            Label label = new Label(); label.Text = "조회 기간"; label.AutoSize = true; filters.Controls.Add(label);
            from = new DateTimePicker(); from.Format = DateTimePickerFormat.Short;
            from.Width = 125; from.Value = DateTime.Today.AddDays(-6); filters.Controls.Add(from);
            through = new DateTimePicker(); through.Format = DateTimePickerFormat.Short;
            through.Width = 125; filters.Controls.Add(through);
            active = new CheckBox(); active.Text = "식별/진행"; active.Checked = true; active.Width = 90; filters.Controls.Add(active);
            done = new CheckBox(); done.Text = "종료/입출항"; done.Checked = true; done.Width = 105; filters.Controls.Add(done);
            search = new TextBox(); search.Width = 185; filters.Controls.Add(search);
            count = new Label(); count.AutoSize = true; filters.Controls.Add(count);
            records = RecordTable.Create(); records.Dock = DockStyle.Fill;
            Button open = new Button(); open.Text = "선택 기록 열기 (또는 두 번 클릭)";
            open.Height = 35; open.Dock = DockStyle.Bottom; open.Click += OpenSelected;
            records.DoubleClick += OpenSelected;
            Controls.Add(records); Controls.Add(open); Controls.Add(filters);
            from.ValueChanged += RefreshRows; through.ValueChanged += RefreshRows;
            active.CheckedChanged += RefreshRows; done.CheckedChanged += RefreshRows;
            search.TextChanged += RefreshRows; RefreshRows(this, EventArgs.Empty);
        }
        void RefreshRows(object sender, EventArgs e)
        {
            if (from.Value.Date > through.Value.Date)
            { records.Items.Clear(); count.Text = "시작일과 종료일을 확인하세요."; return; }
            List<Entry> entries = Data.SelectEntries(book, from.Value, through.Value,
                active.Checked, done.Checked, search.Text);
            RecordTable.Fill(records, entries, "");
            count.Text = entries.Count + "건 / 상태를 모두 해제하면 전체 상태 조회";
        }
        void OpenSelected(object sender, EventArgs e)
        {
            Entry entry = RecordTable.Selected(records); if (entry == null) return;
            SelectedId = entry.Id; DialogResult = DialogResult.OK;
        }
    }

    public class TextDialog : Form
    {
        public TextBox Input = new TextBox();
        public TextDialog(string title, string text)
        {
            Text = title; Width = 560; Height = 470;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("맑은 고딕", 9F);
            Input.Multiline = true; Input.ScrollBars = ScrollBars.Both;
            Input.Dock = DockStyle.Fill; Input.Text = text;
            Button save = new Button();
            save.Text = "적용"; save.Dock = DockStyle.Bottom; save.Height = 36;
            save.DialogResult = DialogResult.OK;
            Controls.Add(Input); Controls.Add(save);
        }
    }

    public class ConfigDialog : Form
    {
        public Settings Value;
        TextBox name, csv, photos, kinds, alarms, cameras, places, vesselTypes;
        CheckBox copy, success;
        Settings previous;

        public ConfigDialog(Settings current)
        {
            previous = current;
            Text = "기록장 설정"; Width = 540; Height = 670;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("맑은 고딕", 9F);
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill; panel.AutoScroll = true;
            panel.FlowDirection = FlowDirection.TopDown; panel.WrapContents = false;
            Controls.Add(panel);
            name = Field(panel, "구간 명 (예시: 4구간)", current.Name, 25);
            csv = FolderField(panel, "CSV 출력 폴더", current.CsvFolder);
            photos = FolderField(panel, "사진 할당 폴더", current.PhotoFolder);
            cameras = Field(panel, "카메라명: 한 줄에 하나 (직접 입력도 가능)", current.Cameras, 60);
            places = Field(panel, "장소명: 한 줄에 하나 (직접 입력도 가능)", current.Places, 60);
            vesselTypes = Field(panel, "선종(선박 종류): 한 줄에 하나 (직접 입력도 가능)", current.VesselTypes, 60);
            List<string> lines = new List<string>();
            foreach (Kind kind in current.Kinds) lines.Add(kind.Name + "|" + kind.Unit);
            kinds = Field(panel, "특이표적: 이름|단위, /로 하위항목 구분 (예: 채집/해루질|명)", String.Join("\r\n", lines.ToArray()), 130);
            alarms = Field(panel, "일반 일정: 한 줄에 HH:mm|일정 이름 (빈칸이면 알림 없음)", current.Alarms, 100);
            copy = new CheckBox(); copy.Text = "저장 후 문장 복사";
            copy.Checked = current.CopyText; copy.Width = 440; panel.Controls.Add(copy);
            success = new CheckBox(); success.Text = "저장 결과 팝업 표시";
            success.Checked = current.ShowSuccess; success.Width = 440; panel.Controls.Add(success);
            Label notice = new Label(); notice.Width = 470; notice.Height = 45;
            notice.Text = "유형 이름을 바꾸면 새 유형으로 등록됩니다. 과거 기록은 유지됩니다.\r\n외부로 복사한 파일은 설정 변경이나 기록 삭제로 자동 제거되지 않습니다.";
            panel.Controls.Add(notice);
            Button save = new Button(); save.Text = "설정 저장"; save.Size = new Size(470, 38);
            save.Click += Save; panel.Controls.Add(save);
        }

        TextBox Field(Control panel, string label, string text, int height)
        {
            Label title = new Label(); title.Text = label; title.Width = 470; title.Height = 22;
            panel.Controls.Add(title);
            TextBox box = new TextBox(); box.Text = text; box.Width = 470;
            box.Multiline = height > 30; box.Height = height;
            if (box.Multiline) box.ScrollBars = ScrollBars.Vertical;
            panel.Controls.Add(box); return box;
        }

        TextBox FolderField(Control panel, string label, string text)
        {
            Label title = new Label(); title.Text = label; title.Width = 470; title.Height = 22;
            panel.Controls.Add(title);
            FlowLayoutPanel row = new FlowLayoutPanel(); row.Width = 470; row.Height = 36;
            row.WrapContents = false; panel.Controls.Add(row);
            TextBox box = new TextBox(); box.Width = 365; box.Text = text; row.Controls.Add(box);
            Button browse = new Button(); browse.Text = "찾아보기"; browse.Width = 90; browse.Height = 27;
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
            ParseAlarms(settings.Alarms);
        }

        void Save(object sender, EventArgs e)
        {
            try
            {
                Settings settings = new Settings(); settings.Configured = true;
                settings.Name = name.Text.Trim(); settings.CsvFolder = csv.Text.Trim();
                settings.PhotoFolder = photos.Text.Trim(); settings.CopyText = copy.Checked;
                settings.ShowSuccess = success.Checked; settings.Alarms = alarms.Text;
                settings.Cameras = cameras.Text; settings.Places = places.Text;
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
