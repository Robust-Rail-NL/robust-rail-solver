using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Trains
{
    class ArrivalTrain
    {
        public int Length { get; private set; }
        public TrainUnit[] Units { get; private set; }
        public Track[] ParkingLocations { get; set; }
        public Track Track { get; private set; }
        public Side Side { get; private set; }
        public Time Time { get; private set; }

        public bool InStanding {get; set;}
        // TODO: add bolean isInstanding
        public ArrivalTrain(TrainUnit[] units, Track track, Side side, Time time, bool inStanding = false)
        {
            this.Units = units;
            this.Length = units.Sum(unit => unit.Type.Length);
            this.Track = track;
            this.Side = side;
            this.Time = time;
            this.InStanding = inStanding;

            List<Track> allowedparking = new List<Track>();
            IEnumerable<TrainType> types = units.Select(unit => unit.Type).Distinct();
            foreach (Track t in units[0].Type.ParkingLocations)
                if (types.All(type => type.ParkingLocations.Contains(t)))
                    allowedparking.Add(t);
            this.ParkingLocations = allowedparking.ToArray();
        }

        public ArrivalTrain(TrainUnit unit, Track track, Side side, Time time, bool inStanding = false) : this(new TrainUnit[1] { unit }, track, side, time, inStanding) { }

        public override string ToString()
        {
            return $"{string.Join(",", this.Units.Select(unit => unit.ToString()))} at {this.Time}";
        }

        // Returns if the arrival train is an instanding one which was already in the shunting yard before the scenario started
        public bool IsItInStanding()
        {
            return this.InStanding;
        }
    }
}
