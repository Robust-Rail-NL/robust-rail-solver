// A nested namespace, rather than the top-level `Tests` namespace: TestPlan.cs
// declares its own minimal TaskType/PredefinedTaskType directly in `Tests`
// for a different purpose, which would otherwise shadow the wire-format
// types of the same name used here.
namespace Tests.UnifiedSchema;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling.Interchange;
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

    private static TrainUnitType MakeTrainUnitType(string typePrefix, uint carriages) =>
        new()
        {
            TypePrefix = typePrefix,
            Carriages = carriages,
            CombineDuration = 180,
            SplitDuration = 120,
            BackNormTime = 120,
            BackAdditionTime = 16,
        };

    [Fact]
    public void TrainUnitType_SerializesTypePrefix_NotDisplayName()
    {
        TrainUnitType type = MakeTrainUnitType("SLT", 4);

        string json = type.SerializeJson();

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal("SLT", doc.RootElement.GetProperty("typePrefix").GetString());
        Assert.Equal(4u, doc.RootElement.GetProperty("carriages").GetUInt32());
        Assert.False(doc.RootElement.TryGetProperty("displayName", out _));
    }

    [Fact]
    public void TrainUnitType_Deserialize_RequiresTypePrefix()
    {
        string json = "{\"carriages\": 4}";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<TrainUnitType>(json, options)
        );
    }

    [Fact]
    public void TrainUnitType_EqualityAndHash_DistinguishSharedTypePrefixByCarriages()
    {
        // SLT-4 and SLT-6 share a TypePrefix and must only be disambiguated
        // by Carriages -- the exact case this rename exists to handle.
        TrainUnitType slt4 = MakeTrainUnitType("SLT", 4);
        TrainUnitType slt6 = MakeTrainUnitType("SLT", 6);

        Assert.NotEqual(slt4, slt6);
        Assert.NotEqual(slt4.GetHashCode(), slt6.GetHashCode());
        Assert.Equal(slt4, MakeTrainUnitType("SLT", 4));

        Dictionary<(string TypePrefix, uint Carriages), TrainUnitType> byType = new()
        {
            [slt4.TypeDisplayName()] = slt4,
            [slt6.TypeDisplayName()] = slt6,
        };

        Assert.Equal(2, byType.Count);
        Assert.Same(slt4, byType[("SLT", 4u)]);
        Assert.Same(slt6, byType[("SLT", 6u)]);
    }

    [Fact]
    public void TrainUnit_SerializesTypePrefixAndCarriages_NotTypeDisplayName()
    {
        TrainUnit unit = new()
        {
            Id = 2422,
            TypePrefix = "SLT",
            Carriages = 4,
        };

        string json = unit.SerializeJson();

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal("SLT", doc.RootElement.GetProperty("typePrefix").GetString());
        Assert.Equal(4u, doc.RootElement.GetProperty("carriages").GetUInt32());
        Assert.False(doc.RootElement.TryGetProperty("typeDisplayName", out _));
    }

    [Fact]
    public void IncomingTrainUnit_SerializesTypePrefixAndCarriages_NotTypeDisplayName()
    {
        IncomingTrainUnit unit = new()
        {
            Id = 2422,
            TypePrefix = "SLT",
            Carriages = 4,
        };

        string json = unit.SerializeJson();

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal("SLT", doc.RootElement.GetProperty("typePrefix").GetString());
        Assert.Equal(4u, doc.RootElement.GetProperty("carriages").GetUInt32());
        Assert.False(doc.RootElement.TryGetProperty("typeDisplayName", out _));
        Assert.Equal(("SLT", 4u), unit.TypeDisplayName());
    }

    // trackParts and actions became required in the interchange schema on
    // 2026-08-12, and these models mirror that with C# `required` members.
    // Before, a location file missing its track graph deserialized to an empty
    // yard and the solver reported an infeasible instance — a malformed input
    // presenting as a legitimate negative result.
    [Fact]
    public void Location_WithoutTrackParts_IsRejected()
    {
        string json = "{\"schemaVersion\": 1}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Location>(json, options));
    }

    [Fact]
    public void Location_WithEmptyTrackParts_IsAccepted()
    {
        // `required` is presence, not non-emptiness: minItems was deliberately
        // left off the schema, so an empty list stays legal here too.
        string json = "{\"schemaVersion\": 1, \"trackParts\": []}";
        Location? location = JsonSerializer.Deserialize<Location>(json, options);
        Assert.NotNull(location);
        Assert.Empty(location.TrackParts);
    }

    [Fact]
    public void Plan_WithoutActions_IsRejected()
    {
        string json = "{\"schemaVersion\": 1}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Plan>(json, options));
    }
}
