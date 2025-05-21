
namespace ServiceSiteScheduling.Trains
{
    class DepartureTrain
    {
        public TrackParts.Track Track { get; private set; }
        public Side Side { get; private set; }
        public Utilities.Time Time { get; private set; }
        public DepartureTrainUnit[] Units { get; private set; }

        public bool InStanding {get; set;}
        public DepartureTrain(Utilities.Time time, DepartureTrainUnit[] units, TrackParts.Track track, Side side, bool inStanding = false)
        {
            this.Track = track;
            this.Time = time;
            this.Side = side;
            this.Units = units;
            this.InStanding = inStanding;

            foreach (var unit in units)
                unit.Train = this;
        }

        public DepartureTrain(Utilities.Time time, DepartureTrainUnit unit, TrackParts.Track track, Side side, bool inStanding = false) : this(time, new DepartureTrainUnit[1] { unit }, track, side, inStanding) { }

        public override string ToString()
        {
            return $"{string.Join(",", this.Units.Select(unit => unit.ToString()))} at {this.Time}";
        }
        
        // Returns if the departure train is an outstanding one which stays in the shunting yard after the scenario ends
        public bool IsItInStanding()
        {
            return this.InStanding;
        }
    }

}
