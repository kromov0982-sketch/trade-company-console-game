using Godot;
using TradeCompany;
using System.Text.Json;

public partial class Main
{
    public void CityNeedsTest()
    {
        string path = ProjectSettings.GlobalizePath("user://city-needs-test.json");
        try
        {
            static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Should reject operation"); }
            var g = new Game { Gold = 10000 };
            var mining = g.NeedsAt("northmine").Single(n => n.Kind == "project");
            var port = g.NeedsAt("riverport").Single(n => n.Kind == "project");
            Check(mining.Title != port.Title && mining.Required.ContainsKey("tools") && port.Required.ContainsKey("cloth") && !port.Required.ContainsKey("tools"), "Mining and port requests differ");
            g.CityNeeds.Clear(); g.Stocks["oakwood"]["grain"] = 0; g.Stocks["oakwood"]["bread"] = 0;
            g.Enterprises.Add(new Enterprise { Id = 500, City = "oakwood", Type = "smelter" });
            g.Stocks["oakwood"]["ore"] = 0; g.Stocks["oakwood"]["coal"] = 0;
            g.RefreshCityNeeds();
            var food = g.NeedsAt("oakwood").Single(n => n.Kind == "food");
            var craft = g.NeedsAt("oakwood").Single(n => n.Kind == "craft");
            Check(craft.Required.Count > 0 && craft.Required.Keys.All(k => g.Stocks["oakwood"][k] < 20), "Workshop task follows real shortage");
            Check(food.Reward == 12 && craft.Reward == 8 && g.NeedsAt("oakwood").Single(n => n.Kind == "project").Reward == 10, "Reduced influence rewards");
            int count = g.CityNeeds.Count; g.RefreshCityNeeds(); Check(count == g.CityNeeds.Count, "No duplicate active needs");
            Reject(() => g.SupplyCityNeed(food.Id, 1));
            g.Wagons[0].Cargo["grain"] = 8; g.SupplyCityNeed(food.Id, 1);
            Check(food.Delivered["grain"] == 8 && !food.Completed && g.InfluenceAt("oakwood") == 0 && g.Stocks["oakwood"]["grain"] == 8, "Partial delivery has real effect without early reward");
            g.Wagons[0].City = "riverport"; Reject(() => g.SupplyCityNeed(food.Id, 1)); g.Wagons[0].City = "oakwood";
            g.Wagons[0].Cargo["grain"] = 20; g.Wagons[0].Cargo["bread"] = 12;
            g.SupplyCityNeed(food.Id, 1);
            Check(food.Completed && g.InfluenceAt("oakwood") == food.Reward && g.Wagons[0].Cargo["grain"] == 8 && g.Wagons[0].Cargo["bread"] == 2, "Only missing amount consumed and reward paid once");
            Reject(() => g.SupplyCityNeed(food.Id, 1));
            // Even if the market stays empty, relief cannot be farmed again immediately.
            g.Stocks["oakwood"]["grain"] = 0; g.Stocks["oakwood"]["bread"] = 0;
            g.RefreshCityNeeds(); Check(!g.NeedsAt("oakwood").Any(n => n.Kind == "food"), "Completed need cooldown");
            var project = g.NeedsAt("oakwood").Single(n => n.Kind == "project");
            foreach (var p in project.Required) g.Wagons[0].Cargo[p.Key] = p.Value;
            int influence = g.InfluenceAt("oakwood"); int stock = g.Stocks["oakwood"]["tools"];
            g.SupplyCityNeed(project.Id, 1);
            Check(!project.Completed && g.InfluenceAt("oakwood") == influence && g.Stocks["oakwood"]["tools"] == stock, "Construction uses materials and waits for funding");
            int money = g.Gold; g.FundCityNeed(project.Id, 1);
            Check(project.Completed && g.Gold == money - project.Funding && g.InfluenceAt("oakwood") == influence + project.Reward, "Project completion and payment");
            Reject(() => g.FundCityNeed(project.Id, 1));
            // Ordinary contracts and branch ownership no longer mint flat influence.
            var fresh = new Game { Gold = 10000, CompanyFounded = true };
            fresh.OpenBranch("oakwood", 1, false); Check(fresh.InfluenceAt("oakwood") == 0, "Branch purchase alone grants no influence");
            var contract = fresh.Contracts[0]; fresh.Wagons[0].City = contract.City;
            fresh.AcceptContract(contract.Id); fresh.Wagons[0].Cargo[contract.Good] = contract.Amount;
            int oldStock = fresh.Stocks[contract.City][contract.Good];
            fresh.DeliverContract(contract.Id, 1);
            Check(fresh.InfluenceAt(contract.City) == 0 && fresh.Stocks[contract.City][contract.Good] == oldStock + contract.Amount, "Ordinary contract supplies market without unrelated reward");
            var trading = new Game(); trading.CityNeeds.Clear();
            trading.CityNeeds.Add(new CityNeed { Id = trading.NextCityNeedId++, City = "oakwood", Kind = "food", Title = "Помощь пекарям", Reason = "Test", Required = new() { ["bread"] = 3 }, Reward = 25, CreatedDay = 1 });
            trading.Wagons[0].Cargo["bread"] = 5; trading.Trade(1, "bread", 3, false);
            Check(trading.InfluenceAt("oakwood") == 25, "Useful market sale resolves a need");
            trading.Trade(1, "bread", 2, false); Check(trading.InfluenceAt("oakwood") == 25, "Extra sales cannot repeat the reward");
            g.Save(path); var loaded = Game.Load(path);
            Check(JsonSerializer.Serialize(g.CityNeeds) == JsonSerializer.Serialize(loaded.CityNeeds), "Progress and completion survive save");
            var batch = Game.Load(path); var daily = Game.Load(path);
            batch.Advance(21);
            Check(batch.CityNeeds.Count(n => n.City == "oakwood" && n.Kind == "project") == 1, "No early project renewal at 21 days");
            batch.Advance(21); for (int i = 0; i < 42; i++) daily.Advance(1);
            Check(JsonSerializer.Serialize(batch.CityNeeds) == JsonSerializer.Serialize(daily.CityNeeds), "Batch and daily task generation agree");
            Check(batch.CityNeeds.Count(n => n.City == "oakwood" && n.Kind == "project") == 2, "Project regenerates only after cooldown");
            var rebalance = JsonSerializer.SerializeToNode(g)!;
            var pending = rebalance["CityNeeds"]!.AsArray().First(n => n!["Completed"]!.GetValue<bool>() == false)!;
            pending["Reward"] = 35;
            System.IO.File.WriteAllText(path, rebalance.ToJsonString()); var rebalanced = Game.Load(path);
            Check(rebalanced.CityNeeds.Where(n => !n.Completed).All(n => n.Reward <= 12) && rebalanced.InfluenceAt("oakwood") == g.InfluenceAt("oakwood"), "Existing offers rebalanced without removing earned influence");
            var legacy = JsonSerializer.SerializeToNode(g)!;
            legacy.AsObject().Remove("CityNeeds"); legacy.AsObject().Remove("CityNeedsVersion"); legacy.AsObject().Remove("NextCityNeedId");
            System.IO.File.WriteAllText(path, legacy.ToJsonString()); loaded = Game.Load(path);
            Check(loaded.NeedsAt("northmine").Any() && loaded.InfluenceAt("oakwood") == g.InfluenceAt("oakwood"), "Legacy saves gain needs without losing influence");
            // Verify an actual city aid action is wired into the UI, with disabled remote actions.
            game = new Game(); city = "oakwood"; var uiNeed = game.NeedsAt(city).Single(n => n.Kind == "project");
            foreach (var item in uiNeed.Required) Wagon.Cargo[item.Key] = item.Value;
            Refresh(); OpenMenu(7);
            Check(menuTabs.CurrentTab == 7 && cityTitle.Text.Contains("Дубрава"), "City needs window");
            var button = cityNeedsPanel.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "Передать нужные товары из каравана");
            Check(!button.Disabled, "Local supply enabled"); button.EmitSignal(Button.SignalName.Pressed);
            Check(uiNeed.Delivered.Sum(p => p.Value) == uiNeed.Required.Sum(p => p.Value), "UI delivery wiring");
            CloseMenu(); SelectCity("riverport"); OpenMenu(7);
            Check(cityNeedsPanel.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.Text == "Передать нужные товары из каравана").All(b => b.Disabled), "Remote aid disabled");
            GD.Print("CITY_NEEDS_OK: specialization, real shortages, partial supplies, funding, one-time rewards, cooldowns, contracts, save migration, deterministic turns and UI actions");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        finally { System.IO.File.Delete(path); }
    }
}
