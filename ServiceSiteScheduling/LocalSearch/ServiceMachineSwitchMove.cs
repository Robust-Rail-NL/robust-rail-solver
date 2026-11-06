using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.LocalSearch
{
    class ServiceMachineSwitchMove : LocalSearchMove
    {
        public Tasks.ServiceTask Selected { get; private set; }
        public Servicing.ServiceResource Resource { get; private set; }
        public Side Side { get; private set; }
        public Tasks.ServiceTask Predecessor { get; private set; }

        private Tasks.ParkingTask previousparking,
            nextparking;
        private Servicing.ServiceResource originalresource;
        private TrackParts.Track originaltrack;
        private Side originalside;
        private Tasks.MoveTask originalpreviousposition,
            originalnextposition;
        private Tasks.ServiceTask originalpredecessor;
        private Tasks.ParkingTask originalpredecessorparking;

        private bool skippedprevious,
            skippednext;

        public ServiceMachineSwitchMove(
            PlanGraph graph,
            Tasks.ServiceTask selected,
            Servicing.ServiceResource resource,
            Side side,
            Tasks.ServiceTask predecessor
        )
            : base(graph)
        {
            this.Selected = selected;
            this.Resource = resource;
            this.Side = side;
            this.Predecessor = predecessor;

            this.originalresource = selected.Resource;
            this.originaltrack = selected.Track;
            this.originalside = selected.ArrivalSide;
            this.originalpreviousposition = selected.Previous.PreviousMove;
            this.originalnextposition = selected.Next.PreviousMove;
            this.originalpredecessor = selected.PreviousServiceTask;
        }

        public override SolutionCost Execute()
        {
            Tasks.MoveTask previous,
                next,
                lower,
                upper;

            if (this.Selected.Previous.IsParkingSkipped(this.Selected.Train))
            {
                previous = this.Selected.Previous;
                lower = this.Selected.Previous.GetRouteToSkippedParking(this.Selected.Train);
                this.previousparking = this.Selected.Previous.GetSkippedParking(
                    this.Selected.Train
                );
                this.Selected.Previous.UnskipParking(this.Selected.Train);
            }
            else
            {
                previous = this.Selected.Previous;
                lower = previous.LatestPrevious();
            }

            if (this.Selected.Next.IsParkingSkipped(this.Selected.Train))
            {
                next = this.Selected.Next.GetRouteToSkippedParking(this.Selected.Train);
                upper = this.Selected.Next;
                this.nextparking = this.Selected.Next.GetSkippedParking(this.Selected.Train);
                this.Selected.Next.UnskipParking(this.Selected.Train);
            }
            else
            {
                next = this.Selected.Next;
                upper = next.EarliestNext();
            }

            if (this.Selected.Type.LocationType == Servicing.ServiceLocationType.Fixed)
            {
                if (this.Resource is Servicing.ServiceLocation location)
                {
                    this.Selected.Track = location.Track;
                    this.Selected.ArrivalSide = this.Side;

                    this.Selected.Previous.ToTrack = location.Track;
                    this.Selected.Previous.ToSide = this.Side;

                    if (this.Selected.Next is Tasks.RoutingTask routing)
                        routing.FromTrack = location.Track;
                }
            }

            this.Selected.SwapAfter(this.Predecessor, this.Resource);

            if (this.Predecessor != null && this.Predecessor.Next == upper)
            {
                this.originalpredecessorparking = upper.GetSkippedParking(this.Predecessor.Train);
                upper.UnskipParking(this.Predecessor.Train);
            }

            if (lower.MoveOrder > (this.Predecessor?.Next.MoveOrder ?? double.NegativeInfinity))
                previous.InsertAfter(lower);
            else
                previous.InsertAfter(this.Predecessor?.Next);

            if (
                upper.MoveOrder
                < (this.Selected.NextServiceTask?.Previous.MoveOrder ?? double.PositiveInfinity)
            )
                next.InsertBefore(upper);
            else
                next.InsertBefore(this.Selected.NextServiceTask?.Previous);

            // Merge
            if (previous.AllPrevious.Count == 1)
            {
                if (
                    previous.AllPrevious.First() is Tasks.ParkingTask parking
                    && !previous.IsParkingSkipped(previous.Train)
                    && previous.Train.Equals(previous.PreviousMove?.Train)
                )
                {
                    previous.SkipParking(parking);
                    skippedprevious = true;
                }
            }
            if (next.AllNext.Count == 1)
            {
                if (
                    next.AllNext.First() is Tasks.ParkingTask parking
                    && !next.IsParkingSkipped(previous.Train)
                    && next.Train.Equals(next.NextMove?.Train)
                )
                {
                    next.NextMove.SkipParking(parking);
                    skippednext = true;
                }
            }

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            if (skippedprevious)
                this.Selected.Previous.UnskipParking(this.Selected.Train);
            if (skippednext)
                this.Selected.Next.UnskipParking(this.Selected.Train);

            if (this.originalpredecessorparking != null)
                this.originalpredecessorparking.Next.SkipParking(this.originalpredecessorparking);

            this.Selected.Track = this.originaltrack;
            this.Selected.ArrivalSide = this.originalside;
            this.Selected.SwapAfter(this.originalpredecessor, this.originalresource);

            this.Selected.Previous.ToTrack = this.originaltrack;
            this.Selected.Previous.ToSide = this.originalside;
            if (this.Selected.Next is Tasks.RoutingTask routing)
                routing.FromTrack = this.originaltrack;

            if (this.previousparking != null)
            {
                this.previousparking.Next.InsertAfter(this.originalpreviousposition);
                this.previousparking.Previous.InsertAfter(this.originalpreviousposition);
                this.previousparking.Next.SkipParking(this.previousparking);
            }
            else
                this.Selected.Previous.InsertAfter(this.originalpreviousposition);

            if (this.nextparking != null)
            {
                this.nextparking.Next.InsertAfter(this.originalnextposition);
                this.nextparking.Previous.InsertAfter(this.originalnextposition);
                this.nextparking.Next.SkipParking(this.nextparking);
            }
            else
                this.Selected.Next.InsertAfter(this.originalnextposition);

            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            if (move is not ServiceMachineSwitchMove machinemove)
                return false;

            return this.Selected == machinemove.Selected
                || this.Selected == machinemove.Predecessor
                || this.Predecessor == machinemove.Selected
                || this.Predecessor == machinemove.Predecessor;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: moved {this.Selected.Type.Name} of {this.Selected.Train} from {this.originalresource} to {this.Resource}";
        }

        public static IList<ServiceMachineSwitchMove> GetMoves(PlanGraph graph)
        {
            List<ServiceMachineSwitchMove> moves = [];

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                if (movetask is Tasks.RoutingTask routing)
                {
                    if (routing.IsSplit) { }
                    else
                    {
                        if (routing.Next.First() is not Tasks.ServiceTask service)
                            continue;

                        if (service.Type.Resources.Count == 1)
                            continue;

                        if (service.Type.LocationType == Servicing.ServiceLocationType.Fixed)
                        {
                            foreach (var track in service.Type.Tracks)
                            {
                                {
                                    Tasks.MoveTask lower = service.Previous,
                                        upper = service.Next;
                                    if (!lower.SkipsParking)
                                    {
                                        // No move before a StandIn: nothing to anchor against.
                                        if (lower.AllPrevious.Any(task => task.Previous == null))
                                            continue;
                                        lower = lower.LatestPrevious();
                                    }
                                    if (!upper.SkipsParking)
                                        upper.EarliestNext();

                                    var location = ProblemInstance.Current.ServiceLocations[
                                        track.Index
                                    ];
                                    var position = location.First;
                                    if (position == null || position.Start > lower.End)
                                    {
                                        if (track.Access == Side.Both)
                                        {
                                            ServiceMachineSwitchMove move = new(
                                                graph,
                                                service,
                                                location,
                                                Side.A,
                                                null
                                            );
                                            moves.Add(move);
                                            move = new ServiceMachineSwitchMove(
                                                graph,
                                                service,
                                                location,
                                                Side.B,
                                                null
                                            );
                                            moves.Add(move);
                                        }
                                        else
                                        {
                                            ServiceMachineSwitchMove move = new(
                                                graph,
                                                service,
                                                location,
                                                track.Access,
                                                null
                                            );
                                            moves.Add(move);
                                        }
                                    }
                                    while (position != null && position.End < upper.End)
                                    {
                                        if (
                                            position.NextServiceTask == null
                                            || position.NextServiceTask.Start > lower.End
                                        )
                                        {
                                            if (track.Access == Side.Both)
                                            {
                                                ServiceMachineSwitchMove move = new(
                                                    graph,
                                                    service,
                                                    location,
                                                    Side.A,
                                                    position
                                                );
                                                moves.Add(move);
                                                move = new ServiceMachineSwitchMove(
                                                    graph,
                                                    service,
                                                    location,
                                                    Side.B,
                                                    position
                                                );
                                                moves.Add(move);
                                            }
                                            else
                                            {
                                                ServiceMachineSwitchMove move = new(
                                                    graph,
                                                    service,
                                                    location,
                                                    track.Access,
                                                    position
                                                );
                                                moves.Add(move);
                                            }
                                        }

                                        position = position.NextServiceTask;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (
                                service.PreviousServiceTask == null
                                && service.NextServiceTask == null
                            )
                                continue;

                            foreach (var resource in service.Type.Resources)
                            {
                                if (resource == service.Resource)
                                    continue;

                                Tasks.MoveTask lower = service.Previous,
                                    upper = service.Next;
                                if (!lower.SkipsParking)
                                {
                                    // No move before a StandIn: nothing to anchor against.
                                    if (lower.AllPrevious.Any(task => task.Previous == null))
                                        continue;
                                    lower = lower.LatestPrevious();
                                }
                                if (!upper.SkipsParking)
                                    upper.EarliestNext();

                                var position = service.Resource.First;
                                if (position == null || position.Start > lower.End)
                                {
                                    ServiceMachineSwitchMove move = new(
                                        graph,
                                        service,
                                        service.Resource,
                                        Side.None,
                                        null
                                    );
                                    moves.Add(move);
                                }
                                while (position != null && position.End < upper.End)
                                {
                                    if (
                                        position.NextServiceTask == null
                                        || position.NextServiceTask.Start > lower.End
                                    )
                                    {
                                        ServiceMachineSwitchMove move = new(
                                            graph,
                                            service,
                                            service.Resource,
                                            Side.None,
                                            position
                                        );
                                        moves.Add(move);
                                    }

                                    position = position.NextServiceTask;
                                }
                            }
                        }
                    }
                }
            }

            return moves;
        }
    }
}
