namespace JianyingBatchExporter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
        void Log(string message)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
            catch { }
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            Log("UI THREAD ERROR" + Environment.NewLine + args.Exception);
            MessageBox.Show(args.Exception.ToString(), "剪映导出助手发生错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log("UNHANDLED ERROR" + Environment.NewLine + args.ExceptionObject);

        try
        {
            Log("Application starting");
            ApplicationConfiguration.Initialize();
            using var form = new MainForm();
            form.Shown += (_, _) => Log("Main window shown");
            form.FormClosed += (_, _) => Log("Main window closed");
            Application.Run(form);
            Log("Application exited normally");
        }
        catch (Exception exception)
        {
            Log("STARTUP ERROR" + Environment.NewLine + exception);
            MessageBox.Show(
                $"程序启动失败：{exception.Message}{Environment.NewLine}{Environment.NewLine}详细日志：{logPath}",
                "无法启动剪映导出助手",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }
}
