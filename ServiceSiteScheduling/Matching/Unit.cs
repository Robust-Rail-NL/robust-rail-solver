
namespace ServiceSiteScheduling.Matching
{
    class Unit
    {
        public Trains.DepartureTrainUnit Departure { get; private set; }
        public int Index { get; set; }
        public Train Train { get; set; }
        public Part Part { get; set; }
        public bool IsFixed { get { return this.Departure.IsFixed; } }
        public Unit(Trains.DepartureTrainUnit unit)
        {
            this.Departure = unit;
        }
        public override string ToString()
        {
            return $"{this.Departure} ({this.Index})";
        }
    }
}
