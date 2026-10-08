namespace TradeCompany;

public sealed class TerrainArea
{
    public MapPoint Center { get; set; } = new();
    public int Radius { get; set; }
}
public sealed partial class WorldMap
{
    public List<TerrainArea> Mountains { get; set; } = [];
    public List<TerrainArea> Forests { get; set; } = [];
    public List<List<MapPoint>> Rivers { get; set; } = [];
    private const int Cell = 32;
    public static bool InHex(double x, double y, double radius)
    {
        x = Math.Abs(x); y = Math.Abs(y);
        return x <= radius && y <= radius*Math.Sqrt(3)/2 && x*Math.Sqrt(3)+y <= radius*Math.Sqrt(3);
    }
    public bool IsMountain(double x, double y) => Mountains.Any(a => InHex(x-a.Center.X,y-a.Center.Y,a.Radius+14));
    public bool IsForest(double x, double y) => Forests.Any(a => InHex(x-a.Center.X,y-a.Center.Y,a.Radius));
    private static double SegmentDistance(double x, double y, MapPoint a, MapPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / Math.Max(1, dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(y - a.Y - t * dy, 2));
    }
    private bool IsRiver(double x, double y) => Rivers.Any(r => Enumerable.Range(1, r.Count - 1).Any(i => SegmentDistance(x, y, r[i - 1], r[i]) < 22));
    private void GenerateTerrain()
    {
        if (Rivers.Count > 0) return;
        var random = new Random(Seed ^ 0x1462AB);
        for (int ridge = 0; ridge < 3; ridge++)
        {
            int x = random.Next(220, Width - 220), y = random.Next(220, Height - 220);
            for (int step = -2; step <= 2; step++)
            {
                var p = new MapPoint { X = Math.Clamp(x + step * 75, 90, Width - 90), Y = Math.Clamp(y + step * 45, 90, Height - 90) };
                int radius = random.Next(45, 70);
                if (Cities.Values.All(c => Distance(c, p) > radius + 100)) Mountains.Add(new TerrainArea { Center = p, Radius = radius });
            }
        }
        for (int i = 0; i < 10; i++)
            Forests.Add(new TerrainArea { Center = new MapPoint { X = random.Next(100, Width - 100), Y = random.Next(100, Height - 100) }, Radius = random.Next(90, 160) });
        for (int river = 0; river < 2; river++)
        {
            var points = new List<MapPoint>();
            double center = Width * (river == 0 ? 0.34 : 0.69), phase = random.NextDouble() * 6;
            for (int y = 0; y <= Height + 32; y += 32)
            {
                double x = center + Math.Sin(y / 210.0 + phase) * 90 + Math.Sin(y / 90.0 + phase) * 18;
                foreach (var city in Cities.Values.Where(c => Math.Abs(c.Y - y) < 110))
                {
                    double dx = x - city.X;
                    if (Math.Abs(dx) < 90) x += (dx >= 0 ? 1 : -1) * (90 - Math.Abs(dx)) * Math.Pow(1 - Math.Abs(city.Y - y) / 110.0, 2);
                }
                points.Add(new MapPoint { X = (int)x, Y = y });
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var smooth = new List<MapPoint> { points[0] };
                for (int i = 1; i < points.Count; i++)
                {
                    var a = points[i-1]; var b = points[i];
                    smooth.Add(new MapPoint { X = (3*a.X+b.X)/4, Y = (3*a.Y+b.Y)/4 });
                    smooth.Add(new MapPoint { X = (a.X+3*b.X)/4, Y = (a.Y+3*b.Y)/4 });
                }
                smooth.Add(points[^1]); points = smooth;
            }
            Rivers.Add(points);
        }
    }
    public void RebuildRoads()
    {
        GenerateTerrain(); Roads.Clear(); graphRoadCount = -1;
        int columns = Width / Cell + 1, rows = Height / Cell + 1;
        var cost = new double[columns * rows];
        for (int i = 0; i < cost.Length; i++)
        {
            int x = i % columns * Cell, y = i / columns * Cell;
            cost[i] = IsMountain(x, y) ? double.PositiveInfinity : (IsForest(x, y) ? 2.8 : 1) + (IsRiver(x, y) ? 12 : 0);
        }
        var used = new HashSet<(int, int)>();
        (int, int) Edge(int a, int b) => a < b ? (a, b) : (b, a);
        MapRoad Route(string a, string b)
        {
            int Node(MapPoint p) => Math.Clamp((int)Math.Round(p.Y / (double)Cell), 0, rows - 1) * columns + Math.Clamp((int)Math.Round(p.X / (double)Cell), 0, columns - 1);
            MapPoint Point(int i) => new() { X = i % columns * Cell, Y = i / columns * Cell };
            int start = Node(Cities[a]), goal = Node(Cities[b]);
            var distances = Enumerable.Repeat(double.PositiveInfinity, cost.Length).ToArray();
            var previous = Enumerable.Repeat(-1, cost.Length).ToArray();
            var queue = new PriorityQueue<int, double>(); distances[start] = 0; queue.Enqueue(start, 0);
            while (queue.TryDequeue(out int current, out double priority))
            {
                if (current == goal) break;
                var point = Point(current);
                if (priority > distances[current] + Distance(point, Point(goal)) * 0.65 + 0.001 && current != start) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = current % columns + dx, y = current / columns + dy;
                    if (x < 0 || y < 0 || x >= columns || y >= rows) continue;
                    int next = y * columns + x;
                    if (double.IsInfinity(cost[next]) || IsMountain((point.X + x * Cell) / 2.0, (point.Y + y * Cell) / 2.0)) continue;
                    double length = Cell * (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                    double candidate = distances[current] + length * (cost[current] + cost[next]) / 2 * (used.Contains(Edge(current, next)) ? 0.65 : 1);
                    if (candidate >= distances[next]) continue;
                    distances[next] = candidate; previous[next] = current;
                    queue.Enqueue(next, candidate + Distance(Point(next), Point(goal)) * 0.65);
                }
            }
            if (previous[goal] < 0) throw new InvalidOperationException("Terrain route is disconnected");
            var nodes = new List<int> { goal };
            while (nodes[^1] != start) nodes.Add(previous[nodes[^1]]);
            nodes.Reverse();
            for (int i = 1; i < nodes.Count; i++) used.Add(Edge(nodes[i - 1], nodes[i]));
            var path = nodes.Select(Point).ToList(); path[0] = Cities[a]; path[^1] = Cities[b];
            // Remove grid subdivisions; genuine bends remain, with no decorative detours.
            for (int i = path.Count - 2; i > 0; i--)
            {
                var p = path[i - 1]; var q = path[i]; var r = path[i + 1];
                if ((q.X - p.X) * (r.Y - q.Y) == (q.Y - p.Y) * (r.X - q.X)) path.RemoveAt(i);
            }
            var road = new MapRoad { A = a, B = b, Path = path };
            double days = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var p = path[i - 1]; var q = path[i];
                double length = Distance(p, q);
                int samples = Math.Max(1, (int)Math.Ceiling(length / 16));
                for (int sample = 0; sample < samples; sample++)
                {
                    double t = (sample + 0.5) / samples;
                    days += length / samples / 100 * (IsForest(p.X + (q.X-p.X)*t, p.Y + (q.Y-p.Y)*t) ? 1.5 : 1);
                }
                foreach (var river in Rivers) for (int j = 1; j < river.Count; j++)
                {
                    var r = river[j - 1]; var t = river[j];
                    double ax = q.X - p.X, ay = q.Y - p.Y, bx = t.X - r.X, by = t.Y - r.Y;
                    double cross = ax * by - ay * bx;
                    if (Math.Abs(cross) < 0.001) continue;
                    double u = ((r.X - p.X) * by - (r.Y - p.Y) * bx) / cross;
                    double v = ((r.X - p.X) * ay - (r.Y - p.Y) * ax) / cross;
                    if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                    var bridge = new MapPoint { X = (int)Math.Round(p.X + u * ax), Y = (int)Math.Round(p.Y + u * ay) };
                    if (road.Bridges.All(existing => Distance(existing, bridge) > 20)) road.Bridges.Add(bridge);
                }
            }
            road.Days = Math.Max(0.1m, Math.Round((decimal)days + road.Bridges.Count * 0.2m, 1));
            return road;
        }
        var connected = new HashSet<string> { Game.Cities[0] };
        while (connected.Count < Game.Cities.Length)
        {
            var edge = (from a in connected from b in Game.Cities where !connected.Contains(b)
                orderby Distance(Cities[a], Cities[b]) select (a, b)).First();
            Roads.Add(Route(edge.a, edge.b)); connected.Add(edge.b);
        }
        var candidates = (from a in Game.Cities from b in Game.Cities
            where Array.IndexOf(Game.Cities, a) < Array.IndexOf(Game.Cities, b)
            orderby Distance(Cities[a], Cities[b]) select (a, b)).ToArray();
        foreach (var (a, b) in candidates)
        {
            if (Roads.Count >= Game.Cities.Length + 3) break;
            if (AreNeighbors(a, b) || Roads.Count(r => r.A == a || r.B == a) >= 3 || Roads.Count(r => r.A == b || r.B == b) >= 3) continue;
            if (TravelDays(a, b) < (decimal)Distance(Cities[a], Cities[b]) / 100 * 1.6m) continue;
            Roads.Add(Route(a, b));
        }
        NetworkVersion = 4; graphRoadCount = -1;
    }
}
