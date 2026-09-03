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
        /// <summary>
        /// The scenario end time the train must be parked by. Mirrors what
        /// DepartureTask.ScheduledTime is for a departing train: a fixed value set
        /// once, kept apart from <see cref="TrackTask.Start"/> because
        /// PlanGraph.ComputeModel overwrites Start on every pass with wherever the
        /// chain's forward move actually lands it. The two happen to coincide for a
        /// DepartureTask, whose preceding move is scheduled backward from
        /// ScheduledTime so it lands there whenever the rest of the chain allows;
        /// a StandOutTask's preceding move is instead scheduled forward with no
        /// awareness of this deadline at all ("no fixed deadline, schedule
        /// forward" in PlanGraph.ComputeTime), so Start may legitimately land past
        /// it. Without this separate field, Deadline would be indistinguishable
        /// from Start and get silently overwritten the same way -- which is
        /// exactly how solver#14 happened.
        /// </summary>
        public Utilities.Time Deadline { get; set; }

        public StandOutTask(Trains.ShuntTrain train, TrackParts.Track track)
            : base(train, track, TrackTaskType.StandOut, false) { }
    }
}
