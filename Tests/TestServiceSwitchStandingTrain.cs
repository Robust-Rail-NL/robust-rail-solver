namespace Tests.ServiceSwitchStandingTrain;

using ServiceSiteScheduling;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Solutions;
using Tests.InPlaceSplit;

// Regression test for issue #75: ServiceMachineSwitchMove.GetMoves crashed
// (NullReferenceException; an assertion failure in Debug builds) when an
// inStanding train has a service task. Its first routing follows a StandIn
// task, which has no preceding move for LatestPrevious() to return.
//
// Same collection as the other tests that mutate the shared static
// ProblemInstance.Current.
[Collection(PlanBuilding.Name)]
public class ServiceSwitchStandingTrainTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void TabuSearch_StandingTrainWithService_DoesNotThrow()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_kleine_binckhorst_8fea78e.json"),
            TestData("scenario_standing_service_switch.json")
        );

        var search = new TabuSearch(new Random(1), 0);
        string tmp =
            Directory.CreateTempSubdirectory("tabu_75_").FullName + Path.DirectorySeparatorChar;
        try
        {
            search.Run(40, 100, 16, 0.5, 0, tmp);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }
}
