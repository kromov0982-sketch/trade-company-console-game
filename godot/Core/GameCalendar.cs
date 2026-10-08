using System.Globalization;

namespace TradeCompany;

public static class GameCalendar
{
    public static readonly DateOnly StartDate = new(1715, 5, 1);
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    // Day remains the simulation/save counter. Calendar dates share one fixed epoch,
    // so existing saves and generated deadlines keep exactly the same duration.
    public static DateOnly DateForDay(int day) => StartDate.AddDays(day - 1);
    public static string FormatDay(int day, bool compact = false) =>
        DateForDay(day).ToString(compact ? "dd.MM.yyyy" : "d MMMM yyyy 'года'", Russian);
}
