// A nested namespace with its own scoped usings below (not the top-level
// `Tests` namespace): this used to be required to dodge a real shadowing
// hazard - TestPlan.cs declared its own Plan/TaskType/PredefinedTaskType
// directly in `Tests`, which would otherwise have hidden the wire-format
// types of the same name used here (see TestUnifiedSchema.cs's matching
// comment). That local copy is gone now, so the hazard no longer applies;
// left nested regardless; usings still placed after the namespace line as a
// harmless habit, not because it's load-bearing anymore.
namespace Tests.SawMovement;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using Tests.InPlaceSplit;

// Exercises PlanGraph.BuildMoveActionsWithSetbacks (internal - see
// AssemblyInfo.cs's InternalsVisibleTo) directly against a real Route
// containing a genuine ArcType.Reverse arc, obtained via
// RoutingGraph.ComputeRoute rather than the full heuristic search: the
// search actively avoids reversals (they cost more), and arrival/departure
// are separate MoveTasks that don't appear to carry a reversal requirement
// across their boundary in this model, so relying on the search to produce
// one organically is unreliable. Calling ComputeRoute directly with
// departure/arrival sides that force a reversal sidesteps that entirely.
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
