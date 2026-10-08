namespace TradeCompany;

internal sealed record MarketEvent(string Title, string Reason, string Good, int Change);

public sealed partial class Game
{
    internal static readonly MarketEvent[] MarketEvents =
    [
        new("Богатый урожай", "Крестьяне привезли на торг излишки зерна.", "grain", 45),
        new("Ремонт городского моста", "Строители выкупили доски для настила.", "lumber", -30),
        new("Прибытие лесного сплава", "Лесорубы доставили новую партию брёвен.", "wood", 40),
        new("Заказ гарнизона", "Военные закупили инструменты для укреплений.", "tools", -18),
        new("Обоз овцеводов", "Овцеводы привезли на торг новую партию шерсти.", "wool", 35),
        new("Похолодание", "Жители закупили уголь для отопления.", "coal", -35),
        new("Рудный обоз", "Рудокопы доставили добычу на городской рынок.", "ore", 40),
        new("Праздник ремесленников", "Горожане скупили ткань для праздничных нарядов.", "cloth", -25),
        new("Новая каменная выработка", "Каменотёсы привезли дополнительную партию камня.", "stone", 40),
        new("Постоялый двор полон", "Хозяева дворов закупили хлеб для приезжих.", "bread", -30),
        new("Удачная неделя мельников", "Мельницы отправили на торг лишнюю муку.", "flour", 35),
        new("Строительство ратуши", "Городской совет выкупил тёсаный камень.", "cutstone", -25),
        new("Большая плавка", "Плавильщики выставили новую партию слитков.", "ingots", 30),
        new("Закупка зерна в резерв", "Город пополнил продовольственные амбары.", "grain", -35)
    ];

    internal string ApplyMarketEvent(int index, string city)
    {
        var e = MarketEvents[index];
        int before = Stocks[city][e.Good], buyBefore = Price(city, e.Good, true), sellBefore = Price(city, e.Good, false);
        int after = Math.Clamp(before + e.Change, 0, 1000000);
        Stocks[city][e.Good] = after;
        int delta = after - before, buyAfter = Price(city, e.Good, true), sellAfter = Price(city, e.Good, false);
        string impact = buyAfter < buyBefore ? "Закупать этот товар стало дешевле."
            : sellAfter > sellBefore ? "Продавать этот товар стало выгоднее."
            : buyAfter > buyBefore ? "Закупка этого товара подорожала."
            : sellAfter < sellBefore ? "Выручка за единицу при продаже снизилась."
            : "Цены за единицу пока не изменились.";
        string reason = delta == 0 && e.Change < 0 ? "Товар уже отсутствует на рынке; запас не сократился." : e.Reason;
        RefreshCityNeeds();
        return $"{GameCalendar.FormatDay(Day)}: {e.Title} — {Ru.Name(city)}. {reason} "
            + $"Запас «{Ru.Name(e.Good)}»: {before} → {after} ({delta:+0;-0;0} ед.). "
            + $"Покупка: {buyBefore} → {buyAfter}; продажа: {sellBefore} → {sellAfter} монет/ед. {impact}";
    }

    private string RunMarketEvent()
    {
        var random = new Random(World.Seed ^ Day * 104729);
        string center = Wagons[0].Destination ?? Wagons[0].City;
        var nearby = Cities.Where(c => c == center || World.AreNeighbors(center, c)).ToArray();
        var pool = random.Next(3) == 0 ? Cities : nearby;
        string city = pool[random.Next(pool.Length)];
        int index = (int)(((long)World.Seed + Day / 3) % MarketEvents.Length);
        return ApplyMarketEvent(index, city);
    }
}
