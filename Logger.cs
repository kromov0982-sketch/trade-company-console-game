using System.Text;

namespace TradeCompany;

public static class Logger
{
    private static readonly object Gate = new();

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Error("Необработанная ошибка", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Error("Ошибка фоновой задачи", args.Exception);
            args.SetObserved();
        };
    }

    public static void Error(string context, Exception? exception = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Logs);
                RotateIfNeeded();
                var text = new StringBuilder()
                    .Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ERROR ")
                    .AppendLine(context);
                if (exception is not null) text.AppendLine(exception.ToString());
                File.AppendAllText(AppPaths.LogFile, text.ToString(), Encoding.UTF8);
            }
        }
        catch { /* Logging must never terminate the game. */ }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(AppPaths.LogFile) || new FileInfo(AppPaths.LogFile).Length < 1_000_000) return;
        string previous = AppPaths.LogFile + ".old";
        File.Move(AppPaths.LogFile, previous, true);
    }
}
