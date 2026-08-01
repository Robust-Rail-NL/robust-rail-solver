// A nested namespace, rather than the top-level `Tests` namespace: TestPlan.cs
// declares its own minimal TaskType/PredefinedTaskType directly in `Tests`
// for a different purpose, which would otherwise shadow the wire-format
// types of the same name used here.
namespace Tests.UnifiedSchema;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling.NoProto;
using ServiceSiteScheduling.Utilities;

// Covers the Phase 1 scenario-unification wire-format changes: the Resource
// kind/id discriminator, the extended PredefinedTaskType enum, and the
// removal of TaskSpec.Priority.
public class TestUnifiedSchema
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData("trackPart", 57ul)]
    [InlineData("facility", 72ul)]
    public void Resource_SerializesWithKindAndId(string kind, ulong id)
    {
        Resource resource = new(kind, id);
        string json = resource.SerializeJson();

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal(kind, doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal(id, doc.RootElement.GetProperty("id").GetUInt64());
        Assert.False(doc.RootElement.TryGetProperty("name", out _));
        Assert.False(doc.RootElement.TryGetProperty("facilityId", out _));
        Assert.False(doc.RootElement.TryGetProperty("trackPartId", out _));
    }

    [Fact]
    public void Resource_RoundTrips_ThroughJson()
    {
        Resource resource = new("facility", 72);
        string json = resource.SerializeJson();

        Resource? roundTripped = JsonSerializer.Deserialize<Resource>(json, options);

        Assert.Equal(resource, roundTripped);
    }

    [Fact]
    public void Resource_Deserialize_ToleratesUnrecognisedStaffKind()
    {
        // The unified schema includes a "staff" Resource variant that the
        // solver never produces but must not choke on if it ever appears on
        // a read path.
        string json = "{\"kind\": \"staff\", \"id\": 5}";

        Resource? resource = JsonSerializer.Deserialize<Resource>(json, options);

        Assert.NotNull(resource);
        Assert.Equal("staff", resource.Kind);
        Assert.Equal(5ul, resource.Id);
    }

    [Theory]
    [InlineData(PredefinedTaskType.Walking)]
    [InlineData(PredefinedTaskType.Break)]
    [InlineData(PredefinedTaskType.NonService)]
    [InlineData(PredefinedTaskType.StandIn)]
    [InlineData(PredefinedTaskType.StandOut)]
    public void PredefinedTaskType_NewValues_RoundTripThroughJson(PredefinedTaskType value)
    {
        TaskType taskType = TaskType.FromPredefined(value);
        string json = JsonSerializer.Serialize(taskType, options);

        TaskType? roundTripped = JsonSerializer.Deserialize<TaskType>(json, options);

        Assert.NotNull(roundTripped);
        Assert.Equal(value, roundTripped.Predefined);
    }

    [Fact]
    public void TaskSpec_Deserialize_IgnoresLegacyPriorityField()
    {
        // Older scenario fixtures may still carry a "priority" field now that
        // it's dropped from the unified schema; reading them must not throw.
        string json = "{\"duration\": 120, \"priority\": 1, \"requiredSkills\": []}";

        TaskSpec? taskSpec = JsonSerializer.Deserialize<TaskSpec>(json, options);

        Assert.NotNull(taskSpec);
        Assert.Equal(120ul, taskSpec.Duration);
    }

    [Theory]
    [InlineData(PredefinedTaskType.Move, "Move")]
    [InlineData(PredefinedTaskType.StandIn, "StandIn")]
    [InlineData(PredefinedTaskType.NonService, "NonService")]
    public void PredefinedTaskType_SerializesAsPascalCase(PredefinedTaskType value, string expected)
    {
        string json = JsonSerializer.Serialize(value, options);

        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void TrackPartType_SerializesAsPascalCase()
    {
        string json = JsonSerializer.Serialize(TrackPartType.RailRoad, options);

        Assert.Equal("\"RailRoad\"", json);
    }
}
