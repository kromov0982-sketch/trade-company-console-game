namespace TradeCompany;

public sealed partial class Game
{
    public static int ProvisionCost(decimal days) => (int)Math.Ceiling(days * 2);
    public string BuyProvisions(int wagonId, int bread)
    {
        Quantity(bread); var w = Idle(wagonId, 0); string city = w.City;
        if (bread > Stocks[city]["bread"] || w.Provisions > 10000 - bread * 5) throw new InvalidOperationException("Недостаточно хлеба на рынке или запас провизии уже полон.");
        int cost = 0;
        for (int i = 0; i < bread; i++) cost += Price(city, "bread", true, Stocks[city]["bread"] - i);
        if (Gold < cost) throw new InvalidOperationException($"Нужно {cost} монет для покупки хлеба.");
        Gold -= cost; Stocks[city]["bread"] -= bread; w.Provisions += bread * 5;
        return $"Закуплен хлеб ×{bread} за {cost} монет. Провизия каравана №{wagonId}: +{bread * 5}, запас {w.Provisions}.";
    }
    public string PackProvisions(int wagonId, int bread)
    {
        Quantity(bread); var w = Idle(wagonId, 0);
        if (w.Cargo["bread"] < bread || w.Provisions > 10000 - bread * 5) throw new InvalidOperationException("Недостаточно хлеба в грузе или запас провизии уже полон.");
        w.Cargo["bread"] -= bread; w.Provisions += bread * 5;
        return $"Хлеб ×{bread} из груза упакован в провизию. Запас: {w.Provisions}.";
    }
}
