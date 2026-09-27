using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TradeCompany;

// Own the console input mode only while a screen is using event-based input.
internal sealed class PointerInput : IDisposable
{
    internal readonly record struct Event(ConsoleKeyInfo? Key = null, int X = 0, int Y = 0, bool Click = false, int Wheel = 0);
    private readonly IntPtr handle;
    private readonly uint originalMode;
    private readonly bool native;
    private bool leftDown;
    private ConsoleKeyInfo repeatedKey;
    private int repeats;

    public PointerInput()
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected) return;
        handle = GetStdHandle(-10);
        native = GetConsoleMode(handle, out originalMode) &&
            SetConsoleMode(handle, (originalMode | 0x98u) & ~0x0246u);
    }

    public bool TryRead(out Event result)
    {
        result = default;
        if (!native)
        {
            if (!Console.KeyAvailable) return false;
            result = new(Console.ReadKey(true));
            return true;
        }
        if (repeats > 0) { repeats--; result = new(repeatedKey); return true; }
        while (true)
        {
            if (!GetNumberOfConsoleInputEvents(handle, out uint count)) throw InputError();
            if (count == 0) return false;
            if (!ReadConsoleInputW(handle, out var record, 1, out _)) throw InputError();
            if (record.Type == 1 && record.Down != 0)
            {
                uint mods = record.Controls;
                repeatedKey = new ConsoleKeyInfo(record.Character, (ConsoleKey)record.VirtualKey,
                    (mods & 0x10) != 0, (mods & 3) != 0, (mods & 12) != 0);
                repeats = Math.Max(0, record.Repeat - 1);
                result = new(repeatedKey);
                return true;
            }
            if (record.Type != 2) continue;
            bool down = (record.Buttons & 1) != 0;
            bool click = down && !leftDown && record.Flags is 0 or 2;
            leftDown = down;
            int wheel = record.Flags == 4 ? Math.Sign(unchecked((short)(record.Buttons >> 16))) : 0;
            if (!click && wheel == 0) continue;
            result = new(null, record.X - Console.WindowLeft, record.Y - Console.WindowTop, click, wheel);
            return true;
        }
    }

    public void Dispose() { if (native) SetConsoleMode(handle, originalMode); }
    private static IOException InputError() => new("Не удалось прочитать события консоли.", new Win32Exception(Marshal.GetLastWin32Error()));

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 20)]
    private struct InputRecord
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(4)] public int Down;
        [FieldOffset(8)] public ushort Repeat;
        [FieldOffset(10)] public ushort VirtualKey;
        [FieldOffset(14)] public char Character;
        [FieldOffset(16)] public uint Controls;
        [FieldOffset(4)] public short X;
        [FieldOffset(6)] public short Y;
        [FieldOffset(8)] public uint Buttons;
        [FieldOffset(16)] public uint Flags;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNumberOfConsoleInputEvents(IntPtr handle, out uint count);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] private static extern bool ReadConsoleInputW(IntPtr handle, out InputRecord record, uint length, out uint read);
}
