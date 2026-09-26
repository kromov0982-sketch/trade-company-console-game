namespace TradeCompany;

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Saves => Path.Combine(Root, "saves");
    public static string Logs => Path.Combine(Root, "logs");
    public static string SaveFile => Path.Combine(Saves, "company.json");
    public static string LogFile => Path.Combine(Logs, "trade-company.log");

    public static void Initialize()
    {
        Directory.CreateDirectory(Saves);
        Directory.CreateDirectory(Logs);
    }
}
