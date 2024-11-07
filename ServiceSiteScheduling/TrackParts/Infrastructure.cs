using System.Collections.Generic;

namespace ServiceSiteScheduling.TrackParts
{
    abstract class Infrastructure
    {
        public ulong ID;
        public string PrettyName;

        public Infrastructure(ulong id, string prettyname)
        {
            this.ID = id;
            this.PrettyName = prettyname;
        }

        public abstract IList<TrackSwitchContainer> GetTracksConnectedTo(Infrastructure infrastructure, int switches, List<Infrastructure> path, bool ignoreInactive = true);

        public override string ToString()
        {
            return $"{this.ID.ToString()} {this.PrettyName}";
        }
    }

    class TrackSwitchContainer
    {
        public Track Track { get; }
        public int Switches { get; }
        public Side Side { get; }
        public Infrastructure[] Path { get; }
        
        public TrackSwitchContainer(Track track, int switchcost, Side side, Infrastructure[] path)
        {
            this.Track = track;
            this.Switches = switchcost;
            this.Side = side;
            this.Path = path;
        }

        public override string ToString()
        {
            return $"{this.Track.ID.ToString()}{this.Side} {this.Switches}";
        }
    }
}
