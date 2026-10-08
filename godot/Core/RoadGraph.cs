using System.Text.Json.Serialization;

namespace TradeCompany;

public sealed record RoadEdge(string A, string B, MapPoint Start, MapPoint End, decimal Days);
public sealed partial class WorldMap
{
    private int graphRoadCount = -1;
    private Dictionary<string, MapPoint> graphPoints = new();
    private Dictionary<string, List<RoadEdge>> graphLinks = new();
    private List<RoadEdge> graphEdges = [];
    [JsonIgnore] public IReadOnlyList<RoadEdge> NetworkEdges { get { EnsureRoadGraph(); return graphEdges; } }
    public MapPoint RoutePoint(string node) { EnsureRoadGraph(); return graphPoints[node]; }
    public decimal RouteEdgeDays(string a, string b) { EnsureRoadGraph(); return graphLinks[a].First(e => e.A == b || e.B == b).Days; }
    private void EnsureRoadGraph()
    {
        if (graphRoadCount == Roads.Count) return;
        graphRoadCount = Roads.Count; graphPoints = new(); graphLinks = new(); graphEdges = [];
        string Key(MapPoint p)
        {
            string? city = Cities.FirstOrDefault(c => Distance(c.Value, p) <= 1.5).Key;
            string key = city ?? $"junction:{p.X}:{p.Y}";
            if (!graphPoints.ContainsKey(key)) { graphPoints[key] = city is null ? p : Cities[city]; graphLinks[key] = []; }
            return key;
        }
        foreach (var city in Cities) Key(city.Value);
        var segments = new List<(MapPoint a, MapPoint b, List<MapPoint> cuts)>();
        foreach (var road in Roads)
        {
            var path = road.Path.Count >= 2 ? road.Path : new List<MapPoint> { Cities[road.A], Cities[road.B] };
            for (int i = 1; i < path.Count; i++)
                if (Distance(path[i-1],path[i]) > 0.1) segments.Add((path[i-1], path[i], new List<MapPoint> { path[i-1], path[i] }));
        }
        bool On(MapPoint p, MapPoint a, MapPoint b) => SegmentDistance(p.X,p.Y,a,b) < 0.75;
        for (int i = 0; i < segments.Count; i++) for (int j = i+1; j < segments.Count; j++)
        {
            var s = segments[i]; var t = segments[j];
            if (Math.Max(s.a.X,s.b.X)+1 < Math.Min(t.a.X,t.b.X) || Math.Max(t.a.X,t.b.X)+1 < Math.Min(s.a.X,s.b.X)
                || Math.Max(s.a.Y,s.b.Y)+1 < Math.Min(t.a.Y,t.b.Y) || Math.Max(t.a.Y,t.b.Y)+1 < Math.Min(s.a.Y,s.b.Y)) continue;
            foreach (var p in new[] { s.a, s.b }) if (On(p,t.a,t.b)) t.cuts.Add(p);
            foreach (var p in new[] { t.a, t.b }) if (On(p,s.a,s.b)) s.cuts.Add(p);
            double ax = s.b.X-s.a.X, ay = s.b.Y-s.a.Y, bx = t.b.X-t.a.X, by = t.b.Y-t.a.Y;
            double cross = ax*by-ay*bx;
            if (Math.Abs(cross) < 0.001) continue;
            double u = ((t.a.X-s.a.X)*by-(t.a.Y-s.a.Y)*bx)/cross;
            double v = ((t.a.X-s.a.X)*ay-(t.a.Y-s.a.Y)*ax)/cross;
            if (u < 0 || u > 1 || v < 0 || v > 1) continue;
            var point = new MapPoint { X = (int)Math.Round(s.a.X+u*ax), Y = (int)Math.Round(s.a.Y+u*ay) };
            s.cuts.Add(point); t.cuts.Add(point);
        }
        var bridges = Roads.SelectMany(r => r.Bridges).ToArray();
        var existing = new HashSet<string>();
        foreach (var segment in segments)
        {
            var points = segment.cuts.DistinctBy(p => (p.X,p.Y)).OrderBy(p => Distance(segment.a,p)).ToArray();
            for (int i = 1; i < points.Length; i++)
            {
                string a = Key(points[i-1]), b = Key(points[i]);
                if (a == b) continue;
                string edgeKey = string.CompareOrdinal(a,b) < 0 ? a+"/"+b : b+"/"+a;
                if (!existing.Add(edgeKey)) continue;
                var p = graphPoints[a]; var q = graphPoints[b];
                double length = Distance(p,q), days = 0;
                int samples = Math.Max(1,(int)Math.Ceiling(length/16));
                for (int k = 0; k < samples; k++)
                {
                    double f = (k+0.5)/samples;
                    days += length / samples / 100 * (IsForest(p.X+(q.X-p.X)*f,p.Y+(q.Y-p.Y)*f) ? 1.5 : 1);
                }
                // Split crossings share one edge, and the bridge toll is charged only on that edge.
                days += bridges.DistinctBy(x => (x.X,x.Y)).Count(x => SegmentDistance(x.X,x.Y,p,q) < 2 && Distance(x,q) > 1) * 0.2;
                var edge = new RoadEdge(a,b,p,q,Math.Max(0.0001m,(decimal)days));
                graphEdges.Add(edge); graphLinks[a].Add(edge); graphLinks[b].Add(edge);
            }
        }
    }
    private List<string> FindGraphRoute(string from, string to)
    {
        EnsureRoadGraph();
        var distance = new Dictionary<string,decimal> { [from] = 0 };
        var previous = new Dictionary<string,string>();
        var queue = new PriorityQueue<string,decimal>(); queue.Enqueue(from,0);
        while (queue.TryDequeue(out string? current,out decimal cost))
        {
            if (distance[current] != cost) continue;
            if (current == to) break;
            foreach (var edge in graphLinks[current])
            {
                string next = edge.A == current ? edge.B : edge.A;
                decimal candidate = cost + edge.Days;
                if (distance.TryGetValue(next,out var known) && known <= candidate) continue;
                distance[next] = candidate; previous[next] = current; queue.Enqueue(next,candidate);
            }
        }
        if (!distance.ContainsKey(to)) throw new InvalidOperationException("Между городами нет дороги.");
        var route = new List<string> { to };
        while (route[^1] != from) route.Add(previous[route[^1]]);
        route.Reverse(); return route;
    }
}
