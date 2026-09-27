namespace TradeCompany;

// A command builder shared by all mouse actions; Session retains validation and networking.
internal sealed class MouseCommand
{
    internal record Choice(string Label, string Value);
    private readonly List<string> arguments = new();
    public string Command { get; }
    public int Quantity { get; private set; } = 1;
    public int Page { get; set; }
    public MouseCommand(string command) => Command = command;
    private bool HasWagon => Command is "купить" or "продать" or "ехать" or "выгрузить" or "загрузить";
    public bool NeedsQuantity => Command is "купить" or "продать" or "выгрузить" or "загрузить" or "далее";
    public bool Ready => arguments.Count >= (Command == "сравнить" ? 2 : HasWagon ? 2 : Command is "рынок" or "разведка" or "повозка" or "склад" ? 1 : 0);
    public string Prompt => Ready ? (NeedsQuantity ? "Количество: " + Quantity : "Выполнить действие?") : HasWagon && arguments.Count == 0 ? "Выберите повозку" : Command == "сравнить" ? (arguments.Count == 0 ? "Выберите первый город" : "Выберите второй город") : Command == "ехать" || !HasWagon ? "Выберите город" : "Выберите товар";
    public string Preview => string.Join(" ", new[] { Command }.Concat(arguments)) + (Ready && NeedsQuantity ? " " + Quantity : "");
    public void Adjust(int delta) => Quantity = Math.Clamp(Quantity + delta, 1, Command == "далее" ? 30 : 300);
    public void Select(string value) { arguments.Add(value); Page = 0; }
    public IReadOnlyList<Choice> Choices(Session session)
    {
        if (Ready) return Array.Empty<Choice>();
        if (HasWagon && arguments.Count == 0)
            return session.Game.WagonsFor(session.IsCoop && !session.IsHost ? 1 : 0)
                .Select(w => new Choice($"#{w.Id} {Ru.Name(w.City)}" + (w.Destination is null ? "" : " (в пути)"), w.Id.ToString())).ToArray();
        return (Command == "ехать" || !HasWagon ? Game.Cities : Game.Goods)
            .Where(value => Command != "сравнить" || arguments.Count == 0 || Ru.Name(value) != arguments[0])
            .Select(value => new Choice(Ru.Name(value), Ru.Name(value))).ToArray();
    }
}
