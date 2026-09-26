namespace Tests.SameTrackDeparture;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.Utilities;
using Tests.InPlaceSplit;

// Regression fixture for #51: the final hop from a DepartureRoutingTask onto
// a scenario-fixed DepartureTask has no general Reverse-arc discovery of its
// own (#46 fixed every *other* leg; this one was deliberately left alone,
// see #51's own description). A single unit arrives and departs through the
// same gate, parking on the exact track it must later leave from with no
// intervening move -- so the DepartureRoutingTask's FromTrack and ToTrack
// are identical, and the resting side it arrived via always equals the side
// it must depart via (same gate, same connection.Track.GetSide computation
// for both ends). That forces a real in-place reversal.
//
// The park track (parkexit) is deliberately given Access.Both (a real
// neighbour on both sides, one of them a switch leading to the gate) rather
// than a single-sided dead end: PlanGraph's heuristic only ever *guesses*
// the routing target's side (Side.A) when Access is Both
// (Initial/SimpleHeuristic.cs's `Access == Side.Both ? Side.A : Access`
// ternary) -- for a single-sided track that guess is always physically
// forced and therefore always safe, which is why the older
// location_deadend_reversal.json/scenario_deadend_reversal.json fixture
// (also from #46) never exercised this gap: it happened to still discover
// its own required reversal mid-route regardless.
//
// The fix: computeDepartureRoute now routes this leg with
// RouteDestination.ReadyToDepart (Routing/RouteDestination.cs) targeting the
// DepartureTask's real, scenario-fixed DepartureSide directly, rather than
// move.ToSide's heuristic guess. RoutingGraph.ResolveEndpoints then targets
// the AB/BA "ready to depart" vertex instead of AA/BB "rest", so the search
// itself discovers the reversal (same mechanism #46 already uses for every
// other leg) instead of ComputeTime papering over it afterwards.
[Collection(PlanBuilding.Name)]
public class SameTrackDepartureTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void DepartingFromTheArrivalTrack_EmitsARealReverseAction()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_samesite_departure.json"),
            TestData("scenario_samesite_departure.json")
        );

        var tabuSearch = new TabuSearch(new Random(1), 0);
        var departureTask = tabuSearch.Graph.DepartureTasks.Single();
        var departureRouting = departureTask.GetDepartureRoutingTask();

        // Same gate for arrival and departure, same track for parking and
        // departure: there's nowhere to travel, only to turn around.
        Assert.Equal(departureRouting.FromTrack, departureRouting.ToTrack);

        // The reversal is real (same side in and out) -- Train.ReversalDuration
        // is nonzero for this fixture's train type precisely so a missing
        // action wouldn't be masked by a legitimately-zero duration.
        Assert.True(departureRouting.Train.ReversalDuration > 0);

        var route = departureRouting.GetRoutes().Single();

        // Fixed: the route itself now carries the reversal as a genuine
        // Reverse arc (RouteDestination.ReadyToDepart's AB/BA target isn't
        // reachable from the train's true resting vertex without one here),
        // rather than reporting Route.EmptyRoute and leaving ComputeTime to
        // silently pad the schedule instead.
        Assert.Equal(ArcType.Reverse, Assert.Single(route.Arcs).Type);

        // A single-arc route's Duration charges its own track's crossing
        // twice (once as the track being crossed, once more for the
        // reversal itself, per Route.ComputeDuration/BuildMoveActionsWith-
        // Reverses's terminal-reversal credit) plus the reversal itself --
        // there's no other piece to share either charge with.
        Time expectedDuration =
            2 * Settings.TrackCrossingTime + departureRouting.Train.ReversalDuration;
        Assert.Equal(expectedDuration, route.Duration);

        var plan = tabuSearch.Graph.ToPlan();
        Assert.NotNull(plan);

        // Fixed: a real Reverse action appears, spanning exactly the route's
        // own duration -- no more silent gap in the timeline.
        var reverseAction = Assert.Single(
            plan.Actions,
            a => a.TaskType.Predefined == PredefinedTaskType.Reverse
        );
        Assert.Equal(departureRouting.ToTrack.ID, reverseAction.Location);
        Assert.Equal(
            (ulong)expectedDuration,
            reverseAction.EndTime!.Value - reverseAction.StartTime!.Value
        );

        // Fixed: departurerouting.Start is pulled earlier by the route's own
        // (now correctly-costed) Duration alone -- no separate ad hoc
        // reversalduration term on top -- and the Reverse action's own end
        // lines up exactly with the scheduled departure time, with nothing
        // left unaccounted for.
        ulong scheduledTime = (ulong)departureTask.ScheduledTime;
        Assert.Equal(scheduledTime - (ulong)expectedDuration, (ulong)departureRouting.Start);
        Assert.Equal(scheduledTime, reverseAction.EndTime!.Value);
        Assert.NotNull(tabuSearch.Graph.Cost);
        Assert.Equal(0, tabuSearch.Graph.Cost.DepartureDelays);
    }
}
