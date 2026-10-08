using Godot;
using TradeCompany;

public partial class Main
{
    private void BuildDevUi(TabContainer tabs)
    {
        var dev = ScrollTab(tabs, "DEV");
        dev.AddChild(Text("Деньги", 22));
        var money = new SpinBox { MinValue = 1, MaxValue = 1000000, Step = 100, Value = 10000 };
        dev.AddChild(money);
        dev.AddChild(ActionButton("Начислить монеты", () => Execute(() => game.DevMoney((int)money.Value))));
        dev.AddChild(new HSeparator());
        dev.AddChild(Text("Товары для выбранного каравана", 22));
        var goods = new OptionButton();
        foreach (string item in Game.Goods) { goods.AddIconItem(goodIcons[item], Ru.Name(item)); }
        goods.AddThemeConstantOverride("icon_max_width", 24); dev.AddChild(goods);
        var count = new SpinBox { MinValue = 1, MaxValue = 60, Step = 1, Value = 10 }; dev.AddChild(count);
        dev.AddChild(ActionButton("Добавить товар в караван", () => Execute(() => game.DevGoods(wagonId, Game.Goods[goods.Selected], (int)count.Value))));
        dev.AddChild(ActionButton("Очистить груз каравана", () => Execute(() => game.DevClearCargo(wagonId))));
        dev.AddChild(new HSeparator());
        dev.AddChild(Text("Вызвать событие без смены дня", 22));
        var cities = new OptionButton();
        foreach (string target in Game.Cities) cities.AddItem(Ru.Name(target));
        dev.AddChild(cities);
        var events = new OptionButton();
        foreach (var entry in Game.MarketEvents) events.AddItem(entry.Title);
        dev.AddChild(events);
        dev.AddChild(ActionButton("Вызвать выбранное событие", () => Execute(() => game.DevEvent(events.Selected, Game.Cities[cities.Selected]))));
        dev.AddChild(ActionButton("Случайное событие", () => Execute(() => game.DevEvent(Random.Shared.Next(Game.MarketEvents.Length), Game.Cities[cities.Selected]))));
    }
}
