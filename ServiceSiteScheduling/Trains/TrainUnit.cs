using ServiceSiteScheduling.Servicing;

namespace ServiceSiteScheduling.Trains
{
    class TrainUnit : IEquatable<TrainUnit>
    {
        public string Name { get; set; }
        public int Index { get; private set; }
        public TrainType Type { get; private set; }
        public Service[] RequiredServices { get; set; }
        public Utilities.Time[] ServiceDurations { get; private set; }

        public TrainUnit(string name, int index, TrainType type, Service[] required, ServiceType[] types)
        {
            this.Name = name;
            this.Index = index;
            this.Type = type;
            this.RequiredServices = required;
            this.ServiceDurations = new Utilities.Time[types.Length];
            foreach (var service in required)
                ServiceDurations[service.Type.Index] = service.Duration;
        }

        public TrainUnit(int index, TrainType type, Service[] required, ServiceType[] types) : this(index.ToString(), index, type, required, types)
        {
        }

        public bool Equals(TrainUnit other)
        {
            return this.Index == other.Index;
        }

        public override string ToString()
        {
            return $"{this.Name} {this.Index} ({this.Type.Name})";
        }
    }
}
