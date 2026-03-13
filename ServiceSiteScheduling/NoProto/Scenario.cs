#nullable enable

namespace ServiceSiteScheduling.NoProto
{
    // A Scenario contains the part of the problem specification which varies daily,
    // that is the trains which come in and go out of the shunting area.
    public class Scenario
    {
        public ScenarioIn? In { get; set; }
        public ScenarioOut? Out { get; set; }

        public ScenarioInStanding? InStanding { get; set; }
        public ScenarioOutStanding? OutStanding { get; set; }

        public ulong? StartTime { get; set; }
        public ulong? EndTime { get; set; }
    }

    // Defines all trains arriving at the shunting area.
    public class ScenarioIn
    {
        public IncomingTrain[]? Trains { get; set; }
    }

    // Defines all requests for trains leaving the shunting area.
    public class ScenarioOut
    {
        public TrainRequest[]? TrainRequests { get; set; }
    }

    // Defines all trains that were alrady at the shunting area (before the scenario starts).
    public class ScenarioInStanding
    {
        public IncomingTrain[]? Trains { get; set; }
    }

    // Defines all trains that will stay at the shunting area (after the scenario ends).
    public class ScenarioOutStanding
    {
        public TrainRequest[]? TrainRequests { get; set; }
    }

    // An incoming train
    public class IncomingTrain
    {
        // The TrackPart ID of the location this train arrives over.
        public ulong? EntryTrackPart { get; set; }

        // The TrackPart ID of the location this train is at after arriving.
        public ulong? FirstParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        public string? Id { get; set; }

        public IncomingTrainUnit[]? Members { get; set; }

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track
        public double? StandingIndex { get; set; }
    }

    public class IncomingTrainUnit
    {
        public TrainUnit? TrainUnit { get; set; }

        // Tasks for this train unit
        public TaskSpec[]? Tasks { get; set; }
    }

    // A request for a train to leave the shunting area
    public class TrainRequest
    {
        // The TrackPart ID of the location this train leaves over.
        public ulong? LeaveTrackPart { get; set; }

        // The TrackPart ID of the location this train is at before leaving.
        public ulong? LastParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        // Name of this train
        public string? DisplayName { get; set; }

        // Outgoing train units; if in a TrainUnit the id field is not specified, then any train unit will do, provided that the other fields (train type, number of carriages) are still adhered to.
        public TrainUnit[]? TrainUnits { get; set; }

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track
        public double? StandingIndex { get; set; }
    }

    // TrainUnit represents a combination of carriages which can move independently.
    public class TrainUnit
    {
        // A unique identifier of the unit
        public string? Id { get; set; }

        public TrainUnitType? Type { get; set; }
    }

    // TrainUnitType is a type of train unit
    public class TrainUnitType
    {
        // Name of the train unit type
        // For example, "SGM" or "SLT".
        public string? DisplayName { get; set; }

        // Number of carriages. This is the total number of carriages,
        // including the first and last carriage.
        public uint? Carriages { get; set; }

        // Length of this train unit, in meters
        public double? Length { get; set; }

        // The time it takes to reverse ("kopmaken"), in seconds
        public ulong? ReversalDuration { get; set; }

        // Time it takes to perform a combine in seconds
        public ulong? CombineDuration { get; set; }

        // Time it takes to perform a split in seconds
        public ulong? SplitDuration { get; set; }

        // kopmaaktijd = backNormTime + #carriage * backAdditionTime
        public ulong? BackNormTime { get; set; }
        public ulong? BackAdditionTime { get; set; }
    }

    // A ShuntingUnit is a combination of TrainUnits,
    // and moves as a unit at some point in time.
    public class ShuntingUnit
    {
        // Unique ID of this ShuntingUnit
        public string? Id { get; set; }

        // The TrainUnits contained in this ShuntingUnit
        public TrainUnit[]? Members { get; set; }

        // The parents of a current ShuntingUnit,
        // that is, the shuntingunits which have been merged into this one,
        // or the shuntingunit that has been split into (among others) this one.
        public string[]? ParentIDs { get; set; }

        // The children of the current ShuntingUnit,
        // that is, the shuntingunits which contain parts of this shuntingunit.
        // Alternatively, ShuntingUnit S has parent P iff P has child S.
        public string[]? ChildIDs { get; set; }

        // If field is defined it states InStanding when the train unit was alredy on the yard even if the action says it is an arrival
        // or it states OutStanding when the train unit will stay in the shunting yards after the scenario ends even if the action is an exite one
        public string? StandingType { get; set; }
    }

    // A task specification specifies a certain task.
    public class TaskSpec
    {
        // The type of the task
        public TaskType? Type { get; set; }

        // The priority; higher values indicate that this task is more important.
        public uint? priority { get; set; } // TODO set deprecation? or remove wholesale?

        // Time this task takes, in seconds
        public ulong? Duration { get; set; }
    }
}
