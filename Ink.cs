namespace TradeCompany;

// Restore the caller's colors after every fragment; redirected output stays plain text.
public static class Ink
{
    public const ConsoleColor Command = ConsoleColor.Cyan;
    public const ConsoleColor Parameter = ConsoleColor.Yellow;
    public const ConsoleColor Heading = ConsoleColor.White;
    public const ConsoleColor Muted = ConsoleColor.DarkGray;
    public const ConsoleColor Success = ConsoleColor.Green;
    public const ConsoleColor Error = ConsoleColor.Red;
    public const ConsoleColor Text = ConsoleColor.Gray;

    public static void Write(string text, ConsoleColor color = Text)
    {
        if (Console.IsOutputRedirected) { Console.Write(text); return; }
        var previous = Console.ForegroundColor;
        try { Console.ForegroundColor = color; Console.Write(text); }
        finally { Console.ForegroundColor = previous; }
    }
    public static void Line(string text = "", ConsoleColor color = Text)
    {
        Write(text, color);
        Console.WriteLine();
    }
    public static void Section(string title)
    {
        Console.WriteLine();
        Line("  " + title, Heading);
        Line("  " + new string('-', title.Length), Muted);
    }
    public static void Example(string command, string parameters)
    {
        Write("    > ", Muted);
        Write(command, Command);
        Line(" " + parameters, Parameter);
    }
}
