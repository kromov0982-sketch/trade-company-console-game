namespace TradeCompany;

public sealed class CityNeed
{
    public int Id { get; set; }
    public string City { get; set; } = "oakwood";
    public string Kind { get; set; } = "project";
    public string Title { get; set; } = "";
    public string Reason { get; set; } = "";
    public Dictionary<string, int> Required { get; set; } = new();
    public Dictionary<string, int> Delivered { get; set; } = new();
    public int Funding { get; set; }
    public bool Funded { get; set; }
    public int Reward { get; set; }
    public int CreatedDay { get; set; }
    public int ClosedDay { get; set; }
    public bool Completed { get; set; }
}

public sealed partial class Game
{
    public int CityNeedsVersion { get; set; } = 1;
    public int NextCityNeedId { get; set; } = 1;
    public List<CityNeed> CityNeeds { get; set; } = [];
    public static string CitySpecialization(string city) => city switch
    {
        "northmine" or "ironbay" or "silvermine" or "whitecliff" or "redhill" or "highland" => "Горное дело",
        "riverport" or "saltport" or "eastbay" or "seaside" or "lakegrad" => "Торговый порт",
        "grainfield" or "windmill" or "goldfield" or "greenvalley" or "meadowtown" => "Земледелие",
        "oakwood" or "pinehaven" or "birchwood" => "Лесные промыслы",
        _ => "Ремесло"
    };
    public IEnumerable<CityNeed> NeedsAt(string city) => CityNeeds.Where(n => n.City == city && !n.Completed);
    private void AddCityNeed(string city, string kind, string title, string reason, Dictionary<string, int> goods, int reward, int funding = 0)
    {
        if (CityNeeds.Any(n => n.City == city && n.Kind == kind && (!n.Completed || Day - n.ClosedDay < (kind == "project" ? 42 : 28)))) return;
        CityNeeds.Add(new CityNeed { Id = NextCityNeedId++, City = city, Kind = kind, Title = title, Reason = reason,
            Required = goods, Reward = reward, Funding = funding, CreatedDay = Day });
    }
    // Evaluate real stock levels and production inputs, not random quest rolls.
    internal void RefreshCityNeeds()
    {
        foreach (string city in Cities)
        {
            var stock = Stocks[city];
            if (stock["grain"] + stock["bread"] < 45)
                AddCityNeed(city, "food", "Нехватка продовольствия", $"В амбарах и пекарнях мало еды: зерно {stock["grain"]}, хлеб {stock["bread"]}. Горожанам нужна помощь.",
                    new() { ["grain"] = 20, ["bread"] = 10 }, 12);
            var stalled = EnterprisesAt(city).Where(e => EnterpriseRecipes[e.Type].Inputs.Any(p => stock[p.Key] < p.Value * 3)).OrderBy(e => e.Id).FirstOrDefault();
            if (stalled is not null)
            {
                var recipe = EnterpriseRecipes[stalled.Type];
                var inputs = recipe.Inputs.Where(p => stock[p.Key] < p.Value * 3).ToDictionary(p => p.Key, p => p.Value * 5);
                AddCityNeed(city, "craft", "Поддержка городских мастерских", $"{recipe.Name}: запасы сырья на исходе. Поставка пополнит рынок и позволит продолжить производство.", inputs, 8);
            }
            switch (CitySpecialization(city))
            {
                case "Горное дело":
                    AddCityNeed(city, "project", "Укрепление шахт", "Горный город обновляет крепи и рабочий инструмент. Материалы пойдут на ремонт шахтных крепей.", new() { ["lumber"] = 12, ["tools"] = 5 }, 10, 250); break;
                case "Торговый порт":
                    AddCityNeed(city, "project", "Ремонт причалов", "Порту нужны настилы и парусина для обновления причалов и грузовых навесов.", new() { ["lumber"] = 10, ["cloth"] = 8 }, 10, 300); break;
                case "Земледелие":
                    AddCityNeed(city, "project", "Подготовка сельских амбаров", "Земледельцы ремонтируют хранилища урожая и заменяют изношенный инвентарь.", new() { ["wood"] = 12, ["tools"] = 4 }, 10, 200); break;
                case "Лесные промыслы":
                    AddCityNeed(city, "project", "Обустройство лесного промысла", "Лесорубам нужны новые инструменты и одежда для работы в лесных лагерях.", new() { ["tools"] = 5, ["cloth"] = 6 }, 10, 200); break;
                default:
                    AddCityNeed(city, "project", "Восстановление ремесленного двора", "Город восстанавливает общие мастерские: нужны камень и инструменты.", new() { ["cutstone"] = 10, ["tools"] = 4 }, 10, 250); break;
            }
        }
    }
    private CityNeed ActiveNeed(int id)
    {
        var need = CityNeeds.SingleOrDefault(n => n.Id == id) ?? throw new ArgumentException("Потребность не найдена.");
        if (need.Completed) throw new InvalidOperationException("Эта городская проблема уже решена.");
        return need;
    }
    public string FundCityNeed(int id, int wagonId)
    {
        var need = ActiveNeed(id); Present(need.City, wagonId);
        if (need.Funding == 0 || need.Funded) throw new InvalidOperationException("Финансирование уже обеспечено.");
        if (Gold < need.Funding) throw new InvalidOperationException($"Нужно {need.Funding} монет.");
        Gold -= need.Funding; need.Funded = true;
        return $"{Ru.Name(need.City)}: проект «{need.Title}» профинансирован на {need.Funding} монет. " + CompleteCityNeed(need);
    }
    public string SupplyCityNeed(int id, int wagonId)
    {
        var need = ActiveNeed(id); var w = Present(need.City, wagonId); int total = 0;
        foreach (var (good, required) in need.Required)
        {
            int amount = Math.Min(w.Cargo[good], required - need.Delivered.GetValueOrDefault(good));
            if (amount <= 0) continue;
            w.Cargo[good] -= amount; need.Delivered[good] = need.Delivered.GetValueOrDefault(good) + amount; total += amount;
            // Civic construction consumes materials; relief and workshop supplies enter the market.
            if (need.Kind != "project") Stocks[need.City][good] += amount;
        }
        if (total == 0) throw new InvalidOperationException("В караване нет товаров, которые ещё нужны городу.");
        return $"{Ru.Name(need.City)}: для «{need.Title}» передано {total} ед. товаров. " + CompleteCityNeed(need);
    }
    private string CompleteCityNeed(CityNeed need)
    {
        if (need.Completed || need.Funding > 0 && !need.Funded || need.Required.Any(p => need.Delivered.GetValueOrDefault(p.Key) < p.Value))
            return "Потребность ещё не закрыта; влияние будет начислено после выполнения всех условий.";
        need.Completed = true; need.ClosedDay = Day;
        return $"Проблема «{need.Title}» решена. " + GainInfluence(need.City, need.Reward);
    }
    // Contract deliveries can count toward an existing city need, without granting an arbitrary flat bonus.
    private string ApplyContractAid(string city, string good, int amount)
    {
        List<string> results = [];
        foreach (var need in NeedsAt(city).Where(n => n.Kind != "project" && n.Required.ContainsKey(good)).OrderBy(n => n.Id).ToList())
        {
            int used = Math.Min(amount, need.Required[good] - need.Delivered.GetValueOrDefault(good));
            if (used <= 0) continue;
            need.Delivered[good] = need.Delivered.GetValueOrDefault(good) + used; amount -= used;
            results.Add($"«{need.Title}»: зачтено {Ru.Name(good)} ×{used}. " + CompleteCityNeed(need));
            if (amount == 0) break;
        }
        return string.Join(" ", results);
    }
    private void ValidateCityNeeds()
    {
        if (CityNeedsVersion != 1 || CityNeeds is null || NextCityNeedId < 1 || CityNeeds.Any(n => n is null) || CityNeeds.Select(n => n.Id).Distinct().Count() != CityNeeds.Count ||
            CityNeeds.Any(n => n is null || n.Id < 1 || !Cities.Contains(n.City) || n.Kind is not ("food" or "craft" or "project") ||
                string.IsNullOrWhiteSpace(n.Title) || n.Required is null || n.Required.Count == 0 || n.Required.Any(p => !Goods.Contains(p.Key) || p.Value is < 1 or > 60) ||
                n.Delivered is null || n.Delivered.Any(p => !n.Required.TryGetValue(p.Key, out int required) || p.Value < 0 || p.Value > required) ||
                n.Reward is < 1 or > 100 || n.Funding is < 0 or > 10000 || n.CreatedDay < 1 || n.CreatedDay > Day ||
                n.Completed && (n.ClosedDay < n.CreatedDay || n.ClosedDay > Day || n.Funding > 0 && !n.Funded || n.Required.Any(p => n.Delivered.GetValueOrDefault(p.Key) < p.Value))) ||
            CityNeeds.GroupBy(n => (n.City, n.Kind)).Any(g => g.Count(n => !n.Completed) > 1))
            throw new InvalidOperationException("Сохранение содержит некорректные городские потребности.");
        // Apply the new balance to unfinished offers from older saves, retaining deliveries and earned influence.
        foreach (var need in CityNeeds.Where(n => !n.Completed))
            need.Reward = Math.Min(need.Reward, need.Kind == "food" ? 12 : need.Kind == "craft" ? 8 : 10);
        NextCityNeedId = Math.Max(NextCityNeedId, CityNeeds.Select(n => n.Id).DefaultIfEmpty(0).Max() + 1);
    }
}
