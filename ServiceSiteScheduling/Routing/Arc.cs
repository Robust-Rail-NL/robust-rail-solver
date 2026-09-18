using System.Diagnostics;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Routing
{
    public enum ArcType
    {
        Reverse,
        Switch,
        Track,
    }

    class Arc
    {
        public Vertex Tail,
            Head;
        public ArcType Type;
        public TrackSwitchContainer Path;

        public Time Duration;
        public int Crossings;
        public int Cost;

        public int Switches
        {
            get { return this.Path.Switches; }
        }

        // The single Track a Reverse arc reverses on. Tail and Head always
        // resolve to the same Track for this arc type: a reversal never
        // leaves its track, and Graph.cs only ever creates a Reverse arc
        // between two of one track's own four vertices (aa<->ab, bb<->ba -
        // see BuildGraph's "Add reversal arcs" block), never across tracks.
        public Track ReversalTrack
        {
            get
            {
                Debug.Assert(
                    this.Type == ArcType.Reverse,
                    "ReversalTrack is only meaningful for a Reverse arc"
                );
                return this.Tail.SuperVertex.Track;
            }
        }

        public Arc(Vertex tail, Vertex head, ArcType type, TrackSwitchContainer path)
        {
            this.Tail = tail;
            this.Head = head;
            this.Type = type;
            this.Path = path;
        }

        public void ComputeCost(ShuntTrain train)
        {
            Parking.TrackOccupation trackOccupation = this.Head.SuperVertex.TrackOccupation;
            switch (this.Type)
            {
                case ArcType.Track:
                    this.Cost = this.Duration = Settings.TrackCrossingTime;
                    if (trackOccupation != null)
                        this.Cost +=
                            Settings.CrossingWeight
                            * (this.Crossings = trackOccupation.StateDeque.Count > 0 ? 1 : 0);
                    break;
                case ArcType.Reverse:
                    this.Duration = Settings.TrackCrossingTime + train.ReversalDuration;
                    if (trackOccupation != null)
                        this.Crossings = trackOccupation.CountCrossingsIfTurning(
                            train,
                            this.Head.TrackSide
                        );
                    this.Cost = this.Duration + Settings.CrossingWeight * this.Crossings;
                    break;
                case ArcType.Switch:
                    this.Crossings = 0;
                    this.Cost = this.Duration = this.Path.Switches * Settings.SwitchCrossingTime;
                    break;
            }
        }

        public override string ToString()
        {
            return $"{this.Tail}->{this.Head} ({this.Type})";
        }
    }
}
