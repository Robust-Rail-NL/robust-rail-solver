#nullable enable

namespace ServiceSiteScheduling.Interchange
{
    // The shared interchange schemaVersion carried by Location, Scenario, and
    // Plan. Independent monotonic integer, decoupled from tool release
    // versions; increments only on breaking changes to the wire format. See
    // SCHEMA_CHANGELOG.md in robust-rail-generator for what changed at each
    // version.
    public static class InterchangeSchema
    {
        public const int ExpectedVersion = 1;
    }
}
