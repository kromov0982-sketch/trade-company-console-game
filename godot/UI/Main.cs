using Godot;
using TradeCompany;

public partial class Main : Control
{
    private Game game = new();
    private string city = "oakwood", good = "wood";
    private int wagonId = 1;
    private Atlas atlas = null!;
    private Label summary = null!, cityTitle = null!, marketStatus = null!, wagonStatus = null!, routeStatus = null!, quote = null!;
    private OptionButton wagonChoice = null!;
    private Control tradePanel = null!;
    private Button scout = null!, marketButton = null!;
    private Label tradeGood = null!;
    private TextureRect tradeIcon = null!;
    private SpinBox quantity = null!;
    private Tree market = null!;
    private VBoxContainer enterprises = null!, contracts = null!, company = null!;
    private RichTextLabel journal = null!;
    private Button buy = null!, sell = null!, unload = null!, load = null!;
    private readonly Dictionary<string, Texture2D> goodIcons = new();
    private bool refreshing;
    private MarginContainer mainMargin = null!;
    private VBoxContainer mainLayout = null!;
    private PanelContainer menuFrame = null!;
    private Label gameTitle = null!;
    private Button fullscreenButton = null!;
    private Control menuOverlay = null!;
    private TabContainer menuTabs = null!;
    private PanelContainer toast = null!;
    private Label toastText = null!;
    private Button notificationsButton = null!;
    private Label notificationBadge = null!;
    private Tween? toastTween;
    private int unreadNotifications;
    private ScrollContainer sidebar = null!;
    private Control systemOverlay = null!;
    private PanelContainer systemFrame = null!;
    private Button menuButton = null!;
    private PopupMenu cityContext = null!;
    private string contextCity = "oakwood";
    private Button nextDay = null!, nextWeek = null!, arrival = null!;
    private Label nextDayDate = null!, provisionsStatus = null!;
    private ConfirmationDialog travelConfirmation = null!;
    private string SavePath => ProjectSettings.GlobalizePath("user://company.json");
    private Wagon Wagon => game.Wagons.First(w => w.Id == wagonId);

    public override void _Ready()
    {
        if (DisplayServer.GetName() != "headless" && OS.GetCmdlineUserArgs().Length == 0 && !OS.GetCmdlineArgs().Contains("--resolution") && GetWindow().Mode == Window.ModeEnum.Windowed)
        {
            var usable = DisplayServer.ScreenGetUsableRect(GetWindow().CurrentScreen);
            var size = new Vector2I(Math.Clamp(usable.Size.X * 9 / 10, 800, 1440), Math.Clamp(usable.Size.Y * 9 / 10, 600, 900));
            GetWindow().Size = size;
            GetWindow().Position = usable.Position + (usable.Size - size) / 2;
        }
        BuildUi();
        Refresh();
        GetWindow().MinSize = new Vector2I(800, 600);
        GetViewport().SizeChanged += ApplyResponsiveLayout;
        ApplyResponsiveLayout();
        CenterInitialMap();
        if (OS.GetCmdlineUserArgs().Contains("--capture-menu")) OpenMenu(OS.GetCmdlineUserArgs().Contains("--contracts") ? 2 : 0);
        if (OS.GetCmdlineUserArgs().Contains("--capture") || OS.GetCmdlineUserArgs().Contains("--capture-menu")) CapturePreview();
        if (OS.GetCmdlineUserArgs().Contains("--road-graph-test")) CallDeferred(nameof(RoadGraphTest));
        if (OS.GetCmdlineUserArgs().Contains("--terrain-test")) CallDeferred(nameof(TerrainTest));
        if (OS.GetCmdlineUserArgs().Contains("--events-test")) CallDeferred(nameof(EventsTest));
        if (OS.GetCmdlineUserArgs().Contains("--city-needs-test")) CallDeferred(nameof(CityNeedsTest));
        if (OS.GetCmdlineUserArgs().Contains("--company-test")) CallDeferred(nameof(CompanyTest));
        if (OS.GetCmdlineUserArgs().Contains("--calendar-test")) CallDeferred(nameof(CalendarTest));
        if (OS.GetCmdlineUserArgs().Contains("--layout-test")) RunLayoutTest();
        if (OS.GetCmdlineUserArgs().Contains("--map-test")) RunMapTest();
        if (OS.GetCmdlineUserArgs().Contains("--notifications-test")) RunNotificationsTest();
        if (OS.GetCmdlineUserArgs().Contains("--map-stability-test")) RunMapStabilityTest();
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) CallDeferred(nameof(SmokeTest));
    }
    private async void CenterInitialMap()
    {
        for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        atlas.ResetView();
    }
    private async void CapturePreview()
    {
        for (int frame = 0; frame < 10; frame++)
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (OS.GetCmdlineUserArgs().Contains("--needs"))
        {
            OpenMenu(7);
            for (int frame = 0; frame < 3; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        if (OS.GetCmdlineUserArgs().Contains("--company"))
        {
            game.CompanyFounded = true;
            game.Branches[city] = new Branch { ReadyDay = game.Day, Budget = 500 };
            OpenMenu(5);
            for (int frame = 0; frame < 3; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        if (OS.GetCmdlineUserArgs().Contains("--toast"))
        {
            Log("Повозка №1 прибыла в Дубраву. Теперь можно торговать на рынке.");
            for (int frame = 0; frame < 3; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        if (OS.GetCmdlineUserArgs().Contains("--system-menu"))
        {
            ToggleSystemMenu();
            for (int frame = 0; frame < 3; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        if (OS.GetCmdlineUserArgs().Contains("--overview"))
        {
            atlas.ShowWholeMap();
            for (int frame = 0; frame < 3; frame++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--capture-menu") ? "res://preview-menu.png" : "res://preview.png"));
        GetTree().Quit();
    }
    private static Label Text(string text, int size = 16)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeFontOverride("font", size >= 20 ? GameFonts.Heading : GameFonts.Body);
        return label;
    }
    private HBoxContainer IconTitle(string icon, string caption, int size = 21, bool wrap = true)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new TextureRect { Texture = goodIcons[icon], CustomMinimumSize = new Vector2(size >= 20 ? 30 : 22, size >= 20 ? 30 : 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var label = Text(caption, size); label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        if (icon == "coin") label.AddThemeFontOverride("font", GameFonts.Controls);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(label); return row;
    }
    private void ShowContractCity(string target)
    {
        CloseMenu(); SelectCity(target); atlas.FocusCity(target);
        decimal days = Wagon.Destination is null ? game.World.TravelDays(Wagon.City, target)
            : Wagon.DaysLeft + game.World.TravelDays(Wagon.Destination, target);
        string via = Wagon.Destination is null ? "" : $" · через {Ru.Name(Wagon.Destination)}";
        routeStatus.Text = days == 0 ? $"{Ru.Name(target)} · повозка уже в городе"
            : $"{Ru.Name(target)} · {Game.FormatDays(days)} · {Game.DistanceKm(days)} км{via}";
    }
    private static Button ActionButton(string text, Action action)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 38) };
        b.Pressed += action;
        return b;
    }
    private static StyleBoxFlat Box(string color, int radius = 2)
    {
        return new StyleBoxFlat { BgColor = new Color(color), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ContentMarginLeft = 12,
            ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
            BorderColor = new Color("8f7550"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 };
    }
    private void BuildUi()
    {
        goodIcons["coin"] = GD.Load<Texture2D>("res://Art/Icons/coin.svg");
        foreach (string key in Game.Goods) goodIcons[key] = GD.Load<Texture2D>($"res://Art/Icons/Goods/{key}.svg");
        var background = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        background.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/parchment.gdshader") };
        AddChild(background);
        var theme = new Theme { DefaultFontSize = 17, DefaultFont = GameFonts.Body };
        foreach (string type in new[] { "Button", "OptionButton", "Tree", "LineEdit", "PopupMenu", "SpinBox" })
            theme.SetFont("font", type, GameFonts.Controls);
        theme.SetFont("title_button_font", "Tree", GameFonts.Controls);
        var ink = new Color("382b20");
        foreach (string type in new[] { "Label", "Button", "OptionButton", "Tree", "LineEdit", "PopupMenu", "TabContainer", "RichTextLabel", "LinkButton" })
        {
            theme.SetColor("font_color", type, ink);
            theme.SetColor("font_hover_color", type, ink);
            theme.SetColor("font_pressed_color", type, ink);
            theme.SetColor("font_focus_color", type, ink);
            theme.SetColor("font_selected_color", type, new Color("fff0cc"));
            theme.SetColor("font_disabled_color", type, new Color("91846d"));
            theme.SetColor("font_outline_color", type, new Color(0, 0, 0, 0));
        }
        theme.SetColor("default_color", "RichTextLabel", ink);
        foreach (string type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Box("d8c49c"));
            theme.SetStylebox("hover", type, Box("ecdbb1"));
            theme.SetStylebox("pressed", type, Box("ad9063"));
            theme.SetStylebox("disabled", type, Box("d3c6a9"));
            var focus = Box("ffffff00"); focus.BorderColor = new Color("8e3e2c");
            theme.SetStylebox("focus", type, focus);
        }
        theme.SetStylebox("panel", "Tree", Box("e6d7b4"));
        theme.SetStylebox("selected", "Tree", Box("c8ae7f", 0));
        theme.SetStylebox("selected_focus", "Tree", Box("c8ae7f", 0));
        theme.SetColor("font_selected_color", "Tree", ink);
        theme.SetStylebox("title_button_normal", "Tree", Box("c8b187", 0));
        theme.SetStylebox("title_button_hover", "Tree", Box("ddc89f", 0));
        theme.SetColor("title_button_color", "Tree", ink);
        theme.SetColor("guide_color", "Tree", new Color("b9a581"));
        theme.SetStylebox("normal", "LineEdit", Box("eee1c2"));
        theme.SetColor("caret_color", "LineEdit", ink);
        theme.SetStylebox("panel", "PopupMenu", Box("e5d5b0"));
        theme.SetStylebox("hover", "PopupMenu", Box("bba078"));
        theme.SetStylebox("panel", "TabContainer", Box("ddcca5"));
        theme.SetStylebox("tab_selected", "TabContainer", Box("ddcca5"));
        theme.SetStylebox("tab_unselected", "TabContainer", Box("bda57c"));
        theme.SetStylebox("tab_hovered", "TabContainer", Box("ead7af"));
        theme.SetColor("font_selected_color", "TabContainer", ink);
        theme.SetColor("font_unselected_color", "TabContainer", new Color("59452f"));
        theme.SetColor("font_hovered_color", "TabContainer", ink);
        theme.SetStylebox("panel", "AcceptDialog", Box("e5d5b0"));
        Theme = theme;
        BuildSystemMenu();
        var margin = mainMargin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 22);
        AddChild(margin);
        var layout = mainLayout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 14); margin.AddChild(layout);
        var topBar = new HBoxContainer(); layout.AddChild(topBar);
        summary = Text("", 19); summary.AddThemeFontOverride("font", GameFonts.Controls); summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        summary.SizeFlagsHorizontal = SizeFlags.ExpandFill; topBar.AddChild(summary);
        notificationsButton = ActionButton("", () => OpenMenu(4));
        notificationsButton.Icon = GD.Load<Texture2D>("res://Art/Icons/notification.svg");
        notificationsButton.AddThemeConstantOverride("icon_max_width", 26); notificationsButton.ExpandIcon = true;
        notificationsButton.IconAlignment = HorizontalAlignment.Center;
        notificationsButton.CustomMinimumSize = new Vector2(48, 40);
        notificationsButton.TooltipText = "События"; topBar.AddChild(notificationsButton);
        notificationBadge = Text("", 12); notificationBadge.MouseFilter = MouseFilterEnum.Ignore;
        notificationBadge.HorizontalAlignment = HorizontalAlignment.Center;
        notificationBadge.VerticalAlignment = VerticalAlignment.Center;
        notificationBadge.AddThemeColorOverride("font_color", Colors.White);
        var badgeStyle = new StyleBoxFlat { BgColor = new Color("943f2c"), CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9, ContentMarginLeft = 3, ContentMarginRight = 3 };
        notificationBadge.AddThemeStyleboxOverride("normal", badgeStyle);
        notificationBadge.AnchorLeft = notificationBadge.AnchorRight = 1;
        notificationBadge.OffsetLeft = -18; notificationBadge.OffsetRight = 5;
        notificationBadge.OffsetTop = -3; notificationBadge.OffsetBottom = 16;
        notificationBadge.Visible = false; notificationsButton.AddChild(notificationBadge);
        topBar.AddChild(new Control { CustomMinimumSize = new Vector2(64, 0) });
        menuButton = ActionButton("Меню", ToggleSystemMenu); topBar.AddChild(menuButton);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); layout.AddChild(body);
        var mapColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mapColumn.AddThemeConstantOverride("separation", 8); body.AddChild(mapColumn);
        atlas = new Atlas { CustomMinimumSize = new Vector2(0, 180), SizeFlagsVertical = SizeFlags.ExpandFill };
        atlas.CitySelected = SelectCity;
        atlas.CityContextRequested = OpenCityContext; mapColumn.AddChild(atlas);
        travelConfirmation = new ConfirmationDialog { Title = "Отправление каравана", OkButtonText = "Отправиться", CancelButtonText = "Отмена" };
        AddChild(travelConfirmation);
        travelConfirmation.Confirmed += () => Execute(() => game.Travel(wagonId, pendingTravelCity));
        cityContext = new PopupMenu(); AddChild(cityContext);
        cityContext.IdPressed += CityContextAction;
        var rightColumn = new VBoxContainer(); rightColumn.AddThemeConstantOverride("separation", 12); body.AddChild(rightColumn);
        sidebar = new ScrollContainer { CustomMinimumSize = new Vector2(230, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        rightColumn.AddChild(sidebar);
        var toolbar = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toolbar.AddThemeConstantOverride("separation", 8); sidebar.AddChild(toolbar);
        gameTitle = Text("Торговая компания", 24); gameTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart; toolbar.AddChild(gameTitle);
        string[] menuNames = { "Рынок", "Предприятия", "Контракты", "Склад" };
        for (int i = 0; i < menuNames.Length; i++)
        {
            int tab = i;
            var navigation = ActionButton(menuNames[i], () =>
            {
                if (tab == 0 || tab == 1) { if (menuOverlay.Visible) return; SelectCity(Wagon.City); }
                OpenMenu(tab);
            });
            if (tab == 0) marketButton = navigation;
            toolbar.AddChild(navigation);
        }
        toolbar.AddChild(ActionButton("Компания и влияние", () => OpenMenu(5)));
        toolbar.AddChild(ActionButton("Потребности городов", () => OpenMenu(7)));
        var selection = new VBoxContainer(); selection.AddThemeConstantOverride("separation", 8); rightColumn.AddChild(selection);
        selection.AddChild(new HSeparator());
        selection.AddChild(Text("Повозка"));
        wagonChoice = new OptionButton { CustomMinimumSize = new Vector2(0, 38), FitToLongestItem = false };
        wagonChoice.ItemSelected += index => { if (refreshing) return; wagonId = game.Wagons[(int)index].Id; Refresh(); };
        selection.AddChild(wagonChoice);
        wagonStatus = Text("", 15); wagonStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        wagonStatus.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        wagonStatus.MaxLinesVisible = 2;
        wagonStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        wagonStatus.CustomMinimumSize = new Vector2(0, 42); selection.AddChild(wagonStatus);
        provisionsStatus = Text("", 15); selection.AddChild(provisionsStatus);
        var turnPanel = new PanelContainer();
        var turnStyle = Box("cdb68b", 6);
        turnStyle.BorderColor = new Color("9a7b4e");
        turnPanel.AddThemeStyleboxOverride("panel", turnStyle);
        rightColumn.AddChild(turnPanel);
        var turns = new VBoxContainer(); turns.AddThemeConstantOverride("separation", 6); turnPanel.AddChild(turns);
        var turnTitle = Text("Течение времени", 15);
        turnTitle.HorizontalAlignment = HorizontalAlignment.Center; turns.AddChild(turnTitle);
        nextDay = ActionButton("Завершить день", () => Execute(() => game.Advance(1)));
        nextDay.CustomMinimumSize = new Vector2(0, 48);
        nextDay.AddThemeFontSizeOverride("font_size", 18);
        foreach (var state in new[] { ("normal", "685039"), ("hover", "7d6041"), ("pressed", "493827") })
        {
            var style = Box(state.Item2, 4); style.BorderColor = new Color("a48654");
            nextDay.AddThemeStyleboxOverride(state.Item1, style);
        }
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            nextDay.AddThemeColorOverride(state, new Color("fff0cc"));
        nextDay.TooltipText = "Продвинуть время на один день: повозки продолжат путь, рынки и предприятия обновятся.";
        turns.AddChild(nextDay);
        nextDayDate = Text("", 14); nextDayDate.AddThemeFontOverride("font", GameFonts.Controls); nextDayDate.HorizontalAlignment = HorizontalAlignment.Center;
        turns.AddChild(nextDayDate);
        turns.AddChild(new HSeparator());
        nextWeek = ActionButton("Пропустить 7 дней", () => Execute(() => game.Advance(7)));
        nextWeek.AddThemeFontSizeOverride("font_size", 15);
        nextWeek.TooltipText = "Продвинуть время на неделю. Сроки контрактов продолжают идти.";
        turns.AddChild(nextWeek);
        arrival = ActionButton("До прибытия", AdvanceToArrival);
        arrival.AddThemeFontSizeOverride("font_size", 15); turns.AddChild(arrival);
        routeStatus = Text("", 16);
        routeStatus.MouseFilter = MouseFilterEnum.Ignore;
        routeStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        routeStatus.MaxLinesVisible = 2;
        routeStatus.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        atlas.AddChild(routeStatus);
        routeStatus.AnchorLeft = 0; routeStatus.AnchorRight = 1;
        routeStatus.AnchorTop = routeStatus.AnchorBottom = 1;
        routeStatus.OffsetLeft = 22; routeStatus.OffsetRight = -22;
        routeStatus.OffsetTop = -60; routeStatus.OffsetBottom = -18;
        routeStatus.VerticalAlignment = VerticalAlignment.Bottom;
        // Menus are embedded in the game and leave the atlas visible behind them.
        menuOverlay = new Control { Visible = false };
        menuOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(menuOverlay);
        var veil = new ColorRect { Color = new Color(0.12f, 0.09f, 0.05f, 0.48f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); menuOverlay.AddChild(veil);
        veil.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseMenu(); };
        var frame = menuFrame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, Size = new Vector2(840, 760) };
        frame.MinimumSizeChanged += () => Callable.From(ApplyResponsiveLayout).CallDeferred();
        menuOverlay.AddChild(frame);
        var frameStyle = Box("decca4", 2); frameStyle.BorderWidthLeft = frameStyle.BorderWidthRight = frameStyle.BorderWidthTop = frameStyle.BorderWidthBottom = 3;
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        var contents = new VBoxContainer(); contents.AddThemeConstantOverride("separation", 12); frame.AddChild(contents);
        var windowHeader = new HBoxContainer(); contents.AddChild(windowHeader);
        cityTitle = Text("", 26); cityTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart; cityTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; windowHeader.AddChild(cityTitle);
        windowHeader.AddChild(ActionButton("Закрыть ×", CloseMenu));
        menuTabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, UseHiddenTabsForMinSize = false, TabsVisible = false }; contents.AddChild(menuTabs);
        menuTabs.TabChanged += tab =>
        {
            if (tab == 4 && menuOverlay.Visible) { MarkHistoryRead(); HideToast(); }
        };
        var tabs = menuTabs;
        var trading = new VBoxContainer { Name = "Рынок" }; tabs.AddChild(trading);
        marketStatus = Text("", 14); marketStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart; trading.AddChild(marketStatus);
        market = new Tree { Columns = 5, HideRoot = true, ColumnTitlesVisible = true, SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 120), SelectMode = Tree.SelectModeEnum.Row };
        string[] headings = { "Товар", "На рынке", "Покупка", "Продажа", "В повозке" };
        for (int i = 0; i < headings.Length; i++) { market.SetColumnTitle(i, headings[i]); market.SetColumnCustomMinimumWidth(i, i == 0 ? 175 : 80); }
        market.ItemSelected += () => { if (!refreshing && market.GetSelected() is { } item) { good = item.GetMetadata(0).AsString(); UpdateTrade(); } };
        market.AddThemeConstantOverride("v_separation", 8);
        market.AddThemeConstantOverride("h_separation", 10);
        trading.AddThemeConstantOverride("separation", 10);
        trading.AddChild(market);
        scout = ActionButton("Разведать рынок · 40 монет", () => Execute(() => game.BuyMarketInfo(city)));
        trading.AddChild(scout);
        var dealPanel = new PanelContainer(); tradePanel = dealPanel; dealPanel.AddThemeStyleboxOverride("panel", Box("d4be94", 5)); trading.AddChild(dealPanel);
        var deal = new VBoxContainer(); deal.AddThemeConstantOverride("separation", 8); dealPanel.AddChild(deal);
        var selectedRow = new HBoxContainer(); selectedRow.AddThemeConstantOverride("separation", 10); deal.AddChild(selectedRow);
        tradeIcon = new TextureRect { CustomMinimumSize = new Vector2(28, 28), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered }; selectedRow.AddChild(tradeIcon);
        tradeGood = Text("", 21); tradeGood.SizeFlagsHorizontal = SizeFlags.ExpandFill; selectedRow.AddChild(tradeGood);
        var amount = new HFlowContainer(); amount.AddThemeConstantOverride("h_separation", 10); deal.AddChild(amount); amount.AddChild(Text("Количество"));
        quantity = new SpinBox { MinValue = 1, MaxValue = 60, Step = 1, Value = 10, CustomMinimumSize = new Vector2(110, 0) };
        quantity.ValueChanged += _ => UpdateTrade(); amount.AddChild(quantity);
        buy = ActionButton("Купить", () => Execute(() => game.Trade(wagonId, good, (int)quantity.Value, true))); amount.AddChild(buy);
        sell = ActionButton("Продать", () => Execute(() => game.Trade(wagonId, good, (int)quantity.Value, false))); amount.AddChild(sell);
        quote = Text("", 14); quote.AutowrapMode = TextServer.AutowrapMode.WordSmart; deal.AddChild(quote);
        BuildProvisionUi(deal);
        enterprises = ScrollTab(tabs, "Предприятия");
        contracts = ScrollTab(tabs, "Контракты");
        var storage = new VBoxContainer { Name = "Склад" }; tabs.AddChild(storage);
        storage.AddChild(Text("Склад вмещает 300 единиц.\nКоличество и товар выбираются во вкладке «Рынок»."));
        storage.AddChild(ActionButton("Открыть склад • 400 монет", () => Execute(() => game.BuyWarehouse(city))));
        unload = ActionButton("Выгрузить выбранный товар", () => Execute(() => game.Transfer(wagonId, good, (int)quantity.Value, true))); storage.AddChild(unload);
        load = ActionButton("Загрузить выбранный товар", () => Execute(() => game.Transfer(wagonId, good, (int)quantity.Value, false))); storage.AddChild(load);
        storage.AddChild(ActionButton("Купить повозку здесь • 500 монет", () => Execute(() => game.BuyWagon(city))));
        var warehouseInfo = new Label { Name = "WarehouseInfo", AutowrapMode = TextServer.AutowrapMode.WordSmart }; storage.AddChild(warehouseInfo);
        var journalTab = new VBoxContainer { Name = "События" }; tabs.AddChild(journalTab);
        journalTab.AddChild(Text("История событий компании", 18));
        journal = new RichTextLabel { CustomMinimumSize = new Vector2(0, 120), ScrollFollowing = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        journalTab.AddChild(journal);
        company = ScrollTab(tabs, "Компания");
        BuildDevUi(tabs);
        cityNeedsPanel = ScrollTab(tabs, "Потребности");
        MoveChild(systemOverlay, -1);
        BuildToast();
        MoveChild(toast, menuOverlay.GetIndex());
    }
    private void BuildToast()
    {
        toast = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        toast.AddThemeStyleboxOverride("panel", Box("eee0bd", 2)); AddChild(toast);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); toast.AddChild(row);
        toastText = Text("", 15); toastText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        toastText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        toastText.MaxLinesVisible = 6; toastText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        toastText.MouseFilter = MouseFilterEnum.Ignore; row.AddChild(toastText);
        var dismiss = ActionButton("×", HideToast); dismiss.CustomMinimumSize = new Vector2(28, 28);
        dismiss.SizeFlagsVertical = SizeFlags.ShrinkBegin; row.AddChild(dismiss);
        toast.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenMenu(4); };
    }
    private void HideToast()
    {
        toastTween?.Kill(); toastTween = null; toast.Hide();
    }
    private void ShowToast(string message)
    {
        toastTween?.Kill();
        toastText.Text = message.Replace(System.Environment.NewLine, " • ");
        toast.Modulate = Colors.White; toast.Show(); PositionToast();
        toastTween = CreateTween();
        toastTween.TweenInterval(5.5);
        toastTween.TweenProperty(toast, "modulate:a", 0f, 0.5);
        toastTween.TweenCallback(Callable.From(() => toast.Hide()));
    }
    private void PositionToast()
    {
        if (toast is null || atlas is null) return;
        float width = Mathf.Min(380, atlas.Size.X - 24);
        toast.Size = new Vector2(width, 152);
        toast.Position = atlas.GlobalPosition + new Vector2(atlas.Size.X - width - 12, 12);
    }
    private void UpdateNotificationsButton()
    {
        notificationBadge.Visible = unreadNotifications > 0;
        notificationBadge.Text = unreadNotifications > 99 ? "99+" : unreadNotifications.ToString();
        notificationsButton.TooltipText = unreadNotifications == 0 ? "События" : $"События: {unreadNotifications} непрочитанных";
    }
    private void UpdateWindowTitle()
    {
        cityTitle.Text = menuTabs.CurrentTab == 7 ? "Потребности · " + Ru.Name(city) : menuTabs.CurrentTab == 6 ? "DEV · инструменты разработчика" : menuTabs.CurrentTab == 5 ? "Компания и влияние" : menuTabs.CurrentTab == 2 ? "Контракты" : menuTabs.CurrentTab == 4 ? "События"
            : new[] { "Рынок", "Предприятия", "Контракты", "Склад" }[Math.Clamp(menuTabs.CurrentTab, 0, 3)] + " · " + Ru.Name(city);
    }
    private void OpenCityContext(string target, Vector2 pointer)
    {
        SelectCity(target); contextCity = target;
        cityContext.Clear();
        cityContext.AddItem(Ru.Name(target)); cityContext.SetItemDisabled(0, true);
        cityContext.AddSeparator();
        string action = Wagon.Destination is not null ? "Повозка уже в пути" : Wagon.City == target ? "Повозка уже в этом городе" : "Отправить повозку сюда";
        bool disabled = Wagon.Destination is not null || Wagon.City == target;
        if (!disabled && Game.ProvisionCost(game.World.TravelDays(Wagon.City, target)) > Wagon.Provisions)
        { action = "Недостаточно провизии на рейс"; disabled = true; }
        cityContext.AddItem(action, 1); cityContext.SetItemDisabled(2, disabled);
        cityContext.AddSeparator();
        cityContext.AddItem(Wagon.Destination is null && Wagon.City == target ? "Рынок" : "Обзор рынка", 2);
        cityContext.AddItem("Контракты", 3);
        cityContext.AddItem("Склад", 4);
        cityContext.AddItem("Предприятия", 5);
        cityContext.AddItem("Филиал и влияние", 6);
        cityContext.AddItem("Потребности города", 7);
        cityContext.ResetSize();
        Vector2 extent = cityContext.Size;
        Vector2 local = pointer.Clamp(Vector2.Zero, (GetViewportRect().Size - extent).Max(Vector2.Zero));
        cityContext.Position = (Vector2I)local + (GetViewport().GuiEmbedSubwindows ? Vector2I.Zero : GetWindow().Position);
        cityContext.Popup();
    }
    private void CityContextAction(long id)
    {
        cityContext.Hide();
        SelectCity(contextCity);
        switch (id)
        {
            case 1: ConfirmTravel(contextCity); break;
            case 2: OpenMenu(0); break;
            case 3: OpenMenu(2); break;
            case 4: OpenMenu(3); break;
            case 5: OpenMenu(1); break;
            case 6: OpenMenu(5); break;
            case 7: OpenMenu(7); break;
        }
    }
    private void BuildSystemMenu()
    {
        systemOverlay = new Control { Visible = false };
        systemOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(systemOverlay);
        var veil = new ColorRect { Color = new Color(0.12f, 0.09f, 0.05f, 0.48f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); systemOverlay.AddChild(veil);
        veil.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) systemOverlay.Hide(); };
        systemFrame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        systemFrame.AddThemeStyleboxOverride("panel", Box("decca4", 3)); systemOverlay.AddChild(systemFrame);
        var actions = new VBoxContainer(); actions.AddThemeConstantOverride("separation", 12); systemFrame.AddChild(actions);
        var title = Text("Меню", 28); title.HorizontalAlignment = HorizontalAlignment.Center; actions.AddChild(title);
        actions.AddChild(ActionButton("Продолжить игру", () => systemOverlay.Hide()));
        actions.AddChild(ActionButton("Сохранить", () => { Execute(() => { game.Save(SavePath); return "Партия сохранена."; }); systemOverlay.Hide(); }));
        actions.AddChild(ActionButton("Загрузить", () => { LoadGame(); systemOverlay.Hide(); }));
        actions.AddChild(ActionButton("Новая партия", () => { systemOverlay.Hide(); ConfirmNewGame(); }));
        fullscreenButton = ActionButton("На весь экран", ToggleFullscreen); actions.AddChild(fullscreenButton);
        actions.AddChild(ActionButton("DEV · читы и события", () => OpenMenu(6)));
    }
    private void ToggleSystemMenu()
    {
        if (systemOverlay.Visible) systemOverlay.Hide();
        else { CloseMenu(); systemOverlay.Show(); ApplyResponsiveLayout(); }
    }
    private void OpenMenu(int tab)
    {
        if (menuOverlay.Visible) return;
        systemOverlay.Hide();
        Refresh();
        menuTabs.CurrentTab = tab;
        UpdateWindowTitle();
        if (tab == 4) { MarkHistoryRead(); HideToast(); }
        menuOverlay.Show();
        ApplyResponsiveLayout();
    }
    private void ToggleFullscreen()
    {
        var window = GetWindow();
        window.Mode = window.Mode == Window.ModeEnum.Fullscreen || window.Mode == Window.ModeEnum.ExclusiveFullscreen
            ? Window.ModeEnum.Windowed : Window.ModeEnum.Fullscreen;
        fullscreenButton.Text = window.Mode == Window.ModeEnum.Windowed ? "На весь экран" : "В окно";
    }
    private void ApplyResponsiveLayout()
    {
        if (mainMargin is null || menuFrame is null) return;
        var size = GetViewportRect().Size;
        bool compact = size.X < 1100 || size.Y < 720;
        mainLayout.AddThemeConstantOverride("separation", compact ? 6 : 12);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            mainMargin.AddThemeConstantOverride("margin_" + side, compact ? 12 : 22);
        gameTitle.AddThemeFontSizeOverride("font_size", compact ? 20 : 24);
        sidebar.CustomMinimumSize = new Vector2(compact ? 190 : 230, 0);
        summary.AddThemeFontSizeOverride("font_size", compact ? 16 : 19);
        summary.MaxLinesVisible = 2;
        summary.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        summary.CustomMinimumSize = new Vector2(0, 44);
        market.CustomMinimumSize = new Vector2(0, compact ? 120 : 220);
        cityTitle.AddThemeFontSizeOverride("font_size", compact ? 22 : 26);
        systemFrame.Size = new Vector2(Mathf.Min(340, size.X - 32), 400);
        systemFrame.Position = (size - systemFrame.Size) / 2;
        var frameSize = new Vector2(Mathf.Min(840, size.X - 32), Mathf.Min(760, size.Y - 32));
        menuFrame.Size = frameSize;
        menuFrame.Position = (size - menuFrame.Size) / 2;
        fullscreenButton.Text = GetWindow().Mode == Window.ModeEnum.Fullscreen || GetWindow().Mode == Window.ModeEnum.ExclusiveFullscreen ? "В окно" : "На весь экран";
        UpdateSummary();
        PositionToast();
    }
    private void UpdateSummary()
    {
        bool compact = GetViewportRect().Size.X < 1100 || GetViewportRect().Size.Y < 720;
        summary.Text = compact
            ? $"{GameCalendar.FormatDay(game.Day, true)}   •   Казна: {game.Gold:N0}   •   Влияние: {game.ControlledCities}/{game.RequiredCities} городов"
            : $"{GameCalendar.FormatDay(game.Day)}       Казна: {game.Gold:N0}       Влияние: {game.ControlledCities}/{game.RequiredCities} городов";
        summary.TooltipText = $"{GameCalendar.FormatDay(game.Day)} • день партии {game.Day}";
        gameTitle.Text = game.CompanyFounded ? "Торговая компания" : "Вольный купец";
        if (game.Won) summary.Text += "   •   Цель достигнута!";
    }
    private void CloseMenu() { menuOverlay.Hide(); }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (cityContext.Visible) { cityContext.Hide(); GetViewport().SetInputAsHandled(); return; }
            if (menuOverlay.Visible) CloseMenu();
            else ToggleSystemMenu();
            GetViewport().SetInputAsHandled();
        }
    }
    private static VBoxContainer ScrollTab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var contents = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; contents.AddThemeConstantOverride("separation", 12); scroll.AddChild(contents); return contents;
    }
    private void SelectCity(string value) { city = value; Refresh(); }
    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var events = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in events) AddHistory(entry);
        RefreshHistory();
        journal.ScrollToLine(0);
        string featured = events.FirstOrDefault(e => e.Contains("прибыла в город") || e.Contains("просрочен")) ?? events.LastOrDefault() ?? message;
        ShowToast(events.Length > 1 ? $"Новых событий: {events.Length}. {featured} Остальные — в уведомлениях." : featured);
    }
    private void Execute(Func<string> operation)
    {
        try { Log(operation()); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.IO.IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Log("Не выполнено: " + e.Message); }
        Refresh();
    }
    private void LoadGame() => Execute(() => { var loaded = Game.Load(SavePath); game = loaded; atlas.ResetView(); wagonId = game.Wagons[0].Id; city = Wagon.City; return "Партия загружена."; });
    private void ConfirmNewGame()
    {
        var dialog = new ConfirmationDialog { Title = "Новая партия", DialogText = "Начать новую компанию? Несохранённый прогресс будет потерян.", OkButtonText = "Начать", CancelButtonText = "Отмена" };
        AddChild(dialog); dialog.Confirmed += () => { game = new Game(); atlas.ResetView(); wagonId = 1; city = "oakwood"; eventHistory.Clear(); journal.Clear(); MarkHistoryRead(); HideToast(); Refresh(); Log("Начат путь вольного купца."); dialog.QueueFree(); };
        dialog.Canceled += dialog.QueueFree; dialog.PopupCentered();
    }
    private void AdvanceToArrival()
    {
        if (Wagon.Destination is null) { Log("Выбранная повозка уже в городе."); return; }
        Execute(() => game.Advance(Math.Min(30, (int)Math.Ceiling(Wagon.DaysLeft))));
    }
    private static void Clear(Container container)
    {
        foreach (Node child in container.GetChildren()) { container.RemoveChild(child); child.QueueFree(); }
    }
    private void Refresh()
    {
        refreshing = true;
        RefreshHistory();
        UpdateSummary();
        UpdateWindowTitle();
        wagonChoice.Clear();
        foreach (var w in game.Wagons) wagonChoice.AddItem($"№{w.Id} • {Ru.Name(w.Destination ?? w.City)} • {w.Used}/60");
        wagonChoice.Select(game.Wagons.FindIndex(w => w.Id == wagonId));
        var wagon = Wagon;
        provisionsStatus.Text = $"Провизия: {wagon.Provisions} · 2 в день пути";
        nextDayDate.Text = $"Завтра: {GameCalendar.FormatDay(game.Day + 1, true)}";
        arrival.Disabled = wagon.Destination is null;
        arrival.Text = wagon.Destination is null ? "Повозка в городе" : $"До прибытия · {Math.Min(30, (int)Math.Ceiling(wagon.DaysLeft))} дн.";
        arrival.TooltipText = wagon.Destination is null ? "Отправьте выбранную повозку в путь через ПКМ по городу на карте." : "Ждать прибытия выбранной повозки (до 30 дней за раз).";
        wagonStatus.Text = wagon.Destination is null ? $"Повозка №{wagonId} в городе {Ru.Name(wagon.City)}. Груз: {wagon.Used}/60." : $"Повозка №{wagonId} → {Ru.Name(wagon.Destination)}. Осталось: {Game.FormatDays(wagon.DaysLeft)}. Груз: {wagon.Used}/60.";
        if (wagon.Destination is null)
        {
            decimal days = game.World.TravelDays(wagon.City, city);
            routeStatus.Text = wagon.City == city ? "Выберите другой город на карте для планирования рейса." : $"Маршрут: {Game.DistanceKm(days)} км • {Game.FormatDays(days)} • {Game.ProvisionCost(days)} провизии • прибытие {GameCalendar.FormatDay(game.Day + (int)Math.Ceiling(days), true)}";
        }
        else routeStatus.Text = $"Прибытие: {GameCalendar.FormatDay(game.Day + (int)Math.Ceiling(wagon.DaysLeft))}. Кнопка «До прибытия» пропускает до 30 дней.";
        atlas.Model = game; atlas.Selected = city; atlas.ActiveWagon = wagon; atlas.QueueRedraw();
        var knowledge = game.MarketInfo(city);
        marketStatus.Text = knowledge.Unknown ? "Нет рыночных данных" : knowledge.Current ? "Цены на сегодня · монет за единицу" : knowledge.Approximate ? "Оценка цен · монет за единицу" : $"Цены на {GameCalendar.FormatDay(knowledge.Day!.Value, true)} · монет за единицу";
        market.Clear(); var root = market.CreateItem();
        foreach (string itemGood in Game.Goods)
        {
            var item = market.CreateItem(root); item.SetText(0, Ru.Name(itemGood)); item.SetMetadata(0, itemGood);
            item.SetIcon(0, goodIcons[itemGood]); item.SetIconMaxWidth(0, 24);
            for (int col = 1; col < 5; col++) item.SetTextAlignment(col, HorizontalAlignment.Center);
            item.SetText(1, knowledge.Unknown ? "—" : knowledge.Approximate ? "~" + knowledge.Stocks[itemGood] : knowledge.Stocks[itemGood].ToString());
            for (int col = 2; col <= 3; col++) item.SetText(col, knowledge.Unknown ? "—" : (knowledge.Approximate ? "~" : "") + game.Price(city, itemGood, col == 2, knowledge.Stocks[itemGood]));
            item.SetText(4, wagon.Cargo[itemGood].ToString());
            if (itemGood == good) item.Select(0);
        }
        Clear(enterprises);
        if (!game.CompanyFounded) enterprises.AddChild(Text("Покупка предприятий доступна после основания компании", 16));
        foreach (var enterprise in game.EnterprisesAt(city))
        {
            var e = enterprise;
            var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Box("d4be94", 5)); enterprises.AddChild(card);
            var details = new VBoxContainer(); details.AddThemeConstantOverride("separation", 8); card.AddChild(details);
            details.AddChild(IconTitle(Game.EnterpriseRecipes[e.Type].Outputs.Keys.First(), Game.EnterpriseRecipes[e.Type].Name));
            var recipe = Game.EnterpriseRecipes[e.Type];
            var production = new HFlowContainer(); production.AddThemeConstantOverride("h_separation", 10); details.AddChild(production);
            if (recipe.Inputs.Count == 0) production.AddChild(Text("Без сырья", 15));
            foreach (var input in recipe.Inputs)
                production.AddChild(IconTitle(input.Key, $"{Ru.Name(input.Key)} ×{input.Value}", 15, false));
            production.AddChild(Text("→", 20));
            foreach (var output in recipe.Outputs)
                production.AddChild(IconTitle(output.Key, $"{Ru.Name(output.Key)} ×{output.Value}", 15, false));
            details.AddChild(IconTitle("coin", $"Доход за 7 дней: {Game.EnterpriseQuarterIncome(e):N0}", 16));
            if (e.Owner is not null) details.AddChild(Text(e.Owner == 0 ? "Ваше предприятие" : "Предприятие другого торговца", 16));
            else if (wagon.Destination is null && wagon.City == city)
            {
                var button = ActionButton($"Купить · {Game.EnterprisePrice(e):N0} монет", () => Execute(() => game.BuyEnterprise(e.Id)));
                button.Icon = goodIcons["coin"]; button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 22);
                button.Disabled = !game.CompanyFounded || game.Gold < Game.EnterprisePrice(e); details.AddChild(button);
            }
            else details.AddChild(IconTitle("coin", $"Стоимость: {Game.EnterprisePrice(e):N0}", 16));
        }
        Clear(contracts);
        contracts.AddChild(Text($"Сегодня: {GameCalendar.FormatDay(game.Day)}", 18));
        var cityContracts = game.Contracts.Where(c => !c.Completed && !c.Failed)
            .OrderBy(c => c.AcceptedBy is null).ThenBy(c => c.DeadlineDay).ToList();
        if (cityContracts.Count == 0) contracts.AddChild(Text("Пока нет доступных контрактов", 18));
        foreach (var group in cityContracts.GroupBy(c => c.City).OrderBy(g => Ru.Name(g.Key)))
        {
            contracts.AddChild(Text($"{Ru.Name(group.Key)} · {group.Count()}", 23));
            contracts.AddChild(new HSeparator());
        foreach (var c in group)
        {
            var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Box("d4be94", 5)); contracts.AddChild(card);
            var details = new VBoxContainer(); details.AddThemeConstantOverride("separation", 8); card.AddChild(details);
            details.AddChild(IconTitle(c.Good, $"{Ru.Name(c.Good)} ×{c.Amount} · {c.Reward} монет"));
            details.AddChild(ActionButton("Показать город на карте", () => ShowContractCity(c.City)));
            details.AddChild(Text(c.AcceptedBy is null ? $"Предложение №{c.Id}" : $"Контракт №{c.Id} · принят", 14));
            var deadline = Text($"Сдать до {GameCalendar.FormatDay(c.DeadlineDay)} включительно", 16);
            deadline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (c.DeadlineDate == game.CurrentDate) { deadline.Text = "Последний день сдачи — сегодня"; deadline.AddThemeColorOverride("font_color", new Color("943f2c")); }
            details.AddChild(deadline);
            var issuer = Text(c.Issuer, 14); issuer.AutowrapMode = TextServer.AutowrapMode.WordSmart; details.AddChild(issuer);
            {
                if (c.AcceptedBy is null)
                {
                    var accept = ActionButton("Принять контракт", () => Execute(() => game.AcceptContract(c.Id)));
                    accept.Disabled = game.Contracts.Count(x => x.AcceptedBy == 0 && !x.Completed && !x.Failed) >= 3;
                    details.AddChild(accept);
                }
                else if (c.AcceptedBy == 0)
                {
                    details.AddChild(Text($"В повозке: {wagon.Cargo[c.Good]} / {c.Amount}", 16));
                    var deliver = ActionButton($"Сдать груз · +{c.Reward} монет", () => Execute(() => game.DeliverContract(c.Id, wagonId)));
                    deliver.Disabled = wagon.Destination is not null || wagon.City != c.City || wagon.Cargo[c.Good] < c.Amount; details.AddChild(deliver);
                }
            }
        }
        }
        RefreshCompany();
        RefreshCityNeedsUi();
        var warehouse = FindChild("WarehouseInfo", true, false) as Label;
        if (warehouse is not null) warehouse.Text = game.Warehouses.TryGetValue(city, out var stock) ? "На складе: " + stock.Values.Sum() + "/300\n" + string.Join("\n", stock.Where(p => p.Value > 0).Select(p => $"{Ru.Name(p.Key)}: {p.Value}")) : "В выбранном городе пока нет вашего склада.";
        refreshing = false;
        UpdateTrade();
    }
    private void UpdateTrade()
    {
        if (refreshing || quantity is null) return;
        tradeGood.Text = Ru.Name(good);
        tradeIcon.Texture = goodIcons[good];
        buy.Text = "Купить"; sell.Text = "Продать";
        bool here = Wagon.Destination is null && Wagon.City == city;
        tradePanel.Visible = here;
        scout.Visible = !here && !game.MarketInfo(city).Current;
        scout.Disabled = game.Gold < 40;
        buy.Disabled = sell.Disabled = !here;
        unload.Disabled = load.Disabled = !here || !game.Warehouses.ContainsKey(city);
        if (!here) { quote.Text = Wagon.Destination is null ? $"Повозка в городе {Ru.Name(Wagon.City)} · торговля недоступна" : "Повозка в пути · торговля недоступна"; return; }
        int n = (int)quantity.Value, stock = game.Stocks[city][good];
        long buying = 0, selling = 0;
        for (int i = 0; i < n; i++) { buying += game.Price(city, good, true, stock - i); selling += game.Price(city, good, false, stock + i); }
        buy.Disabled = n > stock || n + Wagon.Used > 60 || buying > game.Gold;
        sell.Disabled = n > Wagon.Cargo[good];
        buy.Text = $"Купить · {buying} монет";
        sell.Text = $"Продать · {selling} монет";
        quote.Text = $"В повозке: {Wagon.Cargo[good]} · Свободно: {60 - Wagon.Used} · На рынке: {stock}";
        if (buy.Disabled) quote.Text += n > stock ? " · Недостаточно товара" : n + Wagon.Used > 60 ? " · Не хватает места" : " · Недостаточно монет";
    }
    private async void RunNotificationsTest()
    {
        try
        {
            for (int frame = 0; frame < 10; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var bounds = atlas.GetGlobalRect();
            Log("Проверка уведомления: повозка прибыла в город.");
            if (!toast.Visible || unreadNotifications != 1 || !journal.GetParsedText().Contains("повозка прибыла"))
                throw new Exception("Event must appear in toast and history");
            await ToSignal(GetTree().CreateTimer(6.2), SceneTreeTimer.SignalName.Timeout);
            if (toast.Visible || atlas.GetGlobalRect() != bounds) throw new Exception("Toast must disappear without moving the map");
            Log("Второе событие");
            notificationsButton.EmitSignal(Button.SignalName.Pressed);
            if (!menuOverlay.Visible || menuTabs.CurrentTab != 4 || unreadNotifications != 0
                || toast.Visible || !journal.GetParsedText().Contains("Второе событие")) throw new Exception("Notification history and unread count");
            CloseMenu();
            Log("Закрыть вручную"); HideToast();
            if (toast.Visible) throw new Exception("Dismiss toast");
            GD.Print("NOTIFICATIONS_OK: toast, expiry, history, unread count and fixed map");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async void RunMapStabilityTest()
    {
        try
        {
            async Task Settle() { for (int frame = 0; frame < 10; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            foreach (var size in new[] { new Vector2I(800, 600), new Vector2I(1280, 800), new Vector2I(1920, 1080) })
            {
                GetWindow().Size = size;
                game = new Game(); wagonId = 1; city = "oakwood"; Refresh(); await Settle();
                Vector2 focus = atlas.Size / 2;
                atlas._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = focus });
                atlas._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = focus });
                atlas._Input(new InputEventMouseMotion { Position = atlas.GetGlobalTransformWithCanvas() * (focus + new Vector2(60, 35)) });
                atlas._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = atlas.GetGlobalTransformWithCanvas() * (focus + new Vector2(60, 35)) });
                var bounds = atlas.GetGlobalRect();
                var positions = Game.Cities.Select(atlas.CityScreenPosition).ToArray();
                var geometry = atlas.GeometrySnapshot();
                var view = (atlas.PanOffset, atlas.Zoom);
                foreach (string target in Game.Cities.Skip(1).Where((_, index) => index % 6 == 0))
                {
                    game.Gold = 100000;
                    SelectCity(target); await Settle(); Check();
                    Execute(() => game.Travel(1, target)); await Settle(); Check();
                    while (Wagon.Destination is not null)
                    {
                        Execute(() => game.Advance(1)); await Settle(); Check();
                    }
                    SelectCity(Wagon.City); await Settle(); Check();
                }
                void Check()
                {
                    if (atlas.GetGlobalRect() != bounds || !positions.SequenceEqual(Game.Cities.Select(atlas.CityScreenPosition))
                        || !geometry.SequenceEqual(atlas.GeometrySnapshot()) || view != (atlas.PanOffset, atlas.Zoom))
                        throw new Exception($"Map moved during a journey at {size}: {bounds} -> {atlas.GetGlobalRect()}");
                }
                GD.Print($"MAP_STABLE {size}");
            }
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async void RunMapTest()
    {
        try
        {
            for (int frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var resolution in new[] { new Vector2I(800, 600), new Vector2I(1280, 800) })
            {
                GetWindow().Size = resolution;
                for (int frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                for (int sample = 0; sample < 5; sample++)
                {
                    game.World = WorldMap.Generate();
                    atlas.VerifyNavigation();
                }
            }
            var legacy = new Game();
            foreach (string added in Game.Cities.Skip(8))
            { legacy.World.Cities.Remove(added); legacy.Stocks.Remove(added); }
            foreach (var enterprise in legacy.Enterprises) enterprise.City = "oakwood";
            legacy.World.Roads.Clear();
            foreach (string a in Game.Cities.Take(8))
                foreach (string b in Game.Cities.Take(8).Where(b => Array.IndexOf(Game.Cities, b) > Array.IndexOf(Game.Cities, a)))
                    legacy.World.Roads.Add(new MapRoad { A = a, B = b, Days = 3.5m });
            var originalCities = legacy.World.Cities.ToDictionary(p => p.Key, p => (p.Value.X, p.Value.Y));
            legacy.Travel(1, "riverport");
            legacy.Wagons[0].DaysLeft = 0.1m;
            legacy.World.NetworkVersion = 0;
            string migrationPath = ProjectSettings.GlobalizePath("user://road-migration-test.json");
            try
            {
                legacy.Save(migrationPath);
                var restored = Game.Load(migrationPath);
                if (restored.World.NetworkVersion != 4 || restored.Gold != legacy.Gold
                    || restored.Wagons[0].DaysLeft != 0.1m || restored.Wagons[0].TotalDays != legacy.Wagons[0].TotalDays)
                    throw new Exception("Legacy road migration must preserve paid cost and arrival time");
                if (restored.World.Cities.Count != 24 || restored.Stocks.Count != 24
                    || originalCities.Any(p => (restored.World.Cities[p.Key].X, restored.World.Cities[p.Key].Y) != p.Value)
                    || Game.Cities.Skip(8).Any(c => !restored.Enterprises.Any(e => e.City == c)))
                    throw new Exception("World expansion must preserve old towns and populate new markets and enterprises");
                restored.Advance(1);
                if (restored.Wagons[0].City != "riverport" || restored.Wagons[0].Destination is not null)
                    throw new Exception("Arrival after road migration");
            }
            finally { System.IO.File.Delete(migrationPath); }
            GD.Print("MAP_NAVIGATION_OK: 10 maps, road geometry, distance costs and legacy journey migration");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async void RunLayoutTest()
    {
        try
        {
            foreach (var font in new[] { GameFonts.Body, GameFonts.Controls, GameFonts.Heading, GameFonts.Map })
                foreach (char glyph in "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789")
                    if (!font.HasChar(glyph)) throw new Exception($"Font missing Cyrillic glyph: {glyph}");
            foreach (var resolution in new[] { new Vector2I(800, 600), new Vector2I(1024, 768), new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(900, 1100), new Vector2I(2560, 1080) })
            {
                GetWindow().Size = resolution;
                for (int frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                ApplyResponsiveLayout();
                for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var bounds = GetViewportRect().Grow(1);
                foreach (var control in new Control[] { atlas, summary, routeStatus, nextDay, nextWeek, arrival, fullscreenButton, wagonChoice, notificationsButton })
                    if (!bounds.Encloses(control.GetGlobalRect())) throw new Exception($"Main layout overflow at {resolution}: {control.GetType().Name} {control.GetGlobalRect()} / {bounds}");
                if (atlas.Size.Y < 179) throw new Exception($"Map too small at {resolution}");
                for (int tab = 0; tab < 8; tab++)
                {
                    OpenMenu(tab);
                    for (int frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (!bounds.Encloses(menuFrame.GetGlobalRect())) { throw new Exception($"Menu overflow at {resolution}, tab {tab}: {menuFrame.GetGlobalRect()} / {bounds}"); }
                    CheckVisibleBounds(menuFrame, bounds);
                    CloseMenu();
                }
                ToggleSystemMenu();
                for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!bounds.Encloses(systemFrame.GetGlobalRect())) throw new Exception("System menu overflow");
                CheckVisibleBounds(systemFrame, bounds);
                ToggleSystemMenu();
                GD.Print($"LAYOUT_OK {resolution}");
            }
            GD.Print("RESPONSIVE_LAYOUT_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void CheckVisibleBounds(Node node, Rect2 bounds)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Control control && control.IsVisibleInTree())
            {
                // Scroll contents may extend beyond their viewport by design.
                if (control is ScrollContainer) continue;
                if (!bounds.Encloses(control.GetGlobalRect())) throw new Exception($"Menu control overflow: {control.GetType().Name} {control.GetGlobalRect()}");
                CheckVisibleBounds(control, bounds);
            }
        }
    }
    public void CalendarTest()
    {
        try
        {
            static void Check(bool condition, string name)
            {
                if (!condition) throw new Exception(name);
                GD.Print("CALENDAR_OK " + name);
            }
            static int DayOf(DateOnly date) => date.DayNumber - GameCalendar.StartDate.DayNumber + 1;
            Check(GameCalendar.DateForDay(1) == new DateOnly(1715, 5, 1), "start date");
            Check(GameCalendar.DateForDay(32) == new DateOnly(1715, 6, 1), "month boundary");
            Check(GameCalendar.DateForDay(DayOf(new DateOnly(1715, 12, 31)) + 1) == new DateOnly(1716, 1, 1), "year boundary");
            Check(GameCalendar.DateForDay(DayOf(new DateOnly(1716, 2, 28)) + 1) == new DateOnly(1716, 2, 29), "leap day");
            Check(GameCalendar.FormatDay(18) == "18 мая 1715 года", "Russian deadline format");
            var due = new Contract { Id = 901, Issuer = "Совет города", City = "oakwood", Good = "bread", Amount = 1, Reward = 100, DeadlineDay = 32 };
            var onTime = new Game { Day = 31, Contracts = new List<Contract> { due } };
            string accepted = onTime.AcceptContract(due.Id);
            Check(accepted.Contains("1 июня 1715 года включительно") && !accepted.Contains("до дня"), "acceptance has a date");
            onTime.Advance(1);
            Check(!due.Failed && onTime.CurrentDate == due.DeadlineDate, "deadline inclusive");
            onTime.Wagons[0].Cargo["bread"] = 1;
            onTime.DeliverContract(due.Id, 1);
            Check(due.Completed, "delivery on final date");
            var late = new Contract { Id = 902, Issuer = "Совет города", City = "oakwood", Good = "bread", Amount = 1, Reward = 100, DeadlineDay = 32 };
            var overdue = new Game { Day = 31, Contracts = new List<Contract> { late } };
            overdue.AcceptContract(late.Id);
            string news = overdue.Advance(2);
            Check(late.Failed && news.Contains("2 июня 1715 года"), "expires following date");
            overdue.Wagons[0].Cargo["bread"] = 1;
            bool rejected = false;
            try { overdue.DeliverContract(late.Id, 1); }
            catch (InvalidOperationException e) { rejected = e.Message.Contains("1 июня 1715 года"); }
            Check(rejected && overdue.Wagons[0].Cargo["bread"] == 1, "late delivery rejected without cargo loss");
            string path = ProjectSettings.GlobalizePath("user://calendar-test.json");
            try
            {
                onTime.Save(path);
                string json = System.IO.File.ReadAllText(path);
                Check(!json.Contains("CurrentDate") && !json.Contains("DeadlineDate"), "compatible save schema");
                var loaded = Game.Load(path);
                Check(loaded.CurrentDate == onTime.CurrentDate && loaded.Contracts[0].DeadlineDate == due.DeadlineDate, "saved dates restored");
            }
            finally { System.IO.File.Delete(path); }
            game = new Game { Day = 32, Contracts = new List<Contract> {
                new() { Id = 903, Issuer = "Совет города", City = "oakwood", Good = "bread", Amount = 1, Reward = 100, DeadlineDay = 32 }
            } };
            Refresh();
            Check(contracts.GetChildren().OfType<Label>().Any(l => l.Text == "Сегодня — последний день сдачи"), "final day highlighted in UI");
            Check(summary.Text.Contains("1 июня 1715 года") || summary.Text.Contains("01.06.1715"), "current date shown in UI");
            GD.Print("CALENDAR_TEST_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    public void SmokeTest()
    {
        try
        {
            if (menuOverlay.Visible || systemOverlay.Visible) throw new Exception("Menus must start closed");
            _Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            if (!systemOverlay.Visible) throw new Exception("Escape must open system menu from map");
            _Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            if (systemOverlay.Visible) throw new Exception("Escape must close system menu");
            menuButton.EmitSignal(Button.SignalName.Pressed);
            if (!systemOverlay.Visible) throw new Exception("Menu button must open system menu");
            ToggleSystemMenu();
            for (int tab = 0; tab < 8; tab++)
            {
                OpenMenu(tab);
                if (!menuOverlay.Visible || menuTabs.CurrentTab != tab || menuTabs.TabsVisible) throw new Exception("Independent window open failed");
                OpenMenu((tab + 1) % 5);
                if (menuTabs.CurrentTab != tab) throw new Exception("Switching windows must require closing the current window");
                _Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
                if (menuOverlay.Visible) throw new Exception("Escape menu close failed");
            }
            SelectCity("riverport");
            marketButton.EmitSignal(Button.SignalName.Pressed);
            if (city != Wagon.City || !tradePanel.Visible || scout.Visible) throw new Exception("Sidebar market must open local trading without scouting");
            CloseMenu();
            OpenCityContext("riverport", new Vector2(100, 100));
            CityContextAction(2);
            if (city != "riverport" || tradePanel.Visible || !scout.Visible) throw new Exception("Remote market must show scouting without trade controls");
            CloseMenu();
            city = "oakwood"; good = "wood"; quantity.Value = 1; Refresh();
            OpenCityContext("oakwood", new Vector2(100, 100));
            if (!cityContext.IsItemDisabled(2)) throw new Exception("Context travel must be disabled in current city");
            _Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            if (cityContext.Visible || systemOverlay.Visible) throw new Exception("Escape must only dismiss context menu");
            int before = game.Gold;
            buy.EmitSignal(Button.SignalName.Pressed);
            if (Wagon.Cargo[good] != 1 || game.Gold >= before) throw new Exception("UI purchase failed");
            int oldGold = game.Gold, oldCargo = Wagon.Used;
            SelectCity("riverport");
            if (!buy.Disabled || !sell.Disabled) throw new Exception("Remote trading not disabled");
            OpenCityContext(city, atlas.GlobalPosition + atlas.Size / 2);
            if (cityContext.IsItemDisabled(2)) throw new Exception("Context travel unexpectedly disabled");
            int foodBefore = Wagon.Provisions;
            cityContext.EmitSignal(PopupMenu.SignalName.IdPressed, 1L);
            if (!travelConfirmation.Visible || Wagon.Destination is not null) throw new Exception("Travel requires provision confirmation");
            travelConfirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed); travelConfirmation.Hide();
            if (Wagon.Destination != city || game.Gold != oldGold || Wagon.Provisions >= foodBefore || Wagon.Used != oldCargo) throw new Exception("UI travel failed");
            OpenCityContext(city, new Vector2(100, 100));
            if (!cityContext.IsItemDisabled(2)) throw new Exception("Context travel must be disabled during a journey");
            cityContext.Hide();
            AdvanceToArrival();
            while (Wagon.Destination is not null) AdvanceToArrival();
            if (Wagon.City != city || sell.Disabled) throw new Exception("Arrival UI failed");
            sell.EmitSignal(Button.SignalName.Pressed);
            if (Wagon.Cargo[good] != 0) throw new Exception("UI sale failed");
            Execute(() => game.BuyWarehouse(city));
            if (!game.Warehouses.ContainsKey(city)) throw new Exception("Warehouse purchase failed");
            quantity.Value = 1;
            buy.EmitSignal(Button.SignalName.Pressed);
            unload.EmitSignal(Button.SignalName.Pressed);
            if (game.Warehouses[city][good] != 1 || Wagon.Cargo[good] != 0) throw new Exception("Warehouse unload UI failed");
            load.EmitSignal(Button.SignalName.Pressed);
            if (game.Warehouses[city][good] != 0 || Wagon.Cargo[good] != 1) throw new Exception("Warehouse load UI failed");
            var offer = game.Contracts.First(c => !c.Failed && !c.Completed);
            Execute(() => game.AcceptContract(offer.Id));
            if (offer.AcceptedBy != 0) throw new Exception("Contract accept failed");
            // Give the fixture the required destination cargo to check delivery wiring.
            Wagon.City = offer.City; Wagon.Cargo[offer.Good] = offer.Amount;
            Execute(() => game.DeliverContract(offer.Id, wagonId));
            if (!offer.Completed) throw new Exception("Contract delivery failed");
            var enterprise = game.EnterprisesAt(city).First();
            game.Gold = Game.CharterPrice + Game.EnterprisePrice(enterprise);
            game.FoundCompany();
            Execute(() => game.BuyEnterprise(enterprise.Id));
            if (enterprise.Owner != 0) throw new Exception("Enterprise purchase failed");
            int beforeIncome = game.Gold;
            Execute(() => game.Advance(7));
            if (game.Gold <= beforeIncome) throw new Exception("Enterprise income failed");
            string testSave = ProjectSettings.GlobalizePath("user://smoke-test.json");
            game.Save(testSave); var restored = Game.Load(testSave); System.IO.File.Delete(testSave);
            if (restored.Day != game.Day || restored.Gold != game.Gold || restored.World.Seed != game.World.Seed) throw new Exception("Save round trip failed");
            GD.Print("GODOT_UI_SMOKE_OK: map menus, buy, remote lock, route, arrival, sell, warehouse, contracts, enterprises, save/load");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
