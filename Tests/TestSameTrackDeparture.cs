namespace Tests.SameTrackDeparture;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.LocalSearch;
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
// This test documents *current* (buggy) behaviour, not desired behaviour:
// RoutingGraph.ComputeRoute's origin==destination check (same track, same
// side) returns Route.EmptyRoute -- zero duration, zero arcs -- because
// FromTrack and ToTrack resolve to the exact same vertex. There is
// therefore no Route.Arcs for a Reverse to live in, and PlanGraph.ToPlan()
// only ever emits a Reverse action by inspecting a route's arcs. The
// reversal instead only shows up as silent schedule padding in
// PlanGraph.ComputeTime (departurerouting.Start pulled earlier by
// Train.ReversalDuration, with no Settings.TrackCrossingTime and no
// action to represent it) -- exactly the gap #51 describes. Once fixed,
// this test's assertions should flip: a real Reverse action should appear,
// and the route itself should carry the reversal instead of ComputeTime
// papering over it.
[Collection(PlanBuilding.Name)]
public class SameTrackDepartureTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void DepartingFromTheArrivalTrack_HidesItsReversalInsteadOfEmittingOne()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_samesite_departure.json"),
            TestData("scenario_samesite_departure.json")
        );

        var tabuSearch = new TabuSearch(new Random(1), 0);
        var departureTask = tabuSearch.Graph.DepartureTasks.Single();
        var departureRouting = departureTask.GetDepartureRoutingTask();

        // Same gate for arrival and departure, same track for parking and
        // departure: the route has nowhere to travel.
        Assert.Equal(departureRouting.FromTrack, departureRouting.ToTrack);

        var route = departureRouting.GetRoutes().Single();
        Assert.Equal((Time)0, route.Duration);
        Assert.Empty(route.Arcs);

        // The reversal is real (same side in and out) -- Train.ReversalDuration
        // is nonzero for this fixture's train type precisely so the missing
        // action isn't masked by a legitimately-zero duration.
        Assert.Equal((Side?)departureTask.DepartureSide, departureRouting.ToSide);
        Assert.True(departureRouting.Train.ReversalDuration > 0);

        var plan = tabuSearch.Graph.ToPlan();
        Assert.NotNull(plan);

        // Bug: no Reverse action anywhere in the plan, despite the reversal
        // being physically necessary.
        Assert.DoesNotContain(
            plan.Actions,
            a => a.TaskType.Predefined == PredefinedTaskType.Reverse
        );

        // Bug: departurerouting.Start is pulled earlier than the schedule by
        // exactly Train.ReversalDuration (PlanGraph.ComputeTime's ad hoc
        // check), padding for a reversal no action ever covers -- a gap in
        // the timeline that both the last real action's end and the
        // scheduled departure time can be seen straddling.
        ulong scheduledTime = (ulong)departureTask.ScheduledTime;
        ulong expectedStart = scheduledTime - (ulong)departureRouting.Train.ReversalDuration;
        Assert.Equal(expectedStart, (ulong)departureRouting.Start);
        Assert.Equal(
            expectedStart,
            plan.Actions.Where(a => a.EndTime < scheduledTime).Max(a => a.EndTime)!.Value
        );
    }
}
