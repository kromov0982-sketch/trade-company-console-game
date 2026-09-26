namespace TradeCompany;

public enum MenuChoice { NewGame, LoadGame, HostGame, JoinGame }

public static class MainMenu
{
    private static readonly string[] Items = ["НОВАЯ ИГРА", "ЗАГРУЗИТЬ ИГРУ", "СОЗДАТЬ СЕТЕВУЮ ИГРУ", "ПОДКЛЮЧИТЬСЯ", "ВЫХОД"];
    private static string? error;

    public static MenuChoice? Choose()
    {
        int selected = 0;
        int oldWidth = 0, oldHeight = 0;
        bool dirty = true;
        Console.CursorVisible = false;
        ConsoleWindow.ClearViewport();
        while (true)
        {
            int width = Console.WindowWidth, height = Console.WindowHeight;
            if (width != oldWidth || height != oldHeight)
            {
                oldWidth = width;
                oldHeight = height;
                ConsoleWindow.ClearViewport();
                dirty = true;
            }
            if (dirty)
            {
                Draw(selected, width, height);
                dirty = false;
            }
            if (!Console.KeyAvailable) { Thread.Sleep(30); continue; }
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.W:
                    selected = (selected + Items.Length - 1) % Items.Length;
                    error = null;
                    dirty = true;
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.S:
                    selected = (selected + 1) % Items.Length;
                    error = null;
                    dirty = true;
                    break;
                case ConsoleKey.Escape:
                    Console.Clear();
                    return null;
                case ConsoleKey.Enter:
                    if (selected == 1 && !File.Exists("saves/company.json"))
                    {
                        error = "Сохранённая игра не найдена.";
                        dirty = true;
                        break;
                    }
                    Console.Clear();
                    return selected switch
                    {
                        0 => MenuChoice.NewGame,
                        1 => MenuChoice.LoadGame,
                        2 => MenuChoice.HostGame,
                        3 => MenuChoice.JoinGame,
                        _ => null
                    };
            }
        }
    }

    public static void ShowError(string message) => error = message;

    private static void Draw(int selected, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        int boxWidth = Math.Min(62, Math.Max(30, width - 4));
        int x = Math.Max(0, (width - boxWidth) / 2);
        int y = Math.Max(0, (height - 20) / 2);

        void At(int row, string text, ConsoleColor color = Ink.Text)
        {
            if (y + row >= height - 1) return;
            Console.SetCursorPosition(x, y + row);
            Ink.Write(text.PadRight(boxWidth)[..boxWidth], color);
        }

        At(0, "+" + new string('-', boxWidth - 2) + "+", Ink.Muted);
        At(1, Center("ТОРГОВАЯ КОМПАНИЯ", boxWidth), Ink.Heading);
        At(2, Center("экономическая стратегия", boxWidth), Ink.Muted);
        At(3, "+" + new string('-', boxWidth - 2) + "+", Ink.Muted);
        for (int i = 0; i < Items.Length; i++)
        {
            string marker = i == selected ? "> " : "  ";
            At(5 + i * 2, Center(marker + Items[i], boxWidth), i == selected ? Ink.Command : Ink.Text);
        }
        At(16, Center("Стрелки / W,S — выбор    Enter — открыть", boxWidth), Ink.Muted);
        bool saveExists = File.Exists("saves/company.json");
        At(17, Center(saveExists ? "Сохранённая игра найдена" : "Сохранённой игры пока нет", boxWidth), saveExists ? Ink.Success : Ink.Muted);
        At(19, Center(error ?? "", boxWidth), error is null ? Ink.Text : Ink.Error);
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        return new string(' ', (width - text.Length) / 2) + text;
    }
}
