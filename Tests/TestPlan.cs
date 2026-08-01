namespace Tests;

using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        var location_path = Path.Combine(test_data_path, "location_kleine_binckhorst.json");
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

    readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private bool JsonIsSorted(string json_file)
    {
        output.WriteLine($"Verifying {json_file}");

        string json_text = File.ReadAllText(json_file);
        Plan? plan = JsonSerializer.Deserialize<Plan>(json_text, options);
        long lastStartTime = long.MinValue;
        long lastEndTime = long.MinValue;
        PredefinedTaskType? lastPTT = TaskType.ORDER[0];
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
                return Array.IndexOf(TaskType.ORDER, lastPTT)
                    - Array.IndexOf(TaskType.ORDER, task.TaskType.Predefined);
            }
            else
            {
                Debug.Assert(lastOTT != null);
                return 1;
            }
        }
    }
}

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

public class TaskType
{
    public static readonly PredefinedTaskType[] ORDER =
    [
        PredefinedTaskType.Arrive,
        PredefinedTaskType.Move,
        PredefinedTaskType.Wait,
        PredefinedTaskType.Split,
        PredefinedTaskType.Combine,
        PredefinedTaskType.Exit,
    ];

    public PredefinedTaskType? Predefined { get; set; }
    public string? Other { get; set; }
}

public enum PredefinedTaskType
{
    Move,
    Split,
    Combine,
    Wait,
    Arrive,
    Exit,
}
