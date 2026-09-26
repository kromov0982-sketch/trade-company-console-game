using System.Text;
using TradeCompany;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
if (args.Contains("--self-test")) { SelfTest.Run(); return; }
if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
{
    ConsoleWindow.Prepare();
    while (MainMenu.Choose() is { } choice)
    {
        Session? session = null;
        if (choice == MenuChoice.NewGame) session = Session.NewGame();
        else if (choice == MenuChoice.HostGame)
        {
            var link = CoopLobby.WaitForGuest();
            if (link is not null) session = Session.HostGame(link);
        }
        else if (choice == MenuChoice.JoinGame)
        {
            var link = CoopLobby.Connect();
            if (link is not null) session = Session.JoinGame(link);
        }
        else
        {
            try { session = Session.LoadGame(); }
            catch (Exception e) when (e is IOException or InvalidOperationException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                MainMenu.ShowError("Не удалось загрузить игру: " + e.Message);
                continue;
            }
        }
        if (session is not null)
        {
            using (session) Dashboard.Run(session);
        }
    }
    return;
}
// Plain input/output remains available for scripts and automated checks.
var scriptedSession = Session.NewGame();
while (Console.ReadLine() is { } line && !scriptedSession.Exit)
{
    scriptedSession.Execute(line);
    Console.WriteLine(scriptedSession.Message);
}
