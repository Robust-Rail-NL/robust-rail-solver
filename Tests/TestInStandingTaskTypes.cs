// Regression fixture for #32: an inStanding train's task type must be
// registered even when no `in` (arriving) train uses that same type.
//
// ProblemInstance.Parse used to build its TaskType -> ServiceType map
// (taskmap) from scenario.In alone, while GetTrainUnits looks up every
// task's type in that same map for both scenario.In and scenario.InStanding.
// A task type that only appeared on an inStanding train therefore threw
// KeyNotFoundException at parse time.
namespace Tests.InStandingTaskTypes;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;

public class InStandingOnlyTaskTypeTests
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    private static T ReadJson<T>(string name) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(TestData(name)), options)!;

    [Fact]
    public void Parse_RegistersATaskType_UsedOnlyByAnInStandingTrain()
    {
        var location = ReadJson<Location>("location_kleine_binckhorst.json");
        var scenario = ReadJson<Scenario>("scenario_inplace_split.json");

        var onlyOnInStanding = new TaskType(null, "OnlyOnInStanding");
        var standingUnit = scenario.InStanding![0].Members[0];
        standingUnit.Tasks.Add(new TaskSpec { Type = onlyOnInStanding, Duration = 100 });

        // Before the fix, this threw KeyNotFoundException: the type was
        // never added to taskmap, which was built from scenario.In alone.
        var instance = ProblemInstance.Parse(location, scenario);

        var service = Assert.Single(instance.ServiceTypes, s => s.Name == onlyOnInStanding.Other);

        var trainUnit = instance
            .TrainUnitConversion.Single(kv => kv.Value.Id == standingUnit.Id)
            .Key;
        Assert.Contains(trainUnit.RequiredServices, s => s.Type == service);
    }
}
