namespace TradeCompany;

public static class Ru
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["oakwood"] = "Дубрава", ["crossroads"] = "Перекрёсток",
        ["northmine"] = "Северорудск", ["riverport"] = "Речнопорт",
        ["hillford"] = "Холмоград", ["grainfield"] = "Хлебное",
        ["weavertown"] = "Ткацк", ["ironbay"] = "Железобухта",
        ["grain"] = "зерно", ["wood"] = "древесина", ["stone"] = "камень",
        ["ore"] = "руда", ["coal"] = "уголь", ["wool"] = "шерсть",
        ["flour"] = "мука", ["ingots"] = "слитки", ["lumber"] = "доски",
        ["cutstone"] = "тёсаный камень", ["tools"] = "инструменты",
        ["cloth"] = "ткань", ["bread"] = "хлеб"
    };
    private static string Normalize(string value) => value.ToLowerInvariant().Replace('ё', 'е');
    public static string Name(string key) => Names.GetValueOrDefault(key, key);
    public static string Key(string value) => Names.FirstOrDefault(pair => Normalize(pair.Value) == Normalize(value)).Key ?? value.ToLowerInvariant();
    public static string Command(string value) => Normalize(value) switch
    {
        "помощь" => "help", "карта" => "map", "статус" => "status",
        "рынок" => "market", "сравнить" => "compare", "сравнение" => "compare",
        "разведка" => "intel", "сведения" => "intel", "информация" => "intel", "купить" => "buy", "продать" => "sell",
        "принять" => "accept", "доставить" => "deliver",
        "мастерская" => "enterprise", "предприятие" => "enterprise",
        "ехать" => "travel", "далее" => "next", "повозка" => "wagon",
        "склад" => "warehouse", "выгрузить" => "store", "загрузить" => "load",
        "сохранить" => "save", "продолжить" => "loadgame", "меню" => "menu", "выход" => "quit",
        var command => command
    };
}
