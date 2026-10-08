using Godot;
using TradeCompany;

public partial class Main
{
    private VBoxContainer cityNeedsPanel = null!;
    private void RefreshCityNeedsUi()
    {
        Clear(cityNeedsPanel);
        var cities = new OptionButton { FitToLongestItem = false };
        foreach (string target in Game.Cities) cities.AddItem($"{Ru.Name(target)} · потребности: {game.NeedsAt(target).Count()}");
        cities.Select(Array.IndexOf(Game.Cities, city));
        cities.ItemSelected += index => { if (!refreshing) SelectCity(Game.Cities[(int)index]); };
        cityNeedsPanel.AddChild(cities);
        cityNeedsPanel.AddChild(Text(Game.CitySpecialization(city), 23));
        cityNeedsPanel.AddChild(Text($"Влияние: {game.InfluenceAt(city)}/100", 17));
        cityNeedsPanel.AddChild(ActionButton("Показать город на карте", () => ShowContractCity(city)));
        var active = game.NeedsAt(city).OrderBy(n => n.Kind == "food" ? 0 : n.Kind == "craft" ? 1 : 2).ToList();
        if (active.Count == 0) cityNeedsPanel.AddChild(Text("Городские потребности сейчас закрыты", 18));
        bool here = Wagon.Destination is null && Wagon.City == city;
        foreach (var need in active)
        {
            var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Box("d4be94", 5)); cityNeedsPanel.AddChild(card);
            var details = new VBoxContainer(); details.AddThemeConstantOverride("separation", 8); card.AddChild(details);
            var title = Text(need.Title, 23); title.AutowrapMode = TextServer.AutowrapMode.WordSmart; details.AddChild(title);
            var reason = Text(need.Reason, 16); reason.AutowrapMode = TextServer.AutowrapMode.WordSmart; details.AddChild(reason);
            details.AddChild(Text($"Выявлено: {GameCalendar.FormatDay(need.CreatedDay, true)}", 14));
            details.AddChild(Text($"За решение: +{need.Reward} влияния", 17));
            foreach (var (good, count) in need.Required)
                details.AddChild(IconTitle(good, $"{Ru.Name(good)}: {need.Delivered.GetValueOrDefault(good)}/{count} · в караване {Wagon.Cargo[good]}", 16));
            if (need.Funding > 0)
            {
                details.AddChild(IconTitle("coin", need.Funded ? "Работы оплачены" : $"Оплата работ: {need.Funding} монет", 16));
                if (!need.Funded)
                {
                    var fund = ActionButton($"Оплатить работы · {need.Funding} монет", () => Execute(() => game.FundCityNeed(need.Id, wagonId)));
                    fund.Disabled = !here || game.Gold < need.Funding; details.AddChild(fund);
                }
            }
            var supply = ActionButton("Передать нужные товары из каравана", () => Execute(() => game.SupplyCityNeed(need.Id, wagonId)));
            supply.Disabled = !here || !need.Required.Any(p => need.Delivered.GetValueOrDefault(p.Key) < p.Value && Wagon.Cargo[p.Key] > 0);
            details.AddChild(supply);
            if (!here) details.AddChild(Text("Для помощи нужен караван в этом городе", 15));
        }
        var completed = game.CityNeeds.Where(n => n.City == city && n.Completed).OrderByDescending(n => n.ClosedDay).Take(3).ToList();
        if (completed.Count > 0) cityNeedsPanel.AddChild(Text("Решённые проблемы", 22));
        foreach (var need in completed)
        {
            var entry = Text($"{GameCalendar.FormatDay(need.ClosedDay, true)} · {need.Title} · +{need.Reward} влияния", 15);
            entry.AutowrapMode = TextServer.AutowrapMode.WordSmart; cityNeedsPanel.AddChild(entry);
        }
    }
}
