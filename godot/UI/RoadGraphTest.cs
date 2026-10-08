using Godot;
using TradeCompany;

public partial class Main
{
    public void RoadGraphTest()
    {
        try
        {
            // A-D and D-B share a trunk. A-B must turn at the junction, not visit D first.
            var map = new WorldMap { Cities = new() {
                ["oakwood"] = new() { X=100,Y=100 }, ["crossroads"] = new() { X=300,Y=100 },
                ["riverport"] = new() { X=200,Y=300 } } };
            map.Roads = [
                new() { A="oakwood",B="riverport",Days=3,Path=[map.Cities["oakwood"],new(){X=200,Y=100},map.Cities["riverport"]] },
                new() { A="riverport",B="crossroads",Days=3,Path=[map.Cities["riverport"],new(){X=200,Y=100},map.Cities["crossroads"]] }
            ];
            var route = map.FindRoute("oakwood","crossroads");
            if (route.Contains("riverport") || route.Count != 3 || map.TravelDays("oakwood","crossroads") != 2)
                throw new Exception("Route must turn on the shared road without detouring to another town");
            if (map.NetworkEdges.Count != 3) throw new Exception("Shared trunk must be one edge");
            // Two lines cross in their interiors, with no pre-existing waypoint at the intersection.
            map = new WorldMap { Cities = new() {
                ["oakwood"]=new(){X=100,Y=200}, ["crossroads"]=new(){X=300,Y=200},
                ["riverport"]=new(){X=200,Y=100}, ["ironbay"]=new(){X=200,Y=300} } };
            map.Roads = [
                new(){A="oakwood",B="crossroads",Path=[map.Cities["oakwood"],map.Cities["crossroads"]]},
                new(){A="riverport",B="ironbay",Path=[map.Cities["riverport"],map.Cities["ironbay"]]}
            ];
            route = map.FindRoute("oakwood","ironbay");
            if (route.Count != 3 || map.NetworkEdges.Count != 4 || map.TravelDays("oakwood","ironbay") != 2)
                throw new Exception("Crossing road segments must create a usable intersection");
            if (!route.SequenceEqual(map.FindRoute("ironbay","oakwood").AsEnumerable().Reverse()))
                throw new Exception("Road routes must work in both directions");
            GD.Print("ROAD_GRAPH_OK: turns at shared trunks, crossing interiors, deduplication and shortest cost");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
