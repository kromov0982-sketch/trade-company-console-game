using Godot;
using TradeCompany;

public partial class Atlas
{
    private void DrawTerrain()
    {
        var random = new Random(Model.World.Seed ^ 0x654C);
        foreach (var forest in Model.World.Forests)
        {
            var center = new Vector2(forest.Center.X, forest.Center.Y);
            var outline = Enumerable.Range(0, 6).Select(i => center + Vector2.FromAngle(i * Mathf.Tau / 6) * forest.Radius).ToArray();
            DrawColoredPolygon(outline, new Color(0.27f, 0.36f, 0.22f, 0.15f));
            DrawPolyline(outline.Append(outline[0]).ToArray(), new Color(0.27f,0.36f,0.22f,0.3f),1,true);
            if (zoom < 0.35f) continue;
            for (int y = -forest.Radius; y <= forest.Radius; y += 28)
                for (int x = -forest.Radius; x <= forest.Radius; x += 27)
                {
                    var p = center + new Vector2(x + random.Next(-8, 9), y + random.Next(-8, 9));
                    if (!WorldMap.InHex(p.X-center.X,p.Y-center.Y,forest.Radius) || Game.Cities.Any(c => PositionOf(c).DistanceTo(p) < 35)) continue;
                    TreeMark(p);
                }
        }
        foreach (var mountain in Model.World.Mountains)
        {
            var center = new Vector2(mountain.Center.X, mountain.Center.Y);
            var hex = Enumerable.Range(0,6).Select(i => center + Vector2.FromAngle(i*Mathf.Tau/6)*mountain.Radius).ToArray();
            DrawColoredPolygon(hex, new Color(0.42f,0.32f,0.20f,0.14f));
            DrawPolyline(hex.Append(hex[0]).ToArray(), new Color(0.42f,0.32f,0.20f,0.3f),1,true);
            for (int y = -mountain.Radius + 15; y < mountain.Radius; y += 26)
                for (int x = -mountain.Radius + 10; x < mountain.Radius; x += 30)
                {
                    var p = center + new Vector2(x + random.Next(-7, 8), y);
                    if (WorldMap.InHex(p.X-center.X,p.Y-center.Y,mountain.Radius-8)) Mountain(p, random.Next(16, 28));
                }
        }
        foreach (var river in Model.World.Rivers)
        {
            var points = river.Select(p => new Vector2(p.X, p.Y)).ToArray();
            DrawPolyline(points, new Color("6f8177"), 19, true);
            DrawPolyline(points, new Color("9fb4aa"), 14, true);
            DrawPolyline(points, new Color(0.85f, 0.9f, 0.78f, 0.55f), 2, true);
        }
    }
    private void DrawBridges()
    {
        var placed = new List<Vector2>();
        foreach (var road in Model.World.Roads)
            foreach (var bridge in road.Bridges)
            {
                var p = new Vector2(bridge.X, bridge.Y);
                if (placed.Any(q => q.DistanceTo(p) < 24)) continue;
                var path = GetRoadPoints(road.A, road.B);
                int segment = Enumerable.Range(1, path.Length - 1).MinBy(i => Geometry2D.GetClosestPointToSegment(p, path[i - 1], path[i]).DistanceTo(p));
                p = Geometry2D.GetClosestPointToSegment(p, path[segment - 1], path[segment]);
                var tangent = (path[segment] - path[segment - 1]).Normalized();
                var normal = tangent.Orthogonal();
                placed.Add(p);
                DrawLine(p - tangent * 17, p + tangent * 17, new Color("755b3e"), 12, true);
                DrawLine(p - tangent * 17, p + tangent * 17, new Color("cbb086"), 8, true);
                for (int i = -14; i <= 14; i += 5)
                    DrawLine(p + tangent*i - normal*4, p + tangent*i + normal*4, ink, 0.8f, true);
                DrawLine(p - tangent*19 - normal*6, p + tangent*19 - normal*6, ink, 1.4f, true);
                DrawLine(p - tangent*19 + normal*6, p + tangent*19 + normal*6, ink, 1.4f, true);
            }
    }
}
