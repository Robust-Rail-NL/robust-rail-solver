using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.LocalSearch
{
    class ServiceMachineSwapMove : LocalSearchMove
    {
        public Tasks.ServiceTask First { get; private set; }
        public Tasks.ServiceTask Second { get; private set; }

        protected Tasks.MoveTask previoustofirst, previousfromfirst, previoustosecond, previousfromsecond;
        protected Tasks.ParkingTask parkingtofirst, firsttoparking, secondtoparking;

        bool skippedtofirst, skippedfromfirst, skippedtosecond, skippedfromsecond;

        public ServiceMachineSwapMove(PlanGraph graph, Tasks.ServiceTask first, Tasks.ServiceTask second) : base(graph)
        {
            this.First = first;
            this.Second = second;

            previoustofirst = first.Previous.PreviousMove;
            previousfromfirst = first.Next.PreviousMove;
            previoustosecond = second.Previous.PreviousMove;
            previousfromsecond = second.Next.PreviousMove;
        }

        public override SolutionCost Execute()
        {
            Tasks.RoutingTask toFirst = this.First.Previous as Tasks.RoutingTask,
                toSecond = this.Second.Previous as Tasks.RoutingTask;
            Tasks.MoveTask fromFirst = this.First.Next,
                fromSecond = this.Second.Next;

            Tasks.MoveTask fromPreviousOnFirstMachine = this.First.PreviousServiceTask?.Next,
                fromPreviousOnSecondMachine = this.Second.PreviousServiceTask?.Next,
                toNextOnFirstMachine = this.First.NextServiceTask?.Previous,
                toNextOnSecondMachine = this.Second.NextServiceTask?.Previous,
                fromPreviousOfFirst = toFirst.Previous.Previous,
                toNextOfFirst = fromFirst.AllNext.MinItem(task => task.Next?.MoveOrder ?? double.PositiveInfinity)?.Next,
                fromPreviousOfSecond = toSecond.Previous.Previous,
                toNextOfSecond = fromSecond.AllNext.MinItem(task => task.Next?.MoveOrder ?? double.PositiveInfinity)?.Next;

            Time timeOfPreviousOnFirstMachine = fromPreviousOnFirstMachine?.End ?? int.MinValue,
                timeOfPreviousOnSecondMachine = fromPreviousOnSecondMachine?.End ?? int.MinValue;

            // Reinsert parking where needed
            if (toFirst.IsParkingSkipped(this.First.Train))
            {
                parkingtofirst = this.First.Previous.GetSkippedParking(this.First.Train);
                var route = toFirst.GetRouteToSkippedParking(this.First.Train);
                toFirst.UnskipParking(this.First.Train);
                fromPreviousOfFirst = route;
                if (this.First.PreviousServiceTask?.Next == route)
                    fromPreviousOnFirstMachine = route;
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
                    toNextOnSecondMachine = fromSecond;
                fromSecond = route;
            }

            if (toSecond.IsParkingSkipped(this.Second.Train))
            {
                // Arrival
                var arrival = toSecond.Previous as Tasks.ArrivalTask;
                if (arrival != null)
                {
                    if (arrival.ScheduledTime > timeOfPreviousOnFirstMachine)
                        this.insert(
                            toSecond,
                            new Tasks.MoveTask[2] { fromPreviousOnFirstMachine, fromPreviousOfSecond },
                            new Tasks.MoveTask[2] { toNextOnFirstMachine, toNextOfSecond },
                            arrival.ScheduledTime);
                    else
                    {
                        toSecond.InsertAfter(fromPreviousOnFirstMachine);
                        toSecond.End = timeOfPreviousOnFirstMachine;
                    }
                }
                else
                {
                    // Service
                    var service = toSecond.Previous as Tasks.ServiceTask;
                    if (fromPreviousOfSecond.End + (service?.MinimumDuration ?? 0) > timeOfPreviousOnFirstMachine)
                        this.insert(
                            toSecond,
                            new Tasks.MoveTask[2] { fromPreviousOnFirstMachine, fromPreviousOfSecond },
                            new Tasks.MoveTask[2] { toNextOnFirstMachine, toNextOfSecond },
                            fromPreviousOfSecond.End + (service?.MinimumDuration ?? 0));
                    else
                    {
                        toSecond.InsertAfter(fromPreviousOnFirstMachine);
                        toSecond.End = timeOfPreviousOnFirstMachine;
                    }
                }
            }
            else
                this.insert(
                    toSecond,
                    new Tasks.MoveTask[2] { fromPreviousOnFirstMachine, fromPreviousOfSecond },
                    null,
                    0);

            this.insert(
                fromSecond,
                new Tasks.MoveTask[1] { toSecond },
                new Tasks.MoveTask[2] { toNextOnFirstMachine, toNextOfSecond },
                toSecond.End + this.Second.MinimumDuration);

            this.insert(
                toFirst,
                new Tasks.MoveTask[2] { fromPreviousOfFirst, fromPreviousOnSecondMachine },
                new Tasks.MoveTask[2] { toNextOnSecondMachine, toNextOfFirst },
                0);

            this.insert(
                fromFirst,
                new Tasks.MoveTask[1] { toFirst },
                new Tasks.MoveTask[2] { toNextOnSecondMachine, toNextOfFirst },
                toFirst.End + this.First.MinimumDuration);

            // update location
            var firstlocation = this.First.Resource as Servicing.ServiceLocation;
            var secondlocation = this.Second.Resource as Servicing.ServiceLocation;
            var secondside = this.Second.ArrivalSide;
            
            this.Second.Track = firstlocation.Track;
            this.Second.ArrivalSide = this.First.ArrivalSide;
            this.Second.Previous.ToTrack = firstlocation.Track;
            this.Second.Previous.ToSide = this.First.ArrivalSide;
            Tasks.RoutingTask routing = this.Second.Next as Tasks.RoutingTask;
            if (routing != null)
                routing.FromTrack = firstlocation.Track;

            this.First.Track = secondlocation.Track;
            this.First.ArrivalSide = secondside;
            this.First.Previous.ToTrack = secondlocation.Track;
            this.First.Previous.ToSide = secondside;
            routing = this.First.Next as Tasks.RoutingTask;
            if (routing != null)
                routing.FromTrack = secondlocation.Track;

            // swap services
            Tasks.ServiceTask predecessor = this.First.PreviousServiceTask;
            this.First.SwapAfter(this.Second.PreviousServiceTask, this.Second.Resource);
            this.Second.SwapAfter(predecessor, firstlocation);

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

            // update location
            var firstlocation = this.First.Resource as Servicing.ServiceLocation;
            var secondlocation = this.Second.Resource as Servicing.ServiceLocation;
            var secondside = this.Second.ArrivalSide;

            this.Second.Track = firstlocation.Track;
            this.Second.ArrivalSide = this.First.ArrivalSide;
            this.Second.Previous.ToTrack = firstlocation.Track;
            this.Second.Previous.ToSide = this.First.ArrivalSide;
            Tasks.RoutingTask routing = this.Second.Next as Tasks.RoutingTask;
            if (routing != null)
                routing.FromTrack = firstlocation.Track;

            this.First.Track = secondlocation.Track;
            this.First.ArrivalSide = secondside;
            this.First.Previous.ToTrack = secondlocation.Track;
            this.First.Previous.ToSide = secondside;
            routing = this.First.Next as Tasks.RoutingTask;
            if (routing != null)
                routing.FromTrack = secondlocation.Track;

            // swap services
            Tasks.ServiceTask predecessor = this.First.PreviousServiceTask;
            this.First.SwapAfter(this.Second.PreviousServiceTask, this.Second.Resource);
            this.Second.SwapAfter(predecessor, firstlocation);

            if (parkingtofirst != null)
            {
                parkingtofirst.Next.InsertAfter(previoustofirst);
                parkingtofirst.Previous.InsertAfter(previoustofirst);
                parkingtofirst.Next.SkipParking(parkingtofirst);
            }
            else
                this.First.Previous.InsertAfter(previoustofirst);

            this.Second.Previous.InsertAfter(previoustosecond);

            if (previousfromfirst == this.Second.Next)
            {
                if (secondtoparking != null)
                {
                    secondtoparking.Next.InsertAfter(previousfromsecond);
                    secondtoparking.Previous.InsertAfter(previousfromsecond);
                    secondtoparking.Next.SkipParking(secondtoparking);
                }
                else
                    this.Second.Next.InsertAfter(previousfromsecond);

                if (firsttoparking != null)
                {
                    firsttoparking.Next.InsertAfter(previousfromfirst);
                    firsttoparking.Previous.InsertAfter(previousfromfirst);
                    firsttoparking.Next.SkipParking(firsttoparking);
                }
                else
                    this.First.Next.InsertAfter(previousfromfirst);
            }
            else
            {
                if (firsttoparking != null)
                {
                    firsttoparking.Next.InsertAfter(previousfromfirst);
                    firsttoparking.Previous.InsertAfter(previousfromfirst);
                    firsttoparking.Next.SkipParking(firsttoparking);
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
            }

            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var machinemove = move as ServiceMachineSwapMove;
            if (machinemove == null)
                return false;

            return this.First == machinemove.First || this.Second == machinemove.First || this.First == machinemove.Second || this.Second == machinemove.Second;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: swapped {this.First.Type.Name} of {this.First.Train} at {this.First.Resource} with {this.Second.Type.Name} of {this.Second.Train} at {this.Second.Resource}";
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

        public static IList<ServiceMachineSwapMove> GetMoves(PlanGraph graph)
        {
            List<ServiceMachineSwapMove> moves = new List<ServiceMachineSwapMove>();

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                if (movetask.TaskType == Tasks.MoveTaskType.Departure || movetask.AllNext.Count > 1)
                    continue;

                Tasks.ServiceTask service = movetask.AllNext.First() as Tasks.ServiceTask;
                if (service == null)
                    continue;

                if (service.Type.Resources.Count == 1 || service.Type.LocationType == Servicing.ServiceLocationType.Free)
                    continue;

                for (var nextmovetask = movetask.NextMove; nextmovetask != null && nextmovetask.MoveOrder < service.Next.MoveOrder; nextmovetask = nextmovetask.NextMove)
                {
                    if (nextmovetask.TaskType == Tasks.MoveTaskType.Departure || nextmovetask.AllNext.Count > 1)
                        continue;

                    Tasks.ServiceTask nextservice = nextmovetask.AllNext.First() as Tasks.ServiceTask;
                    if (nextservice == null)
                        continue;

                    if (nextservice.Type.Resources.Count == 1 || nextservice.Type != service.Type || nextservice.Train.UnitBits.Intersects(service.Train.UnitBits))
                        continue;

                    ServiceMachineSwapMove move = new ServiceMachineSwapMove(graph, service, nextservice);
                    moves.Add(move);
                }
            }

            return moves;
        }
    }
}
