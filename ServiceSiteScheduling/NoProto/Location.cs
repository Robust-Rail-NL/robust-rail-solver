#nullable enable

namespace ServiceSiteScheduling.NoProto
{
    public class Location
    {
        public IList<TrackPart>? TrackParts { get; set; }
        public IList<Facility>? Facilities { get; set; }
        public IList<TaskType>? TaskTypes { get; set; }
    }

    public class Resource
    {
        public string? Name { get; set; }
        public ulong? FacilityId { get; internal set; }
        public ulong? TrackPartId { get; internal set; }
    }

    public class Facility
    {
        public ulong? Id { get; set; }
        public string? Type { get; set; }
        public IList<TrackPart>? RelatedTrackParts { get; set; }
        public IList<TaskType>? TaskTypes { get; set; }
        public int? SimultaneousUsageCount { get; set; }
    }

    public enum TrackPartType
    {
        RailRoad = 0,

        // Switches
        Switch = 1,
        EnglishSwitch = 2,
        HalfEnglishSwitch = 3,

        // Other
        Intersection = 4,
        Bumper = 5,
    }

    public class TrackPart
    {
        public required ulong Id { get; set; }
        public TrackPartType? Type { get; set; }
        public IList<TrackPart>? ASide { get; set; }
        public IList<TrackPart>? BSide { get; set; }
        public double? Length { get; set; }
        public string? Name { get; set; }
        public required bool SawMovementAllowed { get; set; }
        public required bool ParkingAllowed { get; set; }
    }

    public class TaskType
    {
        public PredefinedTaskType? Predefined { get; set; }
        public string? Other { get; set; }
    }

    public enum PredefinedTaskType
    {
        // Movement
        Move = 0,
        Split = 1,
        Combine = 2,

        // Special
        Wait = 3,
        Arrive = 4,
        Exit = 5,

        // StandOut = 6,
        // StandIn = 7,
    }
}
