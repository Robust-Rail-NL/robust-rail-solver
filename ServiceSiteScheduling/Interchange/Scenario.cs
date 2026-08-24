#nullable enable

using System.Collections;

namespace ServiceSiteScheduling.Interchange
{
    // A Scenario contains the part of the problem specification which varies daily,
    // that is the trains which come in and go out of the shunting area.
    public record Scenario
    {
        public int? SchemaVersion { get; init; }

        public required IList<TrainUnitType> TrainUnitTypes { get; init; }

        public required IList<IncomingTrain> In { get; init; }
        public required IList<TrainRequest> Out { get; init; }

        public IList<IncomingTrain>? InStanding { get; init; }
        public IList<TrainRequest>? OutStanding { get; init; }

        public required ulong StartTime { get; init; }
        public required ulong EndTime { get; set; }
    }

    // Defines all trains arriving at the shunting area.
    public record ScenarioIn
    {
        public IList<IncomingTrain> Trains { get; init; } = [];
    }

    // Defines all requests for trains leaving the shunting area.
    public record ScenarioOut
    {
        public IList<TrainRequest> TrainRequests { get; init; } = [];
    }

    // Defines all trains that were alrady at the shunting area (before the scenario starts).
    public record ScenarioInStanding
    {
        public IList<IncomingTrain> Trains { get; init; } = [];
    }

    // Defines all trains that will stay at the shunting area (after the scenario ends).
    public record ScenarioOutStanding
    {
        public IList<TrainRequest> TrainRequests { get; init; } = [];
    }

    // An incoming train
    public record IncomingTrain
    {
        // The TrackPart ID of the location this train arrives over.
        public required ulong EntryTrackPart { get; set; }

        // The TrackPart ID of the location this train is at after arriving.
        public required ulong FirstParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        public ulong? Id { get; set; }

        public IList<IncomingTrainUnit> Members { get; init; } = [];

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track. Required and mutually distinct within
        // inStanding groups sharing a track; see unified-schema-design.md's
        // "Standing order" decision.
        public int? StandingIndex { get; set; }
    }

    // A request for a train to leave the shunting area
    public class TrainRequest
    {
        // The TrackPart ID of the location this train leaves over.
        public required ulong LeaveTrackPart { get; set; }

        // The TrackPart ID of the location this train is at before leaving.
        public required ulong LastParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        // Unique ID of this train request
        public ulong? Id { get; set; }

        // Outgoing train units; if in a TrainUnit the id field is not specified, then any train unit will do, provided that the other fields (train type, number of carriages) are still adhered to.
        public IList<TrainUnit>? TrainUnits { get; set; }

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track. Optional within outStanding: null means
        // no preference; see unified-schema-design.md's "Standing order" decision.
        public int? StandingIndex { get; set; }
    }

    // TrainUnit represents a combination of carriages which can move independently.
    public record TrainUnit
    {
        // A unique identifier of the unit. Null means "any unit of the
        // matching type is acceptable" (used for departing trains whose
        // exact composition isn't tracked).
        public uint? Id { get; set; }

        public string? TypePrefix { get; set; }
        public uint? Carriages { get; set; }

        // The (TypePrefix, Carriages) pair identifying this unit's TrainUnitType.
        // Nullable fields are unwrapped here on the assumption that, by the
        // time this is called, the unit's type has already been resolved.
        public (string TypePrefix, uint Carriages) TypeDisplayName() =>
            (this.TypePrefix!, this.Carriages!.Value);

        public IList<TaskSpec> Tasks { get; init; } = [];
    }

    public record IncomingTrainUnit
    {
        public required uint Id { get; set; }

        public required string TypePrefix { get; set; }
        public required uint Carriages { get; set; }

        // The (TypePrefix, Carriages) pair identifying this unit's TrainUnitType.
        public (string TypePrefix, uint Carriages) TypeDisplayName() =>
            (this.TypePrefix, this.Carriages);

        // Tasks for this train unit
        public IList<TaskSpec> Tasks { get; init; } = [];
    }

    // TrainUnitType is a type of train unit
    public class TrainUnitType
    {
        public override bool Equals(object? obj)
        {
            return obj is TrainUnitType other && other.TypeDisplayName() == this.TypeDisplayName();
        }

        public override int GetHashCode()
        {
            return this.TypeDisplayName().GetHashCode();
        }

        // The (TypePrefix, Carriages) pair identifying this TrainUnitType.
        // Two variants of the same family (e.g. SLT-4 and SLT-6) share a
        // TypePrefix and are only disambiguated by Carriages.
        public (string TypePrefix, uint Carriages) TypeDisplayName() =>
            (this.TypePrefix, this.Carriages);

        // Name of the train unit type family, e.g. "SLT" or "VIRM".
        public required string TypePrefix { get; set; }

        // Number of carriages. This is the total number of carriages,
        // including the first and last carriage.
        public required uint Carriages { get; set; }

        // Length of this train unit, in meters
        public double? Length { get; set; }

        // Time it takes to perform a combine in seconds
        public ulong? CombineDuration { get; set; }

        // Time it takes to perform a split in seconds
        public ulong? SplitDuration { get; set; }

        // kopmaaktijd = backNormTime + #carriage * backAdditionTime
        public ulong? BackNormTime { get; set; }
        public ulong? BackAdditionTime { get; set; }

        // this is the speed of the train but that is yet to be determinded wether that is here or location specific #warning
        public ulong? TravelSpeed { get; set; }

        // Startup + Shutdown
        public ulong? StartUpTime { get; set; }

        // This TrainUnitType needs a locomotive, e.g. it cannot drive itself
        public bool NeedsLoco { get; set; }

        // Can pull/push other wagons
        public bool IsLoco { get; set; }

        // This train needs electricity, so it can only drive on electrified trackparts
        public bool NeedsElectricity { get; set; }

        // Prefix of train IDs of this type (i.e., the last two digits are removed)
        // For example, for SLT4 this is 24
        public int? IdPrefix { get; set; }
    }

    // A ShuntingUnit is a combination of TrainUnits,
    // and moves as a unit at some point in time.
    public class ShuntingUnit
    {
        public ShuntingUnit(ulong id)
        {
            this.MemberIDs = [];
            this.ParentIDs = [];
            this.ChildIDs = [];
            this.Id = id;
        }

        public ShuntingUnit(ShuntingUnit other)
        {
            this.Id = other.Id;
            this.MemberIDs = new List<uint>(other.MemberIDs);
            this.ParentIDs = new List<ulong>(other.ParentIDs);
            this.ChildIDs = new List<ulong>(other.ChildIDs);
        }

        // Unique ID of this ShuntingUnit
        public ulong Id { get; set; }

        // The TrainUnits contained in this ShuntingUnit
        public IList<uint> MemberIDs { get; set; }

        // The parents of a current ShuntingUnit,
        // that is, the shuntingunits which have been merged into this one,
        // or the shuntingunit that has been split into (among others) this one.
        public IList<ulong> ParentIDs { get; set; }

        // The children of the current ShuntingUnit,
        // that is, the shuntingunits which contain parts of this shuntingunit.
        // Alternatively, ShuntingUnit S has parent P iff P has child S.
        public IList<ulong> ChildIDs { get; set; }
    }

    // A task specification specifies a certain task.
    public class TaskSpec
    {
        // The type of the task
        public TaskType? Type { get; set; }

        // Time this task takes, in seconds
        public ulong? Duration { get; set; }

        // The skills required to perform the task. Each entry in the list indicates that a member of staff
        // with the given skill is required.
        // Examples:
        // [] => no personnel required
        // ["B-controle"] => one member of staff with skill "B-controle" required
        // ["B-controle", "B-controle"] => two members of staff with skill "B-controle" required
        public IList<string>? RequiredSkills { get; set; }
    }
}
