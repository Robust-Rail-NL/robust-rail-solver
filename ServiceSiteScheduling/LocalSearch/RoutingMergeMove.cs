using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Solutions;

namespace ServiceSiteScheduling.LocalSearch
{
    class RoutingMergeMove : RoutingMove
    {
        public Tasks.RoutingTask To { get; private set; }
        public Tasks.MoveTask From { get; private set; }
        public Tasks.ParkingTask Parking { get; private set; }
        public bool MoveFirst { get; private set; }

        protected Tasks.MoveTask originalposition;

        public RoutingMergeMove(PlanGraph graph, Tasks.ParkingTask parking, bool moveFirst) : base(graph)
        {
            this.Parking = parking;
            this.To = parking.Previous as Tasks.RoutingTask;
            this.From = parking.Next;
            this.MoveFirst = moveFirst;

            this.originalposition = moveFirst ? this.To.PreviousMove : this.From.PreviousMove;

            /*this.AffectedTracks = new Utilities.BitSet(ProblemInstance.Current.Tracks.Length);
            this.AffectedTracks[this.Parking.Track.Index] = true;
            this.AffectedTracks[this.From.ToTrack.Index] = true;
            foreach (var task in this.To.AllPrevious)
                this.AffectedTracks[task.Track.Index] = true;*/
        }

        public override SolutionCost Execute()
        {
            if (this.MoveFirst)
                this.To.InsertBefore(this.From);
            else
                this.From.InsertAfter(this.To);

            this.From.SkipParking(this.Parking);

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            this.From.UnskipParking(this.Parking.Train);
            if (this.MoveFirst)
                this.To.InsertAfter(this.originalposition);
            else
                this.From.InsertAfter(this.originalposition);
            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var mergemove = move as RoutingMergeMove;
            if (mergemove == null)
            {
                var shiftmove = move as RoutingShiftMove;
                if (shiftmove == null)
                    return false;

                return this.From == shiftmove.Selected || this.To == shiftmove.Selected || this.From == shiftmove.Position || this.To == shiftmove.Position;
            }

            return this.From == mergemove.From || this.To == mergemove.From || this.From == mergemove.To || this.To == mergemove.To;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: merged parking {this.Parking}";
        }

        public static bool Allowed(Tasks.MoveTask first, Tasks.MoveTask second)
        {
            if (first == null || second == null || first.SkipsParking || ((first as Tasks.RoutingTask)?.IsSplit ?? false) || second.TaskType == Tasks.MoveTaskType.Departure)
                return false;

            if (second.AllNext.Any(task => task is Tasks.DepartureTask) && (second.AllPrevious.Any(task => task is Tasks.ArrivalTask) || first.AllPrevious.Any(task => task is Tasks.ArrivalTask)))
                return false;

            if (!first.Train.Equals(second.Train) && !(second is Tasks.DepartureRoutingTask && first.Train.UnitBits.IsSubsetOf(second.Train.UnitBits)))
                return false;

            if (first.AllNext.First() is Tasks.ParkingTask)
                return true;

            return false;
        }
    }
}
