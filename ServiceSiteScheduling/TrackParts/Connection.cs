using System;
using System.Collections.Generic;

namespace ServiceSiteScheduling.TrackParts
{
    abstract class Connection : Infrastructure
    {
        public Dictionary<Infrastructure, IList<Infrastructure>> Connections;

        protected int cost = 1;

        public Connection(ulong id, string name) : base(id, name)
        {
            this.Connections = new Dictionary<Infrastructure, IList<Infrastructure>>();
        }

        public override IList<TrackSwitchContainer> GetTracksConnectedTo(Infrastructure infrastructure, int switches, List<Infrastructure> path, bool ignoreInactive = true)
        {
            path.Add(this);
            List<TrackSwitchContainer> result = new List<TrackSwitchContainer>();
            foreach (var infra in this.GetInfrastructureConnectedTo(infrastructure))
                result.AddRange(infra.GetTracksConnectedTo(this, switches + this.cost, path, ignoreInactive));
            path.RemoveAt(path.Count - 1);
            return result;
        }

        public IList<Infrastructure> GetInfrastructureConnectedTo(Infrastructure infrastructure)
        {
            return this.Connections[infrastructure];
        }
    }
}
