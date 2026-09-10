namespace Tests;

using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling.Interchange;

// Shares the mutable static ProblemInstance.Current with the other tests that
// build a plan, so it must not run alongside them.
[Collection(InPlaceSplit.PlanBuilding.Name)]
public class TestPlan(ITestOutputHelper output)
{
    private readonly ITestOutputHelper output = output;

    [Fact]
    public void TestJsonSorted()
    {
        // Where to put the temp and final plans
        DirectoryInfo temp_dir = Directory.CreateTempSubdirectory("robust_test_");
        var plan_path = Path.GetTempFileName();

        // Where to get the input
        var test_data_path = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        var location_path = Path.Combine(test_data_path, "location_simple_service.json");
        var scenario_path = Path.Combine(test_data_path, "scenario_example1.json");
        var config_file = Path.Combine(test_data_path, "config.yaml");

        // Now create a plan...
        var config = ServiceSiteScheduling.Config.ReadFrom(config_file);
        ServiceSiteScheduling.Program.CreatePlan(
            location_path,
            scenario_path,
            plan_path,
            config,
            0,
            temp_dir.ToString()
        );

        // ...and assert that all of the JSON output is sorted.
        Assert.True(JsonIsSorted(plan_path));
        foreach (FileInfo file in temp_dir.GetFiles())
        {
            Assert.True(JsonIsSorted(file.FullName));
        }

        // Finally, clean up.
        // File.Delete(plan_path);
        foreach (FileInfo file in temp_dir.GetFiles())
        {
            file.Delete();
        }
        temp_dir.Delete();
    }

    // Guards TaskTypeOrder.ORDER's completeness: a value missing from it
    // doesn't fail loudly on its own - TestJsonSorted only turns up the gap
    // when CreatePlan's unseeded random search happens to produce a plan
    // containing the missing value, so it can pass for a long time before
    // failing intermittently (this is exactly how Break/NonService/StandIn/
    // StandOut/Setback were found missing, back when ORDER lived alongside a
    // local copy of PredefinedTaskType that had gone stale - see the removal
    // of that copy in favor of the real ServiceSiteScheduling.Interchange
    // enum). Comparing the full enum, not just spot-checking recently-added
    // values, so this catches the next omission too.
    [Fact]
    public void PredefinedTaskType_AllValuesHaveATieBreakOrder()
    {
        var allValues = Enum.GetValues<PredefinedTaskType>();

        Assert.Equal(allValues.Length, TaskTypeOrder.ORDER.Length);
        Assert.Equal(allValues.OrderBy(v => v), TaskTypeOrder.ORDER.OrderBy(v => v));
    }

    readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    private bool JsonIsSorted(string json_file)
    {
        output.WriteLine($"Verifying {json_file}");

        string json_text = File.ReadAllText(json_file);
        Plan? plan = JsonSerializer.Deserialize<Plan>(json_text, options);
        long lastStartTime = long.MinValue;
        long lastEndTime = long.MinValue;
        PredefinedTaskType? lastPTT = TaskTypeOrder.ORDER[0];
        string? lastOTT = null;
        foreach (Task task in plan?.Actions ?? [])
        {
            Assert.True(task.TaskType.Predefined == null || task.TaskType.Other == null);
            Assert.True(task.TaskType.Predefined != null || task.TaskType.Other != null);
            if (lastStartTime == task.StartTime)
            {
                if (lastEndTime == task.EndTime)
                {
                    Assert.True(CompareTaskType(lastPTT, lastOTT, task) <= 0);
                }
                else
                {
                    Assert.True(lastEndTime < task.EndTime);
                }
            }
            else
            {
                Assert.True(lastStartTime < task.StartTime);
            }
            lastStartTime = task.StartTime;
            lastEndTime = task.EndTime;
            lastPTT = task.TaskType.Predefined;
            lastOTT = task.TaskType.Other;
        }
        return true;
    }

    private static int CompareTaskType(PredefinedTaskType? lastPTT, string? lastOTT, Task task)
    {
        if (task.TaskType.Predefined == null)
        {
            if (lastOTT != null)
            {
                return lastOTT.CompareTo(task.TaskType.Other);
            }
            else
            {
                Debug.Assert(lastPTT != null);
                return -1;
            }
        }
        else
        {
            if (lastPTT != null)
            {
                return Array.IndexOf(TaskTypeOrder.ORDER, lastPTT)
                    - Array.IndexOf(TaskTypeOrder.ORDER, task.TaskType.Predefined);
            }
            else
            {
                Debug.Assert(lastOTT != null);
                return 1;
            }
        }
    }
}

// A deliberately narrow view of the real Plan/Action shape: only the fields
// this file's sortedness check actually reads. System.Text.Json ignores the
// JSON members that don't map to a property here (ShuntingUnit, Resources,
// Location, ...), so there's nothing to keep in sync as those evolve - unlike
// TaskType/PredefinedTaskType below, which mirror the real thing exactly and
// so used to be their own, driftable copy.
public class Plan
{
    public required Task[] Actions { get; set; }
}

public class Task
{
    public required long StartTime { get; set; }
    public required long EndTime { get; set; }
    public required TaskType TaskType { get; set; }
}

// Test-only tie-break order for actions sharing both StartTime and EndTime;
// there's no production equivalent to reuse here, since real plans never
// need to break a tie between two actions on the same shunting unit at the
// same instant - only this test's monotonicity check does.
internal static class TaskTypeOrder
{
    // StandIn/StandOut are the standing-train equivalents of Arrive/Exit, so
    // placed immediately alongside them; Walking is placed with the other
    // movement types, and Break/NonService at the end. Unlike the original
    // six (Arrive, Move, Wait, Split, Combine, Exit), this placement for the
    // five newer values is a reasonable default, not a verified business
    // requirement - revisit if a real scenario ever exercises a tie-break
    // between any of them and something else.
    // PredefinedTaskType_AllValuesHaveATieBreakOrder above only guards
    // completeness (every value appears exactly once), not that the order
    // itself is correct.
    public static readonly PredefinedTaskType[] ORDER =
    [
        PredefinedTaskType.Arrive,
        PredefinedTaskType.StandIn,
        PredefinedTaskType.Move,
        PredefinedTaskType.Walking,
        PredefinedTaskType.Wait,
        PredefinedTaskType.Split,
        PredefinedTaskType.Combine,
        PredefinedTaskType.Exit,
        PredefinedTaskType.StandOut,
        PredefinedTaskType.Break,
        PredefinedTaskType.NonService,
    ];
}
