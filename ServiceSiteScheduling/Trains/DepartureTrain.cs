namespace ServiceSiteScheduling.Trains
{
    class DepartureTrain
    {
        public TrackParts.Track Track { get; private set; }
        public Side Side { get; private set; }
        public Utilities.Time Time { get; private set; }
        public DepartureTrainUnit[] Units { get; private set; }

        public bool OutStanding { get; set; }

        public DepartureTrain(
            Utilities.Time time,
            DepartureTrainUnit[] units,
            TrackParts.Track track,
            Side side,
            bool outStanding = false
        )
        {
            this.Track = track;
            this.Time = time;
            this.Side = side;
            this.Units = units;
            this.OutStanding = outStanding;

            foreach (var unit in units)
                unit.Train = this;
        }

        public DepartureTrain(
            Utilities.Time time,
            DepartureTrainUnit unit,
            TrackParts.Track track,
            Side side,
            bool outStanding = false
        )
            : this(time, new DepartureTrainUnit[1] { unit }, track, side, outStanding) { }

        public override string ToString()
        {
            return $"{string.Join(",", this.Units.Select(unit => unit.ToString()))} at {this.Time}";
        }
    }
}
