using System;
using System.IO;
using System.Windows.Forms;

namespace CoastNotebook
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            try
            {
                Directory.CreateDirectory(folder);
                // The open handle prevents two processes from writing this data folder.
                using (FileStream gate = new FileStream(Path.Combine(folder, "app.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                using (MainForm form = new MainForm(folder))
                    Application.Run(form);
            }
            catch (Exception ex)
            {
                MessageBox.Show("시작하지 못했습니다. 이미 실행 중인지, 폴더 권한과 원본 파일을 확인하세요.\r\n"
                    + "손상된 원본을 빈 데이터로 덮어쓰지 않았습니다.\r\n\r\n" + ex.Message,
                    "기록장 시작 오류");
            }
        }
    }
}
