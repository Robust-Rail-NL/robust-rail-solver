// Covers #14: an outStanding train has no DepartureTask, so ComputeCost's
// departure-lateness check never sees it. A plan that finishes an outStanding
// train's chain (e.g. a required service) after the scenario ends must be
// costed and reported as infeasible, not scored as a perfect plan.
namespace Tests.OutStandingDeadline;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Initial;
using ServiceSiteScheduling.Solutions;
using Tests.InPlaceSplit;

[Collection(PlanBuilding.Name)]
public class OutStandingOverrunTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    private static (PlanGraph graph, SolutionCost cost) BuildAndComputeModel(string scenarioFile)
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_kleine_binckhorst.json"),
            TestData(scenarioFile)
        );
        var graph = SimpleHeuristic.Construct(new Random(1));
        var cost = graph.ComputeModel();
        graph.Cost = cost;
        return (graph, cost);
    }

    [Fact]
    public void OutStandingTrain_FinishingAfterScenarioEnd_IsCountedAsAnOverrun()
    {
        // The only outStanding unit needs a 3599s Reinigingsperron service and the
        // scenario ends at 3600s: matching accepts it (arrival + service duration
        // is just under the deadline), but the routing/reversal overhead around
        // that service still pushes the StandOutTask's actual finish past it.
        var (graph, cost) = BuildAndComputeModel("scenario_outstanding_overrun.json");

        Assert.Single(graph.StandOutTasks);
        var standout = graph.StandOutTasks[0];
        Assert.True(
            standout.Start > standout.Deadline,
            $"expected the StandOut to finish after its deadline, but Start={standout.Start}, Deadline={standout.Deadline}"
        );

        Assert.Equal(1, cost.OutStandingOverruns);
        Assert.False(cost.IsFeasible);
    }

    [Fact]
    public void OutStandingTrain_FinishingOnTime_IsNotCountedAsAnOverrun()
    {
        // Same fixture, but with a scenario end time generous enough for the
        // 5000s service to complete: same shape of plan, no violation.
        var (graph, cost) = BuildAndComputeModel("scenario_outstanding_no_overrun.json");

        Assert.Single(graph.StandOutTasks);
        var standout = graph.StandOutTasks[0];
        Assert.True(
            standout.Start <= standout.Deadline,
            $"expected the StandOut to finish on time, but Start={standout.Start}, Deadline={standout.Deadline}"
        );

        Assert.Equal(0, cost.OutStandingOverruns);
    }
}
