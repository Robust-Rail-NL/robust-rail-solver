using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Routing
{
    public enum ArcType { Reverse, Switch, Track }
    class Arc
    {
        public Vertex Tail, Head;
        public ArcType Type;
        public TrackSwitchContainer Path;

        public Time Duration;
        public int Crossings;
        public int Cost;

        public int Switches { get { return this.Path.Switches; } }

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
                        this.Cost += Settings.CrossingWeight * /*(this.Crossings = trackOccupation.StateDeque.Count);//*/(this.Crossings = trackOccupation.StateDeque.Count > 0 ? 1 : 0);
                    break;
                case ArcType.Reverse:
                    this.Duration = Settings.TrackCrossingTime + train.ReversalDuration;
                    if (trackOccupation != null)
                        this.Crossings = trackOccupation.CountCrossingsIfTurning(train, this.Head.TrackSide);
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
