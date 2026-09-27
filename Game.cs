using System.Text.Json;

namespace TradeCompany;

public sealed class Wagon
{
    public int Id { get; set; }
    public string City { get; set; } = "oakwood";
    public string? Destination { get; set; }
    public decimal DaysLeft { get; set; }
    public Dictionary<string, int> Cargo { get; set; } = Game.EmptyCargo();
    public List<string> Route { get; set; } = [];
    public decimal TotalDays { get; set; }
    public int Used => Cargo.Values.Sum();
}

public sealed class Merchant
{
    public int Gold { get; set; } = 1000;
    public bool Won { get; set; }
    public List<Wagon> Wagons { get; set; } = [new() { Id = 1 }];
    public Dictionary<string, Dictionary<string, int>> Warehouses { get; set; } = new();
    public Dictionary<string, MarketReport> MarketReports { get; set; } = new();
    public List<string> VisitedCities { get; set; } = ["oakwood"];
}

public sealed class MarketReport
{
    public int Day { get; set; }
    public Dictionary<string, int> Stocks { get; set; } = new();
}

public sealed record MarketKnowledge(string City, int? Day, bool Approximate, bool Current, Dictionary<string, int> Stocks, bool Unknown = false);

public sealed class Enterprise
{
    public int Id { get; set; }
    public string City { get; set; } = "oakwood";
    public string Type { get; set; } = "farm";
    public int? Owner { get; set; }
}

public sealed record EnterpriseRecipe(string Name, Dictionary<string, int> Inputs, Dictionary<string, int> Outputs);

public sealed class Contract
{
    public int Id { get; set; }
    public string Issuer { get; set; } = "";
    public string City { get; set; } = "oakwood";
    public string Good { get; set; } = "bread";
    public int Amount { get; set; }
    public int Reward { get; set; }
    public int DeadlineDay { get; set; }
    public int? AcceptedBy { get; set; }
    public bool Completed { get; set; }
    public bool Failed { get; set; }
}

public sealed class Game
{
    public const int TargetCompanyValue = 50_000;
    public static readonly string[] Cities = ["oakwood", "crossroads", "northmine", "riverport", "hillford", "grainfield", "weavertown", "ironbay"];
    public static readonly string[] Goods = ["grain", "wood", "stone", "ore", "coal", "wool", "flour", "ingots", "lumber", "cutstone", "tools", "cloth", "bread"];
    public static readonly int[] BasePrices = [12, 18, 16, 25, 28, 20, 22, 45, 30, 32, 70, 50, 35];
    public static readonly IReadOnlyDictionary<string, EnterpriseRecipe> EnterpriseRecipes =
        new Dictionary<string, EnterpriseRecipe>
        {
            ["farm"] = new("Ферма", new(), new() { ["grain"] = 10 }),
            ["logging"] = new("Лесозаготовка", new(), new() { ["wood"] = 10 }),
            ["quarry"] = new("Каменоломня", new(), new() { ["stone"] = 9 }),
            ["mine"] = new("Рудник", new(), new() { ["ore"] = 8 }),
            ["sheepfarm"] = new("Овцеводческая ферма", new(), new() { ["wool"] = 8 }),
            ["charcoal"] = new("Углежогная мастерская", new() { ["wood"] = 4 }, new() { ["coal"] = 3 }),
            ["sawmill"] = new("Лесопилка", new() { ["wood"] = 4 }, new() { ["lumber"] = 3 }),
            ["mill"] = new("Мельница", new() { ["grain"] = 5 }, new() { ["flour"] = 4 }),
            ["smelter"] = new("Плавильня", new() { ["ore"] = 4, ["coal"] = 2 }, new() { ["ingots"] = 3 }),
            ["stoneworks"] = new("Камнерезная мастерская", new() { ["stone"] = 4 }, new() { ["cutstone"] = 3 }),
            ["workshop"] = new("Инструментальная мастерская", new() { ["ingots"] = 2, ["lumber"] = 1 }, new() { ["tools"] = 2 }),
            ["weaver"] = new("Ткацкая мастерская", new() { ["wool"] = 4 }, new() { ["cloth"] = 3 }),
            ["bakery"] = new("Пекарня", new() { ["flour"] = 3, ["coal"] = 1 }, new() { ["bread"] = 4 })
        };
    private static readonly HashSet<string> ConsumerGoods = ["grain", "lumber", "cutstone", "tools", "cloth", "bread"];
    public static readonly string[] FinalGoods = ["bread", "cloth", "tools", "cutstone"];
    public int Version { get; set; } = 1;
    public int Day { get; set; } = 1;
    public int Gold { get; set; } = 1000;
    public bool Won { get; set; }
    public List<Wagon> Wagons { get; set; } = [new() { Id = 1 }];
    public Dictionary<string, Dictionary<string, int>> Stocks { get; set; } = Cities.ToDictionary(c => c, _ => Goods.ToDictionary(g => g, _ => 100));
    public Dictionary<string, Dictionary<string, int>> Warehouses { get; set; } = new();
    public Dictionary<string, MarketReport> MarketReports { get; set; } = new();
    public List<string> VisitedCities { get; set; } = ["oakwood"];
    public Merchant? SecondMerchant { get; set; }
    public WorldMap World { get; set; } = WorldMap.Generate();
    public List<Enterprise> Enterprises { get; set; } = [];
    public List<Contract> Contracts { get; set; } = [];
    public Game()
    {
        Enterprises = GenerateEnterprises(World.Seed);
        InitializeMarketStocks(World.Seed);
        Contracts = GenerateContracts(World.Seed, Day, 1, 5);
        ObserveMarket("oakwood", 0);
    }
    public static Dictionary<string, int> EmptyCargo() => Goods.ToDictionary(g => g, _ => 0);
    public static string CityKey(string city) => Cities.Contains(Ru.Key(city)) ? Ru.Key(city) : throw new ArgumentException("Нет такого города. Введите карта.");
    private static string GoodKey(string good) => Goods.Contains(Ru.Key(good)) ? Ru.Key(good) : throw new ArgumentException("Нет такого товара. Откройте рынок, чтобы увидеть список.");
    public void EnsureSecondMerchant() => SecondMerchant ??= new Merchant();
    public int GoldFor(int player) => player == 0 ? Gold : Second().Gold;
    public bool WonFor(int player) => player == 0 ? Won : Second().Won;
    public List<Wagon> WagonsFor(int player) => player == 0 ? Wagons : Second().Wagons;
    public Dictionary<string, Dictionary<string, int>> WarehousesFor(int player) => player == 0 ? Warehouses : Second().Warehouses;
    public Dictionary<string, MarketReport> MarketReportsFor(int player) => player == 0 ? MarketReports : Second().MarketReports;
    public List<string> VisitedCitiesFor(int player) => player == 0 ? VisitedCities : Second().VisitedCities;
    private Merchant Second() => SecondMerchant ?? throw new InvalidOperationException("Второй торговец не создан.");
    private void SetGold(int player, int value) { if (player == 0) Gold = value; else Second().Gold = value; }
    private void SetWon(int player, bool value) { if (player == 0) Won = value; else Second().Won = value; }
    public int CompanyValue(int player)
    {
        long cargo = WagonsFor(player).Sum(w => CargoValue(w.Cargo)) + WarehousesFor(player).Values.Sum(CargoValue);
        long assets = WagonsFor(player).Count * 500L + WarehousesFor(player).Count * 400L + Enterprises.Where(e => e.Owner == player).Sum(EnterprisePrice);
        return (int)Math.Min(int.MaxValue, GoldFor(player) + cargo + assets);
    }
    private static long CargoValue(Dictionary<string, int> cargo) => Goods.Select((good, index) => (long)cargo.GetValueOrDefault(good) * BasePrices[index]).Sum();
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
        int g = Array.IndexOf(Goods, good);
        decimal scarcity = Math.Clamp(1.5m - (stock ?? Stocks[city][good]) / 200m, .5m, 1.5m);
        bool produced = EnterprisesAt(city).Any(e => EnterpriseRecipes[e.Type].Outputs.ContainsKey(good));
        bool consumed = EnterprisesAt(city).Any(e => EnterpriseRecipes[e.Type].Inputs.ContainsKey(good));
        decimal local = produced && consumed ? .95m : produced ? .72m : consumed ? 1.22m : 1m;
        return Math.Max(1, (int)Math.Ceiling(BasePrices[g] * local * scarcity * (buying ? 1.10m : .90m)));
    }
    public MarketKnowledge MarketInfo(string city, int player = 0)
    {
        city = CityKey(city);
        if (WagonsFor(player).Any(w => w.City == city && w.Destination is null))
            return new(city, Day, false, true, new(Stocks[city]));
        if (MarketReportsFor(player).TryGetValue(city, out var report))
            return new(city, report.Day, false, false, new(report.Stocks));
        if (!VisitedCitiesFor(player).Any(visited => World.AreNeighbors(visited, city)))
            return new(city, null, false, false, new(), true);
        var estimate = Goods.ToDictionary(good => good, good => TargetStock(city, good));
        return new(city, null, true, false, estimate);
    }
    public IEnumerable<Enterprise> EnterprisesAt(string city) => Enterprises.Where(e => e.City == city);
    public static string RecipeText(Enterprise enterprise)
    {
        var recipe = EnterpriseRecipes[enterprise.Type];
        string inputs = recipe.Inputs.Count == 0 ? "сырьё не требуется" : string.Join(" + ", recipe.Inputs.Select(p => $"{Ru.Name(p.Key)} {p.Value}"));
        string outputs = string.Join(" + ", recipe.Outputs.Select(p => $"{Ru.Name(p.Key)} {p.Value}"));
        return $"{recipe.Name}: {inputs} → {outputs}";
    }
    public static int EnterprisePrice(Enterprise enterprise)
    {
        int inputs = EnterpriseRecipes[enterprise.Type].Inputs.Count;
        return inputs switch { 0 => 4_000, 1 => 6_500, _ => 9_000 };
    }
    public static int EnterpriseQuarterIncome(Enterprise enterprise)
    {
        int inputs = EnterpriseRecipes[enterprise.Type].Inputs.Count;
        return inputs switch { 0 => 450, 1 => 750, _ => 1_100 };
    }
    public string BuyEnterprise(int id, int player = 0)
    {
        var enterprise = Enterprises.SingleOrDefault(e => e.Id == id) ?? throw new ArgumentException("Предприятие не найдено.");
        if (enterprise.Owner is not null) throw new InvalidOperationException("Это предприятие уже принадлежит торговой компании.");
        int price = EnterprisePrice(enterprise);
        if (GoldFor(player) < price) throw new InvalidOperationException($"Для покупки предприятия нужно {price} монет.");
        SetGold(player, GoldFor(player) - price);
        enterprise.Owner = player;
        return $"Куплено предприятие «{EnterpriseRecipes[enterprise.Type].Name}» в городе {Ru.Name(enterprise.City)} за {price} монет. Квартальный доход: {EnterpriseQuarterIncome(enterprise)}. {Victory(player)}";
    }
    public string AcceptContract(int id, int player = 0)
    {
        var contract = ActiveContract(id);
        if (contract.AcceptedBy is not null) throw new InvalidOperationException("Этот контракт уже принят.");
        if (Contracts.Count(c => c.AcceptedBy == player && !c.Completed && !c.Failed) >= 3)
            throw new InvalidOperationException("Одновременно можно выполнять не больше трёх контрактов.");
        contract.AcceptedBy = player;
        return $"Контракт #{id} принят. Доставьте {Ru.Name(contract.Good)}: {contract.Amount} в город {Ru.Name(contract.City)} до дня {contract.DeadlineDay}.";
    }
    public string DeliverContract(int id, int wagonId, int player = 0)
    {
        var contract = ActiveContract(id);
        if (contract.AcceptedBy != player) throw new InvalidOperationException("Сначала примите этот контракт.");
        var wagon = Idle(wagonId, player);
        if (wagon.City != contract.City) throw new InvalidOperationException($"Товар нужно доставить в город {Ru.Name(contract.City)}.");
        if (wagon.Cargo[contract.Good] < contract.Amount) throw new InvalidOperationException($"Нужно {contract.Amount} ед. товара «{Ru.Name(contract.Good)}».");
        wagon.Cargo[contract.Good] -= contract.Amount;
        SetGold(player, GoldFor(player) + contract.Reward);
        contract.Completed = true;
        return $"Контракт #{id} выполнен. Получено {contract.Reward} монет. {Victory(player)}";
    }
    private Contract ActiveContract(int id)
    {
        var contract = Contracts.SingleOrDefault(c => c.Id == id) ?? throw new ArgumentException("Контракт не найден.");
        if (contract.Completed) throw new InvalidOperationException("Контракт уже выполнен.");
        if (contract.Failed || Day > contract.DeadlineDay) throw new InvalidOperationException("Срок контракта истёк.");
        return contract;
    }
    public string BuyMarketInfo(string city, int player = 0)
    {
        const int fee = 40;
        city = CityKey(city);
        if (WagonsFor(player).Any(w => w.City == city && w.Destination is null))
            throw new InvalidOperationException("Повозка уже находится в этом городе: рынок виден бесплатно.");
        if (GoldFor(player) < fee) throw new InvalidOperationException($"Сведения о рынке стоят {fee} монет.");
        SetGold(player, GoldFor(player) - fee);
        ObserveMarket(city, player);
        return $"Куплены свежие сведения о рынке города {Ru.Name(city)} за {fee} монет.";
    }
    private void ObserveMarket(string city, int player) =>
        MarketReportsFor(player)[city] = new MarketReport { Day = Day, Stocks = new(Stocks[city]) };
    private int TargetStock(string city, string good)
    {
        bool produced = EnterprisesAt(city).Any(e => EnterpriseRecipes[e.Type].Outputs.ContainsKey(good));
        bool consumed = EnterprisesAt(city).Any(e => EnterpriseRecipes[e.Type].Inputs.ContainsKey(good));
        return produced && consumed ? 100 : produced ? 140 : consumed ? 55 : 90;
    }
    private void InitializeMarketStocks(int seed)
    {
        var random = new Random(seed ^ 0x2357A91D);
        foreach (string city in Cities)
            foreach (string good in Goods)
                Stocks[city][good] = Math.Max(20, TargetStock(city, good) + random.Next(-12, 13));
    }
    private static List<Enterprise> GenerateEnterprises(int seed)
    {
        string[] types = [.. EnterpriseRecipes.Keys, "farm", "logging", "quarry"];
        var random = new Random(seed ^ 0x4D3A2B1C);
        string[] cities = Cities.OrderBy(_ => random.Next()).ToArray();
        return types.OrderBy(_ => random.Next()).Select((type, index) => new Enterprise
        {
            Id = index + 1,
            Type = type,
            City = cities[index % cities.Length]
        }).ToList();
    }
    private static List<Contract> GenerateContracts(int seed, int day, int firstId, int count)
    {
        var random = new Random(seed ^ day * 7919 ^ firstId * 104729);
        var guilds = new Dictionary<string, string>
        {
            ["bread"] = "Гильдия пекарей", ["cloth"] = "Гильдия портных",
            ["tools"] = "Гильдия ремесленников", ["cutstone"] = "Гильдия каменщиков"
        };
        var result = new List<Contract>();
        for (int i = 0; i < count; i++)
        {
            string good = FinalGoods[random.Next(FinalGoods.Length)];
            string city = Cities[random.Next(Cities.Length)];
            int amount = random.Next(8, 21);
            int basePrice = BasePrices[Array.IndexOf(Goods, good)];
            result.Add(new Contract
            {
                Id = firstId + i,
                Good = good,
                City = city,
                Amount = amount,
                DeadlineDay = day + random.Next(12, 23),
                Reward = (int)Math.Ceiling(basePrice * amount * 2.4m) + random.Next(80, 181),
                Issuer = random.Next(2) == 0 ? $"Совет города {Ru.Name(city)}" : guilds[good]
            });
        }
        return result;
    }
    private void RunEnterprises()
    {
        // Raw-material producers run first so processors in the same city can use today's output.
        foreach (var enterprise in Enterprises.OrderBy(e => EnterpriseRecipes[e.Type].Inputs.Count))
        {
            var recipe = EnterpriseRecipes[enterprise.Type];
            var stock = Stocks[enterprise.City];
            if (recipe.Inputs.Any(input => stock[input.Key] < input.Value)) continue;
            foreach (var input in recipe.Inputs) stock[input.Key] -= input.Value;
            foreach (var output in recipe.Outputs) stock[output.Key] += output.Value;
        }
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
        int value = CompanyValue(player);
        if (WonFor(player) || value < TargetCompanyValue) return "";
        SetWon(player, true);
        return $"Торговец {player + 1} достиг цели: капитал компании {value} монет!";
    }
    public string Travel(int id, string city, int player = 0)
    {
        city = CityKey(city); var w = Idle(id, player);
        if (city == w.City) throw new InvalidOperationException("Повозка уже здесь.");
        var route = World.FindRoute(w.City, city);
        decimal days = World.TravelDays(w.City, city);
        int fee = (int)Math.Ceiling(days * 8);
        if (GoldFor(player) < fee) throw new InvalidOperationException($"Для рейса нужно {fee} монет.");
        ObserveMarket(w.City, player);
        SetGold(player, GoldFor(player) - fee); w.Destination = city; w.DaysLeft = days; w.TotalDays = days; w.Route = route;
        return $"Повозка #{id} отправлена в город {Ru.Name(city)}: {FormatDays(days)}, {DistanceKm(days)} км. Все расходы рейса ({fee} монет) оплачены.";
    }
    public string Advance(int days)
    {
        if (days is < 1 or > 30) throw new ArgumentException("Можно пропустить от 1 до 30 дней.");
        List<string> news = [];
        for (int d = 0; d < days; d++)
        {
            Day++;
            foreach (var contract in Contracts.Where(c => !c.Completed && !c.Failed && Day > c.DeadlineDay))
            {
                contract.Failed = true;
                if (contract.AcceptedBy is not null) news.Add($"День {Day}: провален контракт #{contract.Id} — истёк срок доставки.");
            }
            foreach (var (player, w) in AllWagons().Where(x => x.Wagon.Destination is not null))
            {
                w.DaysLeft -= 1;
                if (w.DaysLeft <= 0)
                {
                    w.DaysLeft = 0; w.City = w.Destination!; w.Destination = null; w.TotalDays = 0; w.Route = [];
                    if (!VisitedCitiesFor(player).Contains(w.City)) VisitedCitiesFor(player).Add(w.City);
                    news.Add($"День {Day}: повозка #{w.Id} торговца {player + 1} прибыла в город {Ru.Name(w.City)}.");
                }
            }
            RunEnterprises();
            foreach (string city in Cities)
                foreach (string good in ConsumerGoods)
                    Stocks[city][good] = Math.Max(0, Stocks[city][good] - 1);
            if (Day % 7 == 0)
            {
                foreach (int owner in Enterprises.Where(e => e.Owner is not null).Select(e => e.Owner!.Value).Distinct())
                {
                    int income = Enterprises.Where(e => e.Owner == owner).Sum(EnterpriseQuarterIncome);
                    SetGold(owner, GoldFor(owner) + income);
                    news.Add($"День {Day}: квартальный доход предприятий торговца {owner + 1} — {income} монет. {Victory(owner)}");
                }
                string city = Cities[(Day / 7) % Cities.Length], good = Goods[(Day / 7) % Goods.Length];
                Stocks[city][good] = Math.Max(0, Stocks[city][good] - 35);
                news.Add($"День {Day}: ярмарка в городе {Ru.Name(city)}, повышенный спрос на товар «{Ru.Name(good)}».");
                int nextId = Contracts.Count == 0 ? 1 : Contracts.Max(c => c.Id) + 1;
                Contracts.AddRange(GenerateContracts(World.Seed, Day, nextId, 2));
                news.Add($"День {Day}: города и гильдии разместили новые контракты.");
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
        if (!VisitedCitiesFor(player).Contains(city)) VisitedCitiesFor(player).Add(city);
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
    public static int DistanceKm(decimal days) => (int)Math.Ceiling(days * 30);
    public static string FinancialDate(int day)
    {
        int year = (day - 1) / 28 + 1;
        int quarter = (day - 1) % 28 / 7 + 1;
        int dayInQuarter = (day - 1) % 7 + 1;
        return $"год {year}, квартал {quarter}, день {dayInQuarter}/7";
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    public static Game Load(string path)
    {
        string json = File.ReadAllText(path);
        bool hasMarketReports = JsonDocument.Parse(json).RootElement.TryGetProperty(nameof(MarketReports), out _);
        bool hasEnterprises = JsonDocument.Parse(json).RootElement.TryGetProperty(nameof(Enterprises), out _);
        bool hasVisitedCities = JsonDocument.Parse(json).RootElement.TryGetProperty(nameof(VisitedCities), out _);
        bool hasContracts = JsonDocument.Parse(json).RootElement.TryGetProperty(nameof(Contracts), out _);
        var game = JsonSerializer.Deserialize<Game>(json);
        if (game is null || game.Version != 1 || game.Day < 1 || game.Gold < 0 || game.Wagons is null || game.Wagons.Count == 0 || game.Stocks is null || game.Warehouses is null)
            throw new InvalidOperationException("Некорректное сохранение.");
        void RenameLegacyIron(Dictionary<string, int> values)
        {
            if (!values.Remove("iron", out int iron)) return;
            values["ore"] = values.GetValueOrDefault("ore") + iron;
        }
        void UpgradeStock(Dictionary<string, int>? stock)
        {
            if (stock is null) return;
            RenameLegacyIron(stock);
            foreach (string good in Goods) stock.TryAdd(good, 100);
        }
        foreach (string city in Cities)
        {
            if (!game.Stocks.ContainsKey(city)) game.Stocks[city] = Goods.ToDictionary(g => g, _ => 100);
            UpgradeStock(game.Stocks[city]);
        }
        void UpgradeCargo(Dictionary<string, int>? cargo)
        {
            if (cargo is null) return;
            RenameLegacyIron(cargo);
            foreach (string good in Goods) cargo.TryAdd(good, 0);
        }
        foreach (var wagon in game.Wagons) UpgradeCargo(wagon.Cargo);
        foreach (var warehouse in game.Warehouses.Values) UpgradeCargo(warehouse);
        foreach (var report in game.MarketReports.Values) UpgradeStock(report.Stocks);
        if (game.SecondMerchant is not null)
        {
            foreach (var wagon in game.SecondMerchant.Wagons) UpgradeCargo(wagon.Cargo);
            foreach (var warehouse in game.SecondMerchant.Warehouses.Values) UpgradeCargo(warehouse);
            foreach (var report in game.SecondMerchant.MarketReports.Values) UpgradeStock(report.Stocks);
        }
        if (!hasEnterprises) game.Enterprises = GenerateEnterprises(game.World.Seed);
        if (!hasContracts) game.Contracts = GenerateContracts(game.World.Seed, game.Day, 1, 5);
        if (!hasVisitedCities)
        {
            game.VisitedCities = game.MarketReports.Keys.Concat(game.Wagons.Select(w => w.City)).Distinct().ToList();
            if (!game.VisitedCities.Contains("oakwood")) game.VisitedCities.Add("oakwood");
            if (game.SecondMerchant is not null)
            {
                game.SecondMerchant.VisitedCities = game.SecondMerchant.MarketReports.Keys.Concat(game.SecondMerchant.Wagons.Select(w => w.City)).Distinct().ToList();
                if (!game.SecondMerchant.VisitedCities.Contains("oakwood")) game.SecondMerchant.VisitedCities.Add("oakwood");
            }
        }
        if (!hasMarketReports)
        {
            game.MarketReports.Clear();
            foreach (string city in game.Wagons.Where(w => w.Destination is null).Select(w => w.City).Distinct()) game.ObserveMarket(city, 0);
            if (game.SecondMerchant is not null)
                foreach (string city in game.SecondMerchant.Wagons.Where(w => w.Destination is null).Select(w => w.City).Distinct()) game.ObserveMarket(city, 1);
        }
        bool CargoValid(Dictionary<string, int>? cargo, int max) => cargo is not null && cargo.Count == Goods.Length && Goods.All(g => cargo.TryGetValue(g, out int n) && n >= 0 && n <= max) && cargo.Values.Sum(n => (long)n) <= max;
        bool ReportsValid(Dictionary<string, MarketReport>? reports) => reports is not null && reports.All(kv =>
            Cities.Contains(kv.Key) && kv.Value is not null && kv.Value.Day is >= 1 && kv.Value.Day <= game.Day && CargoValid(kv.Value.Stocks, 1000000));
        bool VisitsValid(List<string>? visits) => visits is not null && visits.Count > 0 && visits.Distinct().Count() == visits.Count && visits.All(Cities.Contains);
        bool MerchantValid(int gold, List<Wagon>? wagons, Dictionary<string, Dictionary<string, int>>? warehouses, Dictionary<string, MarketReport>? reports, List<string>? visits) =>
            gold >= 0 && wagons is not null && wagons.Count > 0 && warehouses is not null && ReportsValid(reports) && VisitsValid(visits) &&
            wagons.Select(w => w.Id).Distinct().Count() == wagons.Count &&
            wagons.All(w => w.Id >= 1 && Cities.Contains(w.City) && CargoValid(w.Cargo, 60) && (w.Destination is null ? w.DaysLeft == 0 : Cities.Contains(w.Destination) && w.DaysLeft is >= 0.5m and <= 100)) &&
            warehouses.All(kv => Cities.Contains(kv.Key) && CargoValid(kv.Value, 300));
        if (game.Wagons.Select(w => w.Id).Distinct().Count() != game.Wagons.Count ||
            game.Wagons.Any(w => w.Id < 1 || !Cities.Contains(w.City) || !CargoValid(w.Cargo, 60) || (w.Destination is null ? w.DaysLeft != 0 : !Cities.Contains(w.Destination) || w.DaysLeft is < 0.5m or > 100)) ||
            !Cities.All(c => game.Stocks.TryGetValue(c, out var stock) && CargoValid(stock, 1000000)) ||
            game.Warehouses.Any(kv => !Cities.Contains(kv.Key) || !CargoValid(kv.Value, 300)) ||
            !ReportsValid(game.MarketReports) || !VisitsValid(game.VisitedCities) ||
            game.SecondMerchant is { } second && !MerchantValid(second.Gold, second.Wagons, second.Warehouses, second.MarketReports, second.VisitedCities) ||
            game.Enterprises is null || game.Enterprises.Count < EnterpriseRecipes.Count || game.Enterprises.Select(e => e.Id).Distinct().Count() != game.Enterprises.Count ||
            game.Enterprises.Any(e => e.Id < 1 || !Cities.Contains(e.City) || !EnterpriseRecipes.ContainsKey(e.Type) || e.Owner is < 0 or > 1) ||
            game.SecondMerchant is null && game.Enterprises.Any(e => e.Owner == 1) ||
            game.Contracts is null || game.Contracts.Select(c => c.Id).Distinct().Count() != game.Contracts.Count ||
            game.Contracts.Any(c => c.Id < 1 || string.IsNullOrWhiteSpace(c.Issuer) || !Cities.Contains(c.City) || !FinalGoods.Contains(c.Good) || c.Amount is < 1 or > 60 || c.Reward < 1 || c.DeadlineDay < 1 || c.AcceptedBy is < 0 or > 1 || c.Completed && c.Failed) ||
            game.World is null || !Cities.All(game.World.Cities.ContainsKey) || game.World.Roads.Count < Cities.Length - 1)
            throw new InvalidOperationException("Сохранение содержит некорректные данные.");
        return game;
    }
}


