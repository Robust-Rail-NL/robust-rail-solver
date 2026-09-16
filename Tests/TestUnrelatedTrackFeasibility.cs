// Covers #43: not just "later than ideal" (see TestUnrelatedTrackTiming) but a
// genuine feasibility flip. Same shape as that fixture -- "AAA" undergoes a
// long service and won't depart for a very long time; "BBB" is otherwise
// unrelated (no shared unit, no shared resource -- its service facility sits
// on AAA's own dead-end spur, isolated from BBB's route, so there is no
// routing/crossing cost either) and has a tight but genuinely achievable
// departure deadline.
//
// Verified empirically against the pre-fix ComputeTime (PlanGraph.cs as of
// commit 4d397f9, before #43's STN rewrite), same build otherwise, same
// seed: BBB's departure landed at 880s against its 550s deadline (floored by
// AAA's unrelated move, same mechanism as TestUnrelatedTrackTiming), tripping
// both ArrivalDelays and DepartureDelays and making the whole plan
// infeasible. With the fix, BBB departs at exactly 550s and the plan is
// feasible -- the same instance flips from failing to succeeding.
namespace Tests.UnrelatedTrackFeasibility;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Initial;
using Tests.InPlaceSplit;

[Collection(PlanBuilding.Name)]
public class UnrelatedTrackFeasibilityTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void AnUnrelatedTrainsLateDeparture_DoesNotMakeThisTrainsTightDeadlineInfeasible()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_simple_service_isolated_facility.json"),
            TestData("scenario_unrelated_tracks_tight_deadline.json")
        );
        var graph = SimpleHeuristic.Construct(new Random(1));
        var cost = graph.ComputeModel();

        var early = graph.DepartureTasks.Single(d => d.ScheduledTime == 550);

        Assert.Equal(0, cost.Crossings);
        Assert.Equal(0, cost.ArrivalDelays);
        Assert.Equal(0, cost.DepartureDelays);
        Assert.True(
            cost.IsFeasible,
            $"expected a feasible plan with BBB departing on its own 550s schedule, but "
                + $"Start={early.Start}, cost={cost}"
        );
    }
}
