using System.Runtime.InteropServices;
using System.Text;

namespace TradeCompany;

public static class Dashboard
{
    private static readonly Dictionary<int, string> PreviousRows = new();
    private static bool VirtualTerminal;
    private sealed record Hit(int X, int Y, int Width, Action Activate);
    private static readonly List<Hit> Hits = new();
    private static MouseCommand? mouseCommand;
    private static bool mapOpen;
    private static bool contractsOpen;
    private static bool helpOpen;
    private static int helpPage;
    private static string mapCity = "oakwood";
    private static string? mapCompareFrom;
    private static int mapGoodsPage;
    private record Part(string Text, ConsoleColor Color = ConsoleColor.Gray);
    private static readonly (string Command, string Args)[] Commands =
    [
        ("помощь", ""), ("купить", "№ ТОВАР КОЛ-ВО"), ("продать", "№ ТОВАР КОЛ-ВО"),
        ("далее", "[ДНИ]"), ("повозка", "ГОРОД"),
        ("склад", "ГОРОД"), ("выгрузить", "№ ТОВАР КОЛ-ВО"),
        ("загрузить", "№ ТОВАР КОЛ-ВО"), ("сохранить", ""),
        ("продолжить", ""), ("меню / выход", "")
    ];
    private static readonly string[] HelpTitles = ["Управление", "Цель и время", "Рынки и разведка", "Производство", "Контракты и логистика"];
    private static readonly string[][] HelpPages =
    [
        [
            "# УПРАВЛЕНИЕ МЫШЬЮ",
            "• Голубые и зелёные надписи в квадратных скобках — кнопки. Нажмите левой кнопкой мыши, чтобы выполнить действие.",
            "• На карте можно нажать номер города или его название. Справа откроются рынок, предприятия и маршруты выбранного города.",
            "• Колёсико прокручивает сведения о компании. Щелчок по строке ввода перемещает текстовый курсор.",
            "",
            "# КЛАВИАТУРА",
            "• Esc возвращает с карты, контрактов или справки. В строке команд Esc очищает введённый текст.",
            "• Стрелки ← →, Home, End, Backspace и Delete редактируют команду. Enter выполняет её.",
            "• PageUp и PageDown прокручивают компанию; в справке они переключают разделы. Клавиша M закрывает отдельный экран.",
            "• В главном меню используйте мышь, стрелки или W/S; Enter открывает выбранный пункт."
        ],
        [
            "# ЦЕЛЬ ПАРТИИ",
            "• Нужно увеличить капитал компании до 50 000 монет. После достижения цели партия продолжается.",
            "• Капитал — это казна + базовая стоимость грузов в повозках и на складах + 500 за каждую повозку + 400 за склад + стоимость купленных предприятий.",
            "• Товары на городских рынках вам не принадлежат и в капитал не входят. Покупка предприятия переводит деньги в актив, поэтому сама по себе почти не меняет капитал.",
            "",
            "# ФИНАНСОВОЕ ВРЕМЯ",
            "• Семь игровых дней считаются финансовым кварталом. В конце квартала купленные предприятия выплачивают доход.",
            "• Четыре квартала, или 28 дней, образуют финансовый год.",
            "• Время движется только после команды «далее». В совместной игре новый день начинается, когда оба торговца готовы."
        ],
        [
            "# ЦЕНЫ И ЗАПАСЫ",
            "• «Купить» и «продать» указаны с точки зрения игрока. Цена каждой следующей единицы партии пересчитывается: крупная покупка постепенно дорожает, крупная продажа дешевеет.",
            "• Товары местных производителей обычно дешевле. Компоненты, нужные городским мастерским, обычно дороже. Случайные стартовые запасы делают каждую карту отличной от предыдущей.",
            "",
            "# СВЕДЕНИЯ О РЫНКАХ",
            "• Рынок города с вашей повозкой виден точно. После отъезда остаётся снимок цен на день отправления.",
            "• Знак ~ означает примерную цену соседнего с посещённым города. У дальнего города цены скрыты полностью.",
            "• Разведка за 40 монет даёт свежий точный снимок выбранного рынка. Она не открывает соседние города.",
            "• Сравнение показывает прибыль за одну единицу в обоих направлениях. Расходы на поездку в эту цифру не включены."
        ],
        [
            "# ПРОИЗВОДСТВЕННЫЕ ЦЕПОЧКИ",
            "• Предприятия ежедневно берут компоненты с рынка своего города и добавляют готовый товар. Без нужного сырья переработка останавливается.",
            "• Древесина превращается в уголь или доски; руда + уголь — в слитки; слитки + доски — в инструменты.",
            "• Зерно превращается в муку, затем в хлеб; шерсть — в ткань; камень — в тёсаный камень.",
            "",
            "# ПОКУПКА ПРЕДПРИЯТИЙ",
            "• В карточке города показаны рецепты и кнопки покупки. Добывающие предприятия стоят 4 000, простые перерабатывающие — 6 500, сложные — 9 000 монет.",
            "• Доход за квартал: 450, 750 или 1 100 монет соответственно. Владение не забирает произведённый товар с рынка — предприятие приносит фиксированный доход."
        ],
        [
            "# КОНТРАКТЫ",
            "• Города и гильдии заказывают хлеб, ткань, инструменты и тёсаный камень. Награда выше обычной рыночной цены и помогает окупить производственную торговлю.",
            "• Примите контракт, соберите весь объём в одной повозке, привезите её в город назначения и нажмите «Сдать». Одновременно можно вести до трёх контрактов.",
            "• После указанного дня заказ считается проваленным. Новые предложения появляются каждую неделю. В совместной игре контракт получает первый принявший его торговец.",
            "",
            "# ПЕРЕВОЗКИ, СКЛАДЫ И СОХРАНЕНИЕ",
            "• Повозка вмещает 60 единиц. Цена рейса — 8 монет за день пути и списывается при отправлении. Между командами время не проходит.",
            "• Склад вмещает 300 единиц и позволяет оставить товар в городе. Торговля и перегрузка доступны только стоящей в городе повозке.",
            "• Сохранение ручное: нажмите «сохранить». Команда «меню» выходит без автосохранения; «продолжить» загружает единственный слот."
        ]
    ];

    public static void Run(Session session)
    {
        using var events = new PointerInput();
        mouseCommand = null;
        mapOpen = false;
        contractsOpen = false;
        helpOpen = false;
        helpPage = 0;
        mapCity = session.Game.WagonsFor(session.IsCoop && !session.IsHost ? 1 : 0)[0].City;
        mapCompareFrom = null;
        mapGoodsPage = 0;
        var input = new StringBuilder();
        int caret = 0, scroll = 0, previousWidth = 0, previousHeight = 0, lastRevision = -1;
        bool dirty = true;
        bool alternate = EnableVirtualTerminal();
        VirtualTerminal = alternate; PreviousRows.Clear();
        var oldColor = Console.ForegroundColor;
        bool oldCursor = !OperatingSystem.IsWindows() || Console.CursorVisible;
        try
        {
            if (alternate) Console.Write("\u001b[?1049h");
            Console.Clear();
            while (!session.Exit)
            {
                session.PumpNetwork();
                if (session.Revision != lastRevision) { lastRevision = session.Revision; dirty = true; }
                int width = Console.WindowWidth, height = Console.WindowHeight;
                if (width != previousWidth || height != previousHeight)
                {
                    Console.Clear(); PreviousRows.Clear(); dirty = true;
                    previousWidth = width; previousHeight = height;
                }
                if (dirty)
                {
                    try { Draw(session, input.ToString(), caret, ref scroll, width, height); }
                    catch (ArgumentOutOfRangeException e) { Logger.Error("Ошибка перерисовки после изменения размера окна.", e); previousWidth = 0; }
                    dirty = false;
                }
                if (!events.TryRead(out var action)) { Thread.Sleep(30); continue; }
                dirty = true;
                if (action.Wheel != 0) { scroll = Math.Max(0, scroll - action.Wheel * 3); continue; }
                if (action.Click)
                {
                    var hit = Hits.FirstOrDefault(h => action.Y == h.Y && action.X >= h.X && action.X < h.X + h.Width);
                    if (hit is not null) hit.Activate();
                    else if (action.Y == height - 2)
                    {
                        int room = Math.Max(1, width - 5), offset = Math.Max(0, caret - room);
                        caret = Math.Clamp(action.X - 2 + offset, 0, input.Length);
                    }
                    continue;
                }
                if (action.Key is not { } key) continue;
                if (mapOpen || contractsOpen || helpOpen)
                {
                    if (helpOpen && key.Key is ConsoleKey.LeftArrow or ConsoleKey.PageUp) helpPage = (helpPage + HelpPages.Length - 1) % HelpPages.Length;
                    else if (helpOpen && key.Key is ConsoleKey.RightArrow or ConsoleKey.PageDown) helpPage = (helpPage + 1) % HelpPages.Length;
                    else if (key.Key is ConsoleKey.Escape or ConsoleKey.M) { mapOpen = false; contractsOpen = false; helpOpen = false; }
                    continue;
                }
                if (mouseCommand is not null)
                {
                    if (key.Key == ConsoleKey.Escape) { mouseCommand = null; continue; }
                    if (key.Key == ConsoleKey.Enter && mouseCommand.Ready)
                    {
                        session.Execute(mouseCommand.Preview); mouseCommand = null; continue;
                    }
                    // Typing returns to the regular command line.
                    mouseCommand = null;
                }
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        if (IsMapCommand(input.ToString())) mapOpen = true;
                        else if (IsHelpCommand(input.ToString())) helpOpen = true;
                        else session.Execute(input.ToString());
                        input.Clear(); caret = 0; break;
                    case ConsoleKey.Backspace:
                        if (caret > 0) input.Remove(--caret, 1); break;
                    case ConsoleKey.Delete:
                        if (caret < input.Length) input.Remove(caret, 1); break;
                    case ConsoleKey.LeftArrow: caret = Math.Max(0, caret - 1); break;
                    case ConsoleKey.RightArrow: caret = Math.Min(input.Length, caret + 1); break;
                    case ConsoleKey.Home: caret = 0; break;
                    case ConsoleKey.End: caret = input.Length; break;
                    case ConsoleKey.Escape: input.Clear(); caret = 0; break;
                    case ConsoleKey.PageDown: scroll += 5; break;
                    case ConsoleKey.PageUp: scroll = Math.Max(0, scroll - 5); break;
                    default:
                        if (!char.IsControl(key.KeyChar) && input.Length < 256)
                            input.Insert(caret++, key.KeyChar);
                        break;
                }
            }
        }
        finally
        {
            if (alternate) Console.Write("\u001b[?1049l");
            else Console.Clear();
            Console.ForegroundColor = oldColor;
            Console.CursorVisible = oldCursor;
        }
    }

    private static void Draw(Session s, string input, int caret, ref int scroll, int width, int height)
    {
        Hits.Clear();
        Console.CursorVisible = false;
        int usable = Math.Max(1, width - 1);
        var cells = new char[Math.Max(1, height), usable];
        var colors = new ConsoleColor[Math.Max(1, height), usable];
        void Paint(int x, int y, int limit, params Part[] parts)
        {
            if (y < 0 || y >= height - 1 || x >= usable) return;
            int column = x;
            int remaining = Math.Min(limit, usable - x);
            foreach (var part in parts)
            {
                if (remaining <= 0) break;
                string text = part.Text.Replace('\r', ' ').Replace('\n', ' ');
                if (text.Length > remaining) text = text[..remaining];
                foreach (char ch in text) { cells[y, column] = ch; colors[y, column++] = part.Color; }
                remaining -= text.Length;
            }
            while (remaining-- > 0) { cells[y, column] = ' '; colors[y, column++] = Ink.Text; }
        }
        // Every visible row is overwritten; no newlines or scrolling output.
        for (int y = 0; y < height - 1; y++) Paint(0, y, usable);
        if (width < 76 || height < 24)
        {
            Paint(0, 0, usable, new Part("Увеличьте окно до 76 x 24 или больше.", Ink.Parameter));
            Paint(0, 1, usable, new Part("Рекомендуется 110 x 34. Ввод команд остаётся доступен."));
        }
        else if (helpOpen)
        {
            Paint(0, 0, usable, new Part(" СПРАВКА", Ink.Heading), new Part($"   Раздел {helpPage + 1}/{HelpPages.Length}: {HelpTitles[helpPage]}", Ink.Parameter));
            Paint(0, 1, usable, new Part(new string('─', usable), Ink.Muted));
            int row = 3;
            foreach (string paragraph in HelpPages[helpPage])
            {
                if (paragraph.Length == 0) { row++; continue; }
                bool heading = paragraph.StartsWith("# ");
                string text = heading ? paragraph[2..] : paragraph;
                foreach (string line in Wrap(text, Math.Max(20, usable - 4)))
                    Paint(2, row++, usable - 4, new Part(line, heading ? Ink.Heading : paragraph.StartsWith("•") ? Ink.Text : Ink.Muted));
                if (heading) row++;
            }
            int navRow = height - 5;
            string previous = "[ ← ПРЕДЫДУЩИЙ ]", next = "[ СЛЕДУЮЩИЙ → ]";
            Paint(2, navRow, previous.Length, new Part(previous, Ink.Command));
            Hits.Add(new Hit(2, navRow, previous.Length, () => helpPage = (helpPage + HelpPages.Length - 1) % HelpPages.Length));
            int nextX = Math.Max(previous.Length + 6, usable - next.Length - 2);
            Paint(nextX, navRow, next.Length, new Part(next, Ink.Command));
            Hits.Add(new Hit(nextX, navRow, next.Length, () => helpPage = (helpPage + 1) % HelpPages.Length));
            Hits.Add(new Hit(1, height - 3, Math.Min(34, usable - 1), () => helpOpen = false));
        }
        else if (contractsOpen)
        {
            int player = s.IsCoop && !s.IsHost ? 1 : 0;
            Paint(0, 0, usable, new Part(" КОНТРАКТЫ ГОРОДОВ И ГИЛЬДИЙ", Ink.Heading),
                new Part($"   {Game.FinancialDate(s.Game.Day)}   Активно: {s.Game.Contracts.Count(c => c.AcceptedBy == player && !c.Completed && !c.Failed)}/3", Ink.Parameter));
            Paint(0, 1, usable, new Part(new string('─', usable), Ink.Muted));
            Paint(1, 2, usable - 2, new Part("Примите заказ, доставьте итоговый товар в указанный город и сдайте его подходящей повозкой.", Ink.Muted));
            void ContractButton(int x, int row, string label, Action action)
            {
                if (row >= height - 4 || x >= usable) return;
                int buttonWidth = Math.Min(label.Length, usable - x);
                Paint(x, row, buttonWidth, new Part(label, Ink.Command));
                Hits.Add(new Hit(x, row, buttonWidth, action));
            }
            var visibleContracts = s.Game.Contracts
                .Where(c => !c.Completed && !c.Failed && (c.AcceptedBy is null || c.AcceptedBy == player))
                .OrderByDescending(c => c.AcceptedBy == player).ThenBy(c => c.DeadlineDay).ToList();
            int row = 4;
            int shown = 0;
            foreach (var contract in visibleContracts)
            {
                if (row + 1 >= height - 4) break;
                shown++;
                string owner = contract.AcceptedBy == player ? "ПРИНЯТ" : "ДОСТУПЕН";
                Paint(1, row++, usable - 2,
                    new Part($"#{contract.Id}  {contract.Issuer}", Ink.Heading),
                    new Part($"   {owner}", contract.AcceptedBy == player ? Ink.Parameter : Ink.Success));
                string details = $"  {Ru.Name(contract.Good)}: {contract.Amount} → {Ru.Name(contract.City)} | до дня {contract.DeadlineDay} | {contract.Reward} мон.";
                Paint(1, row, usable - 2, new Part(details));
                int actionX = Math.Min(usable - 22, Math.Max(52, details.Length + 3));
                if (contract.AcceptedBy is null)
                {
                    int contractId = contract.Id;
                    ContractButton(actionX, row, $"[ ПРИНЯТЬ #{contract.Id} ]", () => s.Execute($"принять {contractId}"));
                }
                else
                {
                    var wagon = s.Game.WagonsFor(player).FirstOrDefault(w => w.Destination is null && w.City == contract.City && w.Cargo[contract.Good] >= contract.Amount);
                    if (wagon is not null)
                    {
                        int contractId = contract.Id, wagonId = wagon.Id;
                        ContractButton(actionX, row, $"[ СДАТЬ: ПОВОЗКА #{wagon.Id} ]", () => s.Execute($"доставить {contractId} {wagonId}"));
                    }
                    else Paint(actionX, row, Math.Max(1, usable - actionX), new Part("нужна повозка с товаром", Ink.Muted));
                }
                row += 2;
            }
            if (shown < visibleContracts.Count) Paint(1, height - 5, usable - 2, new Part($"Ещё контрактов: {visibleContracts.Count - shown}. Увеличьте высоту окна.", Ink.Parameter));
            if (visibleContracts.Count == 0) Paint(1, 5, usable - 2, new Part("Сейчас предложений нет. Новые появляются раз в неделю.", Ink.Muted));
            Hits.Add(new Hit(1, height - 3, Math.Min(34, usable - 1), () => contractsOpen = false));
        }
        else if (mapOpen)
        {
            int player = s.IsCoop && !s.IsHost ? 1 : 0;
            Paint(0, 0, usable, new Part(" КАРТА ТОРГОВЫХ ПУТЕЙ", Ink.Heading),
                new Part($"   {Game.FinancialDate(s.Game.Day)}   30 км за день", Ink.Parameter));
            Paint(0, 1, usable, new Part(new string('─', usable), Ink.Muted));
            const int panelWidth = 42;
            int mapWidth = Math.Clamp(usable - panelWidth - 4, 30, 110);
            int legendColumns = mapWidth >= 70 ? 4 : mapWidth >= 42 ? 2 : 0;
            int legendRows = legendColumns == 0 ? 1 : (Game.Cities.Length + legendColumns - 1) / legendColumns;
            int mapHeight = Math.Clamp(height - 11 - legendRows, 9, 28);
            int mapX = 1;
            var markers = s.Game.WagonsFor(0).Select(w => new MapMarker('A', w)).ToList();
            if (s.Game.SecondMerchant is not null) markers.AddRange(s.Game.WagonsFor(1).Select(w => new MapMarker('B', w)));
            string[] map = s.Game.World.Render(mapWidth, mapHeight, markers);
            Paint(mapX, 2, mapWidth + 2, new Part("┌" + new string('─', mapWidth) + "┐", Ink.Muted));
            for (int row = 0; row < map.Length; row++)
            {
                Paint(mapX, row + 3, 1, new Part("│", Ink.Muted));
                Paint(mapX + 1, row + 3, mapWidth, Terrain(map[row]));
                Paint(mapX + mapWidth + 1, row + 3, 1, new Part("│", Ink.Muted));
            }
            Paint(mapX, mapHeight + 3, mapWidth + 2, new Part("└" + new string('─', mapWidth) + "┘", Ink.Muted));
            void SelectCity(string city)
            {
                mapCity = city;
                mapGoodsPage = 0;
                if (mapCompareFrom == city) mapCompareFrom = null;
            }
            for (int i = 0; i < Game.Cities.Length; i++)
            {
                string city = Game.Cities[i];
                MapPoint point = s.Game.World.ScreenPosition(city, mapWidth, mapHeight);
                Hits.Add(new Hit(mapX + 1 + point.X, 3 + point.Y, 1, () => SelectCity(city)));
            }
            int legendRow = mapHeight + 5;
            if (legendColumns == 0)
            {
                Paint(1, legendRow, mapWidth + 2, new Part("Нажмите номер города на карте", Ink.Muted));
            }
            else
            {
                int cellWidth = (mapWidth + 2) / legendColumns;
                for (int i = 0; i < Game.Cities.Length; i += legendColumns)
                {
                    foreach (int n in Enumerable.Range(i, Math.Min(legendColumns, Game.Cities.Length - i)))
                    {
                        string city = Game.Cities[n];
                        int column = 1 + (n - i) * cellWidth;
                        string label = $"{n + 1} {Ru.Name(city)}";
                        if (label.Length > cellWidth - 1) label = label[..Math.Max(1, cellWidth - 2)] + "…";
                        Paint(column, legendRow + i / legendColumns, cellWidth - 1, new Part(label, city == mapCity ? Ink.Command : Ink.Text));
                        Hits.Add(new Hit(column, legendRow + i / legendColumns, label.Length, () => SelectCity(city)));
                    }
                }
            }
            int rx = mapX + mapWidth + 4;
            void MapButton(int row, string label, Action action)
            {
                Paint(rx, row, panelWidth, new Part(label, Ink.Command));
                Hits.Add(new Hit(rx, row, panelWidth, action));
            }
            var cityInfo = s.Game.MarketInfo(mapCity, player);
            Paint(rx, 3, panelWidth, new Part(Ru.Name(mapCity).ToUpperInvariant(), Ink.Heading));
            Paint(rx, 4, panelWidth, new Part(Knowledge(cityInfo), cityInfo.Approximate ? Ink.Parameter : Ink.Muted));
            int panelRow = 6;
            Paint(rx, panelRow++, panelWidth, new Part("ПРЕДПРИЯТИЯ", Ink.Heading));
            foreach (var enterprise in s.Game.EnterprisesAt(mapCity))
            {
                string ownership = enterprise.Owner == player ? $" | ВАШЕ: +{Game.EnterpriseQuarterIncome(enterprise)}/кв." : enterprise.Owner is not null ? $" | торговец {enterprise.Owner + 1}" : "";
                Paint(rx, panelRow++, panelWidth, new Part(Game.RecipeText(enterprise) + ownership, enterprise.Owner == player ? Ink.Success : Ink.Muted));
                if (enterprise.Owner is null)
                {
                    int enterpriseId = enterprise.Id;
                    MapButton(panelRow++, $"[ КУПИТЬ #{enterprise.Id} — {Game.EnterprisePrice(enterprise)} МОН. ]", () => s.Execute($"предприятие {enterpriseId}"));
                }
            }
            const int goodsPerPage = 5;
            int goodsPages = (Game.Goods.Length + goodsPerPage - 1) / goodsPerPage;
            mapGoodsPage = Math.Clamp(mapGoodsPage, 0, goodsPages - 1);
            var visibleGoods = Game.Goods.Skip(mapGoodsPage * goodsPerPage).Take(goodsPerPage).ToArray();
            if (mapCompareFrom is { } firstCity && firstCity != mapCity)
            {
                var firstInfo = s.Game.MarketInfo(firstCity, player);
                Paint(rx, panelRow++, panelWidth, new Part($"СРАВНЕНИЕ: {Short(Ru.Name(firstCity))} → {Short(Ru.Name(mapCity))}", Ink.Heading));
                Paint(rx, panelRow++, panelWidth, new Part("1: " + Knowledge(firstInfo), Ink.Muted));
                Paint(rx, panelRow++, panelWidth, new Part("2: " + Knowledge(cityInfo), Ink.Muted));
                if (firstInfo.Unknown || cityInfo.Unknown)
                {
                    Paint(rx, panelRow++, panelWidth, new Part("Для сравнения нужны сведения о рынках.", Ink.Parameter));
                }
                else
                {
                    Paint(rx, panelRow++, panelWidth, new Part("Товар      1 куп/пр 2 куп/пр  1→2  2→1", Ink.Muted));
                    foreach (string good in visibleGoods)
                    {
                        int buy1 = s.Game.Price(firstCity, good, true, firstInfo.Stocks[good]);
                        int sell1 = s.Game.Price(firstCity, good, false, firstInfo.Stocks[good]);
                        int buy2 = s.Game.Price(mapCity, good, true, cityInfo.Stocks[good]);
                        int sell2 = s.Game.Price(mapCity, good, false, cityInfo.Stocks[good]);
                        int profit12 = sell2 - buy1, profit21 = sell1 - buy2;
                        Paint(rx, panelRow++, panelWidth,
                            new Part($"{Ru.Name(good),-10}{Quote(buy1, firstInfo),3}/{Quote(sell1, firstInfo),-3} {Quote(buy2, cityInfo),3}/{Quote(sell2, cityInfo),-3} "),
                            new Part($"{profit12,4}", profit12 > 0 ? Ink.Success : Ink.Muted),
                            new Part($"{profit21,5}", profit21 > 0 ? Ink.Success : Ink.Muted));
                    }
                    MapButton(panelRow++, $"[ ТОВАРЫ {mapGoodsPage + 1}/{goodsPages} — ДАЛЬШЕ ]", () => mapGoodsPage = (mapGoodsPage + 1) % goodsPages);
                }
                if (!firstInfo.Current) MapButton(panelRow++, "[ РАЗВЕДКА ПЕРВОГО ГОРОДА — 40 ]", () => s.Execute("разведка " + Ru.Name(firstCity)));
                if (!cityInfo.Current) MapButton(panelRow++, "[ РАЗВЕДКА ВТОРОГО ГОРОДА — 40 ]", () => s.Execute("разведка " + Ru.Name(mapCity)));
                MapButton(panelRow++, "[ ЗАКРЫТЬ СРАВНЕНИЕ ]", () => mapCompareFrom = null);
            }
            else
            {
                if (cityInfo.Unknown)
                    Paint(rx, panelRow++, panelWidth, new Part("Цены откроются после визита в соседний", Ink.Parameter), new Part(" город или покупки разведданных.", Ink.Parameter));
                else
                {
                    Paint(rx, panelRow++, panelWidth, new Part("Товар        запас  купить  продать", Ink.Muted));
                    foreach (string good in visibleGoods)
                        Paint(rx, panelRow++, panelWidth,
                            new Part($"{Ru.Name(good),-12}{Quote(cityInfo.Stocks[good], cityInfo),5}  ", Ink.Parameter),
                            new Part($"{Quote(s.Game.Price(mapCity, good, true, cityInfo.Stocks[good]), cityInfo),6}  "),
                            new Part($"{Quote(s.Game.Price(mapCity, good, false, cityInfo.Stocks[good]), cityInfo),7}", Ink.Success));
                    MapButton(panelRow++, $"[ ТОВАРЫ {mapGoodsPage + 1}/{goodsPages} — ДАЛЬШЕ ]", () => mapGoodsPage = (mapGoodsPage + 1) % goodsPages);
                    MapButton(panelRow++, "[ СРАВНИТЬ С ДРУГИМ ГОРОДОМ ]", () => mapCompareFrom = mapCity);
                }
                if (!cityInfo.Current)
                    MapButton(panelRow++, "[ КУПИТЬ РАЗВЕДКУ — 40 МОН. ]", () => s.Execute("разведка " + Ru.Name(mapCity)));
            }
            if (mapCompareFrom == mapCity)
                Paint(rx, panelRow++, panelWidth, new Part("Теперь выберите второй город на карте.", Ink.Parameter));
            Paint(rx, panelRow++, panelWidth, new Part("МАРШРУТ И ПОЕЗДКА", Ink.Heading));
            foreach (var wagon in s.Game.WagonsFor(player).Where(w => w.Destination is null))
            {
                if (wagon.City == mapCity)
                {
                    Paint(rx, panelRow++, panelWidth, new Part($"Повозка #{wagon.Id} уже находится здесь.", Ink.Muted));
                    continue;
                }
                decimal days = s.Game.World.TravelDays(wagon.City, mapCity);
                int fee = (int)Math.Ceiling(days * 8);
                Paint(rx, panelRow++, panelWidth, new Part($"#{wagon.Id} из {Short(Ru.Name(wagon.City))}: {Game.DistanceKm(days)} км, {Game.FormatDays(days)}, {fee} мон."));
                int wagonId = wagon.Id;
                MapButton(panelRow++, $"[ ОТПРАВИТЬ ПОВОЗКУ #{wagon.Id} ]", () => s.Execute($"ехать {wagonId} {Ru.Name(mapCity)}"));
            }
            Paint(1, height - 5, mapWidth + 2,
                new Part("· дорога   ≈ вода   ▲ горы   ♣ лес   ", Ink.Muted),
                new Part("A", Ink.Command), new Part(" торговец 1   "),
                new Part("B", Ink.Success), new Part(" торговец 2   @ оба"));
            Hits.Add(new Hit(1, height - 3, Math.Min(34, usable - 1), () => mapOpen = false));
        }
        else
        {
            int right = Math.Max(37, width / 3), left = usable - right - 3, rx = left + 3;
            int localPlayer = s.IsCoop && !s.IsHost ? 1 : 0;
            string network = s.IsCoop ? $"  |  ТОРГОВЕЦ {localPlayer + 1} ({(s.IsHost ? "ХОСТ" : "ГОСТЬ")})" : "";
            Paint(0, 0, usable, new Part(" ТОРГОВАЯ КОМПАНИЯ", Ink.Heading),
                new Part($"   {Game.FinancialDate(s.Game.Day)} | Казна {s.Game.GoldFor(localPlayer)} | Капитал {s.Game.CompanyValue(localPlayer)} / {Game.TargetCompanyValue}{network}", Ink.Parameter));
            Paint(0, 1, usable, new Part(new string('-', usable), Ink.Muted));
            const string mapButton = "[КАРТА]", contractsButton = "[КОНТРАКТЫ]", helpButton = "[?]";
            Paint(0, 2, left, new Part($" {mapButton}", Ink.Command), new Part($" {contractsButton}", Ink.Success), new Part($" {helpButton}", Ink.Parameter));
            int toolbarX = 1;
            Hits.Add(new Hit(toolbarX, 2, mapButton.Length, () => mapOpen = true));
            toolbarX += mapButton.Length + 1;
            Hits.Add(new Hit(toolbarX, 2, contractsButton.Length, () => contractsOpen = true));
            toolbarX += contractsButton.Length + 1;
            Hits.Add(new Hit(toolbarX, 2, helpButton.Length, () => helpOpen = true));
            int contentHeight = height - 8;
            var lines = Company(s, left);
            scroll = Math.Clamp(scroll, 0, Math.Max(0, lines.Count - contentHeight));
            for (int y = 0; y < contentHeight; y++)
            {
                if (y + scroll < lines.Count) Paint(0, y + 3, left, lines[y + scroll]);
                Paint(left + 1, y + 3, 1, new Part("|", Ink.Muted));
            }
            void Button(int row, string text, Action activate)
            {
                if (row >= height - 5) return;
                Paint(rx, row, right, new Part(text, Ink.Command));
                Hits.Add(new Hit(rx, row, Math.Min(right, usable - rx), activate));
            }
            Paint(rx, 2, right, new Part("КОМАНДЫ — нажмите мышью", Ink.Heading));
            if (mouseCommand is { } builder)
            {
                Paint(rx, 3, right, new Part(builder.Command.ToUpperInvariant(), Ink.Heading));
                Paint(rx, 4, right, new Part(builder.Prompt, Ink.Parameter));
                if (!builder.Ready)
                {
                    var choices = builder.Choices(s);
                    int pageSize = Math.Max(1, height - 15);
                    int pages = Math.Max(1, (choices.Count + pageSize - 1) / pageSize);
                    builder.Page = Math.Clamp(builder.Page, 0, pages - 1);
                    for (int i = 0; i < pageSize && builder.Page * pageSize + i < choices.Count; i++)
                    {
                        var choice = choices[builder.Page * pageSize + i];
                        Button(6 + i, "[ " + choice.Label + " ]", () => builder.Select(choice.Value));
                    }
                    if (pages > 1)
                        Button(height - 8, $"[ Ещё: {builder.Page + 1}/{pages} ]", () => builder.Page = (builder.Page + 1) % pages);
                }
                else
                {
                    if (builder.NeedsQuantity)
                    {
                        Button(6, "[ -10 ]", () => builder.Adjust(-10));
                        Button(7, "[ -1 ]", () => builder.Adjust(-1));
                        Button(8, "[ +1 ]", () => builder.Adjust(1));
                        Button(9, "[ +10 ]", () => builder.Adjust(10));
                    }
                    Paint(rx, 11, right, new Part(builder.Preview, Ink.Parameter));
                    Button(13, "[ Выполнить ]", () => { s.Execute(builder.Preview); mouseCommand = null; });
                }
                Button(height - 7, "[ Отмена / назад к командам ]", () => mouseCommand = null);
            }
            else
            {
            for (int i = 0; i < Commands.Length && i + 3 < height - 5; i++)
            {
                string command = Commands[i].Command.Split(" / ")[0];
                Button(i + 3, Commands[i].Command + " " + Commands[i].Args, () =>
                {
                    if (command == "карта") mapOpen = true;
                    else if (command == "помощь") helpOpen = true;
                    else if (command == "сохранить" || command == "далее" && s.IsCoop) s.Execute(command);
                    else mouseCommand = new MouseCommand(command);
                });
                Paint(rx, i + 3, right, new Part(Commands[i].Command, Ink.Command), new Part(" " + Commands[i].Args, Ink.Parameter));
            }
            string[] notes = ["ЛКМ: команда и выбор параметров.", "Повозка: 500; склад: 400 монет.", "Рейс: 8 монет за день пути.", "Время: только по команде далее.", "Колесо / PgUp, PgDn: прокрутка.", "Esc: очистить ввод / отменить выбор."];
            int notesRow = Commands.Length + 4;
            for (int i = 0; i < notes.Length && i + notesRow < height - 5; i++) Paint(rx, i + notesRow, right, new Part(notes[i]));
            }
            Paint(0, height - 5, usable, new Part(new string('-', usable), Ink.Muted));
        }
        if (mapOpen || contractsOpen || helpOpen)
        {
            Paint(1, height - 3, usable - 1, new Part("[ Вернуться к компании ]", Ink.Command), new Part("   Esc или M", Ink.Muted));
            string specialMessage = helpOpen ? "Стрелки ← → или PageUp/PageDown переключают разделы." : s.Message.Replace('\r', ' ').Replace('\n', ' ');
            Paint(1, height - 2, usable - 2, new Part(specialMessage, s.Error ? Ink.Error : Ink.Success));
        }
        string message = mapOpen || contractsOpen || helpOpen ? "" : s.Message.Replace('\r', ' ').Replace('\n', ' ');
        for (int i = 0; i < 2; i++)
        {
            int start = i * usable;
            Paint(0, height - 4 + i, usable, new Part(start < message.Length ? message.Substring(start, Math.Min(usable, message.Length - start)) : "", s.Error ? Ink.Error : Ink.Success));
        }
        const string prompt = "> ";
        int room = Math.Max(1, usable - prompt.Length - 1), offset = Math.Max(0, caret - room);
        string visible = input[offset..];
        if (visible.Length > room) visible = visible[..room];
        var fragments = new List<Part> { new Part(prompt, Ink.Command) };
        int commandEnd = input.IndexOf(' ');
        if (commandEnd < 0) commandEnd = input.Length;
        int commandLength = Math.Clamp(commandEnd - offset, 0, visible.Length);
        fragments.Add(new Part(visible[..commandLength], Ink.Command));
        fragments.Add(new Part(visible[commandLength..], Ink.Parameter));
        if (!mapOpen && !contractsOpen && !helpOpen) Paint(0, height - 2, usable, fragments.ToArray());
        // Draw only changed rows. Build one terminal write to avoid flashing blank frames.
        var frame = new StringBuilder();
        int[] ansiColors = [30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97];
        for (int y = 0; y < height - 1; y++)
        {
            var signature = new StringBuilder();
            for (int x = 0; x < usable; x++) signature.Append(cells[y, x]).Append((char)colors[y, x]);
            string row = signature.ToString();
            if (PreviousRows.TryGetValue(y, out var old) && row == old) continue;
            if (VirtualTerminal) frame.Append($"\u001b[{y + 1};1H");
            else Console.SetCursorPosition(0, y);
            for (int x = 0; x < usable;)
            {
                var color = colors[y, x];
                var text = new StringBuilder();
                do { text.Append(cells[y, x++]); } while (x < usable && colors[y, x] == color);
                if (VirtualTerminal) frame.Append($"\u001b[{ansiColors[(int)color]}m").Append(text);
                else Ink.Write(text.ToString(), color);
            }
            PreviousRows[y] = row;
        }
        if (VirtualTerminal) { frame.Append("\u001b[0m"); Console.Write(frame); }
        if (!mapOpen && !contractsOpen && !helpOpen)
        {
            Console.SetCursorPosition(Math.Min(usable - 1, prompt.Length + caret - offset), Math.Max(0, height - 2));
            Console.CursorVisible = true;
        }
    }

    private static List<Part[]> Company(Session s, int availableWidth)
    {
        var g = s.Game;
        int player = s.IsCoop && !s.IsHost ? 1 : 0;
        var wagons = g.WagonsFor(player);
        var warehouses = g.WarehousesFor(player);
        var lines = new List<Part[]>();
        void Line(string text, ConsoleColor color = Ink.Text) => lines.Add([new Part(text, color)]);
        Line("МИР И ТОРГОВЛЯ", Ink.Heading);
        if (s.IsCoop)
        {
            Line($"Готовность: хост {(s.HostReady ? "ДА" : "нет")} | игрок 2 {(s.ClientReady ? "ДА" : "нет")}",
                s.HostReady && s.ClientReady ? Ink.Success : Ink.Parameter);
            int rival = player == 0 ? 1 : 0;
            Line($"Вы: торговец {player + 1}, {g.GoldFor(player)} монет | торговец {rival + 1}: {g.GoldFor(rival)} монет");
        }
        var ownedEnterprises = g.Enterprises.Where(e => e.Owner == player).ToArray();
        Line($"ПРЕДПРИЯТИЯ: {ownedEnterprises.Length} | квартальный доход {ownedEnterprises.Sum(Game.EnterpriseQuarterIncome)} мон.", ownedEnterprises.Length > 0 ? Ink.Success : Ink.Muted);
        Line("");
        if (s.ComparedMarket is { } compared)
        {
            string first = Ru.Name(s.Market), second = Ru.Name(compared);
            var firstInfo = g.MarketInfo(s.Market, player);
            var secondInfo = g.MarketInfo(compared, player);
            Line($"СРАВНЕНИЕ: {first} ↔ {second}", Ink.Heading);
            Line($"1: {Knowledge(firstInfo)} | 2: {Knowledge(secondInfo)}", Ink.Muted);
            if (firstInfo.Unknown || secondInfo.Unknown)
            {
                Line("Для сравнения нужны сведения об обоих рынках.", Ink.Parameter);
                Line("");
                goto MarketFinished;
            }
            Line($"Товар        {Short(first),11} {Short(second),11}  1→2  2→1", Ink.Muted);
            Line("             куп/прод    куп/прод   прибыль", Ink.Muted);
            foreach (string good in Game.Goods)
            {
                int buy1 = g.Price(s.Market, good, true, firstInfo.Stocks[good]), sell1 = g.Price(s.Market, good, false, firstInfo.Stocks[good]);
                int buy2 = g.Price(compared, good, true, secondInfo.Stocks[good]), sell2 = g.Price(compared, good, false, secondInfo.Stocks[good]);
                int forward = sell2 - buy1, reverse = sell1 - buy2;
                lines.Add([new Part($"{Ru.Name(good),-12}", Ink.Parameter),
                    new Part($"{Quote(buy1, firstInfo),4}/{Quote(sell1, firstInfo),-4}  {Quote(buy2, secondInfo),4}/{Quote(sell2, secondInfo),-4} "),
                    new Part($"{forward,5}", forward > 0 ? Ink.Success : Ink.Muted),
                    new Part($"{reverse,5}", reverse > 0 ? Ink.Success : Ink.Muted)]);
            }
            Line("1→2: купить в первом, продать во втором.", Ink.Muted);
            Line("Прибыль указана за 1 единицу без цены рейса.", Ink.Muted);
        }
        else
        {
            var info = g.MarketInfo(s.Market, player);
            Line("РЫНОК: " + Ru.Name(s.Market), Ink.Heading);
            Line(Knowledge(info), info.Approximate ? Ink.Parameter : Ink.Muted);
            if (info.Unknown)
            {
                Line("Посетите соседний город или купите разведданные.", Ink.Parameter);
                Line("");
                goto MarketFinished;
            }
            Line("Товар         Запас Купить Продать", Ink.Muted);
            foreach (string good in Game.Goods)
                lines.Add([new Part($"{Ru.Name(good),-12}", Ink.Parameter),
                    new Part($"{Quote(info.Stocks[good], info),6} "),
                    new Part($"{Quote(g.Price(s.Market, good, true, info.Stocks[good]), info),6} ", Ink.Parameter),
                    new Part($"{Quote(g.Price(s.Market, good, false, info.Stocks[good]), info),6}", Ink.Success)]);
        }
        MarketFinished:
        Line("");
        Line($"ВАШИ ПОВОЗКИ ({wagons.Count}) — груз до 60", Ink.Heading);
        foreach (var w in wagons)
        {
            Line($"#{w.Id} {Ru.Name(w.City)}" + (w.Destination is null ? " | стоянка" : $" -> {Ru.Name(w.Destination)}"), Ink.Command);
            if (w.Destination is not null) Line($"   До прибытия: {Game.FormatDays(w.DaysLeft)}.");
            Line($"   Груз {w.Used}/60: " + Cargo(w.Cargo));
        }
        Line("");
        Line($"ВАШИ СКЛАДЫ ({warehouses.Count})", Ink.Heading);
        foreach (var (city, cargo) in warehouses)
        {
            Line($"{Ru.Name(city)}: {cargo.Values.Sum()}/300", Ink.Parameter);
            Line("  " + Cargo(cargo));
        }
        if (warehouses.Count == 0) Line("Складов пока нет.");
        Line("");
        Line("Цены за единицу. Цена партии меняется.");
        return lines;
    }
    private static string Cargo(Dictionary<string, int> cargo) => string.Join(", ", cargo.Where(p => p.Value > 0).Select(p => $"{Ru.Name(p.Key)} {p.Value}")) is { Length: > 0 } text ? text : "пусто";
    private static string Short(string city) => city.Length <= 11 ? city : city[..10] + "…";
    private static string Quote(int value, MarketKnowledge info) => info.Unknown ? "?" : (info.Approximate ? "~" : "") + value;
    private static string Knowledge(MarketKnowledge info) => info.Current ? "точные цены: повозка в городе" : info.Unknown ? "нет сведений: город вне изученной области" : info.Approximate ? "примерная оценка: соседний город" : $"сведения на день {info.Day}";
    private static bool IsMapCommand(string input)
    {
        var parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 && Ru.Command(parts[0]) == "map";
    }
    private static bool IsHelpCommand(string input)
    {
        var parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 && Ru.Command(parts[0]) == "help";
    }
    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (text.Length == 0) { yield return ""; yield break; }
        string indent = text.StartsWith("•") ? "  " : "";
        string line = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line;
                line = indent + word;
            }
            else line += (line.Length == 0 ? "" : " ") + word;
        }
        if (line.Length > 0) yield return line;
    }
    private static Part[] Terrain(string line)
    {
        var parts = new List<Part>();
        foreach (char ch in line)
        {
            ConsoleColor color = ch switch
            {
                '≈' => ConsoleColor.DarkBlue, '▲' => ConsoleColor.DarkGray, '♣' => ConsoleColor.DarkGreen,
                '·' => ConsoleColor.DarkYellow, >= '1' and <= '8' => Ink.Parameter,
                'A' => Ink.Command, 'B' => Ink.Success, '@' => Ink.Heading, _ => Ink.Text
            };
            if (parts.Count > 0 && parts[^1].Color == color) parts[^1] = parts[^1] with { Text = parts[^1].Text + ch };
            else parts.Add(new Part(ch.ToString(), color));
        }
        return parts.ToArray();
    }

    private static bool EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return true;
        var handle = GetStdHandle(-11);
        return GetConsoleMode(handle, out uint mode) && SetConsoleMode(handle, mode | 4);
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
}


