using ServiceSiteScheduling.TrackParts;

namespace ServiceSiteScheduling.Servicing
{
    public enum ServiceLocationType { Fixed, Free }
    class ServiceType
    {
        public static ServiceType None { get; } = new ServiceType(0, "None", ServiceLocationType.Free);

        public int Index { get; private set; }
        public string Name { get; private set; }
        public ServiceLocationType LocationType { get; set; }
        public List<ServiceResource> Resources { get; private set; }
        public HashSet<Track> Tracks { get; private set; }

        public ServiceType(int index, string name, ServiceLocationType locationtype)
        {
            this.Index = index;
            this.Name = name;
            this.Tracks = new HashSet<Track>();
            this.Resources = new List<ServiceResource>();
            this.LocationType = locationtype;
        }

        public override string ToString()
        {
            return $"{this.Name} tracks={string.Join(", ", this.Tracks.Select(track => track.ID))}";
        }
    }
}
