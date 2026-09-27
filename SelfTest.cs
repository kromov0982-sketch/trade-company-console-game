namespace TradeCompany;

public static class SelfTest
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
        void Reject(Action action, string name)
        {
            try { action(); } catch (Exception e) when (e is InvalidOperationException or ArgumentException) { Check(true, name); return; }
            throw new Exception("FAIL: " + name);
        }
        Check(Updater.TryVersion("v1.2.3", out var parsedVersion) && parsedVersion == new Version(1, 2, 3), "updater parses release tags");
        Check(!Updater.TryVersion("release-latest", out _), "updater rejects invalid release tags");
        using (var mouseSession = Session.NewGame())
        {
            var buy = new MouseCommand("купить");
            buy.Select(buy.Choices(mouseSession)[0].Value);
            buy.Select(buy.Choices(mouseSession).Single(c => c.Value == Ru.Name("wood")).Value);
            buy.Adjust(9);
            mouseSession.Execute(buy.Preview);
            Check(!mouseSession.Error && mouseSession.Game.Wagons[0].Cargo["wood"] == 10, "mouse purchase executes through session");
            var travel = new MouseCommand("ехать");
            travel.Select("1"); travel.Select(Ru.Name("crossroads"));
            mouseSession.Execute(travel.Preview);
            Check(!mouseSession.Error && mouseSession.Game.Wagons[0].Destination == "crossroads", "mouse route selection");
            mouseSession.Execute(buy.Preview);
            Check(mouseSession.Error && mouseSession.Game.Wagons[0].Cargo["wood"] == 10, "mouse commands preserve transaction validation");
            var next = new MouseCommand("далее");
            next.Adjust(100); Check(next.Quantity == 30, "mouse day limit");
            next.Adjust(-100); Check(next.Quantity == 1, "mouse quantity stays positive");
            var market = new MouseCommand("рынок");
            market.Select(Ru.Name("crossroads")); mouseSession.Execute(market.Preview);
            Check(mouseSession.Market == "crossroads", "mouse market selection");
            var compare = new MouseCommand("сравнить");
            compare.Select(Ru.Name("oakwood"));
            Check(!compare.Ready && compare.Choices(mouseSession).All(c => c.Value != Ru.Name("oakwood")), "mouse comparison asks for a different second city");
            compare.Select(Ru.Name("riverport")); mouseSession.Execute(compare.Preview);
            Check(!mouseSession.Error && mouseSession.Market == "oakwood" && mouseSession.ComparedMarket == "riverport", "market comparison selection");
            int compareRevision = mouseSession.Revision;
            mouseSession.Execute("сравнить дубрава дубрава");
            Check(mouseSession.Error && mouseSession.Revision == compareRevision + 1 && mouseSession.Market == "oakwood" && mouseSession.ComparedMarket == "riverport", "market comparison rejects identical cities atomically");
            mouseSession.Execute("рынок перекрёсток");
            Check(mouseSession.ComparedMarket is null, "single market closes comparison");
        }
        var g = new Game();
        Check(g.MarketInfo("oakwood").Current && !g.MarketInfo("oakwood").Approximate, "market at wagon is current");
        Check(Game.Cities.Where(city => city != "oakwood").All(city =>
            g.World.AreNeighbors("oakwood", city) ? g.MarketInfo(city).Approximate : g.MarketInfo(city).Unknown), "only neighboring markets have opening estimates");
        string intelCity = Game.Cities.FirstOrDefault(city => city != "oakwood" && !g.World.AreNeighbors("oakwood", city)) ?? "riverport";
        var hiddenMarket = g.MarketInfo(intelCity);
        Check(g.World.AreNeighbors("oakwood", intelCity) ? hiddenMarket.Approximate : hiddenMarket.Unknown, "distant market prices stay hidden");
        int beforeIntel = g.Gold;
        g.BuyMarketInfo(intelCity);
        var purchasedMarket = g.MarketInfo(intelCity);
        Check(g.Gold == beforeIntel - 40 && !purchasedMarket.Approximate && purchasedMarket.Day == g.Day && !purchasedMarket.Current, "market information purchase creates current report");
        var anotherWorld = new Game();
        Check(g.World.Seed != anotherWorld.World.Seed, "new games generate different maps");
        Check(Game.Cities.Any(city => Game.Goods.Any(good => g.Stocks[city][good] != anotherWorld.Stocks[city][good])), "new maps randomize opening market stocks");
        Check(Game.Cities.All(city => Game.Goods.All(good =>
        {
            bool produced = g.EnterprisesAt(city).Any(e => Game.EnterpriseRecipes[e.Type].Outputs.ContainsKey(good));
            bool consumed = g.EnterprisesAt(city).Any(e => Game.EnterpriseRecipes[e.Type].Inputs.ContainsKey(good));
            int stock = g.Stocks[city][good];
            return produced && !consumed ? stock is >= 128 and <= 152 : !produced && consumed ? stock is >= 43 and <= 67 : true;
        })), "opening stocks follow local production and demand");
        Check(Game.Goods.Length == 13 && Game.EnterpriseRecipes.Count == 13, "expanded goods and enterprise catalogue");
        Check(g.Enterprises.Select(e => e.Type).Distinct().Count() == Game.EnterpriseRecipes.Count && Game.Cities.All(city => g.Enterprises.Count(e => e.City == city) == 2), "enterprises are distributed across cities");
        var smelting = new Game { Enterprises = [new Enterprise { Id = 1, City = "oakwood", Type = "smelter" }] };
        foreach (var stock in smelting.Stocks.Values) foreach (string good in Game.Goods) stock[good] = 0;
        smelting.Stocks["oakwood"]["ore"] = 4; smelting.Stocks["oakwood"]["coal"] = 2;
        smelting.Advance(1);
        Check(smelting.Stocks["oakwood"]["ore"] == 0 && smelting.Stocks["oakwood"]["coal"] == 0 && smelting.Stocks["oakwood"]["ingots"] == 3, "smelter consumes ore and coal to produce ingots");
        var extraction = new Game { Enterprises = [new Enterprise { Id = 1, City = "oakwood", Type = "quarry" }] };
        foreach (var stock in extraction.Stocks.Values) foreach (string good in Game.Goods) stock[good] = 0;
        extraction.Advance(1);
        Check(extraction.Stocks["oakwood"]["stone"] == 9, "extractive enterprise creates raw material");
        var investment = new Game { Gold = 10_000 };
        var purchasedEnterprise = investment.Enterprises[0];
        int enterprisePrice = Game.EnterprisePrice(purchasedEnterprise), enterpriseIncome = Game.EnterpriseQuarterIncome(purchasedEnterprise);
        int valueBeforeInvestment = investment.CompanyValue(0);
        investment.BuyEnterprise(purchasedEnterprise.Id);
        Check(purchasedEnterprise.Owner == 0 && investment.Gold == 10_000 - enterprisePrice && investment.CompanyValue(0) == valueBeforeInvestment, "enterprise purchase converts cash into company assets");
        int treasuryBeforeQuarter = investment.Gold;
        investment.Advance(6);
        Check(investment.Gold == treasuryBeforeQuarter + enterpriseIncome, "owned enterprise pays quarterly income");
        Check(Game.FinancialDate(1).Contains("год 1, квартал 1") && Game.FinancialDate(29).Contains("год 2, квартал 1"), "financial calendar has four quarters per year");
        var contractGame = new Game();
        var contract = contractGame.Contracts[0];
        int contractGold = contractGame.Gold;
        contractGame.AcceptContract(contract.Id);
        contractGame.Wagons[0].City = contract.City;
        contractGame.Wagons[0].Cargo[contract.Good] = contract.Amount;
        contractGame.DeliverContract(contract.Id, 1);
        Check(contract.Completed && contractGame.Gold == contractGold + contract.Reward && contractGame.Wagons[0].Cargo[contract.Good] == 0, "contract delivery consumes final goods and pays reward");
        var expiredGame = new Game();
        var expiring = expiredGame.Contracts[0];
        expiredGame.AcceptContract(expiring.Id);
        expiredGame.Advance(expiring.DeadlineDay - expiredGame.Day + 1);
        Check(expiring.Failed, "accepted contract fails after deadline");
        Check(Game.Cities.All(a => Game.Cities.All(b => a == b || g.World.TravelDays(a, b) > 0)), "generated map connects every city");
        var renderedMap = g.World.Render(50, 12);
        Check(renderedMap.Length == 12 && renderedMap.All(line => line.Length == 50), "generated map follows requested dimensions");
        Check(g.World.Render(64, 12, [new MapMarker('A', g.Wagons[0])]).Any(line => line.Contains('A')), "map shows merchant position");
        var oakwoodPoint = g.World.ScreenPosition("oakwood", 50, 12);
        Check(oakwoodPoint.X is >= 0 and < 50 && oakwoodPoint.Y is >= 0 and < 12, "map city hit target stays inside rendered map");
        Check(g.World.Roads.All(road => Game.DistanceKm(road.Days) > 0 && Game.DistanceKm(road.Days) % 15 == 0), "road distances use kilometres");
        g.Gold = 2000;
        int initialWoodStock = g.Stocks["oakwood"]["wood"];
        int initial = g.Gold, price = g.Price("oakwood", "wood", true);
        g.Trade(1, "wood", 30, true);
        Check(g.Wagons[0].Cargo["wood"] == 30 && g.Stocks["oakwood"]["wood"] == initialWoodStock - 30, "purchase conserves goods");
        Check(g.Price("oakwood", "wood", true) > price, "purchase raises price");
        int before = g.Gold;
        Reject(() => g.Trade(1, "wood", 31, true), "capacity enforced");
        Check(g.Gold == before && g.Wagons[0].Used == 30, "rejected transaction is atomic");
        Reject(() => g.Trade(1, "wood", -1, true), "negative quantity rejected");
        Reject(() => g.Trade(1, "ore", 1, false), "cannot sell absent cargo");
        decimal routeDays = g.World.TravelDays("oakwood", "crossroads");
        g.Travel(1, "crossroads");
        int departureDay = g.Day;
        int rememberedWood = g.MarketInfo("oakwood").Stocks["wood"];
        Check(g.Wagons[0].Route.Count > 1 && g.Wagons[0].TotalDays == routeDays, "wagon stores generated route");
        Check(g.Gold == before - (int)Math.Ceiling(routeDays * 8), "route cost charged once");
        Reject(() => g.Trade(1, "wood", 1, false), "cannot trade in transit");
        int routeTurns = (int)Math.Ceiling(routeDays);
        if (routeTurns > 1) { g.Advance(routeTurns - 1); Check(g.Wagons[0].DaysLeft is > 0 and <= 1, "travel consumes days"); }
        Check(g.MarketInfo("oakwood").Day == departureDay && g.MarketInfo("oakwood").Stocks["wood"] == rememberedWood, "departed market keeps last seen snapshot");
        g.Advance(1); Check(g.Wagons[0].City == "crossroads" && g.Wagons[0].Destination is null, "arrival");
        Check(g.VisitedCities.Contains("crossroads"), "arrival expands explored market area");
        g.Trade(1, "wood", 30, false); Check(g.Wagons[0].Cargo["wood"] == 0, "route cargo can be sold");
        var local = new Game(); local.Trade(1, "wood", 30, true); local.Trade(1, "wood", 30, false);
        Check(local.Gold < 1000, "no same-market round-trip exploit");
        g.Gold = Math.Max(g.Gold, 1000);
        g.BuyWarehouse("crossroads"); g.Trade(1, "grain", 10, true); g.Transfer(1, "grain", 7, true);
        Check(g.Wagons[0].Cargo["grain"] == 3 && g.Warehouses["crossroads"]["grain"] == 7, "warehouse transfer conserves goods");
        g.Transfer(1, "grain", 7, false); Check(g.Wagons[0].Cargo["grain"] == 10, "warehouse loading");
        var poor = new Game { Gold = 0 };
        Reject(() => poor.Trade(1, "grain", 1, true), "insufficient funds");
        Reject(() => poor.Travel(1, "riverport"), "cannot travel without funds");
        Reject(() => poor.Advance(31), "turn limit");
        for (int i = 0; i < 20; i++) poor.Advance(30);
        Check(poor.Stocks.Values.All(s => s.Values.All(n => n >= 0)), "600-day simulation has nonnegative stocks");
        string path = Path.Combine(Path.GetTempPath(), $"trade-test-{Guid.NewGuid()}.json");
        try
        {
            int savedJourneyDays = (int)Math.Ceiling(g.World.TravelDays("crossroads", "riverport"));
            g.Travel(1, "riverport"); g.Save(path); var loaded = Game.Load(path);
            Check(loaded.Gold == g.Gold && loaded.Day == g.Day && loaded.World.Seed == g.World.Seed && loaded.Wagons[0].Destination == "riverport" && loaded.Wagons[0].Cargo["grain"] == 10 && loaded.Warehouses.ContainsKey("crossroads") && loaded.MarketReports.ContainsKey(intelCity) && loaded.VisitedCities.Contains("crossroads") && loaded.Contracts.Count == g.Contracts.Count, "save/load round trip");
            loaded.Advance(savedJourneyDays); Check(loaded.Wagons[0].City == "riverport", "saved journey resumes");
            File.WriteAllText(path, "{\"Version\":99}"); Reject(() => Game.Load(path), "unsupported save rejected");
        }
        finally { if (File.Exists(path)) File.Delete(path); }

        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var hostTask = CoopLink.HostAsync(timeout.Token);
            using var clientLink = CoopLink.JoinAsync("127.0.0.1", timeout.Token).GetAwaiter().GetResult();
            using var hostLink = hostTask.GetAwaiter().GetResult();
            using var host = Session.HostGame(hostLink);
            using var client = Session.JoinGame(clientLink);

            bool WaitUntil(Func<bool> condition)
            {
                var limit = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < limit)
                {
                    host.PumpNetwork(); client.PumpNetwork();
                    if (condition()) return true;
                    Thread.Sleep(10);
                }
                return false;
            }

            Check(WaitUntil(() => client.Message.Contains("подключён")), "coop initial state sync");
            int sharedWoodBefore = host.Game.Stocks["oakwood"]["wood"];
            var clientBuy = new MouseCommand("купить");
            clientBuy.Select(clientBuy.Choices(client)[0].Value);
            clientBuy.Select(Ru.Name("wood")); clientBuy.Adjust(9);
            client.Execute(clientBuy.Preview);
            Check(WaitUntil(() => host.Game.WagonsFor(1)[0].Cargo["wood"] == 10 && client.Game.WagonsFor(1)[0].Cargo["wood"] == 10), "coop command sync");
            Check(host.Game.WagonsFor(0)[0].Cargo["wood"] == 0 && host.Game.GoldFor(0) == 1000, "coop merchants own separate assets");
            Check(host.Game.Stocks["oakwood"]["wood"] == sharedWoodBefore - 10, "coop merchants share markets");
            int clientGoldBeforeIntel = host.Game.GoldFor(1);
            client.Execute("разведка речнопорт");
            Check(WaitUntil(() => host.Game.MarketReportsFor(1).ContainsKey("riverport") && client.Game.GoldFor(1) == clientGoldBeforeIntel - 40), "coop market intelligence belongs to buyer");
            Check(!host.Game.MarketReportsFor(0).ContainsKey("riverport"), "coop merchants keep separate market knowledge");
            int offeredContract = host.Game.Contracts.First(c => c.AcceptedBy is null && !c.Failed).Id;
            client.Execute($"принять {offeredContract}");
            Check(WaitUntil(() => host.Game.Contracts.Single(c => c.Id == offeredContract).AcceptedBy == 1), "coop contract is assigned to accepting merchant");
            host.Execute("далее");
            Check(host.Game.Day == 1 && host.HostReady, "coop waits for both players");
            client.Execute("далее");
            Check(WaitUntil(() => host.Game.Day == 2 && client.Game.Day == 2), "coop advances after both ready");
        }
        Console.WriteLine($"All {passed} checks passed.");
    }
}
