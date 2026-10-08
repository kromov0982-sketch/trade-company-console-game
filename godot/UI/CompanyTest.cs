using Godot;
using TradeCompany;
using System.Text.Json;

public partial class Main
{
    public void CompanyTest()
    {
        try
        {
            static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Operation should be rejected"); }
            var travel = new Game { Gold = 0 };
            var caravan = travel.Wagons[0];
            int initial = caravan.Provisions;
            decimal duration = travel.World.TravelDays(caravan.City, "riverport");
            travel.Travel(1, "riverport");
            Check(travel.Gold == 0 && caravan.Provisions == initial - Game.ProvisionCost(duration), "Travel consumes food without money");
            while (caravan.Destination is not null) travel.Advance(1);
            caravan.Provisions = 0;
            Reject(() => travel.Travel(1, "oakwood"));
            Check(caravan.Destination is null && caravan.Provisions == 0, "Insufficient food does not depart");
            caravan.Cargo["bread"] = 3; travel.PackProvisions(1, 2);
            Check(caravan.Provisions == 10 && caravan.Cargo["bread"] == 1, "Cargo bread becomes provisions");
            travel.Gold = 10000; int foodStock = travel.Stocks[caravan.City]["bread"];
            travel.BuyProvisions(1, 2);
            Check(caravan.Provisions == 20 && travel.Stocks[caravan.City]["bread"] == foodStock - 2 && travel.Gold < 10000, "Market provision purchase");
            var g = new Game();
            Check(!g.CompanyFounded && g.Wagons.Count == 1 && g.ControlledCities == 0 && g.RequiredCities == 8, "Personal start and 30% threshold");
            Reject(() => g.FoundCompany()); Reject(() => g.BuyWagon("oakwood")); Reject(() => g.BuyEnterprise(g.Enterprises[0].Id));
            g.Gold = 100000; g.FoundCompany(); Check(g.Gold == 97000, "Charter payment");
            Reject(() => g.OpenBranch("riverport", 1, false));
            Reject(() => g.OpenBranch("oakwood", 1, true));
            g.Wagons[0].Cargo["lumber"] = 10; g.Wagons[0].Cargo["tools"] = 5;
            g.OpenBranch("oakwood", 1, true);
            Check(g.Branches["oakwood"].ReadyDay == 8 && g.Wagons[0].Used == 0 && g.InfluenceAt("oakwood") == 0, "Construction cost and resources");
            Reject(() => g.OpenBranch("oakwood", 1, false));
            g.FundBranch("oakwood", 500); g.Advance(6);
            Check(g.Branches["oakwood"].Good is null, "No trading during construction");
            g.Advance(1); Check(g.Branches["oakwood"].ReadyDay <= g.Day, "Building completion");
            var cityProject = g.NeedsAt("oakwood").Single(n => n.Kind == "project");
            g.FundCityNeed(cityProject.Id, 1); Reject(() => g.FundCityNeed(cityProject.Id, 1));
            foreach (var item in cityProject.Required) g.Wagons[0].Cargo[item.Key] = item.Value;
            g.SupplyCityNeed(cityProject.Id, 1); Check(g.Wagons[0].Used == 0 && g.InfluenceAt("oakwood") == cityProject.Reward, "City resource delivery");
            var contract = g.Contracts[0]; contract.City = "oakwood"; contract.DeadlineDay = 100;
            g.CityNeeds.Add(new CityNeed { Id = g.NextCityNeedId++, City = "oakwood", Kind = "craft", Title = "Test contract aid", Reason = "Test",
                Required = new() { [contract.Good] = contract.Amount }, Reward = 20, CreatedDay = g.Day });
            g.AcceptContract(contract.Id); g.Wagons[0].Cargo[contract.Good] = contract.Amount;
            int influence = g.InfluenceAt("oakwood"); g.DeliverContract(contract.Id, 1);
            Check(g.InfluenceAt("oakwood") == influence + 20, "Contract influence reward");
            // Force a profitable real-market opportunity and check payment, shipment delay and settlement.
            var ai = new Game { Gold = 10000, CompanyFounded = true };
            foreach (var stock in ai.Stocks.Values) foreach (string good in Game.Goods) stock[good] = 100;
            foreach (string good in Game.Goods) { ai.Stocks["riverport"][good] = 500; ai.Stocks["oakwood"][good] = 0; }
            ai.OpenBranch("oakwood", 1, false); ai.FundBranch("oakwood", 1000);
            var b = ai.Branches["oakwood"]; int before = ai.Gold; ai.Advance(1);
            Check(b.Good is not null && b.Amount == 5 && b.Budget < 1000 && b.ArrivalDay > ai.Day && ai.Gold == before, "AI buys with separate budget and waits for delivery");
            ai.ToggleRepresentative("oakwood"); int deadline = b.ArrivalDay;
            while (ai.Day < deadline) ai.Advance(1);
            Check(b.Good is null && b.Budget > 0 && b.Report.Contains("Продано"), "Pending delivery settles while paused");
            int treasury = ai.Gold, budget = b.Budget;
            ai.WithdrawBranch("oakwood", 1);
            Check(ai.Gold == treasury + 1 && b.Budget == budget - 1, "Partial withdrawal");
            Reject(() => ai.WithdrawBranch("oakwood", budget));
            Reject(() => ai.WithdrawBranch("oakwood", 0));
            ai.WithdrawBranch("oakwood");
            Check(b.Budget == 0 && ai.Gold == treasury + budget, "Withdraw without minting money");
            for (int i = 0; i < 8; i++)
            {
                string city = Game.Cities[i]; g.Branches[city] = new Branch { ReadyDay = g.Day };
                g.Influence[city] = new CityInfluence { Points = 100 };
            }
            Check(g.ControlledCities == 8, "Controlled city threshold");
            string lastCity = Game.Cities[7]; g.Influence[lastCity].Points = 90;
            g.Wagons[0].City = lastCity;
            var finalNeed = g.NeedsAt(lastCity).Single(n => n.Kind == "project");
            g.FundCityNeed(finalNeed.Id, 1);
            foreach (var item in finalNeed.Required) g.Wagons[0].Cargo[item.Key] = item.Value;
            g.SupplyCityNeed(finalNeed.Id, 1);
            Check(g.Won && g.ControlledCities == 8, "Victory immediately after eighth city");
            string path = ProjectSettings.GlobalizePath("user://company-test.json");
            g.Save(path); var loaded = Game.Load(path);
            Check(loaded.Won && loaded.CompanyFounded && loaded.ControlledCities == 8 && loaded.Branches.Count == 8, "Company save and victory");
            // Old save keeps assets and unlocks the company without preserving the obsolete capital victory.
            var legacy = JsonSerializer.SerializeToNode(g)!; legacy.AsObject().Remove("ProgressionVersion"); legacy.AsObject().Remove("Branches"); legacy.AsObject().Remove("Influence");
            legacy["CompanyFounded"] = false; legacy["Won"] = true; legacy["Enterprises"]![0]!["Owner"] = 0;
            System.IO.File.WriteAllText(path, legacy.ToJsonString()); loaded = Game.Load(path); System.IO.File.Delete(path);
            Check(loaded.CompanyFounded && !loaded.Won && loaded.ControlledCities == 0, "Legacy migration");
            OpenMenu(5); Check(menuTabs.CurrentTab == 5 && cityTitle.Text == "Компания и влияние", "Company window"); CloseMenu();
            GD.Print("COMPANY_OK: personal start, charter, construction, resources, cooldowns, contract influence, AI budget and delayed delivery, withdrawal, 8/24 victory, saves and legacy migration");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
