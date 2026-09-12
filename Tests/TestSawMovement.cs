// Nested, not the top-level `Tests` namespace: TestPlan.cs declares its own
// Plan/Task classes directly in `Tests` (a deliberately narrow wire-format
// view, see TestPlan.cs), which would otherwise shadow the real types of
// the same name from ServiceSiteScheduling.Interchange used unqualified
// here (see TestUnifiedSchema.cs's matching comment).
namespace Tests.SawMovement;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using Tests.InPlaceSplit;

// Exercises PlanGraph.BuildMoveActionsWithSetbacks directly against a real
// Route containing a genuine ArcType.Reverse arc, obtained via
// RoutingGraph.ComputeRoute rather than the full heuristic search, which
// actively avoids reversals. Calls ComputeRoute directly to sidestep search
// entirely.
//
// Same collection as TestPlan/TestInPlaceSplit/TestSplitDuringMoveOrder:
// all of them mutate the shared static ProblemInstance.Current, which races
// under xUnit's default parallel collections otherwise.
[Collection(PlanBuilding.Name)]
public class SawMovementTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void ReversingRoute_EmitsAnExplicitSetbackAction()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_saw_movement.json"),
            TestData("scenario_saw_movement.json")
        );

        var graph = RoutingGraph.Construct();
        Track siding = ProblemInstance.Current.Tracks.First(t => t.PrettyName == "siding");
        Track approach = ProblemInstance.Current.Tracks.First(t => t.PrettyName == "approach");
        ShuntTrain train = new(
            ProblemInstance.Current.TrainUnits.Select(u => new ShuntTrainUnit(u))
        );

        // siding(B) -> approach(B): entering and leaving siding on the same
        // side requires reversing there first. Confirmed empirically to
        // produce Arcs=[Reverse, Track, Switch].
        Route route = graph.ComputeRoute([], train, siding, Side.B, approach, Side.B);
        Assert.NotEqual(Route.Invalid, route);
        Assert.Contains(route.Arcs, a => a.Type == ArcType.Reverse);
        foreach (var arc in route.Arcs)
            arc.ComputeCost(train);

        var planGraph = new TabuSearch(new Random(1), 0).Graph;
        var shuntingUnit = new ShuntingUnit(1);
        ulong startTime = 1000;
        ulong endTime = startTime + (ulong)(int)route.Duration;

        var actions = planGraph.BuildMoveActionsWithSetbacks(
            route.Arcs,
            startTime,
            endTime,
            siding,
            shuntingUnit
        );

        foreach (var a in actions)
            Console.WriteLine(
                $"{a.TaskType.Predefined} [{a.StartTime}-{a.EndTime}] loc={a.Location} resources=[{string.Join(",", a.Resources.Select(r => r.Id))}]"
            );

        var setbacks = actions
            .Where(a => a.TaskType.Predefined == PredefinedTaskType.Setback)
            .ToList();
        Assert.Single(setbacks);
        Assert.Equal(siding.ID, setbacks[0].Location);
        Assert.Equal((ulong)1000, setbacks[0].StartTime);
        // Non-zero: the train type declares a real reversal cost
        // (backNormTime/backAdditionTime), so the Setback must reflect it,
        // not stand in as a zero-width marker.
        Assert.True(setbacks[0].EndTime > setbacks[0].StartTime);

        // No Move should embed the reversal itself (the same track twice,
        // two apart, in one Move's own resource path).
        foreach (var move in actions.Where(a => a.TaskType.Predefined == PredefinedTaskType.Move))
        {
            var ids = move.Resources.Select(r => r.Id).ToList();
            for (int i = 0; i + 2 < ids.Count; i++)
                Assert.False(
                    ids[i] == ids[i + 2],
                    $"Move still embeds a saw: {string.Join(",", ids)}"
                );
        }

        // The overall span must match the caller-supplied endTime exactly -
        // that's the number the rest of PlanGraph already trusts and
        // advances its own time cursor by. Only *where* the internal
        // Move/Setback boundaries fall is approximate, never the total (see
        // BuildMoveActionsWithSetbacks's own comment).
        Assert.Equal(startTime, actions.Min(a => a.StartTime!.Value));
        Assert.Equal(endTime, actions.Max(a => a.EndTime!.Value));
    }
}
