#nullable enable

namespace ServiceSiteScheduling.Tasks
{
    class ParkingTask : TrackTask
    {
        public bool IsInserted { get; }

        public ParkingTask(Trains.ShuntTrain train, TrackParts.Track track, bool isinserted = false)
            : this(train, track, TrackTaskType.Parking, isinserted) { }

        protected ParkingTask(
            Trains.ShuntTrain train,
            TrackParts.Track track,
            TrackTaskType type,
            bool isinserted
        )
            : base(train, track, type)
        {
            this.IsInserted = isinserted;
        }
    }

    /// <summary>
    /// The first task of an inStanding train: it is already parked in the yard when
    /// the scenario starts, so it has no ArrivalTask. Deriving from ParkingTask keeps
    /// all parking and track-occupation behaviour identical; only plan serialisation
    /// and the PlanGraph structure check distinguish the two.
    /// </summary>
    class StandInTask : ParkingTask
    {
        public StandInTask(Trains.ShuntTrain train, TrackParts.Track track)
            : base(train, track, TrackTaskType.StandIn, false) { }
    }

    /// <summary>
    /// The final task of an outStanding train: it remains in the yard after the
    /// scenario ends, so it has no DepartureTask. See <see cref="StandInTask"/>.
    /// </summary>
    class StandOutTask : ParkingTask
    {
        public StandOutTask(Trains.ShuntTrain train, TrackParts.Track track)
            : base(train, track, TrackTaskType.StandOut, false) { }
    }
}
