// Covers #43: PlanGraph.ComputeTime used to thread one scalar "time" across the
// plan's single interleaved NextMove list, flooring a move's start by whatever
// task happened to precede it in that list -- even an unrelated train on an
// unrelated track. Train "AAA" here undergoes a long service and departs very
// late (90000s); train "BBB" is otherwise unrelated (no shared resource, no
// shared unit) and departs almost immediately (300s). Verified this test fails
// on the pre-fix ComputeTime: BBB's first move landed at 490s -- exactly
// AAA's unrelated service-routing move's End -- instead of near BBB's own 150s
// arrival.
namespace Tests.UnrelatedTrackTiming;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Initial;
using Tests.InPlaceSplit;

[Collection(PlanBuilding.Name)]
public class UnrelatedTrackTimingTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void AnUnrelatedTrainsLateDeparture_DoesNotDelayThisTrainsOwnEarlyDeparture()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_simple_service.json"),
            TestData("scenario_unrelated_tracks.json")
        );
        var graph = SimpleHeuristic.Construct(new Random(1));
        graph.Cost = graph.ComputeModel();

        Assert.Equal(2, graph.DepartureTasks.Length);
        var late = graph.DepartureTasks.Single(d => d.ScheduledTime == 90000);
        var early = graph.DepartureTasks.Single(d => d.ScheduledTime == 300);

        Assert.True(
            late.Start > 50000,
            $"expected AAA's departure to land near its own 90000s schedule, but Start={late.Start}"
        );

        // The crux of the fix: BBB has no relationship to AAA at all (different
        // train, different track, no shared resource), so its own dispatched
        // start must reflect only its own 300s schedule, not AAA's timescale.
        Assert.True(
            early.Start < 1000,
            $"expected BBB's departure to land near its own 300s schedule, unaffected by "
                + $"AAA's much later 90000s one, but Start={early.Start}"
        );

        // The exact spot the pre-fix code got wrong: BBB's very first move (its
        // arrival routing) has nothing to do with AAA at all, so it must start
        // near its own 150s arrival, not get floored by whatever unrelated task
        // happened to precede it in the plan's single interleaved move list --
        // in this fixture, AAA's own service-related move ending at 490s.
        var bbbArrival = graph.ArrivalTasks.Single(a => a.ScheduledTime == 150);
        Assert.True(
            bbbArrival.Next.Start < 200,
            $"expected BBB's first move to start near its own 150s arrival, but Start="
                + $"{bbbArrival.Next.Start} -- this is exactly the #43 symptom (floored by an "
                + "unrelated train's move ending at 490s)"
        );
    }
}
