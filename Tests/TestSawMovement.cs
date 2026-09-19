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
using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using Tests.InPlaceSplit;

// Exercises PlanGraph.BuildMoveActionsWithReverses directly against a real
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
    public void ReversingRoute_EmitsAnExplicitReverseAction()
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

        // siding's only real connection is via its A side (to approach); its
        // B side is a bumper, so a train resting on siding always got there
        // via A and truly rests at siding.AA. ComputeRoute's first Side
        // argument is that true resting side (Side.A here), not a chosen
        // exit -- the search itself discovers that reaching approach's B
        // side (the switch connecting back to siding) requires reversing
        // first, since siding.AA has no switch arcs of its own. Confirmed
        // empirically to produce Arcs=[Reverse, Switch]: the direct reversal
        // is cheaper than the old, buggy path that ran to the dead end at
        // cap and back before reversing.
        Route route = graph.ComputeRoute([], train, siding, Side.A, approach, Side.B);
        Assert.NotEqual(Route.Invalid, route);
        Assert.Contains(route.Arcs, a => a.Type == ArcType.Reverse);
        Assert.Equal(new[] { ArcType.Reverse, ArcType.Switch }, route.Arcs.Select(a => a.Type));
        foreach (var arc in route.Arcs)
            arc.ComputeCost(train);

        var planGraph = new TabuSearch(new Random(1), 0).Graph;
        var shuntingUnit = new ShuntingUnit(1);
        ulong startTime = 1000;
        ulong endTime = startTime + (ulong)(int)route.Duration;

        var actions = planGraph.BuildMoveActionsWithReverses(
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
            .Where(a => a.TaskType.Predefined == PredefinedTaskType.Reverse)
            .ToList();
        Assert.Single(setbacks);
        Assert.Equal(siding.ID, setbacks[0].Location);
        Assert.Equal((ulong)1000, setbacks[0].StartTime);
        // Exact, not just non-zero: a Reverse's duration is Train's own
        // ReversalDuration alone, with no track-crossing time of its own
        // (see BuildMoveActionsWithReverses's comment, #52).
        Assert.Equal(
            (ulong)(int)train.ReversalDuration,
            setbacks[0].EndTime!.Value - setbacks[0].StartTime!.Value
        );

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
        // advances its own time cursor by (see BuildMoveActionsWithReverses's
        // own comment, #52).
        Assert.Equal(startTime, actions.Min(a => a.StartTime!.Value));
        Assert.Equal(endTime, actions.Max(a => a.EndTime!.Value));
    }

    // RoutingGraph.ComputeRoute's route cache (Routing/Storage.cs) is keyed
    // on departure/arrival/side/occupancy only, not train. A cache hit used
    // to hand back a Route whose Reverse arc's Duration/Cost was still the
    // one computed for whichever train's Dijkstra run first populated that
    // cache entry (#55). Two trains with different ReversalDuration, routed
    // through the same siding<->approach reversal: the second (cache-hit)
    // train's Reverse arc must reflect its own ReversalDuration, and the
    // first train's already-returned Route must stay unaffected by the
    // second train's lookup (Route.Arcs used to alias the cached array).
    [Fact]
    public void CachedRoute_RefreshesReverseArcForTheReusingTrain()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_saw_movement.json"),
            TestData("scenario_saw_movement.json")
        );

        var graph = RoutingGraph.Construct();
        Track siding = ProblemInstance.Current.Tracks.First(t => t.PrettyName == "siding");
        Track approach = ProblemInstance.Current.Tracks.First(t => t.PrettyName == "approach");
        ServiceSiteScheduling.Trains.TrainUnit unit = ProblemInstance.Current.TrainUnits[0];

        // trainA: the scenario's single unit. trainB: the same unit type
        // twice, to give it a different ReversalDuration (BaseReversalDuration
        // + Units.Count * VariableReversalDuration) without needing separate
        // TestData.
        ShuntTrain trainA = new(new List<ShuntTrainUnit> { new(unit) });
        ShuntTrain trainB = new(new List<ShuntTrainUnit> { new(unit), new(unit) });
        Assert.NotEqual(trainA.ReversalDuration, trainB.ReversalDuration);

        Route routeA = graph.ComputeRoute([], trainA, siding, Side.B, approach, Side.B);
        Arc reverseA = routeA.Arcs.First(a => a.Type == ArcType.Reverse);
        Assert.Equal(
            (Time)(Settings.TrackCrossingTime + trainA.ReversalDuration),
            reverseA.Duration
        );

        // Same departure/arrival/side/occupancy as routeA - this must be a
        // cache hit, not a fresh Dijkstra run, for the bug to be exercised.
        Route routeB = graph.ComputeRoute([], trainB, siding, Side.B, approach, Side.B);
        Arc reverseB = routeB.Arcs.First(a => a.Type == ArcType.Reverse);
        Assert.Equal(
            (Time)(Settings.TrackCrossingTime + trainB.ReversalDuration),
            reverseB.Duration
        );

        // routeA's own Reverse arc must not have been mutated by routeB's
        // lookup.
        Assert.Equal(
            (Time)(Settings.TrackCrossingTime + trainA.ReversalDuration),
            reverseA.Duration
        );
    }

    // GetFlatDuration is what turns a Move's resource walk into the
    // evaluator-matching duration BuildMoveActionsWithReverses now uses
    // (#52) - unit-tested directly (internal, same reasoning as
    // BuildMoveActionsWithReverses/GroupIntoPieces above) rather than via a
    // hand-built Route, since constructing a Route with a real Move on both
    // sides of a reversal - not the boundary shape above - needs a branching
    // track layout the graph's point-switch model doesn't allow to shortcut
    // around by hand.
    [Theory]
    [InlineData(0, 0)] // zero-length Track (a connector, never a real stop)
    [InlineData(200, 60)] // real Track, regardless of IsActive - see below
    public void GetFlatDuration_ChargesATrackByLengthNotByIsActive(int length, int expected)
    {
        // CanPark=false, CanReverse=false: IsActive is false either way -
        // the fix this guards is exactly that a real (nonzero-length) Track
        // must still be charged despite that, unlike a zero-length one.
        Track track = new(1, "t", ServiceType.None, length, Side.None, false, false, 0);
        Assert.Equal((Time)expected, PlanGraph.GetFlatDuration(track));
    }

    [Theory]
    [InlineData(typeof(Switch), 1)]
    [InlineData(typeof(EnglishSwitch), 2)]
    [InlineData(typeof(HalfEnglishSwitch), 2)]
    [InlineData(typeof(Intersection), 0)]
    public void GetFlatDuration_ChargesAConnectionBySwitchEquivalentCost(
        Type connectionType,
        int switchEquivalent
    )
    {
        var connection = (Connection)Activator.CreateInstance(connectionType, (ulong)1, "c")!;
        Assert.Equal(
            (Time)(switchEquivalent * (int)Settings.SwitchCrossingTime),
            PlanGraph.GetFlatDuration(connection)
        );
    }
}
