namespace TradeCompany;

public sealed class Session : IDisposable
{
    public Game Game { get; private set; }
    public string Market { get; private set; } = "oakwood";
    public string Message { get; private set; }
    public bool Error { get; private set; }
    public bool Exit { get; private set; }
    public bool IsCoop => link is not null;
    public bool IsHost { get; }
    public bool HostReady { get; private set; }
    public bool ClientReady { get; private set; }
    public int Revision { get; private set; }

    private readonly CoopLink? link;

    private Session(Game game, string message, CoopLink? link = null, bool isHost = false)
    {
        Game = game;
        Message = message;
        this.link = link;
        IsHost = isHost;
    }

    public static Session NewGame()
    {
        var game = new Game();
        int days = game.World.TravelDays("oakwood", "crossroads");
        return new Session(game,
            $"Новая карта создана. Первый рейс: купить 1 древесина 30; ехать 1 перекрёсток; далее {days}; продать 1 древесина 30.");
    }

    public static Session LoadGame() => new(Game.Load("saves/company.json"), "Игра загружена.");

    public static Session HostGame(CoopLink link)
    {
        var game = new Game();
        game.EnsureSecondMerchant();
        var session = new Session(game, "Второй торговец подключён. Для смены дня оба игрока должны ввести «далее».", link, true);
        session.Broadcast();
        return session;
    }

    public static Session JoinGame(CoopLink link) =>
        new(new Game(), "Подключение установлено. Ожидание состояния от хоста…", link);

    public void PumpNetwork()
    {
        if (link is null) return;
        while (link.TryReceive(out var packet))
        {
            if (packet.Type == "disconnect")
            {
                Error = true;
                Message = "Соединение со вторым игроком потеряно. Возврат в меню — команда «меню».";
                HostReady = ClientReady = false;
                Revision++;
                continue;
            }
            if (IsHost && packet.Type == "command" && packet.Command is not null)
            {
                ExecuteHostCommand(packet.Command, remote: true);
                Broadcast();
            }
            else if (!IsHost && packet.Type == "state" && packet.Game is not null)
            {
                Game = packet.Game;
                HostReady = packet.HostReady;
                ClientReady = packet.ClientReady;
                Message = packet.Message ?? "Состояние компании обновлено.";
                Error = false;
                Revision++;
            }
        }
    }

    public void Execute(string line)
    {
        var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (p.Length == 0) return;
        string command = Ru.Command(p[0]);
        Error = false;

        // These commands affect only this screen and never cross the network.
        if (command == "menu" || command == "quit") { Exit = true; Message = "Возврат в главное меню."; Revision++; return; }
        if (command == "help") { Message = "Команды справа. В сетевой игре день меняется после команды «далее» от обоих игроков."; Revision++; return; }
        if (command is "map" or "status") { Message = "Состояние компании обновлено."; Revision++; return; }
        if (command == "market" && p.Length == 2)
        {
            try { Market = Game.CityKey(p[1]); Message = "Открыт рынок: " + Ru.Name(Market); }
            catch (ArgumentException e) { Error = true; Message = "Ошибка: " + e.Message; }
            Revision++;
            return;
        }

        if (link is not null && !IsHost)
        {
            link.Send(new CoopPacket { Type = "command", Command = line });
            Message = command == "next" ? "Готовность отправлена хосту…" : "Команда отправлена хосту…";
            Revision++;
            return;
        }

        ExecuteHostCommand(line, remote: false);
        if (IsCoop) Broadcast();
    }

    private void ExecuteHostCommand(string line, bool remote)
    {
        var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (p.Length == 0) return;
        Error = false;
        try
        {
            int Number(int index) => int.Parse(p[index]);
            string command = Ru.Command(p[0]);
            int player = remote ? 1 : 0;
            if (IsCoop && command == "next")
            {
                if (p.Length != 1) throw new ArgumentException("В сетевой игре команда «далее» используется без числа дней.");
                SetReady(remote);
                Revision++;
                return;
            }
            Message = command switch
            {
                "market" when p.Length == 2 && !remote => SelectMarket(p[1]),
                "buy" when p.Length == 4 => Game.Trade(Number(1), p[2], Number(3), true, player),
                "sell" when p.Length == 4 => Game.Trade(Number(1), p[2], Number(3), false, player),
                "travel" when p.Length == 3 => Game.Travel(Number(1), p[2], player),
                "next" when p.Length <= 2 => Game.Advance(p.Length == 2 ? Number(1) : 1),
                "wagon" when p.Length == 2 => Game.BuyWagon(p[1], player),
                "warehouse" when p.Length == 2 => Game.BuyWarehouse(p[1], player),
                "store" when p.Length == 4 => Game.Transfer(Number(1), p[2], Number(3), true, player),
                "load" when p.Length == 4 => Game.Transfer(Number(1), p[2], Number(3), false, player),
                "save" when !remote => Save(),
                "loadgame" when !remote && !IsCoop => Load(),
                _ => throw new ArgumentException("Неизвестная команда или неверные параметры. См. справку справа.")
            };
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or OverflowException or IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Error = true;
            Message = "Ошибка: " + e.Message;
        }
        Revision++;
    }

    private void SetReady(bool remote)
    {
        if (remote) ClientReady = true; else HostReady = true;
        if (HostReady && ClientReady)
        {
            Message = Game.Advance(1);
            Message += " Оба игрока готовы — наступил новый день.";
            HostReady = ClientReady = false;
        }
        else Message = $"{(remote ? "Игрок 2" : "Хост")} готов. Ожидание второго игрока…";
    }

    private void Broadcast() => link?.Send(new CoopPacket
    {
        Type = "state", Game = Game, Message = Message,
        HostReady = HostReady, ClientReady = ClientReady
    });

    private string SelectMarket(string city) { Market = Game.CityKey(city); return "Открыт рынок: " + Ru.Name(Market); }
    private string Save() { Game.Save("saves/company.json"); return "Игра сохранена."; }
    private string Load() { Game = Game.Load("saves/company.json"); return "Игра загружена."; }
    public void Dispose() => link?.Dispose();
}
