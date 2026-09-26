namespace TradeCompany;

public static class Ru
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["oakwood"] = "Дубрава", ["crossroads"] = "Перекрёсток",
        ["northmine"] = "Северорудск", ["riverport"] = "Речнопорт",
        ["grain"] = "зерно", ["wood"] = "древесина", ["iron"] = "железо",
        ["tools"] = "инструменты", ["cloth"] = "ткань"
    };
    private static string Normalize(string value) => value.ToLowerInvariant().Replace('ё', 'е');
    public static string Name(string key) => Names.GetValueOrDefault(key, key);
    public static string Key(string value) => Names.FirstOrDefault(pair => Normalize(pair.Value) == Normalize(value)).Key ?? value.ToLowerInvariant();
    public static string Command(string value) => Normalize(value) switch
    {
        "помощь" => "help", "карта" => "map", "статус" => "status",
        "рынок" => "market", "купить" => "buy", "продать" => "sell",
        "ехать" => "travel", "далее" => "next", "повозка" => "wagon",
        "склад" => "warehouse", "выгрузить" => "store", "загрузить" => "load",
        "сохранить" => "save", "продолжить" => "loadgame", "меню" => "menu", "выход" => "quit",
        var command => command
    };
}
