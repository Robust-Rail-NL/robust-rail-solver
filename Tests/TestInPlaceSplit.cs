// Regression fixture for #11: a train that splits without leaving its track.
//
// The crash it guards against ("Cannot remove from an empty deque") was found by
// sweeping solver seeds against a ten-train scenario, where it appeared on half
// the seeds after thirty seconds of search. This scenario provokes it directly
// and in well under a second, because it leaves the solver no alternative: a
// combined train stands on track 52, one half must still be there at the end,
// and the other must depart. Splitting where it stands is the only way through,
// so the plan graph that triggered #11 is built on the first pass rather than
// stumbled upon.
namespace Tests.InPlaceSplit;

using System.Text.Json;

// ProblemInstance.Current is a mutable static, so two tests building a plan at
// the same time would overwrite each other's problem. xUnit runs the tests in one
// collection one at a time, which is enough to keep them apart.
[CollectionDefinition(PlanBuilding.Name)]
public class PlanBuildingCollection { }

public static class PlanBuilding
{
    public const string Name = "plan building";
}

[Collection(PlanBuilding.Name)]
public class InPlaceSplitTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    private static JsonDocument BuildPlan()
    {
        DirectoryInfo temp = Directory.CreateTempSubdirectory("robust_inplace_split_");
        var planPath = Path.Combine(temp.FullName, "plan.json");

        ServiceSiteScheduling.Program.CreatePlan(
            TestData("location_kleine_binckhorst.json"),
            TestData("scenario_inplace_split.json"),
            planPath,
            ServiceSiteScheduling.Config.ReadFrom(TestData("config_inplace_split.yaml")),
            0,
            temp.FullName
        );

        var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        temp.Delete(recursive: true);
        return plan;
    }

    private static string TaskType(JsonElement action) =>
        action.GetProperty("taskType").GetProperty("predefined").ValueKind == JsonValueKind.Null
            ? ""
            : action.GetProperty("taskType").GetProperty("predefined").GetString() ?? "";

    private static int UnitId(JsonElement action) =>
        action.GetProperty("shuntingUnit").GetProperty("id").GetInt32();

    [Fact]
    public void SplittingWithoutLeavingTheTrack_ProducesAPlan()
    {
        // Before the fix this threw from Deque.Remove: every part of the split
        // shared one State, so each removed the same node and the second found
        // it already gone.
        using var plan = BuildPlan();

        Assert.NotEmpty(plan.RootElement.GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public void ThePlan_ActuallyContainsASplitThatDoesNotMove()
    {
        // Without this the test above would keep passing while covering nothing,
        // should the solver ever stop choosing an in-place split here.
        using var plan = BuildPlan();
        var actions = plan.RootElement.GetProperty("actions").EnumerateArray().ToList();

        var splits = actions.Where(a => TaskType(a) == "Split").ToList();
        Assert.NotEmpty(splits);

        // A split whose shunting unit never moves is one that happened where the
        // train already stood. This is also what keeps the serialisation honest:
        // a routing task's duration covers the decoupling as well as the travel,
        // and emitting a Move because of the former once produced an action with
        // an empty path, which then threw while being trimmed.
        Assert.Contains(
            splits,
            split => !actions.Any(a => TaskType(a) == "Move" && UnitId(a) == UnitId(split))
        );
    }
}
