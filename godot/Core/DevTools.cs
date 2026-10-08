namespace TradeCompany;

public sealed partial class Game
{
    public string DevMoney(int amount)
    {
        if (amount is < 1 or > 1000000) throw new ArgumentException("От 1 до 1 000 000 монет.");
        Gold = checked(Gold + amount);
        return $"DEV: начислено {amount:N0} монет. В казне {Gold:N0}.";
    }
    public string DevGoods(int wagonId, string good, int amount)
    {
        good = GoodKey(good); Quantity(amount);
        var wagon = Wagons.Single(w => w.Id == wagonId);
        int added = Math.Min(amount, 60 - wagon.Used);
        if (added == 0) throw new InvalidOperationException("Караван заполнен. Сначала очистите груз.");
        wagon.Cargo[good] += added;
        return $"DEV: добавлено {Ru.Name(good)} ×{added} в караван №{wagonId}. Груз {wagon.Used}/60.";
    }
    public string DevClearCargo(int wagonId)
    {
        Wagons.Single(w => w.Id == wagonId).Cargo = EmptyCargo();
        return $"DEV: груз каравана №{wagonId} очищен.";
    }
    public string DevEvent(int index, string city)
    {
        city = CityKey(city);
        if (index < 0 || index >= MarketEvents.Length) throw new ArgumentException("Нет такого события.");
        return ApplyMarketEvent(index, city);
    }
}
