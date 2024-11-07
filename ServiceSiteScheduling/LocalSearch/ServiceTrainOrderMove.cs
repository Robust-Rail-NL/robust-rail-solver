using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.LocalSearch
{
    class ServiceTrainOrderMove : LocalSearchMove
    {
        public Tasks.ServiceTask First { get; set; }
        public Tasks.ServiceTask Second { get; set; }
        public Trains.ShuntTrain Train { get; set; }

        protected Tasks.MoveTask previoustofirst, previousfromfirst, previoustosecond, previousfromsecond;
        protected Tasks.ParkingTask parkingtofirst, parkingbetween, secondtoparking;

        private bool skippedfromfirst, skippedbetween, skippedtosecond;

        public ServiceTrainOrderMove(PlanGraph graph, Tasks.ServiceTask first, Tasks.ServiceTask second) : base(graph)
        {
            this.First = first;
            this.Second = second;
            this.Train = this.First.Train;

            this.previoustofirst = first.Previous.PreviousMove;
            this.previousfromfirst = first.Next.PreviousMove;
            this.previoustosecond = second.Previous.PreviousMove;
            this.previousfromsecond = second.Next.PreviousMove;
            
            /*this.AffectedTracks = new BitSet(ProblemInstance.Current.Tracks.Length);
            this.AffectedTracks[this.First.Track.Index] = true;
            this.AffectedTracks[this.Second.Track.Index] = true;
            foreach (var task in this.First.Previous.AllPrevious)
                this.AffectedTracks[task.Track.Index] = true;
            foreach (var task in this.First.Next.AllNext)
                this.AffectedTracks[task.Track.Index] = true;
            foreach (var task in this.Second.Previous.AllPrevious)
                this.AffectedTracks[task.Track.Index] = true;
            foreach (var task in this.Second.Next.AllNext)
                this.AffectedTracks[task.Track.Index] = true;
            if (parkingtofirst != null)
                this.AffectedTracks[parkingtofirst.Track.Index] = true;
            if (firsttoparking != null)
                this.AffectedTracks[firsttoparking.Track.Index] = true;
            if (parkingtosecond != null)
                this.AffectedTracks[parkingtosecond.Track.Index] = true;
            if (secondtoparking != null)
                this.AffectedTracks[secondtoparking.Track.Index] = true;*/
        }

        public override SolutionCost Execute()
        {
            Tasks.RoutingTask toFirst = this.First.Previous as Tasks.RoutingTask,
                toSecond = this.Second.Previous as Tasks.RoutingTask;
            Tasks.MoveTask fromFirst = this.First.Next,
                fromSecond = this.Second.Next;

            Tasks.MoveTask fromPreviousOfFirstOnMachine = this.First.PreviousServiceTask?.Next,
                toNextOfFirstOnMachine = this.First.NextServiceTask?.Previous,
                fromPreviousOfSecondOnMachine = this.Second.PreviousServiceTask?.Next,
                toNextOfSecondOnMachine = this.Second.NextServiceTask?.Previous,
                fromPrevious = toFirst.Previous.Previous,
                toNext = fromSecond.AllNext.MinItem(task => task.Next?.MoveOrder ?? double.PositiveInfinity)?.Next;

            // Reinsert parking where needed
            if (toFirst.IsParkingSkipped(this.Train))
            {
                parkingtofirst = this.First.Previous.GetSkippedParking(this.First.Train);
                var route = toFirst.GetRouteToSkippedParking(this.Train);
                toFirst.UnskipParking(this.Train);
                fromPrevious = route;
                if (this.First.PreviousServiceTask?.Next == route)
                    fromPreviousOfFirstOnMachine = route;
            }
            if (fromFirst.IsParkingSkipped(this.Train))
            {
                parkingbetween = this.First.Next.GetSkippedParking(this.First.Train);
                var route = fromFirst.GetRouteToSkippedParking(this.First.Train);
                fromFirst.UnskipParking(this.First.Train);
                fromFirst = route;
            }
            if (fromSecond.IsParkingSkipped(this.Second.Train))
            {
                secondtoparking = this.Second.Next.GetSkippedParking(this.Second.Train);
                var route = fromSecond.GetRouteToSkippedParking(this.Second.Train);
                fromSecond.UnskipParking(this.Second.Train);
                toNext = fromSecond;
                if (this.Second.NextServiceTask?.Previous == route)
                    toNextOfSecondOnMachine = fromSecond;
                fromSecond = route;
            }

            this.insert(
                toSecond,
                new Tasks.MoveTask[2] { fromPrevious, fromPreviousOfSecondOnMachine },
                new Tasks.MoveTask[3] { toNext, toNextOfSecondOnMachine, toNextOfFirstOnMachine },
                0);

            this.insert(
                fromSecond,
                new Tasks.MoveTask[1] { toSecond },
                new Tasks.MoveTask[3] { toNext, toNextOfSecondOnMachine, toNextOfFirstOnMachine },
                toSecond.End + this.Second.MinimumDuration);

            this.insert(
                toFirst,
                new Tasks.MoveTask[2] { fromSecond, fromPreviousOfFirstOnMachine },
                new Tasks.MoveTask[2] { toNext, toNextOfFirstOnMachine },
                0);

            this.insert(
                fromFirst,
                new Tasks.MoveTask[1] { toFirst },
                new Tasks.MoveTask[2] { toNext, toNextOfFirstOnMachine },
                toFirst.End + this.First.MinimumDuration);

            this.First.SwapAfter(this.Second, this.First.Resource);

            // Merge stuff
            Tasks.ParkingTask parking = toSecond.Previous as Tasks.ParkingTask;
            if (parking != null && !toSecond.IsParkingSkipped(this.Train) && this.Train.Equals(toSecond.PreviousMove?.Train))
            {
                skippedtosecond = true;
                toSecond.SkipParking(parking);
            }
            parking = toFirst.Previous as Tasks.ParkingTask;
            if (parking != null && !toFirst.IsParkingSkipped(this.Train) && this.Train.Equals(toFirst.PreviousMove?.Train))
            {
                skippedbetween = true;
                toFirst.SkipParking(parking);
            }
            if (fromFirst.AllNext.Count == 1)
            {
                parking = fromFirst.AllNext.First() as Tasks.ParkingTask;
                if (parking != null && !fromFirst.IsParkingSkipped(this.Train) && this.Train.Equals(fromFirst.NextMove?.Train))
                {
                    skippedfromfirst = true;
                    fromFirst.NextMove.SkipParking(parking);
                }
            }

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            // split all merged
            if (skippedbetween)
                this.First.Previous.UnskipParking(this.Train);
            if (skippedtosecond)
                this.Second.Previous.UnskipParking(this.Train);
            if (skippedfromfirst)
                this.First.Next.UnskipParking(this.Train);

            this.First.SwapBefore(this.Second, this.First.Resource);

            if (parkingtofirst != null)
            {
                parkingtofirst.Next.InsertAfter(previoustofirst);
                parkingtofirst.Previous.InsertAfter(previoustofirst);
                parkingtofirst.Next.SkipParking(parkingtofirst);
            }
            else
                this.First.Previous.InsertAfter(previoustofirst);

            if (parkingbetween != null)
            {
                parkingbetween.Next.InsertAfter(previousfromfirst);
                parkingbetween.Previous.InsertAfter(previousfromfirst);
                parkingbetween.Next.SkipParking(parkingbetween);
            }
            else
                this.First.Next.InsertAfter(previousfromfirst);

            if (secondtoparking != null)
            {
                secondtoparking.Next.InsertAfter(previousfromsecond);
                secondtoparking.Previous.InsertAfter(previousfromsecond);
                secondtoparking.Next.SkipParking(secondtoparking);
            }
            else
                this.Second.Next.InsertAfter(previousfromsecond);

            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var serviceordermove = move as ServiceTrainOrderMove;
            if (serviceordermove == null)
                return false;

            return this.First == serviceordermove.First || this.Second == serviceordermove.Second || this.First == serviceordermove.Second || this.Second == serviceordermove.First;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: swapped {this.First} and {this.Second}";
        }

        protected void insert(Tasks.MoveTask selected, IEnumerable<Tasks.MoveTask> after, IEnumerable<Tasks.MoveTask> before, Time target)
        {
            Tasks.MoveTask start = null;
            foreach (Tasks.MoveTask earlier in after)
                if (earlier != null && earlier.MoveOrder > (start?.MoveOrder ?? double.NegativeInfinity))
                    start = earlier;
            if (start == null)
                start = this.Graph.First;

            double end = double.PositiveInfinity;
            if (before != null)
            {
                end = before.Min(task => task?.MoveOrder ?? double.PositiveInfinity);
                if (end <= start.MoveOrder)
                    end = double.PositiveInfinity;
            }

            Tasks.MoveTask position = start;
            while (position != null && position.MoveOrder < end && position.End < target)
                position = position.NextMove;

            if (position == null || position.MoveOrder >= end)
            {
                selected.InsertBefore(position);
                selected.End = target;
            }
            else
            {
                selected.InsertAfter(position);
                selected.End = position.End;
            }
        }

        public static IList<ServiceTrainOrderMove> GetMoves(PlanGraph graph)
        {
            List<ServiceTrainOrderMove> moves = new List<ServiceTrainOrderMove>();

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                var routing = movetask as Tasks.RoutingTask;
                if (routing != null)
                {
                    if (routing.IsSplit)
                    {

                    }
                    else
                    {
                        Tasks.ServiceTask service = routing.Next.First() as Tasks.ServiceTask;
                        if (service == null)
                            continue;

                        Tasks.ServiceTask nextservice = service.Next.AllNext.First() as Tasks.ServiceTask;
                        if (nextservice == null || service.Resource == nextservice.Resource)
                            continue;

                        Time previoustime = nextservice.PreviousServiceTask?.End ?? int.MinValue;
                        Time nexttime = service.NextServiceTask?.Start ?? int.MaxValue;

                        if (nexttime < previoustime)
                            continue;

                        var move = new ServiceTrainOrderMove(graph, service, nextservice);
                        moves.Add(move);
                    }
                }
            }

            return moves;
        }
    }
}
