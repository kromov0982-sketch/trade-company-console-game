namespace TradeCompany;

public static class CoopLobby
{
    public static CoopLink? WaitForGuest()
    {
        ConsoleWindow.ClearViewport();
        Console.CursorVisible = false;
        using var cancellation = new CancellationTokenSource();
        Task<CoopLink> task;
        try { task = CoopLink.HostAsync(cancellation.Token); }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException)
        {
            Logger.Error("Не удалось создать сетевую игру.", e);
            MainMenu.ShowError("Не удалось создать игру: " + e.Message);
            return null;
        }

        while (!task.IsCompleted)
        {
            DrawHost();
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                cancellation.Cancel();
                try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                return null;
            }
            Thread.Sleep(50);
        }
        try { return task.GetAwaiter().GetResult(); }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            if (e is not OperationCanceledException) Logger.Error("Ошибка ожидания второго игрока.", e);
            MainMenu.ShowError("Не удалось создать игру: " + e.Message);
            return null;
        }
    }

    public static CoopLink? Connect()
    {
        ConsoleWindow.ClearViewport();
        Console.CursorVisible = true;
        Ink.Line("ПОДКЛЮЧЕНИЕ К ЛОКАЛЬНОЙ ИГРЕ", Ink.Heading);
        Ink.Line();
        Ink.Line("Введите IPv4-адрес Radmin VPN, показанный на экране хоста.");
        Ink.Line("Обычно адрес Radmin начинается с 26, например: 26.14.81.7", Ink.Muted);
        Ink.Line("Оставьте строку пустой для возврата в меню.");
        Ink.Write("\nIP-адрес > ", Ink.Command);
        string? address = ConsoleInput.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(address)) return null;
        if (!System.Net.IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            MainMenu.ShowError("Введите корректный IPv4-адрес Radmin VPN.");
            Logger.Error($"Введён некорректный адрес сетевой игры: {address}");
            return null;
        }

        Ink.Line("Подключение…", Ink.Parameter);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { return CoopLink.JoinAsync(address, cancellation.Token).GetAwaiter().GetResult(); }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or OperationCanceledException)
        {
            Logger.Error($"Не удалось подключиться к хосту {address}:{CoopLink.Port}.", e);
            MainMenu.ShowError("Не удалось подключиться: " + e.Message);
            return null;
        }
    }

    private static void DrawHost()
    {
        int width = Math.Max(1, Console.WindowWidth);
        int height = Math.Max(1, Console.WindowHeight);
        int y = Math.Max(0, height / 2 - 4);
        void Center(int row, string text, ConsoleColor color)
        {
            if (y + row >= height - 1) return;
            int x = Math.Max(0, (width - text.Length) / 2);
            Console.SetCursorPosition(x, y + row);
            Ink.Write(text, color);
        }
        Center(0, "СЕТЕВАЯ ИГРА СОЗДАНА", Ink.Heading);
        Center(2, "Адреса этого компьютера:", Ink.Text);
        var addresses = CoopLink.LocalAddresses();
        for (int i = 0; i < Math.Min(addresses.Count, 4); i++)
        {
            var address = addresses[i];
            string label = address.IsRadmin ? $"RADMIN VPN: {address.Address}" : $"{address.Adapter}: {address.Address}";
            Center(3 + i, label, address.IsRadmin ? Ink.Success : Ink.Parameter);
        }
        if (!addresses.Any(address => address.IsRadmin))
            Center(8, "Radmin VPN не найден. Проверьте, что он запущен.", Ink.Error);
        Center(10, $"TCP-порт: {CoopLink.Port}", Ink.Muted);
        Center(12, "Ожидание второго игрока…", Ink.Command);
        Center(14, "Esc — отменить", Ink.Muted);
    }
}
