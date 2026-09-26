using System.Text.Json;

namespace TradeCompany;

public sealed class Wagon
{
    public int Id { get; set; }
    public string City { get; set; } = "oakwood";
    public string? Destination { get; set; }
    public decimal DaysLeft { get; set; }
    public Dictionary<string, int> Cargo { get; set; } = Game.EmptyCargo();
    public int Used => Cargo.Values.Sum();
}

public sealed class Merchant
{
    public int Gold { get; set; } = 1000;
    public bool Won { get; set; }
    public List<Wagon> Wagons { get; set; } = [new() { Id = 1 }];
    public Dictionary<string, Dictionary<string, int>> Warehouses { get; set; } = new();
}

public sealed class Game
{
    public static readonly string[] Cities = ["oakwood", "crossroads", "northmine", "riverport", "hillford", "grainfield", "weavertown", "ironbay"];
    public static readonly string[] Goods = ["grain", "wood", "iron", "tools", "cloth"];
    public static readonly int[] BasePrices = [12, 18, 30, 55, 40];
    // Local production advantages; prices also respond to available stock.
    private static readonly decimal[][] Factors = [
        [1m, .60m, 1.4m, 1.2m, 1.3m],
        [.65m, 1.2m, 1.2m, .85m, 1.1m],
        [1.5m, 1.3m, .60m, 1.35m, 1.3m],
        [1.2m, 1.4m, 1.1m, 1.1m, .60m],
        [1.1m, .75m, 1.25m, 1.0m, 1.2m],
        [.55m, 1.25m, 1.4m, 1.15m, 1.1m],
        [1.2m, 1.15m, 1.35m, 1.0m, .55m],
        [1.35m, 1.2m, .55m, 1.25m, 1.15m]];
    public int Version { get; set; } = 1;
    public int Day { get; set; } = 1;
    public int Gold { get; set; } = 1000;
    public bool Won { get; set; }
    public List<Wagon> Wagons { get; set; } = [new() { Id = 1 }];
    public Dictionary<string, Dictionary<string, int>> Stocks { get; set; } = Cities.ToDictionary(c => c, _ => Goods.ToDictionary(g => g, _ => 100));
    public Dictionary<string, Dictionary<string, int>> Warehouses { get; set; } = new();
    public Merchant? SecondMerchant { get; set; }
    public WorldMap World { get; set; } = WorldMap.Generate();
    public static Dictionary<string, int> EmptyCargo() => Goods.ToDictionary(g => g, _ => 0);
    public static string CityKey(string city) => Cities.Contains(Ru.Key(city)) ? Ru.Key(city) : throw new ArgumentException("Нет такого города. Введите карта.");
    private static string GoodKey(string good) => Goods.Contains(Ru.Key(good)) ? Ru.Key(good) : throw new ArgumentException("Товары: зерно, древесина, железо, инструменты, ткань.");
    public void EnsureSecondMerchant() => SecondMerchant ??= new Merchant();
    public int GoldFor(int player) => player == 0 ? Gold : Second().Gold;
    public bool WonFor(int player) => player == 0 ? Won : Second().Won;
    public List<Wagon> WagonsFor(int player) => player == 0 ? Wagons : Second().Wagons;
    public Dictionary<string, Dictionary<string, int>> WarehousesFor(int player) => player == 0 ? Warehouses : Second().Warehouses;
    private Merchant Second() => SecondMerchant ?? throw new InvalidOperationException("Второй торговец не создан.");
    private void SetGold(int player, int value) { if (player == 0) Gold = value; else Second().Gold = value; }
    private void SetWon(int player, bool value) { if (player == 0) Won = value; else Second().Won = value; }
    private IEnumerable<(int Player, Wagon Wagon)> AllWagons()
    {
        foreach (var wagon in Wagons) yield return (0, wagon);
        if (SecondMerchant is not null)
            foreach (var wagon in SecondMerchant.Wagons) yield return (1, wagon);
    }
    private Wagon Idle(int id, int player)
    {
        var wagon = WagonsFor(player).SingleOrDefault(w => w.Id == id) ?? throw new ArgumentException("Повозка не найдена.");
        if (wagon.Destination is not null) throw new InvalidOperationException("Повозка ещё в пути.");
        return wagon;
    }
    private static void Quantity(int n) { if (n is < 1 or > 1000) throw new ArgumentException("Количество должно быть от 1 до 1000."); }
    public int Price(string city, string good, bool buying, int? stock = null)
    {
        int c = Array.IndexOf(Cities, city), g = Array.IndexOf(Goods, good);
        decimal scarcity = Math.Clamp(1.5m - (stock ?? Stocks[city][good]) / 200m, .5m, 1.5m);
        return Math.Max(1, (int)Math.Ceiling(BasePrices[g] * Factors[c][g] * scarcity * (buying ? 1.10m : .90m)));
    }
    public string Trade(int id, string good, int amount, bool buying, int player = 0)
    {
        Quantity(amount); good = GoodKey(good); var w = Idle(id, player); var market = Stocks[w.City];
        if (buying && w.Used + amount > 60) throw new InvalidOperationException("Вместимость повозки: 60 единиц.");
        if ((buying ? market[good] : w.Cargo[good]) < amount) throw new InvalidOperationException("Недостаточно товара.");
        int total = 0;
        for (int i = 0; i < amount; i++) total += Price(w.City, good, buying, market[good] + (buying ? -i : i));
        if (buying && GoldFor(player) < total) throw new InvalidOperationException($"Нужно {total} монет.");
        SetGold(player, GoldFor(player) + (buying ? -total : total));
        market[good] += buying ? -amount : amount;
        w.Cargo[good] += buying ? amount : -amount;
        return $"{(buying ? "Куплено" : "Продано")}: {Ru.Name(good)}: {amount} шт., сумма {total}. {Victory(player)}";
    }
    private string Victory(int player)
    {
        if (WonFor(player) || GoldFor(player) < 5000) return "";
        SetWon(player, true);
        return $"Торговец {player + 1} достиг цели: 5000 монет!";
    }
    public string Travel(int id, string city, int player = 0)
    {
        city = CityKey(city); var w = Idle(id, player);
        if (city == w.City) throw new InvalidOperationException("Повозка уже здесь.");
        decimal days = World.TravelDays(w.City, city);
        int fee = (int)Math.Ceiling(days * 8);
        if (GoldFor(player) < fee) throw new InvalidOperationException($"Для рейса нужно {fee} монет.");
        SetGold(player, GoldFor(player) - fee); w.Destination = city; w.DaysLeft = days;
        return $"Повозка #{id} отправлена в город {Ru.Name(city)}: {FormatDays(days)}. Все расходы рейса ({fee}) оплачены.";
    }
    public string Advance(int days)
    {
        if (days is < 1 or > 30) throw new ArgumentException("Можно пропустить от 1 до 30 дней.");
        List<string> news = [];
        for (int d = 0; d < days; d++)
        {
            Day++;
            foreach (var (player, w) in AllWagons().Where(x => x.Wagon.Destination is not null))
            {
                w.DaysLeft -= 1;
                if (w.DaysLeft <= 0) { w.DaysLeft = 0; w.City = w.Destination!; w.Destination = null; news.Add($"День {Day}: повозка #{w.Id} торговца {player + 1} прибыла в город {Ru.Name(w.City)}."); }
            }
            for (int c = 0; c < Cities.Length; c++)
                for (int g = 0; g < Goods.Length; g++)
                {
                    var stock = Stocks[Cities[c]][Goods[g]];
                    int target = Factors[c][g] < 1 ? 150 : 65;
                    Stocks[Cities[c]][Goods[g]] = Math.Max(0, stock + Math.Clamp(target - stock, -5, 8));
                }
            if (Day % 7 == 0)
            {
                string city = Cities[(Day / 7) % Cities.Length], good = Goods[(Day / 7) % Goods.Length];
                Stocks[city][good] = Math.Max(0, Stocks[city][good] - 35);
                news.Add($"День {Day}: ярмарка в городе {Ru.Name(city)}, повышенный спрос на товар «{Ru.Name(good)}».");
            }
        }
        return news.Count == 0 ? "На рынках прошёл очередной день торговли." : string.Join(Environment.NewLine, news);
    }
    public string BuyWagon(string city, int player = 0)
    {
        city = CityKey(city);
        if (GoldFor(player) < 500) throw new InvalidOperationException("Повозка стоит 500 монет.");
        var wagons = WagonsFor(player);
        SetGold(player, GoldFor(player) - 500); wagons.Add(new Wagon { Id = wagons.Max(w => w.Id) + 1, City = city });
        return $"Куплена повозка #{wagons.Last().Id} в городе {Ru.Name(city)}.";
    }
    public string BuyWarehouse(string city, int player = 0)
    {
        city = CityKey(city);
        var warehouses = WarehousesFor(player);
        if (warehouses.ContainsKey(city)) throw new InvalidOperationException("Здесь уже есть склад.");
        if (GoldFor(player) < 400) throw new InvalidOperationException("Склад стоит 400 монет.");
        SetGold(player, GoldFor(player) - 400); warehouses[city] = EmptyCargo();
        return $"Открыт склад в городе {Ru.Name(city)}. Вместимость: 300 единиц.";
    }
    public string Transfer(int id, string good, int amount, bool storing, int player = 0)
    {
        Quantity(amount); good = GoodKey(good); var w = Idle(id, player);
        if (!WarehousesFor(player).TryGetValue(w.City, out var warehouse)) throw new InvalidOperationException("В этом городе нет вашего склада.");
        var source = storing ? w.Cargo : warehouse; var destination = storing ? warehouse : w.Cargo;
        if (source[good] < amount) throw new InvalidOperationException("Недостаточно товара.");
        if (destination.Values.Sum() + amount > (storing ? 300 : 60)) throw new InvalidOperationException("Недостаточно места.");
        source[good] -= amount; destination[good] += amount;
        return $"Перемещено {Ru.Name(good)}: {amount} шт. {(storing ? "на склад" : "в повозку")}.";
    }
    public static string FormatDays(decimal days)
    {
        string value = days.ToString(days % 1 == 0 ? "0" : "0.0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        return value + (days == 1 ? " день" : days is >= 2 and <= 4 ? " дня" : " дней");
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    public static Game Load(string path)
    {
        var game = JsonSerializer.Deserialize<Game>(File.ReadAllText(path));
        if (game is null || game.Version != 1 || game.Day < 1 || game.Gold < 0 || game.Wagons is null || game.Wagons.Count == 0 || game.Stocks is null || game.Warehouses is null)
            throw new InvalidOperationException("Некорректное сохранение.");
        foreach (string city in Cities)
            if (!game.Stocks.ContainsKey(city)) game.Stocks[city] = Goods.ToDictionary(g => g, _ => 100);
        bool CargoValid(Dictionary<string, int>? cargo, int max) => cargo is not null && cargo.Count == Goods.Length && Goods.All(g => cargo.TryGetValue(g, out int n) && n >= 0 && n <= max) && cargo.Values.Sum(n => (long)n) <= max;
        bool MerchantValid(int gold, List<Wagon>? wagons, Dictionary<string, Dictionary<string, int>>? warehouses) =>
            gold >= 0 && wagons is not null && wagons.Count > 0 && warehouses is not null &&
            wagons.Select(w => w.Id).Distinct().Count() == wagons.Count &&
            wagons.All(w => w.Id >= 1 && Cities.Contains(w.City) && CargoValid(w.Cargo, 60) && (w.Destination is null ? w.DaysLeft == 0 : Cities.Contains(w.Destination) && w.DaysLeft is >= 0.5m and <= 100)) &&
            warehouses.All(kv => Cities.Contains(kv.Key) && CargoValid(kv.Value, 300));
        if (game.Wagons.Select(w => w.Id).Distinct().Count() != game.Wagons.Count ||
            game.Wagons.Any(w => w.Id < 1 || !Cities.Contains(w.City) || !CargoValid(w.Cargo, 60) || (w.Destination is null ? w.DaysLeft != 0 : !Cities.Contains(w.Destination) || w.DaysLeft is < 0.5m or > 100)) ||
            !Cities.All(c => game.Stocks.TryGetValue(c, out var stock) && CargoValid(stock, 1000000)) ||
            game.Warehouses.Any(kv => !Cities.Contains(kv.Key) || !CargoValid(kv.Value, 300)) ||
            game.SecondMerchant is { } second && !MerchantValid(second.Gold, second.Wagons, second.Warehouses) ||
            game.World is null || !Cities.All(game.World.Cities.ContainsKey) || game.World.Roads.Count < Cities.Length - 1)
            throw new InvalidOperationException("Сохранение содержит некорректные данные.");
        return game;
    }
}


