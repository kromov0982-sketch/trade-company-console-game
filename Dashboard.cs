using System.Runtime.InteropServices;
using System.Text;

namespace TradeCompany;

public static class Dashboard
{
    private static readonly Dictionary<int, string> PreviousRows = new();
    private static bool VirtualTerminal;
    private record Part(string Text, ConsoleColor Color = ConsoleColor.Gray);
    private static readonly (string Command, string Args)[] Commands =
    [
        ("помощь", ""), ("карта / статус", ""), ("рынок", "ГОРОД"),
        ("купить", "№ ТОВАР КОЛ-ВО"), ("продать", "№ ТОВАР КОЛ-ВО"),
        ("ехать", "№ ГОРОД"), ("далее", "[ДНИ]"), ("повозка", "ГОРОД"),
        ("склад", "ГОРОД"), ("выгрузить", "№ ТОВАР КОЛ-ВО"),
        ("загрузить", "№ ТОВАР КОЛ-ВО"), ("сохранить", ""),
        ("продолжить", ""), ("меню / выход", "")
    ];

    public static void Run(Session session)
    {
        var input = new StringBuilder();
        int caret = 0, scroll = 0, previousWidth = 0, previousHeight = 0, lastRevision = -1;
        bool dirty = true;
        bool alternate = EnableVirtualTerminal();
        VirtualTerminal = alternate; PreviousRows.Clear();
        var oldColor = Console.ForegroundColor;
        bool oldCursor = !OperatingSystem.IsWindows() || Console.CursorVisible;
        try
        {
            if (alternate) Console.Write("\u001b[?1049h");
            Console.Clear();
            while (!session.Exit)
            {
                session.PumpNetwork();
                if (session.Revision != lastRevision) { lastRevision = session.Revision; dirty = true; }
                int width = Console.WindowWidth, height = Console.WindowHeight;
                if (width != previousWidth || height != previousHeight)
                {
                    Console.Clear(); PreviousRows.Clear(); dirty = true;
                    previousWidth = width; previousHeight = height;
                }
                if (dirty)
                {
                    try { Draw(session, input.ToString(), caret, ref scroll, width, height); }
                    catch (ArgumentOutOfRangeException) { previousWidth = 0; }
                    dirty = false;
                }
                if (!Console.KeyAvailable) { Thread.Sleep(30); continue; }
                var key = Console.ReadKey(intercept: true);
                dirty = true;
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        session.Execute(input.ToString()); input.Clear(); caret = 0; break;
                    case ConsoleKey.Backspace:
                        if (caret > 0) input.Remove(--caret, 1); break;
                    case ConsoleKey.Delete:
                        if (caret < input.Length) input.Remove(caret, 1); break;
                    case ConsoleKey.LeftArrow: caret = Math.Max(0, caret - 1); break;
                    case ConsoleKey.RightArrow: caret = Math.Min(input.Length, caret + 1); break;
                    case ConsoleKey.Home: caret = 0; break;
                    case ConsoleKey.End: caret = input.Length; break;
                    case ConsoleKey.Escape: input.Clear(); caret = 0; break;
                    case ConsoleKey.PageDown: scroll += 5; break;
                    case ConsoleKey.PageUp: scroll = Math.Max(0, scroll - 5); break;
                    default:
                        if (!char.IsControl(key.KeyChar) && input.Length < 256)
                            input.Insert(caret++, key.KeyChar);
                        break;
                }
            }
        }
        finally
        {
            if (alternate) Console.Write("\u001b[?1049l");
            else Console.Clear();
            Console.ForegroundColor = oldColor;
            Console.CursorVisible = oldCursor;
        }
    }

    private static void Draw(Session s, string input, int caret, ref int scroll, int width, int height)
    {
        Console.CursorVisible = false;
        int usable = Math.Max(1, width - 1);
        var cells = new char[Math.Max(1, height), usable];
        var colors = new ConsoleColor[Math.Max(1, height), usable];
        void Paint(int x, int y, int limit, params Part[] parts)
        {
            if (y < 0 || y >= height - 1 || x >= usable) return;
            int column = x;
            int remaining = Math.Min(limit, usable - x);
            foreach (var part in parts)
            {
                if (remaining <= 0) break;
                string text = part.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (text.Length > remaining) text = text[..remaining];
                foreach (char ch in text) { cells[y, column] = ch; colors[y, column++] = part.Color; }
                remaining -= text.Length;
            }
            while (remaining-- > 0) { cells[y, column] = ' '; colors[y, column++] = Ink.Text; }
        }
        // Every visible row is overwritten; no newlines or scrolling output.
        for (int y = 0; y < height - 1; y++) Paint(0, y, usable);
        if (width < 76 || height < 24)
        {
            Paint(0, 0, usable, new Part("Увеличьте окно до 76 x 24 или больше.", Ink.Parameter));
            Paint(0, 1, usable, new Part("Рекомендуется 110 x 34. Ввод команд остаётся доступен."));
        }
        else
        {
            int right = Math.Max(37, width / 3), left = usable - right - 3, rx = left + 3;
            int localPlayer = s.IsCoop && !s.IsHost ? 1 : 0;
            string network = s.IsCoop ? $"  |  ТОРГОВЕЦ {localPlayer + 1} ({(s.IsHost ? "ХОСТ" : "ГОСТЬ")})" : "";
            Paint(0, 0, usable, new Part(" ТОРГОВАЯ КОМПАНИЯ", Ink.Heading), new Part($"   День {s.Game.Day}  |  Монеты {s.Game.GoldFor(localPlayer)} / 5000{network}", Ink.Parameter));
            Paint(0, 1, usable, new Part(new string('-', usable), Ink.Muted));
            int contentHeight = height - 7;
            var lines = Company(s, left);
            scroll = Math.Clamp(scroll, 0, Math.Max(0, lines.Count - contentHeight));
            for (int y = 0; y < contentHeight; y++)
            {
                if (y + scroll < lines.Count) Paint(0, y + 2, left, lines[y + scroll]);
                Paint(left + 1, y + 2, 1, new Part("|", Ink.Muted));
            }
            Paint(rx, 2, right, new Part("КОМАНДЫ", Ink.Heading), new Part("  № = номер повозки"));
            for (int i = 0; i < Commands.Length && i + 3 < height - 5; i++)
                Paint(rx, i + 3, right, new Part(Commands[i].Command, Ink.Command), new Part(" " + Commands[i].Args, Ink.Parameter));
            string[] notes = ["Параметры — жёлтым; [ДНИ] необязателен.", "Повозка: 500; склад: 400 монет.", "Рейс: 8 монет за день пути.", "Время: только по команде далее.", "PageUp/Down: прокрутить компанию.", "Esc: очистить ввод. Меню без автосохранения."];
            for (int i = 0; i < notes.Length && i + 18 < height - 5; i++) Paint(rx, i + 18, right, new Part(notes[i]));
            Paint(0, height - 5, usable, new Part(new string('-', usable), Ink.Muted));
        }
        string message = s.Message.Replace('\r', ' ').Replace('\n', ' ');
        for (int i = 0; i < 2; i++)
        {
            int start = i * usable;
            Paint(0, height - 4 + i, usable, new Part(start < message.Length ? message.Substring(start, Math.Min(usable, message.Length - start)) : "", s.Error ? Ink.Error : Ink.Success));
        }
        const string prompt = "> ";
        int room = Math.Max(1, usable - prompt.Length - 1), offset = Math.Max(0, caret - room);
        string visible = input[offset..];
        if (visible.Length > room) visible = visible[..room];
        var fragments = new List<Part> { new Part(prompt, Ink.Command) };
        int commandEnd = input.IndexOf(' ');
        if (commandEnd < 0) commandEnd = input.Length;
        int commandLength = Math.Clamp(commandEnd - offset, 0, visible.Length);
        fragments.Add(new Part(visible[..commandLength], Ink.Command));
        fragments.Add(new Part(visible[commandLength..], Ink.Parameter));
        Paint(0, height - 2, usable, fragments.ToArray());
        // Draw only changed rows. Build one terminal write to avoid flashing blank frames.
        var frame = new StringBuilder();
        int[] ansiColors = [30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97];
        for (int y = 0; y < height - 1; y++)
        {
            var signature = new StringBuilder();
            for (int x = 0; x < usable; x++) signature.Append(cells[y, x]).Append((char)colors[y, x]);
            string row = signature.ToString();
            if (PreviousRows.TryGetValue(y, out var old) && row == old) continue;
            if (VirtualTerminal) frame.Append($"\u001b[{y + 1};1H");
            else Console.SetCursorPosition(0, y);
            for (int x = 0; x < usable;)
            {
                var color = colors[y, x];
                var text = new StringBuilder();
                do { text.Append(cells[y, x++]); } while (x < usable && colors[y, x] == color);
                if (VirtualTerminal) frame.Append($"\u001b[{ansiColors[(int)color]}m").Append(text);
                else Ink.Write(text.ToString(), color);
            }
            PreviousRows[y] = row;
        }
        if (VirtualTerminal) { frame.Append("\u001b[0m"); Console.Write(frame); }
        Console.SetCursorPosition(Math.Min(usable - 1, prompt.Length + caret - offset), Math.Max(0, height - 2));
        Console.CursorVisible = true;
    }

    private static List<Part[]> Company(Session s, int availableWidth)
    {
        var g = s.Game;
        int player = s.IsCoop && !s.IsHost ? 1 : 0;
        var wagons = g.WagonsFor(player);
        var warehouses = g.WarehousesFor(player);
        var lines = new List<Part[]>();
        void Line(string text, ConsoleColor color = Ink.Text) => lines.Add([new Part(text, color)]);
        Line("МИР И ТОРГОВЛЯ", Ink.Heading);
        if (s.IsCoop)
        {
            Line($"Готовность: хост {(s.HostReady ? "ДА" : "нет")} | игрок 2 {(s.ClientReady ? "ДА" : "нет")}",
                s.HostReady && s.ClientReady ? Ink.Success : Ink.Parameter);
            int rival = player == 0 ? 1 : 0;
            Line($"Вы: торговец {player + 1}, {g.GoldFor(player)} монет | торговец {rival + 1}: {g.GoldFor(rival)} монет");
        }
        int mapWidth = Math.Clamp(availableWidth, 30, 72);
        int mapHeight = mapWidth >= 60 ? (s.IsCoop ? 10 : 12) : 9;
        foreach (string mapLine in g.World.Render(mapWidth, mapHeight)) Line(mapLine, Ink.Muted);
        for (int i = 0; i < Game.Cities.Length; i += 2)
            Line($"{i + 1} {Ru.Name(Game.Cities[i]),-18}  {i + 2} {Ru.Name(Game.Cities[i + 1])}");
        Line("# дорога   ~ вода   ^ горы   * лес", Ink.Muted);
        Line("");
        Line("РЫНОК: " + Ru.Name(s.Market), Ink.Heading);
        Line("Товар         Запас Купить Продать", Ink.Muted);
        foreach (string good in Game.Goods)
            lines.Add([new Part($"{Ru.Name(good),-12}", Ink.Parameter), new Part($"{g.Stocks[s.Market][good],6} "), new Part($"{g.Price(s.Market, good, true),6} ", Ink.Parameter), new Part($"{g.Price(s.Market, good, false),6}", Ink.Success)]);
        Line("");
        Line($"ВАШИ ПОВОЗКИ ({wagons.Count}) — груз до 60", Ink.Heading);
        foreach (var w in wagons)
        {
            Line($"#{w.Id} {Ru.Name(w.City)}" + (w.Destination is null ? " | стоянка" : $" -> {Ru.Name(w.Destination)}"), Ink.Command);
            if (w.Destination is not null) Line($"   До прибытия: {w.DaysLeft} дн.");
            Line($"   Груз {w.Used}/60: " + Cargo(w.Cargo));
        }
        Line("");
        Line($"ВАШИ СКЛАДЫ ({warehouses.Count})", Ink.Heading);
        foreach (var (city, cargo) in warehouses)
        {
            Line($"{Ru.Name(city)}: {cargo.Values.Sum()}/300", Ink.Parameter);
            Line("  " + Cargo(cargo));
        }
        if (warehouses.Count == 0) Line("Складов пока нет.");
        Line("");
        Line("Цены за единицу. Цена партии меняется.");
        return lines;
    }
    private static string Cargo(Dictionary<string, int> cargo) => string.Join(", ", cargo.Where(p => p.Value > 0).Select(p => $"{Ru.Name(p.Key)} {p.Value}")) is { Length: > 0 } text ? text : "пусто";

    private static bool EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return true;
        var handle = GetStdHandle(-11);
        return GetConsoleMode(handle, out uint mode) && SetConsoleMode(handle, mode | 4);
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
}


