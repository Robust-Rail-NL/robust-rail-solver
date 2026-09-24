namespace ServiceSiteScheduling.TrackParts
{
    abstract class Connection : Infrastructure
    {
        public Dictionary<Infrastructure, IList<Infrastructure>> Connections;

        protected int cost = 1;

        // How many plain switches this Connection counts as when crossed
        // (e.g. an EnglishSwitch/HalfEnglishSwitch is 2, an Intersection is
        // free) - see each subclass's constructor.
        public int Cost => this.cost;

        public Connection(ulong id, string name)
            : base(id, name)
        {
            this.Connections = [];
        }

        public override IList<TrackSwitchContainer> GetTracksConnectedTo(
            Infrastructure infrastructure,
            int switches,
            List<Infrastructure> path,
            bool ignoreInactive = true
        )
        {
            path.Add(this);
            List<TrackSwitchContainer> result = [];

            foreach (var infra in this.GetInfrastructureConnectedTo(infrastructure))
                result.AddRange(
                    infra.GetTracksConnectedTo(this, switches + this.cost, path, ignoreInactive)
                );
            path.RemoveAt(path.Count - 1);
            return result;
        }

        public IList<Infrastructure> GetInfrastructureConnectedTo(Infrastructure infrastructure)
        {
            return this.Connections[infrastructure];
        }
    }
}
