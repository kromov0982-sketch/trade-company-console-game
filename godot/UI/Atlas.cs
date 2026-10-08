using Godot;
using TradeCompany;

public partial class Atlas : Control
{
    public Game Model = null!;
    public string Selected = "oakwood";
    public Wagon? ActiveWagon;
    public Action<string>? CitySelected;
    public Action<string, Vector2>? CityContextRequested;
    private readonly Color ink = new("594730"), faded = new("9d8860"), carmine = new("943f2c");
    private Texture2D cityTexture = null!, wagonTexture = null!;
    private Font Letter => GameFonts.Map;
    private bool CompactMap => Size.Y < 380 || Size.X < 900;
    private readonly Dictionary<string, Vector2> cityPositions = new();
    private int cachedSeed;
    private WorldMap? cachedWorld;
    private readonly Dictionary<string, Vector2[]> roadCurves = new();
    private Vector2 pan;
    private float zoom = 1;
    private bool mouseHeld, dragged;
    private Vector2 pressPosition, pressPan;
    public Vector2 PanOffset => pan;
    public float Zoom => zoom;
    private float MinimumZoom => Model is null ? 0.3f : Mathf.Clamp(Mathf.Min((Size.X-32)/WorldSize.X, (Size.Y-32)/WorldSize.Y), 0.15f, 1);
    public Vector2 CityScreenPosition(string city) => ToScreen(PositionOf(city));
    private Vector2 ToScreen(Vector2 point) => (point - Size / 2) * zoom + Size / 2 + pan;
    public void ResetView()
    {
        zoom = 1; pan = Model is null ? Vector2.Zero : Size / 2 - PositionOf("oakwood"); mouseHeld = dragged = false;
        MouseDefaultCursorShape = CursorShape.Drag;
        QueueRedraw();
    }
    public void FocusCity(string city)
    {
        zoom = Mathf.Max(1, MinimumZoom);
        pan = (Size / 2 - PositionOf(city)) * zoom;
        ClampPan(); QueueRedraw();
    }
    private void ClampPan()
    {
        if (Model is null) return;
        if (zoom <= MinimumZoom + 0.001f)
        { pan = (Size/2-WorldSize/2)*zoom; return; }
        Vector2 origin = Size / 2 * (zoom - 1);
        pan = pan.Clamp(origin - WorldSize * zoom + Size * 0.2f, origin + Size * 0.8f);
    }

    private Vector2 WorldSize => new(Model.World.Width, Model.World.Height);
    private Vector2 PositionOf(string city)
    {
        if (cachedWorld != Model.World || cachedSeed != Model.World.Seed || cityPositions.Count == 0)
        {
            cachedWorld = Model.World; cachedSeed = Model.World.Seed; cityPositions.Clear(); roadCurves.Clear();
            foreach (var pair in Model.World.Cities)
                cityPositions[pair.Key] = new Vector2(pair.Value.X, pair.Value.Y);
        }
        return cityPositions[city];
    }
    public void ShowWholeMap()
    {
        zoom = MinimumZoom;
        pan = (Size / 2 - WorldSize / 2) * zoom;
        QueueRedraw();
    }
    public override void _GuiInput(InputEvent e)
    {
        if (Model is null) return;
        if (e is InputEventMouseButton { Pressed: true } click)
        {
            if (click.ButtonIndex == MouseButton.Left)
            {
                mouseHeld = true; dragged = false;
                pressPosition = click.Position; pressPan = pan;
                MouseDefaultCursorShape = CursorShape.Move;
                AcceptEvent();
            }
            else if (click.ButtonIndex == MouseButton.Right)
            {
                mouseHeld = dragged = false;
                string city = Game.Cities.MinBy(c => CityScreenPosition(c).DistanceTo(click.Position))!;
                if (CityScreenPosition(city).DistanceTo(click.Position) < 32 * Mathf.Max(zoom, 0.5f))
                    CityContextRequested?.Invoke(city, GetGlobalTransformWithCanvas() * click.Position);
                AcceptEvent();
            }
            else if (click.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                float next = Mathf.Clamp(zoom * (click.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f), MinimumZoom, 2.5f);
                Vector2 focus = click.Position - Size / 2;
                pan = focus - (focus - pan) * (next / zoom);
                zoom = next; ClampPan(); QueueRedraw(); AcceptEvent();
            }
        }
    }
    public override void _Input(InputEvent e)
    {
        if (!mouseHeld) return;
        if (e is InputEventMouseMotion motion)
        {
            Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * motion.Position;
            if (local.DistanceTo(pressPosition) > 6) dragged = true;
            if (dragged) { pan = pressPan + local - pressPosition; ClampPan(); QueueRedraw(); }
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release)
        {
            Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * release.Position;
            mouseHeld = false;
            MouseDefaultCursorShape = CursorShape.Drag;
            if (!dragged && new Rect2(Vector2.Zero, Size).HasPoint(local))
            {
                string city = Game.Cities.MinBy(c => CityScreenPosition(c).DistanceTo(local))!;
                if (CityScreenPosition(city).DistanceTo(local) < 32 * zoom) CitySelected?.Invoke(city);
            }
            GetViewport().SetInputAsHandled();
        }
    }
    public Vector2[] GeometrySnapshot() => Model.World.Roads.SelectMany(r => GetRoadPoints(r.A, r.B)).ToArray();
    public void VerifyNavigation()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var original = CitySelected;
        var originalContext = CityContextRequested;
        int selections = 0;
        string? selected = null;
        CitySelected = city => { selections++; selected = city; };
        try
        {
            ResetView();
            string city = Game.Cities[0];
            Vector2 start = CityScreenPosition(city), delta = new(60, 35), originalPan = pan;
            void Press(Vector2 point) => _GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point });
            void Release(Vector2 point) => _Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = GetGlobalTransformWithCanvas() * point });
            Press(start);
            _Input(new InputEventMouseMotion { Position = GetGlobalTransformWithCanvas() * (start + delta) });
            Release(start + delta);
            Check((PanOffset - originalPan).DistanceTo(delta) < 0.01f && selections == 0, "Dragging must pan without selecting a city");
            Vector2 target = CityScreenPosition(city);
            Press(target); Release(target);
            Check(selections == 1 && selected == city, "City selection after panning");
            _GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = target });
            Check(Zoom > 1 && CityScreenPosition(city).DistanceTo(target) < 0.01f, "Zoom must preserve the point under the cursor");
            Press(CityScreenPosition(city)); Release(CityScreenPosition(city));
            Check(selections == 2 && selected == city, "City selection after zooming");
            string? contextTarget = null;
            CityContextRequested = (targetCity, _) => contextTarget = targetCity;
            _GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = CityScreenPosition(city) });
            Check(contextTarget == city, "Right click finds the city after panning and zooming");
            foreach (var road in Model.World.Roads)
            {
                var points = GetRoadPoints(road.A, road.B);
                var reverse = GetRoadPoints(road.B, road.A);
                Check(points[0].DistanceTo(PositionOf(road.A)) < 0.01f && points[^1].DistanceTo(PositionOf(road.B)) < 0.01f, "Road endpoints");
                Check(points.SequenceEqual(reverse.Reverse()), "Road direction consistency");
                Check(road.Path.Count >= 2 && road.Days > 0, "Terrain route geometry");
                Check(PointOnRoad(road.A, road.B, 0).DistanceTo(points[0]) < 0.01f && PointOnRoad(road.A, road.B, 1).DistanceTo(points[^1]) < 0.01f, "Travel follows road endpoints");
            }
            for (int i = 0; i < 80; i++)
                _GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Position = Size/2 });
            Check(Mathf.IsEqualApprox(zoom, MinimumZoom), "Zoom cannot shrink the world below the viewport fit");
            Check(ToScreen(WorldSize/2).DistanceTo(Size/2) < 0.01f, "Fully zoomed-out world stays centered");
            ResetView();
            Check(CityScreenPosition("oakwood").DistanceTo(Size / 2) < 0.01f && Zoom == 1, "Reset map view");
        }
        finally { CitySelected = original; CityContextRequested = originalContext; ResetView(); }
    }
    public override void _Ready()
    {
        cityTexture = GD.Load<Texture2D>("res://Art/Icons/city.png");
        wagonTexture = GD.Load<Texture2D>("res://Art/Icons/wagon.png");
        MouseDefaultCursorShape = CursorShape.Drag;
        ClipContents = true;
        TooltipText = "";
        Resized += () => { zoom = Mathf.Max(zoom, MinimumZoom); ClampPan(); QueueRedraw(); };
        GetWindow().FocusExited += () => { mouseHeld = false; MouseDefaultCursorShape = CursorShape.Drag; };
        var paper = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Art/parchment.gdshader") };
        material.SetShaderParameter("paper", new Color("e5d5ab"));
        paper.Material = material;
        AddChild(paper);
    }
    public override void _Draw()
    {
        if (Model is null || Size.X < 100 || Size.Y < 100) return;
        if (cachedSeed != Model.World.Seed) ResetView();
        DrawSetTransform(Size / 2 * (1 - zoom) + pan, 0, Vector2.One * zoom);
        var rng = new Random(Model.World.Seed);
        // Fine engraving marks are decorative; roads and city positions come from the model.
        for (int i = 0; i < (zoom < 0.5f ? 0 : 6500); i++)
        {
            var p = new Vector2(rng.Next(15, (int)WorldSize.X - 15), rng.Next(15, (int)WorldSize.Y - 15));
            DrawLine(p, p + new Vector2(rng.Next(1, 4), 0), new Color(0.35f, 0.27f, 0.15f, 0.045f));
        }
        DrawTerrain();
        DrawRoadNetwork();
        if (ActiveWagon is not null)
        {
            var route = ActiveWagon.Destination is null ? Model.World.FindRoute(ActiveWagon.City, Selected) : ActiveWagon.Route;
            var points = RoutePolyline(route);
            if (points.Length > 1)
            {
                DrawPolyline(points, new Color(0.58f, 0.25f, 0.17f, 0.24f), 9, true);
                DrawPolyline(points, carmine, 2.5f, true);
            }
        }

        DrawBridges();
        foreach (var city in Game.Cities)
        {
            var p = PositionOf(city);
            if (city == Selected)
            {
                DrawCircle(p, CompactMap ? 20 : 27, new Color(0.75f, 0.46f, 0.26f, 0.16f));
                DrawArc(p, CompactMap ? 21 : 28, 0, Mathf.Tau, 48, carmine, 1.3f, true);
            }
            DrawSetTransform(Vector2.Zero);
            DrawTownSymbol(ToScreen(p), CompactMap ? 17 : 22);
            if (Model.IsControlled(city)) DrawArc(ToScreen(p), CompactMap ? 21 : 26, 0, Mathf.Tau, 64, new Color("466344"), 2, true);
            DrawSetTransform(Size / 2 * (1 - zoom) + pan, 0, Vector2.One * zoom);
        }
        DrawCityNames();
        foreach (var wagon in Model.Wagons)
        {
            var p = WagonPosition(wagon);
            float wagonSize = CompactMap ? 27 : 38;
            DrawTextureRect(wagonTexture, new Rect2(p - Vector2.One * wagonSize / 2, Vector2.One * wagonSize), false);
            DrawString(Letter, p + new Vector2(22, 5), $"{wagon.Id}", fontSize: 15, modulate: carmine);
        }
        DrawSetTransform(Vector2.Zero);
        // Atlas frame and legend stay in place while the map underneath is moved.
        DrawRect(new Rect2(5, 5, Size.X - 10, Size.Y - 10), ink, false, 2);
        DrawRect(new Rect2(11, 11, Size.X - 22, Size.Y - 22), faded, false, 1);
        for (int x = 22; x < Size.X - 20; x += 22)
        {
            DrawLine(new Vector2(x, 5), new Vector2(x, 11), ink, 1);
            DrawLine(new Vector2(x, Size.Y - 11), new Vector2(x, Size.Y - 5), ink, 1);
        }
    }
    private Vector2[] RoutePolyline(IReadOnlyList<string> route) => SmoothRoad(route.Select(node =>
    {
        var p = Model.World.RoutePoint(node); return new Vector2(p.X,p.Y);
    }).ToArray());
    private static Vector2[] SmoothRoad(Vector2[] source)
    {
        if (source.Length < 2) return source;
        var points = new List<Vector2> { source[0] };
        for (int i = 1; i < source.Length-1; i++)
        {
            var p = source[i];
            float radius = Mathf.Min(10, Mathf.Min(p.DistanceTo(source[i-1]),p.DistanceTo(source[i+1]))*0.3f);
            var entry = p.MoveToward(source[i-1],radius); var exit = p.MoveToward(source[i+1],radius);
            for (int j = 0; j <= 6; j++)
            {
                float t = j/6f; points.Add(entry*(1-t)*(1-t)+p*2*t*(1-t)+exit*t*t);
            }
        }
        points.Add(source[^1]); return points.ToArray();
    }
    private Vector2 WagonPosition(Wagon wagon)
    {
        if (wagon.Destination is null) return PositionOf(wagon.City) + new Vector2(0, -(CompactMap ? 29 : 44) - (wagon.Id-1)%3*16);
        var path = RoutePolyline(wagon.Route);
        if (path.Length == 0) return PositionOf(wagon.City);
        float total = 0;
        for (int i = 1; i < path.Length; i++) total += path[i-1].DistanceTo(path[i]);
        float remaining = total * (wagon.TotalDays > 0 ? (float)(1-wagon.DaysLeft/wagon.TotalDays) : 0);
        for (int i = 1; i < path.Length; i++)
        {
            float length = path[i-1].DistanceTo(path[i]);
            if (remaining <= length) return path[i-1].Lerp(path[i], length == 0 ? 0 : remaining/length);
            remaining -= length;
        }
        return path[^1];
    }
    private readonly HashSet<Vector2> roadJunctions = new();
    private static string RoadKey(string a, string b) => Array.IndexOf(Game.Cities, a) < Array.IndexOf(Game.Cities, b) ? a + ":" + b : b + ":" + a;
    private void BuildRoadNetwork()
    {
        roadJunctions.Clear();
        foreach (var road in Model.World.Roads)
        {
            var source = road.Path.Select(p => new Vector2(p.X, p.Y)).ToArray();
            if (source.Length < 2) source = new[] { PositionOf(road.A), PositionOf(road.B) };
            var points = new List<Vector2> { source[0] };
            for (int i = 1; i < source.Length - 1; i++)
            {
                var p = source[i];
                float radius = Mathf.Min(12, Mathf.Min(p.DistanceTo(source[i - 1]), p.DistanceTo(source[i + 1])) * 0.3f);
                var entry = p.MoveToward(source[i - 1], radius);
                var exit = p.MoveToward(source[i + 1], radius);
                for (int j = 0; j <= 6; j++)
                {
                    float t = j / 6f;
                    points.Add(entry * (1-t)*(1-t) + p * 2*t*(1-t) + exit*t*t);
                }
            }
            points.Add(source[^1]);
            if (Array.IndexOf(Game.Cities, road.A) > Array.IndexOf(Game.Cities, road.B)) points.Reverse();
            roadCurves[RoadKey(road.A, road.B)] = points.ToArray();
        }
    }
    private Vector2[] GetRoadPoints(string from, string to)
    {
        PositionOf(from); // Refresh the network when the viewport or map changes.
        if (roadCurves.Count == 0) BuildRoadNetwork();
        var points = roadCurves[RoadKey(from, to)];
        return Array.IndexOf(Game.Cities, from) > Array.IndexOf(Game.Cities, to) ? points.Reverse().ToArray() : points;
    }
    private void DrawRoadNetwork()
    {
        GetRoadPoints(Model.World.Roads[0].A, Model.World.Roads[0].B);
        // Paint all banks first, then all road surfaces: junctions remain open, without crossing borders.
        var edges = Model.World.NetworkEdges;
        var adjacency = new Dictionary<string,List<RoadEdge>>();
        foreach (var edge in edges)
        {
            foreach (string node in new[] { edge.A, edge.B })
            { if (!adjacency.ContainsKey(node)) adjacency[node] = new(); adjacency[node].Add(edge); }
        }
        var used = new HashSet<RoadEdge>();
        var visibleRoads = new List<Vector2[]>();
        foreach (string start in adjacency.Keys.OrderBy(n => adjacency[n].Count == 2 && !Game.Cities.Contains(n) ? 1 : 0))
            foreach (var first in adjacency[start])
            {
                if (used.Contains(first)) continue;
                var chain = new List<string> { start };
                var edge = first; string node = start;
                while (used.Add(edge))
                {
                    node = edge.A == node ? edge.B : edge.A; chain.Add(node);
                    if (adjacency[node].Count != 2 || Game.Cities.Contains(node)) break;
                    var next = adjacency[node].FirstOrDefault(e => !used.Contains(e));
                    if (next is null) break;
                    edge = next;
                }
                visibleRoads.Add(RoutePolyline(chain));
            }
        roadJunctions.Clear();
        foreach (var node in adjacency.Where(pair => pair.Value.Count >= 3))
        { var p = Model.World.RoutePoint(node.Key); roadJunctions.Add(new Vector2(p.X,p.Y)); }
        foreach (var points in visibleRoads) DrawPolyline(points, new Color("927954"), 5.5f / Mathf.Max(zoom, 0.4f), zoom >= 0.5f);
        foreach (var p in roadJunctions) DrawCircle(p, 5, new Color("927954"));
        foreach (var points in visibleRoads) DrawPolyline(points, new Color("d8c39b"), 3.4f / Mathf.Max(zoom, 0.4f), zoom >= 0.5f);
        foreach (var p in roadJunctions) DrawCircle(p, 3.8f, new Color("d8c39b"));
    }
    private Vector2 PointOnRoad(string from, string to, float fraction)
    {
        var points = GetRoadPoints(from, to);
        float total = 0;
        for (int i = 1; i < points.Length; i++) total += points[i - 1].DistanceTo(points[i]);
        float remaining = total * Mathf.Clamp(fraction, 0, 1);
        for (int i = 1; i < points.Length; i++)
        {
            float length = points[i - 1].DistanceTo(points[i]);
            if (remaining <= length) return points[i - 1].Lerp(points[i], length == 0 ? 0 : remaining / length);
            remaining -= length;
        }
        return points[^1];
    }
    private void DrawTownSymbol(Vector2 center, float radius)
    {
        // Ink town plan: a perimeter wall, four districts and an open central square.
        DrawCircle(center, radius, new Color("dcc89e"));
        DrawArc(center, radius, 0, Mathf.Tau, 64, ink, 1.6f, true);
        DrawArc(center, radius - 2.5f, 0, Mathf.Tau, 64, faded, 0.8f, true);
        float scale = radius / 22;
        var blocks = new[] { new Rect2(-12,-12,8,7), new Rect2(4,-12,8,7),
            new Rect2(-13,4,9,7), new Rect2(4,4,8,9), new Rect2(-3,-16,5,7),
            new Rect2(-16,-3,7,5), new Rect2(9,-3,7,5), new Rect2(-3,9,5,7) };
        foreach (var block in blocks)
        {
            var rect = new Rect2(center + block.Position * scale, block.Size * scale);
            DrawRect(rect, new Color("aa8a5b")); DrawRect(rect, ink, false, 0.8f, true);
        }
        DrawRect(new Rect2(center - Vector2.One * 2.5f * scale, Vector2.One * 5 * scale), ink, false, 1, true);
    }
    private void DrawCityNames()
    {
        int screenFontSize = CompactMap ? 18 : 22;
        float fontSize = screenFontSize / zoom;
        var placed = new List<Rect2>();
        foreach (string city in Game.Cities)
        {
            var p = PositionOf(city);
            string name = Ru.Name(city);
            float width = (Letter.GetStringSize(name, fontSize: screenFontSize).X + 6) / zoom;
            float gap = (CompactMap ? 23 : 29) / zoom;
            Vector2[] offsets = { new(-width / 2, gap), new(-width / 2, -gap - fontSize - 5),
                new(gap, -fontSize / 2f), new(-width - gap, -fontSize / 2f),
                new(-width / 2, gap + 18), new(-width / 2, -gap - fontSize - 23) };
            Rect2 best = default; float bestScore = float.MaxValue;
            foreach (var offset in offsets)
            {
                var origin = (p + offset).Clamp(new Vector2(17, 40), new Vector2(WorldSize.X - width - 17, WorldSize.Y - fontSize - 40));
                var rect = new Rect2(origin, new Vector2(width, fontSize + 6));
                float score = placed.Count(r => r.Grow(2).Intersects(rect)) * 1000;
                score += Game.Cities.Count(c => rect.Grow(2).Intersects(new Rect2(PositionOf(c) - Vector2.One * (CompactMap ? 17 : 23), Vector2.One * (CompactMap ? 34 : 46)))) * 1000;
                score += offset.DistanceTo(offsets[0]);
                if (score < bestScore) { best = rect; bestScore = score; }
            }
            placed.Add(best);
            DrawSetTransform(Vector2.Zero);
            DrawString(Letter, ToScreen(best.Position + new Vector2(3 / zoom, fontSize + 1 / zoom)).Round(), name,
                fontSize: screenFontSize, modulate: ink);
            DrawSetTransform(Size / 2 * (1 - zoom) + pan, 0, Vector2.One * zoom);
        }
    }
    private void Mountain(Vector2 p, float height)
    {
        DrawPolyline(new[] { p + new Vector2(-height, 4), p + new Vector2(0, -height), p + new Vector2(height, 4) }, faded, 1, true);
        for (int i = 1; i < 5; i++) DrawLine(p + new Vector2(i, -height + i * 1.8f), p + new Vector2(i * 2, 3), new Color("b29d74"), 0.7f, true);
    }
    private void TreeMark(Vector2 p)
    {
        DrawLine(p, p + new Vector2(0, 9), faded, 1);
        DrawPolyline(new[] { p + new Vector2(-5, 3), p + new Vector2(0, -7), p + new Vector2(5, 3), p + new Vector2(-5, 3) }, faded, 0.8f, true);
    }
    private void Compass(Vector2 p)
    {
        DrawArc(p, 28, 0, Mathf.Tau, 64, faded, 1, true);
        DrawArc(p, 23, 0, Mathf.Tau, 64, faded, 0.7f, true);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Pi / 4 - Mathf.Pi / 2;
            var tip = p + Vector2.FromAngle(angle) * (i % 2 == 0 ? 31 : 20);
            var a = p + Vector2.FromAngle(angle - 0.65f) * 6;
            var b = p + Vector2.FromAngle(angle + 0.65f) * 6;
            DrawColoredPolygon(new[] { p, tip, a }, ink);
            DrawPolyline(new[] { p, tip, b, p }, ink, 0.8f, true);
        }
        DrawString(Letter, p + new Vector2(-6, -36), "С", fontSize: 15, modulate: ink);
        DrawString(Letter, p + new Vector2(-6, 47), "Ю", fontSize: 13, modulate: ink);
        DrawString(Letter, p + new Vector2(-44, 5), "З", fontSize: 13, modulate: ink);
        DrawString(Letter, p + new Vector2(36, 5), "В", fontSize: 13, modulate: ink);
    }
}
