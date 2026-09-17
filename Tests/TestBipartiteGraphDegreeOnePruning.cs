// Regression fixture for PR #49: BipartiteGraph's degree-1 pruning has two
// symmetric passes over the adjacency matrix, one triggered by an arrival
// with a single remaining candidate departure, the other by a departure with
// a single remaining candidate arrival. In the departure-side pass, the loop
// variables are swapped relative to the arrival-side one (the outer index is
// the departure, the inner one the arrival), but the fix predates this test:
// the buggy code still built `Match(arrivals[i], departures[j])`, plugging a
// departure index into the arrival slot and vice versa.
namespace Tests.BipartiteGraphDegreeOnePruning;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using Tests.InPlaceSplit;

[Collection(PlanBuilding.Name)]
public class DegreeOnePruningTests
{
    [Fact]
    public void DepartureWithASingleCandidateArrival_IsMatchedToThatArrival()
    {
        var type = new TrainType(0, "Type", 1, [], 0, 0, 0, 0);

        // Three same-type arrivals/departures, timed so pruning only ever
        // finds a degree-1 *departure*, never a degree-1 arrival: arrival 0
        // is early enough (t=1) to reach all three departures, while
        // arrivals 1 and 2 (t=10) only reach the two departures at t=20/30.
        // That leaves the t=5 departure (index 2) with arrival 0 (index 0)
        // as its sole remaining candidate - different indices, so a
        // transposed Match is easy to tell apart from the correct one.
        var arrivalUnits = new[]
        {
            new TrainUnit(0, type, [], []),
            new TrainUnit(1, type, [], []),
            new TrainUnit(2, type, [], []),
        };
        var arrivals = new[]
        {
            new ArrivalTrain(arrivalUnits[0], null!, Side.A, new Time(1)),
            new ArrivalTrain(arrivalUnits[1], null!, Side.A, new Time(10)),
            new ArrivalTrain(arrivalUnits[2], null!, Side.A, new Time(10)),
        };
        var departures = new[]
        {
            new DepartureTrain(new Time(20), new DepartureTrainUnit(type), null!, Side.A),
            new DepartureTrain(new Time(30), new DepartureTrainUnit(type), null!, Side.A),
            new DepartureTrain(new Time(5), new DepartureTrainUnit(type), null!, Side.A),
        };

        ProblemInstance.Current = new ProblemInstance
        {
            TrainUnits = arrivalUnits,
            ArrivalsOrdered = arrivals,
            DeparturesOrdered = departures,
        };

        var matches = new BipartiteGraph().MaximumMatching();

        // Before the fix, this pass produced Match(arrivals[2], departures[0])
        // instead - a pairing between two vertices that were never uniquely
        // adjacent - which left arrival 0 unmatched and arrival 2 matched
        // twice (once there, once through the general matching below). The
        // result still came out to 3 entries, so only checking the count, or
        // for an exception, would not have caught it.
        Assert.Equal(3, matches.Count);
        Assert.Equal(new[] { 0, 1, 2 }, matches.Select(m => m.Arrival.Index).OrderBy(i => i));
        Assert.Contains(matches, m => m.Arrival.Index == 0 && m.Departure.Index == 2);
    }
}
