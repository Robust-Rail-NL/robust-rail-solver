using ServiceSiteScheduling.Servicing;

namespace ServiceSiteScheduling.TrackParts
{
    class Track : Infrastructure
    {
        public Infrastructure ASide { get; private set; }
        public Infrastructure BSide { get; private set; }

        public readonly ServiceType[] Services;
        public readonly int Length;
        public Side Access { get; private set; }
        public readonly bool CanReverse;
        public readonly int Index;
        public bool IsActive;

        public readonly bool CanPark;

        public Track(
            ulong id,
            string name,
            ServiceType service,
            int length,
            Side access,
            bool canpark,
            bool canreverse,
            int index
        )
            : base(id, name)
        {
            this.Services = [service];
            this.Length = length;
            this.Access = access;
            this.CanReverse = canreverse;
            this.CanPark = canpark;
            this.IsActive = canpark | canreverse;
            this.Index = index;
        }

        public Side GetSide(Infrastructure needle)
        {
            if (this.ASide == needle)
                return Side.A;
            if (this.BSide == needle)
                return Side.B;

            throw new ArgumentException("Needle not connected to this track");
        }

        public IList<TrackSwitchContainer> GetConnectionsAtSide(Side side)
        {
            var path = new List<Infrastructure> { this };
            if (side == Side.A)
                return this.ASide.GetTracksConnectedTo(this, 0, path);
            if (side == Side.B)
                return this.BSide.GetTracksConnectedTo(this, 0, path);
            throw new ArgumentException("Side is not valid");
        }

        public Infrastructure GetInfrastructureAtSide(Side side)
        {
            if (side == Side.A)
                return this.ASide;
            if (side == Side.B)
                return this.BSide;
            throw new ArgumentException("Side is not valid");
        }

        public override IList<TrackSwitchContainer> GetTracksConnectedTo(
            Infrastructure infrastructure,
            int switches,
            List<Infrastructure> path,
            bool ignoreInactive = true
        )
        {
            if (infrastructure == this.ASide || infrastructure == this.BSide)
            {
                path.Add(this);
                IList<TrackSwitchContainer> result = null;
                if (this.IsActive || !ignoreInactive)
                    result = new TrackSwitchContainer[1]
                    {
                        new(this, switches, this.GetSide(infrastructure), path.ToArray()),
                    };
                else if (infrastructure == this.ASide)
                    result =
                        this.BSide == null
                            ? Array.Empty<TrackSwitchContainer>()
                            : this.BSide.GetTracksConnectedTo(this, switches, path);
                else
                    result =
                        this.ASide == null
                            ? Array.Empty<TrackSwitchContainer>()
                            : this.ASide.GetTracksConnectedTo(this, switches, path);
                path.RemoveAt(path.Count - 1);
                return result;
            }
            throw new ArgumentException("Track not connected to this track.");
        }

        public void Connect(Side side, Infrastructure infra)
        {
            if (side == Side.A)
                this.ASide = infra;
            else
                this.BSide = infra;
        }

        public void Connect(Infrastructure A, Infrastructure B)
        {
            this.ASide = A;
            this.BSide = B;
            if (A != null && A is not GateWay && B != null && B is not GateWay)
                this.Access = Side.Both;
            else if (A != null && A is not GateWay)
                this.Access = Side.A;
            else if (B != null && B is not GateWay)
                this.Access = Side.B;
            else
                this.Access = Side.None;
        }

        public override string ToString()
        {
            return $"{this.ID} {this.PrettyName} ({this.Length} {this.Access})";
        }
    }
}
