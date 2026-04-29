#nullable enable

namespace ServiceSiteScheduling.Matching
{
    class Unit(Trains.DepartureTrainUnit unit, Train train)
    {
        public Trains.DepartureTrainUnit Departure { get; private set; } = unit;

        /// <summary>
        ///  The index of this unit in e.g. the array of TrainUnits maintained in ProblemInstance.TrainUnits.
        /// </summary>
        public int Index { get; set; }
        public Train Train { get; set; } = train;
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
