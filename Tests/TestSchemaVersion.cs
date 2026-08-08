namespace Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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

    // Collects what was logged, so these tests can assert on the warning
    // rather than only on the absence of an exception. It also keeps the two
    // deliberate mismatch cases off the console: they used to print warnings
    // into every CI run, where they read as a real problem.
    private sealed class CapturingLogger : ILogger
    {
        public readonly List<string> Warnings = new();

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter
        )
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    [Fact]
    public void WarnOnSchemaVersionMismatch_SaysWhatIsAssumed_WhenMissing()
    {
        CapturingLogger logger = new();

        ProblemInstance.WarnOnSchemaVersionMismatch("Location", null, logger);

        string warning = Assert.Single(logger.Warnings);
        Assert.Contains("Location", warning);
        Assert.Contains("missing", warning);
        Assert.Contains(InterchangeSchema.ExpectedVersion.ToString(), warning);
    }

    [Fact]
    public void WarnOnSchemaVersionMismatch_NamesBothVersions_WhenMismatched()
    {
        CapturingLogger logger = new();

        ProblemInstance.WarnOnSchemaVersionMismatch("Scenario", 2, logger);

        string warning = Assert.Single(logger.Warnings);
        Assert.Contains("Scenario", warning);
        // Both the value found and the one expected: a warning naming only one
        // of them leaves the reader unable to tell which end is wrong.
        Assert.Contains("2", warning);
        Assert.Contains(InterchangeSchema.ExpectedVersion.ToString(), warning);
    }

    [Fact]
    public void WarnOnSchemaVersionMismatch_IsSilent_WhenMatching()
    {
        CapturingLogger logger = new();

        ProblemInstance.WarnOnSchemaVersionMismatch(
            "Location",
            InterchangeSchema.ExpectedVersion,
            logger
        );

        Assert.Empty(logger.Warnings);
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
