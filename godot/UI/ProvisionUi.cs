using Godot;
using TradeCompany;

public partial class Main
{
    private string pendingTravelCity = "oakwood";
    private void ConfirmTravel(string target)
    {
        decimal days = game.World.TravelDays(Wagon.City, target); int food = Game.ProvisionCost(days);
        pendingTravelCity = target;
        travelConfirmation.DialogText = $"{Ru.Name(Wagon.City)} → {Ru.Name(target)}\nВ пути: {Game.FormatDays(days)} · {Game.DistanceKm(days)} км\nРасход: {food} провизии\nВ запасе: {Wagon.Provisions} → останется {Wagon.Provisions - food}\nПрибытие: {GameCalendar.FormatDay(game.Day + (int)Math.Ceiling(days))}";
        travelConfirmation.PopupCentered();
    }
    private void BuildProvisionUi(VBoxContainer deal)
    {
        deal.AddChild(new HSeparator());
        deal.AddChild(IconTitle("bread", "Дорожная провизия · 1 хлеб = 5 порций", 16));
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 8); deal.AddChild(row);
        var bread = new SpinBox { MinValue = 1, MaxValue = 100, Step = 1, Value = 5, CustomMinimumSize = new Vector2(100, 38) };
        row.AddChild(Text("Хлеб", 16)); row.AddChild(bread);
        row.AddChild(ActionButton("Купить для дороги", () => Execute(() => game.BuyProvisions(wagonId, (int)bread.Value))));
        row.AddChild(ActionButton("Взять хлеб из груза", () => Execute(() => game.PackProvisions(wagonId, (int)bread.Value))));
    }
}
