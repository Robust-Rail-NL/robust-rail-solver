using System;
using System.Collections.Generic;
using System.Linq;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.LocalSearch
{
    class ParkingSwitchMove : LocalSearchMove
    {
        public IList<Tasks.TrackTask> RelatedTasks { get; private set; }
        public TrackParts.Track Track { get; private set; }
        public Side Side { get; private set; }

        protected TrackParts.Track[] originaltracks;
        protected Side[] originalsides;

        public ParkingSwitchMove(Solutions.PlanGraph graph, IList<Tasks.TrackTask> tasks, TrackParts.Track track, Side side) : base(graph)
        {
            if (tasks.Count == 0)
                throw new ArgumentException("The set of tasks cannot be empty");

            this.RelatedTasks = tasks;
            this.Track = track;
            this.Side = side;

            this.originaltracks = new TrackParts.Track[tasks.Count];
            this.originalsides = new Side[tasks.Count];
            for (int i = 0; i < tasks.Count; i++)
            {
                this.originaltracks[i] = tasks[i].Track;
                this.originalsides[i] = tasks[i].ArrivalSide;
            }

            //this.executestart = this.revertstart = tasks.MinItem(t => t.Previous.MoveOrder).Previous;
            //this.executeend = this.revertend = tasks.MaxItem(t => t.Next?.MoveOrder ?? double.NegativeInfinity)?.Next ?? this.Graph.Last;

            /*this.AffectedTracks = new Utilities.BitSet(ProblemInstance.Current.Tracks.Length);
            this.AffectedTracks[this.Track.Index] = true;
            this.AffectedTracks[this.originaltrack.Index] = true;*/
        }

        public override Solutions.SolutionCost Execute()
        {
            foreach (Tasks.TrackTask task in this.RelatedTasks)
            {
                task.Track = this.Track;
                task.ArrivalSide = this.Side;

                task.Previous.ToTrack = this.Track;
                task.Previous.ToSide = this.Side;

                if (task.Next?.TaskType == Tasks.MoveTaskType.Standard)
                    task.Next.FromTrack = this.Track;
            }
            return base.Execute();
        }

        public override Solutions.SolutionCost Revert()
        {
            for (int i = 0; i < this.RelatedTasks.Count; i++)
            {
                var task = this.RelatedTasks[i];
                var track = this.originaltracks[i];
                var side = this.originalsides[i];

                task.Track = track;
                task.ArrivalSide = side;

                task.Previous.ToTrack = track;
                task.Previous.ToSide = side;

                if (task.Next?.TaskType == Tasks.MoveTaskType.Standard)
                    task.Next.FromTrack = track;
            }

            return base.Revert();
        }

        public static IList<ParkingSwitchMove> GetMoves(Solutions.PlanGraph graph)
        {
            List<ParkingSwitchMove> moves = new List<ParkingSwitchMove>();

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                if (movetask.TaskType == Tasks.MoveTaskType.Standard)
                {
                    var routing = movetask as Tasks.RoutingTask;
                    var primarytask = routing.Next[0];
                    var tasks = primarytask.GetRelatedTasks();
                    if (tasks.Min(t => t.Previous.MoveOrder) < routing.MoveOrder)
                        continue;
                    if (tasks.Any(t => !(t.TaskType == Tasks.TrackTaskType.Parking || (t as Tasks.ServiceTask)?.Type.LocationType == Servicing.ServiceLocationType.Free)))
                        continue;

                    var currenttrack = primarytask.Track;
                    var currentside = primarytask.ArrivalSide;

                    IEnumerable<TrackParts.Track> tracks = tasks.Skip(1).Aggregate((IEnumerable<TrackParts.Track>)tasks[0].Train.Units[0].Type.ParkingLocations, (set, t) => set.Intersect(t.Train.Units[0].Type.ParkingLocations));
                    var services = tasks.Where(t => t.TaskType == Tasks.TrackTaskType.Service);
                    if (services.Count() > 0)
                        tracks = services.Aggregate(tracks, (set, t) => set.Intersect((t as Tasks.ServiceTask).Type.Tracks));

                    foreach (var track in tracks)
                    {
                        if (track.Access.HasFlag(Side.A) && !(track == currenttrack && currentside == Side.A))
                        {
                            /*if (graph.RoutingGraph.RoutePossible(routing.Train, routing.Previous.Track, Side.A, track, Side.A) ||
                                graph.RoutingGraph.RoutePossible(routing.Train, routing.Previous.Track, Side.B, track, Side.A))
                            {
                                if (routing.AllNext.All(t =>
                                    graph.RoutingGraph.RoutePossible(t.Train, track, Side.A, t.Next.ToTrack, t.Next.ToSide) ||
                                    graph.RoutingGraph.RoutePossible(t.Train, track, Side.B, t.Next.ToTrack, t.Next.ToSide)))
                                {*/
                            ParkingSwitchMove move = new ParkingSwitchMove(graph, tasks, track, Side.A);
                            moves.Add(move);
                            /*}
                        }*/
                        }
                        if (track.Access.HasFlag(Side.B) && !(track == currenttrack && currentside == Side.B))
                        {
                            /*if (graph.RoutingGraph.RoutePossible(routing.Train, routing.Previous.Track, Side.A, track, Side.B) ||
                                graph.RoutingGraph.RoutePossible(routing.Train, routing.Previous.Track, Side.B, track, Side.B))
                            {
                                if (routing.AllNext.All(t =>
                                    graph.RoutingGraph.RoutePossible(t.Train, track, Side.A, t.Next.ToTrack, t.Next.ToSide) ||
                                    graph.RoutingGraph.RoutePossible(t.Train, track, Side.B, t.Next.ToTrack, t.Next.ToSide)))
                                {*/
                            ParkingSwitchMove move = new ParkingSwitchMove(graph, tasks, track, Side.B);
                            moves.Add(move);
                            /*}
                        }*/
                        }
                    }
                }
                else
                {
                    // Change departure side
                    var departure = movetask as Tasks.DepartureRoutingTask;
                    if (departure.Next.Track.Access == Side.Both)
                    {
                        ParkingSwitchMove move = new ParkingSwitchMove(graph, new Tasks.TrackTask[1] { departure.Next }, departure.Next.Track, departure.ToSide.Flip);
                        moves.Add(move);
                    }
                }
            }


            return moves;
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var shiftmove = move as ParkingSwitchMove;
            if (shiftmove == null)
                return false;

            return this.RelatedTasks.Intersect(shiftmove.RelatedTasks).Count() > 0;// || this.Track == shiftmove.Track || this.originaltrack == shiftmove.Track;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: park {this.RelatedTasks[0].Previous.Train} at {this.Track.ID}{this.Side} instead of {this.originaltracks[0].ID}{this.originalsides[0]}";
        }
    }
}