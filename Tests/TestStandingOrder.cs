// Covers #18: the solver ignores standingIndex, so multiple inStanding trains
// sharing a track end up in whatever order the search happens to produce
// rather than the one the scenario asked for.
namespace Tests.StandingOrder;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Initial;
using ServiceSiteScheduling.Tasks;
using Tests.InPlaceSplit;

// The comparator itself, in isolation: SimpleHeuristic.CompareStandingOrder
// decides which of two same-track StandInTasks should be Add()-ed to the
// track's Deque first, given Deque.Add() always lands the most-recently-added
// node nearest the target side.
public class CompareStandingOrderTests
{
    private static StandInTask StandIn(int standingIndex, Side arrivalSide) =>
        new(null!, null!) { StandingIndex = standingIndex, ArrivalSide = arrivalSide };

    [Fact]
    public void AtSideA_TheLowerIndex_MustArriveLast()
    {
        // Deque.Add(node, Side.A) prepends, so whichever is added last ends up
        // nearest A - the lowest index needs to be added last to land there.
        var lower = StandIn(0, Side.A);
        var higher = StandIn(1, Side.A);

        Assert.True(SimpleHeuristic.CompareStandingOrder(lower, higher) > 0);
        Assert.True(SimpleHeuristic.CompareStandingOrder(higher, lower) < 0);
    }

    [Theory]
    [InlineData(nameof(Side.B))]
    [InlineData(nameof(Side.Both))]
    [InlineData(nameof(Side.None))]
    public void AtAnyNonASide_TheLowerIndex_MustArriveFirst(string sideName)
    {
        // Deque.Add(node, side) appends at B for anything other than Side.A, so
        // the lowest index needs to be added first to end up nearest A.
        Side side = sideName switch
        {
            nameof(Side.B) => Side.B,
            nameof(Side.Both) => Side.Both,
            _ => Side.None,
        };
        var lower = StandIn(0, side);
        var higher = StandIn(1, side);

        Assert.True(SimpleHeuristic.CompareStandingOrder(lower, higher) < 0);
        Assert.True(SimpleHeuristic.CompareStandingOrder(higher, lower) > 0);
    }

    [Fact]
    public void SwappingWhichUnitHasTheLowerIndex_FlipsTheOrder()
    {
        // The result must track the StandingIndex field, not incidental object
        // identity or construction order.
        var first = StandIn(1, Side.A);
        var second = StandIn(0, Side.A);

        Assert.True(SimpleHeuristic.CompareStandingOrder(first, second) < 0);

        first.StandingIndex = 0;
        second.StandingIndex = 1;

        Assert.True(SimpleHeuristic.CompareStandingOrder(first, second) > 0);
    }
}

// End-to-end through parsing and the initial construction heuristic: two
// separate inStanding shunting units sharing a track, disambiguated only by
// standingIndex, must be Add()-ed to the track's deque in the order the
// scenario asked for.
//
// This checks the order via each StandInTask's own routing task's MoveOrder
// rather than the deque itself: MoveOrder is assigned strictly in the order
// tasks are extracted from the moveheap (SimpleHeuristic.Construct), which is
// exactly what the comparator fix controls, and unlike the deque it survives
// running the plan to completion. The deque's own A/B links do not: once a
// "stays on track" train later departs (e.g. an outStanding train reaching
// its StandOut, as here), ComputeLocation departs the shared State and
// re-arrives a fresh one for the following task, so inspecting the deque
// after a full ComputeModel() would be asserting on state this fix has
// nothing to do with.
[Collection(PlanBuilding.Name)]
public class ScenarioStandingOrderTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    private static ServiceSiteScheduling.Solutions.PlanGraph BuildGraph(string scenarioFile)
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_kleine_binckhorst.json"),
            TestData(scenarioFile)
        );
        return SimpleHeuristic.Construct(new Random(1));
    }

    // The physical unit name (the scenario's member id) at each StandInTask, in
    // the order their routing tasks were extracted from the moveheap.
    private static List<string> UnitNamesInMoveOrder(
        ServiceSiteScheduling.Solutions.PlanGraph graph
    ) =>
        graph
            .StandInTasks.OrderBy(t => t.Next.MoveOrder)
            .Select(t => t.Train.Units.First().Name)
            .ToList();

    [Fact]
    public void TwoInStandingTrains_AreOrderedByAscendingStandingIndex()
    {
        var graph = BuildGraph("scenario_standing_order.json");

        Assert.Equal(2, graph.StandInTasks.Length);
        Assert.Equal(["6401", "6402"], UnitNamesInMoveOrder(graph));
    }

    [Fact]
    public void SwappingWhichUnitHasTheLowerStandingIndex_SwapsTheResultingOrder()
    {
        // Same fixture, standingIndex swapped between the two physical units:
        // proves the result tracks the field, not the unit ids or file order.
        var graph = BuildGraph("scenario_standing_order_swapped.json");

        Assert.Equal(2, graph.StandInTasks.Length);
        Assert.Equal(["6402", "6401"], UnitNamesInMoveOrder(graph));
    }

    [Fact]
    public void NullStandingIndex_OnMultipleTrainsSharingATrack_IsStillRejected()
    {
        // Regression test for the guard introduced by 89db251: standingIndex is
        // required (not just distinct) once more than one inStanding train
        // shares a track, since null gives no order to honour.
        var exception = Assert.Throws<NotSupportedException>(() =>
            ProblemInstance.ParseJson(
                TestData("location_kleine_binckhorst.json"),
                TestData("scenario_standing_order_ambiguous.json")
            )
        );
        Assert.Contains("solver#18", exception.Message);
    }
}
