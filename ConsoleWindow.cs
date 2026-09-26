using System.Runtime.InteropServices;

namespace TradeCompany;

internal static class ConsoleWindow
{
    private const int SwMaximize = 3;

    public static void Prepare()
    {
        try
        {
            Console.Title = "Торговая компания";
            if (OperatingSystem.IsWindows())
            {
                IntPtr window = GetConsoleWindow();
                if (window != IntPtr.Zero) ShowWindow(window, SwMaximize);
            }

            // Maximizing is asynchronous. Wait until the host reports a stable
            // viewport so the first frame is never painted at the old size.
            int oldWidth = 0, oldHeight = 0, stableReads = 0;
            for (int i = 0; i < 30 && stableReads < 4; i++)
            {
                int width = Console.WindowWidth, height = Console.WindowHeight;
                if (width == oldWidth && height == oldHeight) stableReads++;
                else { oldWidth = width; oldHeight = height; stableReads = 0; }
                Thread.Sleep(25);
            }
            ClearViewport();
        }
        catch (Exception e) when (e is IOException or ArgumentOutOfRangeException)
        {
            // Some terminal hosts do not expose their native window. The
            // responsive renderer still works at the current dimensions.
        }
    }

    public static void ClearViewport()
    {
        Console.ResetColor();
        Console.Clear();
        Console.SetCursorPosition(0, 0);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
