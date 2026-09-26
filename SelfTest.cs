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
        var g = new Game();
        int initial = g.Gold, price = g.Price("oakwood", "wood", true);
        g.Trade(1, "wood", 30, true);
        Check(g.Wagons[0].Cargo["wood"] == 30 && g.Stocks["oakwood"]["wood"] == 70, "purchase conserves goods");
        Check(g.Price("oakwood", "wood", true) > price, "purchase raises price");
        int before = g.Gold;
        Reject(() => g.Trade(1, "wood", 31, true), "capacity enforced");
        Check(g.Gold == before && g.Wagons[0].Used == 30, "rejected transaction is atomic");
        Reject(() => g.Trade(1, "wood", -1, true), "negative quantity rejected");
        Reject(() => g.Trade(1, "iron", 1, false), "cannot sell absent cargo");
        g.Travel(1, "crossroads");
        Check(g.Gold == before - 16, "route cost charged once");
        Reject(() => g.Trade(1, "wood", 1, false), "cannot trade in transit");
        g.Advance(1); Check(g.Wagons[0].DaysLeft == 1, "travel consumes days");
        g.Advance(1); Check(g.Wagons[0].City == "crossroads" && g.Wagons[0].Destination is null, "arrival");
        g.Trade(1, "wood", 30, false); Check(g.Gold > initial, "starter route profitable");
        var local = new Game(); local.Trade(1, "wood", 30, true); local.Trade(1, "wood", 30, false);
        Check(local.Gold < 1000, "no same-market round-trip exploit");
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
            g.Travel(1, "riverport"); g.Save(path); var loaded = Game.Load(path);
            Check(loaded.Gold == g.Gold && loaded.Day == g.Day && loaded.Wagons[0].Destination == "riverport" && loaded.Wagons[0].Cargo["grain"] == 10 && loaded.Warehouses.ContainsKey("crossroads"), "save/load round trip");
            loaded.Advance(4); Check(loaded.Wagons[0].City == "riverport", "saved journey resumes");
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
            client.Execute("купить 1 древесина 10");
            Check(WaitUntil(() => host.Game.WagonsFor(1)[0].Cargo["wood"] == 10 && client.Game.WagonsFor(1)[0].Cargo["wood"] == 10), "coop command sync");
            Check(host.Game.WagonsFor(0)[0].Cargo["wood"] == 0 && host.Game.GoldFor(0) == 1000, "coop merchants own separate assets");
            Check(host.Game.Stocks["oakwood"]["wood"] == 90, "coop merchants share markets");
            host.Execute("далее");
            Check(host.Game.Day == 1 && host.HostReady, "coop waits for both players");
            client.Execute("далее");
            Check(WaitUntil(() => host.Game.Day == 2 && client.Game.Day == 2), "coop advances after both ready");
        }
        Console.WriteLine($"All {passed} checks passed.");
    }
}
