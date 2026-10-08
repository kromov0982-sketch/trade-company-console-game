using Godot;
using TradeCompany;
using System.Text.Json;

public partial class Main
{
    public void TerrainTest()
    {
        try
        {
            int bridgeCount = 0;
            for (int sample = 0; sample < 8; sample++)
            {
                var map = WorldMap.Generate();
                if (map.Mountains.Count == 0 || map.Forests.Count != 10 || map.Rivers.Count != 2)
                    throw new Exception("Missing terrain");
                if (map.Roads.Count < 23 || map.Roads.Count > 27) throw new Exception("Road network must remain sparse");
                foreach (string city in Game.Cities)
                    if (map.FindRoute("oakwood", city).Last() != city) throw new Exception("Unreachable city");
                foreach (var road in map.Roads)
                {
                    if (road.Path.Count < 2 || road.Path[0].X != map.Cities[road.A].X || road.Path[^1].Y != map.Cities[road.B].Y)
                        throw new Exception("Road endpoints");
                    for (int i = 1; i < road.Path.Count; i++)
                    {
                        var a = road.Path[i-1]; var b = road.Path[i];
                        foreach (var river in map.Rivers) for (int r = 1; r < river.Count; r++)
                        {
                            var c = river[r-1]; var d = river[r];
                            double ax = b.X-a.X, ay = b.Y-a.Y, bx = d.X-c.X, by = d.Y-c.Y;
                            double cross = ax*by-ay*bx;
                            if (Math.Abs(cross) < 0.001) continue;
                            double u = ((c.X-a.X)*by-(c.Y-a.Y)*bx)/cross;
                            double v = ((c.X-a.X)*ay-(c.Y-a.Y)*ax)/cross;
                            if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                            double x = a.X+u*ax, y = a.Y+u*ay;
                            if (!road.Bridges.Any(p => Math.Pow(p.X-x,2)+Math.Pow(p.Y-y,2) <= 22*22))
                                throw new Exception("A river crossing has no bridge");
                        }
                        int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2)) / 4));
                        for (int j = 0; j <= steps; j++)
                        {
                            double t = j / (double)steps, x = a.X + (b.X-a.X)*t, y = a.Y + (b.Y-a.Y)*t;
                            if (map.Mountains.Any(m => WorldMap.InHex(x-m.Center.X,y-m.Center.Y,m.Radius)))
                                throw new Exception("Road crosses mountain");
                        }
                    }
                    bridgeCount += road.Bridges.Count;
                }
                string json = JsonSerializer.Serialize(map);
                var restored = JsonSerializer.Deserialize<WorldMap>(json)!;
                if (JsonSerializer.Serialize(restored) != json) throw new Exception("Terrain save round trip");
                map.RebuildRoads();
                if (JsonSerializer.Serialize(map) != json) throw new Exception("Terrain generation must be deterministic");
            }
            if (bridgeCount == 0) throw new Exception("River crossings need bridges");
            GD.Print($"TERRAIN_OK: 8 maps, mountain avoidance, sparse connected roads, {bridgeCount} bridges, stable saves");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
