// Regression fixture for #31: an arriving (`in`) or departing (`out`) train
// whose entry/exit track part resolves to something other than a GateWay
// (e.g. an ordinary RailRoad or Switch) must fail to parse, rather than
// silently vanishing from the problem instance with no error.
namespace Tests.GatewayValidation;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;

public class GatewayValidationTests
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
    public void Parse_Throws_WhenAnArrivingTrainsEntryTrackPartIsNotAGateway()
    {
        var location = ReadJson<Location>("location_simple_service.json");
        var scenario = ReadJson<Scenario>("scenario_example1.json");

        // Track part 4 is a RailRoad (it's this same train's own
        // firstParkingTrackPart), not a gateway.
        var arrivingTrain = scenario.In[0];
        arrivingTrain.EntryTrackPart = 4;

        var exception = Assert.Throws<ArgumentException>(() =>
            ProblemInstance.Parse(location, scenario)
        );
        Assert.Contains(arrivingTrain.Id.ToString()!, exception.Message);
        Assert.Contains("track part 4", exception.Message);
    }

    [Fact]
    public void Parse_Throws_WhenADepartingTrainsLeaveTrackPartIsNotAGateway()
    {
        var location = ReadJson<Location>("location_simple_service.json");
        var scenario = ReadJson<Scenario>("scenario_example1.json");

        // Track part 3 is a RailRoad (it's this same train's own
        // lastParkingTrackPart), not a gateway.
        var departingTrain = scenario.Out[0];
        departingTrain.LeaveTrackPart = 3;

        var exception = Assert.Throws<ArgumentException>(() =>
            ProblemInstance.Parse(location, scenario)
        );
        Assert.Contains("track part 3", exception.Message);
    }
}
