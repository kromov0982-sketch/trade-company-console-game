namespace TradeCompany;

public sealed class Branch
{
    public int ReadyDay { get; set; }
    public int Budget { get; set; }
    public bool Trading { get; set; } = true;
    public string? Good { get; set; }
    public int Amount { get; set; }
    public int ArrivalDay { get; set; }
    public int ShipmentCost { get; set; }
    public int Profit { get; set; }
    public string Report { get; set; } = "Ожидает торговый бюджет";
}
public sealed class CityInfluence
{
    public int Points { get; set; }
    public int InvestmentDay { get; set; } = -7;
    public int SupplyDay { get; set; } = -7;
}
public sealed partial class Game
{
    public const int CharterPrice = 3000;
    public bool CompanyFounded { get; set; }
    public int ProgressionVersion { get; set; } = 1;
    public Dictionary<string, Branch> Branches { get; set; } = new();
    public Dictionary<string, CityInfluence> Influence { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public int RequiredCities => (int)Math.Ceiling(Cities.Length * .3m);
    [System.Text.Json.Serialization.JsonIgnore] public int ControlledCities => Cities.Count(IsControlled);
    public int InfluenceAt(string city) => Influence.TryGetValue(city, out var value) ? value.Points : 0;
    public bool IsControlled(string city) => CompanyFounded && Branches.TryGetValue(city, out var b) && b.ReadyDay <= Day && InfluenceAt(city) >= 100;
    private CityInfluence Standing(string city)
    {
        if (!Influence.TryGetValue(city, out var value)) Influence[city] = value = new();
        return value;
    }
    private string GainInfluence(string city, int amount)
    {
        bool controlled = IsControlled(city);
        var value = Standing(city); value.Points = Math.Min(100, value.Points + amount);
        string message = $"Влияние в городе {Ru.Name(city)}: +{amount}, теперь {value.Points}/100.";
        if (!controlled && IsControlled(city)) message += " Город теперь под вашим влиянием!";
        return message + " " + Victory(0);
    }
    public string FoundCompany()
    {
        if (CompanyFounded) throw new InvalidOperationException("Компания уже основана.");
        if (Gold < CharterPrice) throw new InvalidOperationException("Право основать компанию стоит 3 000 монет.");
        Gold -= CharterPrice; CompanyFounded = true;
        return "Получена торговая грамота. Компания основана! Теперь можно открывать филиалы и нанимать представителей.";
    }
    private void RequireCompany()
    {
        if (!CompanyFounded) throw new InvalidOperationException("Сначала приобретите право основать компанию.");
    }
    private Wagon Present(string city, int wagonId)
    {
        var w = Idle(wagonId, 0);
        if (w.City != city) throw new InvalidOperationException("Ваш караван должен находиться в этом городе.");
        return w;
    }
    public string OpenBranch(string city, int wagonId, bool build)
    {
        RequireCompany(); city = CityKey(city); var w = Present(city, wagonId);
        if (Branches.ContainsKey(city)) throw new InvalidOperationException("Филиал уже существует или строится.");
        int price = build ? 800 : 1500;
        if (Gold < price || build && (w.Cargo["lumber"] < 10 || w.Cargo["tools"] < 5))
            throw new InvalidOperationException(build ? "Нужно 800 монет, 10 досок и 5 инструментов в караване." : "Нужно 1 500 монет.");
        Gold -= price;
        if (build) { w.Cargo["lumber"] -= 10; w.Cargo["tools"] -= 5; }
        Branches[city] = new Branch { ReadyDay = Day + (build ? 7 : 0) };
        return (build ? $"Филиал в городе {Ru.Name(city)} будет готов {GameCalendar.FormatDay(Day + 7)}. " : $"Филиал в городе {Ru.Name(city)} открыт. ") + "Влияние растёт за решение городских потребностей. " + Victory(0);
    }
    public string FundBranch(string city, int amount)
    {
        RequireCompany(); var b = Branches[CityKey(city)];
        if (amount <= 0 || Gold < amount || b.Budget > 1000000 - amount) throw new InvalidOperationException("Недостаточно денег или превышен лимит бюджета.");
        Gold -= amount; b.Budget += amount;
        if (b.Good is null) b.Report = "Бюджет получен. Поиск поставки — со следующего дня.";
        return $"Представителю в городе {Ru.Name(city)} выделено {amount} монет. Бюджет: {b.Budget}.";
    }
    public string WithdrawBranch(string city, int? amount = null)
    {
        RequireCompany();
        var b = Branches[CityKey(city)]; int money = amount ?? b.Budget;
        if (money <= 0 || money > b.Budget) throw new InvalidOperationException("Сумма должна быть положительной и не превышать свободный бюджет филиала.");
        Gold = checked(Gold + money); b.Budget -= money;
        return $"Из филиала {Ru.Name(city)} получено {money} монет.";
    }
    public string ToggleRepresentative(string city)
    {
        var b = Branches[CityKey(city)]; b.Trading = !b.Trading;
        return $"Представитель в городе {Ru.Name(city)}: " + (b.Trading ? "торговля разрешена." : "новые сделки приостановлены; текущая поставка будет завершена.");
    }
    private void RunRepresentatives(List<string> news)
    {
        foreach (var (city, b) in Branches.OrderBy(p => p.Key))
        {
            if (b.ReadyDay > Day) continue;
            if (b.ReadyDay == Day) news.Add($"{GameCalendar.FormatDay(Day)}: филиал в городе {Ru.Name(city)} построен. " + Victory(0));
            if (b.Good is not null && b.ArrivalDay <= Day)
            {
                int revenue = 0;
                for (int i = 0; i < b.Amount; i++) { revenue += Price(city, b.Good, false); Stocks[city][b.Good]++; }
                int profit = revenue - b.ShipmentCost; b.Budget += revenue; b.Profit += profit;
                b.Report = $"Продано: {Ru.Name(b.Good)} ×{b.Amount}. Выручка {revenue}; результат сделки {profit:+0;-0;0} монет.";
                b.Report += " " + ApplyContractAid(city, b.Good, b.Amount);
                b.Good = null; b.Amount = 0;

                news.Add($"{GameCalendar.FormatDay(Day)}: представитель, {Ru.Name(city)}. {b.Report}");
            }
            if (!b.Trading || b.Good is not null) continue;
            // Buy a small profitable shipment on an actual market; delivery takes road travel time.
            (string City, string Good, int Cost, int Days)? best = null; int margin = 0;
            foreach (string source in Cities.Where(c => c != city))
            {
                decimal days = World.TravelDays(source, city); int fee = (int)Math.Ceiling(days * 2);
                foreach (string good in Goods)
                {
                    if (Stocks[source][good] < 5) continue;
                    int cost = fee, revenue = 0;
                    for (int i = 0; i < 5; i++) { cost += Price(source, good, true, Stocks[source][good] - i); revenue += Price(city, good, false, Stocks[city][good] + i); }
                    if (cost <= b.Budget && revenue - cost > margin + 5)
                    { best = (source, good, cost, Math.Max(1, (int)Math.Ceiling(days))); margin = revenue - cost; }
                }
            }
            if (best is { } deal)
            {
                Stocks[deal.City][deal.Good] -= 5; b.Budget -= deal.Cost; b.Good = deal.Good; b.Amount = 5;
                b.ShipmentCost = deal.Cost; b.ArrivalDay = Day + deal.Days;
                b.Report = $"Поставка из города {Ru.Name(deal.City)}: {Ru.Name(deal.Good)} ×5, затраты {deal.Cost}. Прибытие {GameCalendar.FormatDay(b.ArrivalDay, true)}.";
            }
            else b.Report = b.Budget < 100 ? "Нужен торговый бюджет" : "Ожидает выгодных цен";
        }
    }
    private void MigrateProgression(bool legacy)
    {
        if (legacy) { CompanyFounded = Enterprises.Any(e => e.Owner == 0) || Wagons.Count > 1; Won = false; }
        if (ProgressionVersion != 1 || Branches is null || Influence is null ||
            Branches.Any(p => !Cities.Contains(p.Key) || p.Value is null || p.Value.Budget < 0 || p.Value.Budget > 1000000 || p.Value.ReadyDay < 1 || p.Value.Amount is < 0 or > 60 || p.Value.Good is not null && (!Goods.Contains(p.Value.Good) || p.Value.ArrivalDay < 1 || p.Value.Amount < 1) || p.Value.ShipmentCost < 0) ||
            Influence.Any(p => !Cities.Contains(p.Key) || p.Value is null || p.Value.Points is < 0 or > 100))
            throw new InvalidOperationException("Сохранение содержит некорректные данные компании.");
        Won = ControlledCities >= RequiredCities;
    }
}
