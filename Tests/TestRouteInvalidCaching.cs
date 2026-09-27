namespace Tests.RouteInvalidCaching;

using ServiceSiteScheduling;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Parking;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.TrackParts;
using Tests.InPlaceSplit;

// Regression fixture for #66: RoutingGraph.ComputeRoute caches every route
// Dijkstra returns (Routing/Graph.cs's Storage), including Route.Invalid
// itself when no path exists at all. A cache *hit* unconditionally copies
// the stored route via `new Route(train, route)` and recomputes its
// Duration - correct for a real cached route (a different train needs its
// own Reverse-arc costs), but wrong for a cached Route.Invalid: the copy
// inherits its empty Tracks/Arcs plus its penalty TotalSwitches, and
// ComputeDuration() turns that into a nonzero Duration from nonsense
// inputs. Worse, the copy no longer reference-equals the Route.Invalid
// singleton, so #65's `ReferenceEquals(route, Route.Invalid)` guard can't
// recognize it - it used to flow through as if it were a normal (if empty)
// route and crash PlanGraph.ToPlan() with an IndexOutOfRangeException on
// route.Tracks[0].
//
// Uses a location with two disconnected components (`location_
// disconnected_island.json`, sharing ids 0-6 with location_samesite_
// departure.json so the unrelated scenario_samesite_departure.json fixture
// still parses and solves unchanged, plus an isolated extra island at ids
// 7-9) so a route between them is genuinely, deterministically unroutable -
// unlike #65, which could force Route.Invalid directly via AddRoute, #66 is
// specifically about the cache, so it needs a real Dijkstra failure to
// actually get cached.
[Collection(PlanBuilding.Name)]
public class RouteInvalidCachingTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void CachedRouteInvalid_IsReturnedAsTheSingletonOnASecondLookup()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_disconnected_island.json"),
            TestData("scenario_samesite_departure.json")
        );

        var tabuSearch = new TabuSearch(new Random(1), 0);
        var train = tabuSearch.Graph.DepartureTasks.Single().Train;

        Track parkexit = ProblemInstance.Current.Tracks.Single(t => t.PrettyName == "parkexit");
        Track islandPark = ProblemInstance.Current.Tracks.Single(t =>
            t.PrettyName == "island_park"
        );

        // First call: genuine cache miss, Dijkstra actually runs and finds
        // no path between the two disconnected components - and that
        // failure gets cached.
        var first = tabuSearch.Graph.RoutingGraph.ComputeRoute(
            Array.Empty<TrackOccupation>(),
            train,
            parkexit,
            Side.A,
            islandPark,
            Side.A
        );
        Assert.Same(Route.Invalid, first);

        // Second call, same (from, to, sides, occupancy state): a cache
        // hit. Before the fix, this returned a *copy* - same penalty
        // Crossings/TotalSwitches as Route.Invalid, but a fresh object with
        // Tracks=[] and a nonzero recomputed Duration, no longer
        // ReferenceEquals to the singleton.
        var second = tabuSearch.Graph.RoutingGraph.ComputeRoute(
            Array.Empty<TrackOccupation>(),
            train,
            parkexit,
            Side.A,
            islandPark,
            Side.A
        );
        Assert.Same(Route.Invalid, second);
    }
}
