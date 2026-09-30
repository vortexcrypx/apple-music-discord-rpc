using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AppleMusicDiscordRPC;

internal static class Program
{
    private const string MutexId = "AppleMusicDiscordRPC_SingleInstance_Mutex";
    private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppleMusicDiscordRPC");
    private static readonly string LogFile = Path.Combine(LogDir, "app.log");

    public static void Log(string msg)
    {
        try
        {
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
            File.AppendAllText(LogFile, line + Environment.NewLine);
            Console.WriteLine(line);
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        Log("Application starting...");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"Unhandled Exception: {e.ExceptionObject}");
        };

        Application.ThreadException += (s, e) =>
        {
            Log($"Thread Exception: {e.Exception}");
        };

        using var mutex = new Mutex(true, MutexId, out bool createdNew);
        if (!createdNew)
        {
            Log("Another instance is already running. Exiting.");
            return;
        }

        try
        {
            Log("Initializing ApplicationConfiguration...");
            ApplicationConfiguration.Initialize();

            bool startMinimized = args.Length > 0 && (args[0] == "--silent" || args[0] == "--minimized");
            Log($"Starting TrayApplicationContext (startMinimized={startMinimized})...");
            var context = new TrayApplicationContext(startMinimized);
            Application.Run(context);
            Log("Application.Run exited normally.");
        }
        catch (Exception ex)
        {
            Log($"Fatal error in Main: {ex}");
        }
    }
}
