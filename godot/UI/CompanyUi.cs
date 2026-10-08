using Godot;
using TradeCompany;

public partial class Main
{
    private readonly Dictionary<string, int> branchAmounts = new();
    private void RefreshCompany()
    {
        Clear(company);
        var stage = Text(game.CompanyFounded ? "Торговая грамота получена" : "Вы — вольный купец", 23);
        stage.AutowrapMode = TextServer.AutowrapMode.WordSmart; company.AddChild(stage);
        var goal = Text($"Цель: влияние над 30% городов · {game.ControlledCities} / {game.RequiredCities}\nГород под вашим влиянием при 100 очках и действующем филиале.", 16);
        goal.AutowrapMode = TextServer.AutowrapMode.WordSmart; company.AddChild(goal);
        if (!game.CompanyFounded)
        {
            var intro = Text("Торгуйте своим караваном и выполняйте контракты. Торговая грамота откроет филиалы, представителей и покупку предприятий.", 17);
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart; company.AddChild(intro);
            var charter = ActionButton("Основать компанию · 3 000 монет", () => Execute(game.FoundCompany));
            charter.Disabled = game.Gold < Game.CharterPrice; company.AddChild(charter);
            return;
        }
        // Selected city first, followed by every city where the player has a foothold.
        var cities = new[] { city }.Concat(Game.Cities.Where(c => c != city && (game.Branches.ContainsKey(c) || game.InfluenceAt(c) > 0)).OrderBy(Ru.Name));
        foreach (string target in cities)
        {
            var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Box("d4be94", 5)); company.AddChild(panel);
            var contents = new VBoxContainer(); contents.AddThemeConstantOverride("separation", 8); panel.AddChild(contents);
            var heading = Text($"{Ru.Name(target)} · влияние {game.InfluenceAt(target)}/100" + (game.IsControlled(target) ? " · под вашим влиянием" : ""), 21);
            heading.AutowrapMode = TextServer.AutowrapMode.WordSmart; contents.AddChild(heading);
            var progress = new ProgressBar { Value = game.InfluenceAt(target), MaxValue = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
            progress.AddThemeStyleboxOverride("background", Box("b8a17b", 3)); progress.AddThemeStyleboxOverride("fill", Box("61714d", 3)); contents.AddChild(progress);
            bool here = Wagon.Destination is null && Wagon.City == target;
            if (game.Branches.TryGetValue(target, out var branch))
            {
                if (branch.ReadyDay > game.Day) contents.AddChild(Text($"Строительство до {GameCalendar.FormatDay(branch.ReadyDay, true)}", 17));
                else contents.AddChild(Text(branch.Trading ? "Представитель ведёт торговлю" : "Представитель: торговля приостановлена", 17));
                contents.AddChild(IconTitle("coin", $"Баланс филиала: {branch.Budget:N0} · Прибыль за всё время: {branch.Profit:+0;-0;0}", 16));
                var report = Text(branch.Report, 15); report.AutowrapMode = TextServer.AutowrapMode.WordSmart; contents.AddChild(report);
                var actions = new HFlowContainer(); actions.AddThemeConstantOverride("h_separation", 8); contents.AddChild(actions);
                actions.AddChild(Text("Сумма", 16));
                var amount = new SpinBox { MinValue = 1, MaxValue = 1000000, Step = 1,
                    Value = branchAmounts.GetValueOrDefault(target, 500), UpdateOnTextChanged = true,
                    CustomMinimumSize = new Vector2(140, 38) };
                actions.AddChild(amount);
                var fund = ActionButton("Пополнить", () => Execute(() => game.FundBranch(target, (int)amount.Value)));
                actions.AddChild(fund);
                var withdraw = ActionButton("Вывести", () => Execute(() => game.WithdrawBranch(target, (int)amount.Value)));
                actions.AddChild(withdraw);
                void UpdateAmount()
                {
                    int value = (int)amount.Value; branchAmounts[target] = value;
                    fund.Disabled = value > game.Gold || value > 1000000 - branch.Budget;
                    withdraw.Disabled = value > branch.Budget;
                }
                amount.ValueChanged += _ => UpdateAmount(); UpdateAmount();
                actions.AddChild(ActionButton(branch.Trading ? "Приостановить торговлю" : "Разрешить торговлю", () => Execute(() => game.ToggleRepresentative(target))));
            }
            else
            {
                var actions = new HFlowContainer(); contents.AddChild(actions);
                var buyBranch = ActionButton("Купить филиал · 1 500 монет", () => Execute(() => game.OpenBranch(target, wagonId, false)));
                buyBranch.Disabled = !here || game.Gold < 1500; actions.AddChild(buyBranch);
                var buildBranch = ActionButton("Построить · 800 монет · 7 дней", () => Execute(() => game.OpenBranch(target, wagonId, true)));
                buildBranch.Disabled = !here || game.Gold < 800 || Wagon.Cargo["lumber"] < 10 || Wagon.Cargo["tools"] < 5; actions.AddChild(buildBranch);
                contents.AddChild(IconTitle("lumber", "Для строительства: 10 досок", 15));
                contents.AddChild(IconTitle("tools", "и 5 инструментов из каравана", 15));
            }
            var needs = Text($"Городские потребности: {game.NeedsAt(target).Count()} · влияние за решение проблем", 16);
            needs.AutowrapMode = TextServer.AutowrapMode.WordSmart; contents.AddChild(needs);
            contents.AddChild(ActionButton("Потребности города", () => { CloseMenu(); SelectCity(target); OpenMenu(7); }));
            if (!here) contents.AddChild(ActionButton("Показать город на карте", () => ShowContractCity(target)));
        }
    }
}
