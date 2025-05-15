
namespace ServiceSiteScheduling.Trains
{
    class DepartureTrainUnit : IEquatable<DepartureTrainUnit>
    {
        public bool IsFixed { get; private set; }
        public TrainType Type { get; private set; }
        public TrainUnit Unit { get; private set; }
        public DepartureTrain Train { get; set; }
        public int ID { get; set; }

        public DepartureTrainUnit(TrainType type)
        {
            this.IsFixed = false;
            this.Type = type;
        }

        public DepartureTrainUnit(TrainUnit unit)
        {
            this.IsFixed = true;
            this.Unit = unit;
        }

        public bool Equals(DepartureTrainUnit other)
        {
            return this.ID == other.ID;
        }

        public override string ToString()
        {
            return $"{this.ID} {(this.IsFixed ? $"{this.Unit.Index}({this.Unit.Type.Name})" : this.Type.Name)}";
        }
    }
}
