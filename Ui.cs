namespace TradeCompany;

public static class Ui
{
    public static void Show(Game game)
    {
        void Row(string text) { Ink.Write("|", Ink.Muted); Ink.Write(text.PadRight(65), Ink.Heading); Ink.Line("|", Ink.Muted); }
        void Border() => Ink.Line("+" + new string('-', 65) + "+", Ink.Muted);
        Console.WriteLine();
        Border();
        Row("                       ТОРГОВАЯ КОМПАНИЯ");
        Row($" {Game.FinancialDate(game.Day)}    Казна: {game.Gold}    Капитал: {game.CompanyValue(0)} / {Game.TargetCompanyValue}");
        Border();
        Row("                         [Северорудск]");
        Row("                               | 3 дн.");
        Row(" [Дубрава]---2 дн.---[Перекрёсток]---4 дн.---[Речнопорт]");
        Border();
        Console.WriteLine("Рейсы между крайними городами проходят через Перекрёсток.");
        Console.WriteLine("Расходы: 8 монет/день пути. Вместимость повозки: 60.");
        foreach (var w in game.Wagons)
        {
            Console.WriteLine($"  #{w.Id}: {Ru.Name(w.City)}" + (w.Destination is null ? " | стоит в городе" : $" -> {Ru.Name(w.Destination)} | осталось {w.DaysLeft} дн.") + $" | груз {w.Used}/60");
            Console.WriteLine("      " + Cargo(w.Cargo));
        }
        foreach (var (city, cargo) in game.Warehouses) Console.WriteLine($"  Склад: {Ru.Name(city)} ({cargo.Values.Sum()}/300): {Cargo(cargo)}");
        if (game.Gold < 16 && game.Wagons.All(w => w.Used == 0 && w.Destination is null) && game.Warehouses.Values.All(c => c.Values.Sum() == 0))
            Console.WriteLine("Казна почти пуста. Можно загрузить сохранение или начать заново, перезапустив игру.");
    }
    private static string Cargo(Dictionary<string, int> cargo) => cargo.Values.All(n => n == 0) ? "пусто" : string.Join(", ", cargo.Where(kv => kv.Value > 0).Select(kv => $"{Ru.Name(kv.Key)}: {kv.Value}"));
    public static void Market(Game game, string city)
    {
        city = Game.CityKey(city);
        Ink.Line($"\nРЫНОК: {Ru.Name(city)} | ДЕНЬ {game.Day}", Ink.Heading);
        Ink.Line("+-------------+--------+---------+---------+", Ink.Muted);
        Ink.Line("| ТОВАР       | ЗАПАС  | ПОКУПКА | ПРОДАЖА |", Ink.Heading);
        Ink.Line("+-------------+--------+---------+---------+", Ink.Muted);
        foreach (string good in Game.Goods)
        {
            Ink.Write("| ", Ink.Muted);
            Ink.Write($"{Ru.Name(good),-11}", Ink.Parameter);
            Ink.Write(" | ", Ink.Muted);
            Ink.Write($"{game.Stocks[city][good],6}");
            Ink.Write(" | ", Ink.Muted);
            Ink.Write($"{game.Price(city, good, true),7}", Ink.Parameter);
            Ink.Write(" | ", Ink.Muted);
            Ink.Write($"{game.Price(city, good, false),7}", Ink.Success);
            Ink.Line(" |", Ink.Muted);
        }
        Ink.Line("+-------------+--------+---------+---------+", Ink.Muted);
        Console.WriteLine("Покупка и продажа — с вашей точки зрения. Цена за следующую единицу.");
        Console.WriteLine("Цена каждой следующей единицы в партии меняется вместе с запасом.");
    }
    public static void Help()
    {
        Ink.Section("СПРАВКА ПО КОМАНДАМ");
        Ink.Write("  команда", Ink.Command);
        Ink.Write("  ПАРАМЕТР", Ink.Parameter);
        Ink.Line("  пояснение");
        Ink.Line("  Параметры заменяйте своими значениями. [ДНИ] можно пропустить.");

        void Entry(string command, string parameters, string description)
        {
            Ink.Write("  ");
            Ink.Write(command, Ink.Command);
            if (parameters.Length > 0) Ink.Write(" " + parameters, Ink.Parameter);
            // Keep descriptions on their own line so long commands fit in cmd.
            Console.WriteLine();
            Ink.Line("      " + description);
        }
        Ink.Section("ОБЗОР И ТОРГОВЛЯ");
        Entry("помощь", "", "Открыть эту справку.");
        Entry("карта / статус", "", "Карта, казна, повозки и склады.");
        Entry("рынок", "ГОРОД", "Посмотреть запасы и цены города.");
        Entry("сравнить", "ГОРОД ГОРОД", "Сравнить цены и прибыль между двумя городами.");
        Entry("разведка", "ГОРОД", "Купить свежие сведения о рынке за 40 монет.");
        Entry("принять", "НОМЕР", "Принять доступный контракт.");
        Entry("доставить", "НОМЕР НОМЕР_ПОВОЗКИ", "Сдать товар по контракту.");
        Entry("предприятие", "НОМЕР", "Купить предприятие по номеру с карты.");
        Entry("купить", "НОМЕР ТОВАР КОЛИЧЕСТВО", "Купить товар в повозку.");
        Entry("продать", "НОМЕР ТОВАР КОЛИЧЕСТВО", "Продать товар из повозки.");

        Ink.Section("МАРШРУТЫ И ВРЕМЯ");
        Entry("ехать", "НОМЕР ГОРОД", "Отправить повозку. Расходы: 8 монет за день пути.");
        Entry("далее", "[ДНИ]", "Пропустить от 1 до 30 дней. По умолчанию — один день.");

        Ink.Section("РАЗВИТИЕ КОМПАНИИ");
        Entry("повозка", "ГОРОД", "Купить повозку за 500 монет. Вместимость: 60 единиц.");
        Entry("склад", "ГОРОД", "Купить склад за 400 монет. Вместимость: 300 единиц.");
        Entry("выгрузить", "НОМЕР ТОВАР КОЛИЧЕСТВО", "Переложить груз из повозки на склад.");
        Entry("загрузить", "НОМЕР ТОВАР КОЛИЧЕСТВО", "Забрать груз со склада в повозку.");

        Ink.Section("СОХРАНЕНИЕ И ВЫХОД");
        Entry("сохранить", "", "Сохранить текущую игру.");
        Entry("продолжить", "", "Загрузить сохранённую игру.");
        Entry("выход", "", "Закрыть игру. Перед выходом сохраните прогресс.");

        Ink.Section("ЗНАЧЕНИЯ ПАРАМЕТРОВ");
        Ink.Write("  НОМЕР", Ink.Parameter); Ink.Line(" — номер повозки, например 1.");
        Ink.Write("  КОЛИЧЕСТВО", Ink.Parameter); Ink.Line(" — число единиц товара, например 30.");
        Ink.Write("  ГОРОД", Ink.Parameter); Ink.Line(" — один из городов:");
        foreach (string city in Game.Cities) Ink.Line("    " + Ru.Name(city), Ink.Parameter);
        Ink.Line("  Предприятия распределяются по городам заново в каждой партии.");
        Ink.Write("  ТОВАР", Ink.Parameter); Ink.Line(" — " + string.Join(", ", Game.Goods.Select(Ru.Name)) + ".");

        Ink.Section("ПРИМЕР ПЕРВОГО РЕЙСА");
        Ink.Example("купить", "1 древесина 30");
        Ink.Example("ехать", "1 перекрёсток");
        Ink.Example("далее", "2");
        Ink.Example("продать", "1 древесина 30");

        Ink.Section("ПРАВИЛА");
        Ink.Line("  Регистр не важен; вместо ё можно писать е.");
        Ink.Line("  Английские команды тоже работают.");
        Ink.Line("  Производство пополняет запасы, потребление уменьшает их.");
        Ink.Line("  Раз в неделю ярмарка повышает спрос на один из товаров.");
        Ink.Line("  Торговля и перегрузка доступны только в городе стоянки.");
        Ink.Line("  Между командами время не идёт. Долгов и содержания нет.");
    }
}

