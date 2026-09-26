using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace TradeCompany;

internal static class ConsoleInput
{
    // Read UTF-16 directly from the Windows console, independently of its code page.
    // Redirected input (scripts and tests) continues to use Console.InputEncoding.
    public static string? ReadLine()
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected)
            return Console.ReadLine();

        var result = new StringBuilder();
        var buffer = new StringBuilder(4096);
        while (true)
        {
            buffer.Clear();
            if (!ReadConsoleW(GetStdHandle(-10), buffer, (uint)buffer.Capacity, out uint read, IntPtr.Zero))
                throw new IOException("Не удалось прочитать ввод из консоли.", new Win32Exception(Marshal.GetLastWin32Error()));
            if (read == 0) return result.Length == 0 ? null : result.ToString();
            result.Append(buffer.ToString());
            var text = result.ToString();
            int end = text.IndexOfAny(['\r', '\n']);
            if (end >= 0) return text[..end];
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadConsoleW(IntPtr input, [Out] StringBuilder buffer, uint length, out uint read, IntPtr control);
}
