using Godot;

// Fonts are bundled with the project, independent of fonts installed on the computer.
public static class GameFonts
{
    public static Font Body { get; } = GD.Load<FontFile>("res://Art/Fonts/Alegreya.ttf");
    public static Font Controls { get; } = GD.Load<FontFile>("res://Art/Fonts/AlegreyaSans.ttf");
    public static Font Heading { get; } = Weight("res://Art/Fonts/CormorantGaramond.ttf", 600);
    public static Font Map { get; } = Weight("res://Art/Fonts/CormorantGaramond-Italic.ttf", 600);
    private static Font Weight(string path, int weight) => new FontVariation
    {
        BaseFont = GD.Load<FontFile>(path),
        VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = weight }
    };
}
