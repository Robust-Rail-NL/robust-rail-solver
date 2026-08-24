#nullable enable

namespace ServiceSiteScheduling.Matching
{
    class Part(Unit[] units)
    {
        public Unit[] Units { get; private set; } = units;
        public Trains.TrainType[] Types
        {
            get
            {
                return this
                    .Units.Select(unit => unit.Departure.Unit?.Type ?? unit.Departure.Type)
                    .ToArray();
            }
        }
        public bool IsFixed
        {
            get { return this.Units.Any(unit => unit.IsFixed); }
        }
        public TrainMatching Matching { get; set; } = null!;
        public Train? Train
        {
            get { return this.Units[0].Train; }
        }
        public Trains.DepartureTrain DepartureTrain
        {
            get { return this.Units[0].Departure.Train; }
        }
    }
}
