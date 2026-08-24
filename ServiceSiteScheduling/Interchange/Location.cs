#nullable enable

using System.Collections.Immutable;
using ServiceSiteScheduling.TrackParts;

namespace ServiceSiteScheduling.Interchange
{
    public record Location
    {
        public int? SchemaVersion { get; init; }

        // Required, mirroring the interchange schema: a Location without
        // trackParts is not a location. With a default it deserialized to zero
        // tracks and the solver carried on against an empty yard, which reads
        // as an infeasible instance rather than a malformed file.
        public required ImmutableArray<TrackPart> TrackParts { get; init; }
        public ImmutableArray<Facility> Facilities { get; init; } = [];
        public ImmutableArray<TaskType> TaskTypes { get; init; } = [];
    }

    public record Resource(string Kind, ulong Id)
    {
        internal static Resource FromFacility(Facility facility)
        {
            return new Resource("facility", facility.Id!.Value);
        }

        internal static Resource FromInfra(Infrastructure infra)
        {
            return new Resource("trackPart", infra.ID);
        }
    }

    public record Facility(
        ulong? Id,
        string? Type,
        ImmutableArray<ulong> RelatedTrackPartIDs,
        ImmutableArray<TaskType> TaskTypes,
        int? SimultaneousUsageCount
    );

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
        public ulong Id { get; init; }
        public TrackPartType? Type { get; init; }
        public ImmutableArray<ulong> ASide { get; init; } = [];
        public ImmutableArray<ulong> BSide { get; init; } = [];
        public double? Length { get; init; }
        public string? Name { get; init; }
        public bool SawMovementAllowed { get; init; }
        public bool ParkingAllowed { get; init; }
    }

    public record TaskType
    {
        public PredefinedTaskType? Predefined { get; set; }
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

        private static readonly Dictionary<PredefinedTaskType, TaskType> TaskTypeMap = [];

        public static TaskType FromPredefined(PredefinedTaskType predefined)
        {
            if (TaskTypeMap.TryGetValue(predefined, out var taskType))
            {
                return taskType;
            }
            else
            {
                taskType = new TaskType(predefined, null);
                TaskTypeMap[predefined] = taskType;
                return taskType;
            }
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
        Walking,
        Break,
        NonService,
        StandIn,
        StandOut,
    }
}
