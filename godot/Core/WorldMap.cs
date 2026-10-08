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
    public decimal Days { get; set; }
    public List<MapPoint> Path { get; set; } = [];
    public List<MapPoint> Bridges { get; set; } = [];
}

public sealed record MapMarker(char Symbol, Wagon Wagon);

public sealed partial class WorldMap
{
    public int Seed { get; set; }
    public int NetworkVersion { get; set; }
    public int Width { get; set; } = 2400;
    public int Height { get; set; } = 1600;
    public Dictionary<string, MapPoint> Cities { get; set; } = new();
    public List<MapRoad> Roads { get; set; } = [];

    public static WorldMap Generate()
    {
        int seed = Random.Shared.Next(1, int.MaxValue);
        var map = new WorldMap { Seed = seed };
        map.ExpandCities();
        map.RebuildRoads();
        return map;
    }

    public void ExpandCities()
    {
        var random = new Random(Seed);
        foreach (string city in Game.Cities.Where(c => !Cities.ContainsKey(c)))
        {
            // Bounded farthest-candidate sampling avoids crowding and endless rejection loops.
            var candidates = Enumerable.Range(0, 120).Select(_ => new MapPoint
                { X = random.Next(120, Width - 120), Y = random.Next(120, Height - 120) });
            candidates = candidates.Where(p => Mountains.All(m => Distance(p,m.Center) > m.Radius+100) && !IsRiver(p.X,p.Y));
            Cities[city] = candidates.MaxBy(p => Cities.Count == 0 ? random.NextDouble() : Cities.Values.Min(other => Distance(p, other)))!;
        }
    }

    public decimal TravelDays(string from, string to)
    {
        var route = FindRoute(from, to);
        decimal total = 0;
        for (int i = 0; i < route.Count - 1; i++) total += RouteEdgeDays(route[i], route[i + 1]);
        return total;
    }
    public bool AreNeighbors(string a, string b) => Roads.Any(r => r.A == a && r.B == b || r.A == b && r.B == a);

    public List<string> FindRoute(string from, string to) => FindGraphRoute(from, to);

    public string[] Render(int requestedWidth, int requestedHeight, IEnumerable<MapMarker>? markers = null)
    {
        int width = Math.Clamp(requestedWidth, 30, 140);
        int height = Math.Clamp(requestedHeight, 9, 32);
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int value = Hash(x, y, Seed) % 100;
                canvas[y, x] = value < 6 ? '≈' : value < 12 ? '▲' : value < 19 ? '♣' : ' ';
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
        if (markers is not null)
            foreach (var marker in markers) PlaceMarker(canvas, marker, width, height);
        return Enumerable.Range(0, height).Select(y => new string(Enumerable.Range(0, width).Select(x => canvas[y, x]).ToArray())).ToArray();
    }

    public MapPoint ScreenPosition(string city, int width, int height) =>
        Screen(Cities[Game.CityKey(city)], Math.Clamp(width, 30, 140), Math.Clamp(height, 9, 32));

    private void PlaceMarker(char[,] canvas, MapMarker marker, int width, int height)
    {
        var wagon = marker.Wagon;
        MapPoint position;
        if (wagon.Destination is null)
        {
            position = Screen(Cities[wagon.City], width, height);
            int adjacent = position.X + 1 < width ? position.X + 1 : position.X - 1;
            position = new MapPoint { X = adjacent, Y = position.Y };
        }
        else
        {
            var route = wagon.Route is { Count: > 1 } ? wagon.Route : FindRoute(wagon.City, wagon.Destination);
            decimal total = wagon.TotalDays > 0 ? wagon.TotalDays : TravelDays(wagon.City, wagon.Destination);
            decimal travelled = Math.Clamp(total - wagon.DaysLeft, 0, total);
            position = Screen(RoutePoint(route[0]), width, height);
            for (int i = 0; i < route.Count - 1; i++)
            {
                decimal segment = RouteEdgeDays(route[i], route[i + 1]);
                if (travelled <= segment)
                {
                    var a = Screen(RoutePoint(route[i]), width, height);
                    var b = Screen(RoutePoint(route[i + 1]), width, height);
                    decimal part = segment == 0 ? 1 : travelled / segment;
                    position = new MapPoint
                    {
                        X = (int)Math.Round(a.X + (b.X - a.X) * part),
                        Y = (int)Math.Round(a.Y + (b.Y - a.Y) * part)
                    };
                    break;
                }
                travelled -= segment;
            }
        }
        int markerY = Math.Clamp(position.Y, 0, height - 1), markerX = Math.Clamp(position.X, 0, width - 1);
        char occupied = canvas[markerY, markerX];
        canvas[markerY, markerX] = (occupied is 'A' or 'B' or '@') && occupied != marker.Symbol ? '@' : marker.Symbol;
    }

    private MapRoad RoadBetween(string a, string b) => Roads.First(r => r.A == a && r.B == b || r.A == b && r.B == a);

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
            canvas[y0, x0] = '·';
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private static double Distance(MapPoint a, MapPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static int Hash(int x, int y, int seed) => (int)((uint)(x * 73856093 ^ y * 19349663 ^ seed) % 100u);
}
