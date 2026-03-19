#nullable enable

using System.Collections.Immutable;

namespace ServiceSiteScheduling.NoProto
{
    public record Location
    {
        public IList<TrackPart>? TrackParts { get; set; }
        public IList<Facility>? Facilities { get; set; }
        public IList<TaskType>? TaskTypes { get; set; }
    }

    public record Resource
    {
        public string? Name { get; set; }
        public ulong? FacilityId { get; internal set; }
        public ulong? TrackPartId { get; internal set; }
    }

    public record Facility
    {
        public ulong? Id { get; set; }
        public string? Type { get; set; }
        public ImmutableArray<ulong> RelatedTrackParts { get; init; } = [];
        public ImmutableArray<TaskType> TaskTypes { get; init; } = [];
        public int? SimultaneousUsageCount { get; set; }
    }

    public enum TrackPartType
    {
        RailRoad,

        // Switches
        Switch,
        EnglishSwitch,
        HalfEnglishSwitch,

        // Other
        Intersection,
        Bumper,
    }

    public record TrackPart
    {
        public required ulong Id { get; set; }
        public TrackPartType? Type { get; set; }
        public ImmutableArray<ulong> ASide { get; init; }
        public ImmutableArray<ulong> BSide { get; init; }
        public double? Length { get; set; }
        public string? Name { get; set; }
        public required bool SawMovementAllowed { get; set; }
        public required bool ParkingAllowed { get; set; }
    }

    public record TaskType
    {
        public PredefinedTaskType? Predefined { get; init; }
        public string? Other { get; init; }

        public TaskType(PredefinedTaskType? predefined, string? other)
        {
            if (predefined == null && other == null || predefined != null && other != null)
            {
                throw new ArgumentException("Exactly one constructor argument must be null");
            }
            this.Predefined = predefined;
            this.Other = other;
        }
    }

    public enum PredefinedTaskType
    {
        // Movement
        Move,
        Split,
        Combine,

        // Special
        Wait,
        Arrive,
        Exit,

        // StandOut = 6,
        // StandIn = 7,
    }
}
