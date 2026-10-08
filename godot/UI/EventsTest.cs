using Godot;
using TradeCompany;
using System.Text.Json;

public partial class Main
{
    public void EventsTest()
    {
        string path = ProjectSettings.GlobalizePath("user://events-test.json");
        try
        {
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            var fixture = new Game();
            for (int i = 0; i < Game.MarketEvents.Length; i++)
            {
                var e = Game.MarketEvents[i];
                fixture.Stocks["oakwood"][e.Good] = 90;
                int buy = fixture.Price("oakwood", e.Good, true), sell = fixture.Price("oakwood", e.Good, false);
                string message = fixture.ApplyMarketEvent(i, "oakwood");
                int after = 90 + e.Change;
                Check(fixture.Stocks["oakwood"][e.Good] == after, "Actual event stock effect");
                Check(message.Contains(e.Title) && message.Contains("Дубрава") && message.Contains($"90 → {after}")
                    && message.Contains($"{buy} → {fixture.Price("oakwood", e.Good, true)}")
                    && message.Contains($"{sell} → {fixture.Price("oakwood", e.Good, false)}"), "Event must explain real prices and stock");
                Check(e.Change > 0 ? fixture.Price("oakwood", e.Good, true) <= buy : fixture.Price("oakwood", e.Good, false) >= sell, "Price direction");
                fixture.Stocks["oakwood"][e.Good] = 0;
                fixture.ApplyMarketEvent(i, "oakwood");
                Check(fixture.Stocks["oakwood"][e.Good] >= 0, "No negative stock");
            }
            fixture = new Game(); fixture.Save(path);
            var batch = Game.Load(path); var daily = Game.Load(path);
            batch.Advance(30);
            for (int i = 0; i < 30; i++) daily.Advance(1);
            Check(JsonSerializer.Serialize(batch.Stocks) == JsonSerializer.Serialize(daily.Stocks)
                && JsonSerializer.Serialize(batch.Contracts) == JsonSerializer.Serialize(daily.Contracts), "Batch and daily advance agree");
            fixture = new Game();
            string news = string.Join("\n", Enumerable.Range(0, 42).Select(_ => fixture.Advance(1)));
            Check(Game.MarketEvents.All(e => news.Contains(e.Title)), "All 14 events appear in a full cycle");
            Log("Первое событие\nВторое событие");
            Check(unreadNotifications == 2 && journal.GetParsedText().Contains("Первое событие")
                && journal.GetParsedText().Contains("Второе событие") && toastText.Text.Contains("Новых событий: 2"), "Separate event history and count");
            Check(journal.GetParsedText().IndexOf("Второе событие") < journal.GetParsedText().IndexOf("Первое событие"), "Newest event first");
            game.Day = 41;
            Log($"{GameCalendar.FormatDay(12)}: более новое\n{GameCalendar.FormatDay(10)}: граница месяца\n{GameCalendar.FormatDay(9)}: старше месяца");
            string history = journal.GetParsedText();
            Check(!history.Contains("старше месяца") && !history.Contains("Первое событие") && history.Contains("граница месяца"), "Calendar month retention including boundary");
            Check(history.IndexOf("более новое") < history.IndexOf("граница месяца"), "Batch events sorted by actual date");
            game.Day = 42; Refresh();
            Check(!journal.GetParsedText().Contains("граница месяца"), "Expiry without a new notification");
            GD.Print("EVENTS_OK: 14 effects, actual prices, stock limits, deterministic daily/batch simulation and history");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        finally { System.IO.File.Delete(path); }
    }
}
