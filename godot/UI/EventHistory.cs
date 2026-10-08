using System.Globalization;
using TradeCompany;

public partial class Main
{
    private sealed record HistoryEntry(DateOnly Date, string Text, long Order)
    {
        public bool Read { get; set; }
    }
    private readonly List<HistoryEntry> eventHistory = new();
    private long eventOrder;
    private void AddHistory(string entry)
    {
        DateOnly date = game.CurrentDate;
        int end = entry.IndexOf("года:", StringComparison.Ordinal);
        bool dated = end >= 0 && DateOnly.TryParseExact(entry[..(end + 4)], "d MMMM yyyy 'года'",
            CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date);
        if (!dated) date = game.CurrentDate;
        eventHistory.Add(new HistoryEntry(date, dated ? entry : $"{GameCalendar.FormatDay(game.Day)} · {entry}", ++eventOrder)
            { Read = menuOverlay.Visible && menuTabs.CurrentTab == 4 });
    }
    private void RefreshHistory()
    {
        DateOnly cutoff = game.CurrentDate.AddMonths(-1);
        eventHistory.RemoveAll(e => e.Date < cutoff);
        journal.Clear();
        foreach (var entry in eventHistory.OrderByDescending(e => e.Date).ThenByDescending(e => e.Order))
            journal.AddText(entry.Text + "\n\n");
        unreadNotifications = eventHistory.Count(e => !e.Read);
        UpdateNotificationsButton();
    }
    private void MarkHistoryRead()
    {
        foreach (var entry in eventHistory) entry.Read = true;
        RefreshHistory();
    }
}
