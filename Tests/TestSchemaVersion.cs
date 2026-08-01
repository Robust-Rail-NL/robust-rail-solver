namespace Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceSiteScheduling;
using ServiceSiteScheduling.NoProto;
using ServiceSiteScheduling.Utilities;

public class TestSchemaVersion
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    // WarnOnSchemaVersionMismatch logs via Microsoft.Extensions.Logging's
    // console provider, which writes asynchronously on a background thread,
    // so asserting on captured console text is flaky. These tests instead
    // pin down the contract that matters: warn-and-continue never throws,
    // for any combination of missing/mismatched/matching input.
    [Fact]
    public void WarnOnSchemaVersionMismatch_DoesNotThrow_WhenMissing()
    {
        ProblemInstance.WarnOnSchemaVersionMismatch("Location", null);
    }

    [Fact]
    public void WarnOnSchemaVersionMismatch_DoesNotThrow_WhenMismatched()
    {
        ProblemInstance.WarnOnSchemaVersionMismatch("Scenario", 2);
    }

    [Fact]
    public void WarnOnSchemaVersionMismatch_DoesNotThrow_WhenMatching()
    {
        ProblemInstance.WarnOnSchemaVersionMismatch("Location", InterchangeSchema.ExpectedVersion);
    }

    [Fact]
    public void Plan_DefaultsToExpectedSchemaVersion()
    {
        ServiceSiteScheduling.NoProto.Plan plan = new();
        Assert.Equal(InterchangeSchema.ExpectedVersion, plan.SchemaVersion);
    }

    [Fact]
    public void Plan_SerializeJson_AlwaysIncludesSchemaVersion()
    {
        ServiceSiteScheduling.NoProto.Plan plan = new();
        string json = plan.SerializeJson();
        Assert.Contains("\"schemaVersion\"", json);
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Equal(
            InterchangeSchema.ExpectedVersion,
            doc.RootElement.GetProperty("schemaVersion").GetInt32()
        );
    }

    [Fact]
    public void Location_Deserialize_ReadsSchemaVersionWhenPresent()
    {
        string json = "{\"schemaVersion\": 2, \"trackParts\": []}";
        Location? location = JsonSerializer.Deserialize<Location>(json, options);
        Assert.NotNull(location);
        Assert.Equal(2, location.SchemaVersion);
    }

    [Fact]
    public void Location_Deserialize_SchemaVersionNullWhenAbsent()
    {
        string json = "{\"trackParts\": []}";
        Location? location = JsonSerializer.Deserialize<Location>(json, options);
        Assert.NotNull(location);
        Assert.Null(location.SchemaVersion);
    }
}
