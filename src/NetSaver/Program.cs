using System;
using System.IO;
using System.Windows.Forms;

namespace NetSaver;

static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "NetSaver_log.txt");

    private static void Log(string s)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {s}\n"); } catch { }
    }

    [STAThread]
    static void Main()
    {
        try { File.Delete(LogPath); } catch { }
        Log("=== Main start ===");
        Log($"Exe: {Environment.ProcessPath}");
        Log($"64-bit: {Environment.Is64BitProcess}");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log("ThreadException: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("UnhandledException: " + e.ExceptionObject);

        try
        {
            Log("before ApplicationConfiguration.Initialize");
            ApplicationConfiguration.Initialize();
            Log("after ApplicationConfiguration.Initialize");

            Log("before new MainForm()");
            var f = new MainForm();
            Log("after new MainForm()");

            Log("before Application.Run");
            Application.Run(f);
            Log("after Application.Run");
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            try
            {
                MessageBox.Show("NetSaver упал:\n\n" + ex.Message +
                                "\n\nЛог: " + LogPath,
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
        Log("=== Main end ===");
    }
}