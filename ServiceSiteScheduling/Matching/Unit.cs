#nullable enable

namespace ServiceSiteScheduling.Matching
{
    class Unit(Trains.DepartureTrainUnit unit)
    {
        public Trains.DepartureTrainUnit Departure { get; private set; } = unit;
        public int Index { get; set; }
        public Train? Train { get; set; }
        public Part? Part { get; set; }
        public bool IsFixed
        {
            get { return this.Departure.IsFixed; }
        }

        public override string ToString()
        {
            return $"{this.Departure} ({this.Index})";
        }
    }
}
