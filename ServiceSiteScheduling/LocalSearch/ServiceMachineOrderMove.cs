using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Utilities;
using ServiceSiteScheduling.Solutions;

namespace ServiceSiteScheduling.LocalSearch
{
    class ServiceMachineOrderMove : LocalSearchMove
    {
        public Tasks.ServiceTask First { get; set; }
        public Tasks.ServiceTask Second { get; set; }

        protected Tasks.MoveTask previoustofirst, previousfromfirst, previoustosecond, previousfromsecond;
        protected Tasks.ParkingTask parkingtofirst, firsttoparking, secondtoparking;

        bool skippedtofirst, skippedfromfirst, skippedtosecond, skippedfromsecond;

        public ServiceMachineOrderMove(PlanGraph graph, Tasks.ServiceTask first, Tasks.ServiceTask second) : base(graph)
        {
            this.First = first;
            this.Second = second;

            previoustofirst = first.Previous.PreviousMove;
            previousfromfirst = first.Next.PreviousMove;
            previoustosecond = second.Previous.PreviousMove;
            previousfromsecond = second.Next.PreviousMove;

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

            Tasks.MoveTask fromPreviousOnMachine = this.First.PreviousServiceTask?.Next,
                toNextOnMachine = this.Second.NextServiceTask?.Previous,
                fromPreviousOfFirst = toFirst.Previous.Previous,
                toNextOfFirst = fromFirst.AllNext.MinItem(task => task.Next?.MoveOrder ?? double.PositiveInfinity)?.Next,
                fromPreviousOfSecond = toSecond.Previous.Previous,
                toNextOfSecond = fromSecond.AllNext.MinItem(task => task.Next?.MoveOrder ?? double.PositiveInfinity)?.Next;

            Time timeOfPreviousOnMachine = fromPreviousOnMachine?.End ?? 0;

            // Reinsert parking where needed
            if (toFirst.IsParkingSkipped(this.First.Train))
            {
                parkingtofirst = this.First.Previous.GetSkippedParking(this.First.Train);
                var route = toFirst.GetRouteToSkippedParking(this.First.Train);
                toFirst.UnskipParking(this.First.Train);
                fromPreviousOfFirst = route;
                if (this.First.PreviousServiceTask?.Next == route)
                    fromPreviousOnMachine = route;
            }
            if (fromFirst.IsParkingSkipped(this.First.Train))
            {
                firsttoparking = this.First.Next.GetSkippedParking(this.First.Train);
                var route = fromFirst.GetRouteToSkippedParking(this.First.Train);
                fromFirst.UnskipParking(this.First.Train);
                toNextOfFirst = fromFirst;
                fromFirst = route;
            }
            if (fromSecond.IsParkingSkipped(this.Second.Train))
            {
                secondtoparking = this.Second.Next.GetSkippedParking(this.Second.Train);
                var route = fromSecond.GetRouteToSkippedParking(this.Second.Train);
                fromSecond.UnskipParking(this.Second.Train);
                toNextOfSecond = fromSecond;
                if (this.Second.NextServiceTask?.Previous == route)
                    toNextOnMachine = fromSecond;
                fromSecond = route;
            }

            if (toSecond.IsParkingSkipped(this.Second.Train))
            {
                // Arrival
                var arrival = toSecond.Previous as Tasks.ArrivalTask;
                if (arrival != null)
                {
                    if (arrival.ScheduledTime > timeOfPreviousOnMachine)
                        this.insert(
                            toSecond,
                            new Tasks.MoveTask[2] { fromPreviousOnMachine, fromPreviousOfSecond },
                            new Tasks.MoveTask[3] { toNextOnMachine, toNextOfSecond, toNextOfFirst },
                            arrival.ScheduledTime);
                    else
                    {
                        toSecond.InsertAfter(fromPreviousOnMachine);
                        toSecond.End = timeOfPreviousOnMachine;
                    }
                }
                else
                {
                    var service = toSecond.Previous as Tasks.ServiceTask;
                    if (fromPreviousOfSecond.End + (service?.MinimumDuration ?? 0) > timeOfPreviousOnMachine)
                        this.insert(
                            toSecond,
                            new Tasks.MoveTask[2] { fromPreviousOnMachine, fromPreviousOfSecond },
                            new Tasks.MoveTask[3] { toNextOnMachine, toNextOfSecond, toNextOfFirst },
                            fromPreviousOfSecond.End + (service?.MinimumDuration ?? 0));
                    else
                    {
                        toSecond.InsertAfter(fromPreviousOnMachine);
                        toSecond.End = timeOfPreviousOnMachine;
                    }
                }
            }
            else
                this.insert(
                    toSecond,
                    new Tasks.MoveTask[2] { fromPreviousOnMachine, fromPreviousOfSecond },
                    null,
                    0);

            this.insert(
                fromSecond,
                new Tasks.MoveTask[1] { toSecond },
                new Tasks.MoveTask[3] { toNextOnMachine, toNextOfSecond, toNextOfFirst },
                toSecond.End + this.Second.MinimumDuration);

            this.insert(
                toFirst,
                new Tasks.MoveTask[2] { fromSecond, fromPreviousOfFirst },
                new Tasks.MoveTask[2] { toNextOnMachine, toNextOfFirst },
                0);

            this.insert(
                fromFirst,
                new Tasks.MoveTask[1] { toFirst },
                new Tasks.MoveTask[2] { toNextOnMachine, toNextOfFirst },
                toFirst.End + this.First.MinimumDuration);

            this.First.SwapAfter(this.Second, this.First.Resource);

            // Merge stuff
            Tasks.ParkingTask parking = toSecond.Previous as Tasks.ParkingTask;
            if (parking != null && !toSecond.IsParkingSkipped(this.Second.Train) && this.Second.Train.Equals(toSecond.PreviousMove?.Train))
            {
                skippedtosecond = true;
                toSecond.SkipParking(parking);
            }
            parking = toFirst.Previous as Tasks.ParkingTask;
            if (parking != null && !toFirst.IsParkingSkipped(this.First.Train) && this.First.Train.Equals(toFirst.PreviousMove?.Train))
            {
                skippedtofirst = true;
                toFirst.SkipParking(parking);
            }
            if (fromSecond.AllNext.Count == 1)
            {
                parking = fromSecond.AllNext.First() as Tasks.ParkingTask;
                if (parking != null && !fromSecond.IsParkingSkipped(this.Second.Train) && this.Second.Train.Equals(fromSecond.NextMove?.Train))
                {
                    skippedfromsecond = true;
                    fromSecond.NextMove.SkipParking(parking);
                }
            }
            if (fromFirst.AllNext.Count == 1)
            {
                parking = fromFirst.AllNext.First() as Tasks.ParkingTask;
                if (parking != null && !fromFirst.IsParkingSkipped(this.First.Train) && this.First.Train.Equals(fromFirst.NextMove?.Train))
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
            if (skippedtofirst)
                this.First.Previous.UnskipParking(this.First.Train);
            if (skippedtosecond)
                this.Second.Previous.UnskipParking(this.Second.Train);
            if (skippedfromfirst)
                this.First.Next.UnskipParking(this.First.Train);
            if (skippedfromsecond)
                this.Second.Next.UnskipParking(this.Second.Train);

            this.First.SwapBefore(this.Second, this.First.Resource);

            if (parkingtofirst != null)
            {
                parkingtofirst.Next.InsertAfter(previoustofirst);
                parkingtofirst.Previous.InsertAfter(previoustofirst);
                parkingtofirst.Next.SkipParking(parkingtofirst);
            }
            else
                this.First.Previous.InsertAfter(previoustofirst);

            if (firsttoparking != null)
            {
                firsttoparking.Next.InsertAfter(previousfromfirst);
                firsttoparking.Previous.InsertAfter(previousfromfirst);
                firsttoparking.Next.SkipParking(firsttoparking);
            }
            else
                this.First.Next.InsertAfter(previousfromfirst);

            this.Second.Previous.InsertAfter(previoustosecond);

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
            var serviceordermove = move as ServiceMachineOrderMove;
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

        public static IList<ServiceMachineOrderMove> GetMoves(PlanGraph graph)
        {
            List<ServiceMachineOrderMove> moves = new List<ServiceMachineOrderMove>();

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
                        Time servicetime = service.Start + service.MinimumDuration;

                        var nextservice = service.NextServiceTask;
                        if (nextservice == null || nextservice.Train.UnitBits.Intersects(service.Train.UnitBits))
                            continue;
                        Time nextservicetime = nextservice.Start + nextservice.MinimumDuration;

                        Time nexttraintime = service.Next.FindFirstNext(task => task.Start).Start;
                        if (nexttraintime < nextservicetime)
                            continue;

                        Tasks.TrackTask previoustrain = nextservice.Previous.FindLastPrevious(tt => (tt is Tasks.ServiceTask),
                            tt => tt.Start + ((tt as Tasks.ServiceTask).MinimumDuration));
                        Time previoustraintime = (previoustrain?.Start ?? nextservice.Previous.FindLastPrevious(task => task.End).End) + ((previoustrain as Tasks.ServiceTask)?.MinimumDuration ?? 0);
                        if (previoustraintime > servicetime)
                            continue;

                        var move = new ServiceMachineOrderMove(graph, service, nextservice);
                        moves.Add(move);
                    }
                }
            }

            return moves;
        }
    }
}
