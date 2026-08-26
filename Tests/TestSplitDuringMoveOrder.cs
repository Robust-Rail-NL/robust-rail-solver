namespace Tests.SplitDuringMove;

// Regression fixture for #26: a train that
// splits *during* a move onto a new track (as opposed to splitting where it
// already stands, which #11's fixture covers) could place the split-off
// units on the wrong physical end of the destination track, independently
// of which way the train actually drove in.
//
// Two units arrive coupled and drive nose-first into a dead-end siding
// reached over a single approach track, then split there. The only way out
// is back the way they came, so whichever unit is physically nearest the
// approach can leave first, and the other cannot leave until it does. The
// scenario asks for the *leading* unit -- the one that necessarily ends up
// buried deepest in the dead end, since it drove in first -- to leave
// before the trailing unit. That is physically impossible.
//
// Before the fix, the split-during-a-move placement always put the
// first-listed member on the destination track's "A" side, regardless of
// which side the train actually arrived through. For this layout that
// happened to bury the *trailing* unit and leave the leading one sitting
// at the mouth of the siding, so the impossible order came out costed as a
// perfectly ordinary, crossing-free plan. The fix must at least recognise
// the impossibility: this scenario has no crossing-free plan, and that has
// to show up on the very first constructed graph, without needing any
// search to stumble onto it.
[Collection(InPlaceSplit.PlanBuilding.Name)]
public class SplitDuringMoveOrderTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void LeadingUnitIntoADeadEnd_CannotBeScheduledToLeaveBeforeTheTrailingUnit()
    {
        ServiceSiteScheduling.ProblemInstance.Current =
            ServiceSiteScheduling.ProblemInstance.ParseJson(
                TestData("location_deadend_reversal.json"),
                TestData("scenario_deadend_reversal.json")
            );

        var tabuSearch = new ServiceSiteScheduling.LocalSearch.TabuSearch(new Random(1), 0);

        Assert.NotNull(tabuSearch.Graph.Cost);
        Assert.True(
            tabuSearch.Graph.Cost.Crossings > 0,
            "the leading unit's forced early departure should register as a crossing, "
                + "since the trailing unit is physically in its way"
        );
    }
}
