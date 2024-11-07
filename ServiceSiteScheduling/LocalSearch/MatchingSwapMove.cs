using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.LocalSearch
{
    class MatchingSwapMove : LocalSearchMove
    {
        public Part First { get; private set; }
        public Part Second { get; private set; }

        private Tasks.MoveTask firstprevious, secondprevious;
        private List<Tasks.ParkingTask> parking;
        private bool firstskipped, secondskipped;

        public MatchingSwapMove(PlanGraph graph, Part first, Part second) : base(graph)
        {
            this.First = first;
            this.Second = second;
            this.parking = new List<Tasks.ParkingTask>();

            this.firstprevious = this.First.Train.Routing.PreviousMove;
            this.secondprevious = this.Second.Train.Routing.PreviousMove;

            //this.AffectedTracks = new BitSet(ProblemInstance.Current.Tracks.Length);
        }

        public override SolutionCost Execute()
        {
            var firsttask = this.First.Train.Routing.GetPrevious(t => t.Train.UnitBits[this.First.Units[0].Index]).First();
            var secondtask = this.Second.Train.Routing.GetPrevious(t => t.Train.UnitBits[this.Second.Units[0].Index]).First();

            var firstservice = firsttask as Tasks.ServiceTask;
            if (firstservice != null)
            {
                var park = firstservice.Next.GetSkippedParking(firstservice.Train);
                firstservice.Next.UnskipParking(firstservice.Train);
                //this.AffectedTracks[park.Track.Index] = true;
                firsttask = park;
                this.firstskipped = true;
            }

            var secondservice = secondtask as Tasks.ServiceTask;
            if (secondservice != null)
            {
                var park = secondservice.Next.GetSkippedParking(secondservice.Train);
                secondservice.Next.UnskipParking(secondservice.Train);
                //this.AffectedTracks[park.Track.Index] = true;
                secondtask = park;
                this.secondskipped = true;
            }

            this.swap(firsttask, secondtask);

            // make sure no resource conflicts occur
            if (firstservice != null && (firstservice.NextServiceTask?.Previous.MoveOrder ?? double.PositiveInfinity) > this.Second.Train.Routing.MoveOrder)
            {
                var park = firsttask as Tasks.ParkingTask;
                park.Next.SkipParking(park);
                firsttask = firstservice;
            }

            if (secondservice != null)
            {
                var park = secondtask as Tasks.ParkingTask;
                park.Next.SkipParking(park);
                secondtask = secondservice;
            }

            // make sure that the departure happens after the task is finished
            if (this.First.Train.Routing.MoveOrder < secondtask.Previous.MoveOrder)
            {
                foreach (var task in this.First.Train.Routing.Previous)
                {
                    var otherservice = task as Tasks.ServiceTask;
                    if (otherservice != null && task != secondtask && (otherservice.NextServiceTask?.Previous.MoveOrder ?? double.PositiveInfinity) < this.First.Train.Routing.MoveOrder)
                    {
                        var park = this.First.Train.Routing.GetSkippedParking(task.Train);
                        this.parking.Add(park);
                        //this.AffectedTracks[park.Track.Index] = true;
                        this.First.Train.Routing.UnskipParking(task.Train);
                    }
                }
                this.First.Train.Routing.InsertAfter(secondtask.Previous);
            }

            this.First.Train.Routing.UpdatePreviousTaskOrder();
            this.Second.Train.Routing.UpdatePreviousTaskOrder();
            
            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            foreach (var p in this.parking)
                this.First.Train.Routing.SkipParking(p);

            var firsttask = this.Second.Train.Routing.GetPrevious(t => t.Train.UnitBits[this.Second.Units[0].Index]).First();
            var secondtask = this.First.Train.Routing.GetPrevious(t => t.Train.UnitBits[this.First.Units[0].Index]).First();

            var firstservice = firsttask as Tasks.ServiceTask;
            if (firstservice != null)
            {
                var park = firstservice.Next.GetSkippedParking(firstservice.Train);
                firstservice.Next.UnskipParking(firstservice.Train);
                firsttask = park;
            }

            var secondservice = secondtask as Tasks.ServiceTask;
            if (secondservice != null)
            {
                var park = secondservice.Next.GetSkippedParking(secondservice.Train);
                secondservice.Next.UnskipParking(secondservice.Train);
                secondtask = park;
            }

            this.swap(secondtask, firsttask);

            this.First.Train.Routing.InsertAfter(this.firstprevious);
            this.Second.Train.Routing.InsertAfter(this.secondprevious);

            if (this.firstskipped)
                firsttask.Next.SkipParking(firsttask as Tasks.ParkingTask);
            if (this.secondskipped)
                secondtask.Next.SkipParking(secondtask as Tasks.ParkingTask);

            this.First.Train.Routing.UpdatePreviousTaskOrder();
            this.Second.Train.Routing.UpdatePreviousTaskOrder();

            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var matchingmove = move as MatchingSwapMove;
            if (matchingmove == null)
                return false;

            return this.First == matchingmove.First || this.Second == matchingmove.First || this.First == matchingmove.Second || this.Second == matchingmove.Second;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: swap matching of {string.Join(",", this.First.Units.Select(unit => unit.Index))} and {string.Join(",", this.Second.Units.Select(unit => unit.Index))}";
        }

        protected void swap(Tasks.TrackTask firsttask, Tasks.TrackTask secondtask)
        {
            if (this.First.Train != this.Second.Train)
            {
                this.First.Train.Routing.Previous.Remove(firsttask);
                this.Second.Train.Routing.Previous.Remove(secondtask);

                this.First.Train.Routing.Previous.Add(secondtask);
                secondtask.Next = this.First.Train.Routing;
                this.Second.Train.Routing.Previous.Add(firsttask);
                firsttask.Next = this.Second.Train.Routing;
            }

            // swap
            for (int i = 0; i < this.First.Units.Length; i++)
            {
                Unit firstunit = this.First.Units[i], secondunit = this.Second.Units[i];
                int temp = firstunit.Index;
                firstunit.Index = secondunit.Index;
                secondunit.Index = temp;
            }

            // update trains
            var newfirsttrain = this.Graph.Matching.GetShuntTrain(this.First.Train);
            this.First.Train.Task.Train = newfirsttrain;
            this.First.Train.Routing.Train = newfirsttrain;

            var newsecondtrain = this.Graph.Matching.GetShuntTrain(this.Second.Train);
            this.Second.Train.Task.Train = newsecondtrain;
            this.Second.Train.Routing.Train = newsecondtrain;
        }

        public static IList<MatchingSwapMove> GetMoves(PlanGraph graph)
        {
            List<MatchingSwapMove> moves = new List<MatchingSwapMove>();

            foreach (var departureparts in graph.Matching.DeparturePartsByType)
            {
                for (int i = 0; i < departureparts.Count; i++)
                {
                    var part1 = departureparts[i];
                    if (part1.IsFixed)
                        continue;

                    for (int j = i + 1; j < departureparts.Count; j++)
                    {
                        var part2 = departureparts[j];
                        if (part2.IsFixed)
                            continue;

                        if (Math.Abs(part1.DepartureTrain.Time - part2.DepartureTrain.Time) > 4 * Time.Hour)
                            continue;

                        // swap
                        MatchingSwapMove move = part1.Train.Routing.MoveOrder < part2.Train.Routing.MoveOrder ? new MatchingSwapMove(graph, part1, part2) : new MatchingSwapMove(graph, part2, part1);
                        moves.Add(move);
                    }
                }
            }

            return moves;
        }
    }
}
