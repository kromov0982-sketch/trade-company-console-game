namespace TradeCompany;

public sealed class MapPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class MapRoad
{
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public int Days { get; set; }
}

public sealed class WorldMap
{
    public int Seed { get; set; }
    public Dictionary<string, MapPoint> Cities { get; set; } = new();
    public List<MapRoad> Roads { get; set; } = [];

    public static WorldMap Generate()
    {
        int seed = Random.Shared.Next(1, int.MaxValue);
        var random = new Random(seed);
        var map = new WorldMap { Seed = seed };
        foreach (string city in Game.Cities)
        {
            MapPoint point;
            do
            {
                point = new MapPoint { X = random.Next(70, 931), Y = random.Next(90, 911) };
            }
            while (map.Cities.Values.Any(p => Distance(p, point) < 210));
            map.Cities[city] = point;
        }

        var order = Game.Cities.OrderBy(_ => random.Next()).ToList();
        var connected = new List<string> { order[0] };
        foreach (string city in order.Skip(1))
        {
            string nearest = connected.MinBy(c => Distance(map.Cities[c], map.Cities[city]))!;
            map.AddRoad(city, nearest);
            connected.Add(city);
        }
        if (!map.Roads.Any(r => r.A == "oakwood" && r.B == "crossroads" || r.A == "crossroads" && r.B == "oakwood"))
            map.AddRoad("oakwood", "crossroads");
        while (map.Roads.Count < Game.Cities.Length + 3)
        {
            string a = Game.Cities[random.Next(Game.Cities.Length)];
            string b = Game.Cities[random.Next(Game.Cities.Length)];
            if (a != b && !map.Roads.Any(r => r.A == a && r.B == b || r.A == b && r.B == a)) map.AddRoad(a, b);
        }
        return map;
    }

    private void AddRoad(string a, string b)
    {
        int days = Math.Max(1, (int)Math.Ceiling(Distance(Cities[a], Cities[b]) / 170.0));
        Roads.Add(new MapRoad { A = a, B = b, Days = days });
    }

    public int TravelDays(string from, string to)
    {
        var distances = Game.Cities.ToDictionary(c => c, _ => int.MaxValue);
        distances[from] = 0;
        var remaining = new HashSet<string>(Game.Cities);
        while (remaining.Count > 0)
        {
            string current = remaining.MinBy(c => distances[c])!;
            if (distances[current] == int.MaxValue) break;
            remaining.Remove(current);
            if (current == to) return distances[current];
            foreach (var road in Roads.Where(r => r.A == current || r.B == current))
            {
                string next = road.A == current ? road.B : road.A;
                if (remaining.Contains(next)) distances[next] = Math.Min(distances[next], distances[current] + road.Days);
            }
        }
        throw new InvalidOperationException("Между городами нет дороги.");
    }

    public string[] Render(int requestedWidth, int requestedHeight)
    {
        int width = Math.Clamp(requestedWidth, 30, 72);
        int height = Math.Clamp(requestedHeight, 9, 19);
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int value = Hash(x, y, Seed) % 100;
                canvas[y, x] = value < 7 ? '~' : value < 16 ? '^' : value < 24 ? '*' : '.';
            }

        foreach (var road in Roads)
        {
            var a = Screen(Cities[road.A], width, height);
            var b = Screen(Cities[road.B], width, height);
            DrawLine(canvas, a.X, a.Y, b.X, b.Y);
        }
        for (int i = 0; i < Game.Cities.Length; i++)
        {
            var p = Screen(Cities[Game.Cities[i]], width, height);
            canvas[p.Y, p.X] = (char)('1' + i);
        }
        return Enumerable.Range(0, height).Select(y => new string(Enumerable.Range(0, width).Select(x => canvas[y, x]).ToArray())).ToArray();
    }

    private static MapPoint Screen(MapPoint p, int width, int height) => new()
    {
        X = Math.Clamp(p.X * (width - 1) / 1000, 1, width - 2),
        Y = Math.Clamp(p.Y * (height - 1) / 1000, 1, height - 2)
    };

    private static void DrawLine(char[,] canvas, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            canvas[y0, x0] = '#';
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private static double Distance(MapPoint a, MapPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static int Hash(int x, int y, int seed) => (int)((uint)(x * 73856093 ^ y * 19349663 ^ seed) % 100u);
}
