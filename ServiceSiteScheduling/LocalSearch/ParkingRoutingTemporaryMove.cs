using ServiceSiteScheduling.Solutions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.LocalSearch
{
    class ParkingRoutingTemporaryMove : LocalSearchMove
    {
        public Tasks.ParkingTask OriginalParking { get; }
        public Tasks.ParkingTask TemporaryParking { get; }
        public Tasks.ParkingTask ReturnParking { get; }
        public Tasks.MoveTask Target { get; }
        public Tasks.RoutingTask ToMove { get; }
        public Tasks.RoutingTask FromMove { get; }

        public ParkingRoutingTemporaryMove(PlanGraph graph, Tasks.ParkingTask parking, TrackParts.Track track, Side temporaryside, Side returnside, Tasks.MoveTask target) : base(graph)
        {
            this.OriginalParking = parking;
            this.Target = target;

            this.TemporaryParking = new Tasks.ParkingTask(parking.Train, track, true);
            this.TemporaryParking.ArrivalSide = temporaryside;

            this.ReturnParking = new Tasks.ParkingTask(parking.Train, parking.Track, true);
            this.ReturnParking.ArrivalSide = returnside;
            this.ReturnParking.Next = parking.Next;

            this.ToMove = new Tasks.RoutingTask(parking.Train);
            this.ToMove.Graph = graph;
            this.ToMove.IsRemoved = true;
            this.ToMove.Previous = this.OriginalParking;
            this.ToMove.FromTrack = this.OriginalParking.Track;
            this.ToMove.Next.Add(this.TemporaryParking);
            this.ToMove.ToTrack = track;
            this.ToMove.ToSide = temporaryside;
            this.TemporaryParking.Previous = this.ToMove;

            this.FromMove = new Tasks.RoutingTask(parking.Train);
            this.FromMove.Graph = graph;
            this.FromMove.IsRemoved = true;
            this.FromMove.Previous = this.TemporaryParking;
            this.FromMove.FromTrack = track;
            this.FromMove.Next.Add(this.ReturnParking);
            this.FromMove.ToTrack = parking.Track;
            this.FromMove.ToSide = returnside;
            this.ReturnParking.Previous = this.FromMove;
            this.TemporaryParking.Next = this.FromMove;
        }

        public override SolutionCost Execute()
        {
            this.ReturnParking.Next.ReplacePreviousTask(this.ReturnParking);
            this.OriginalParking.Next = this.ToMove;
            this.ToMove.InsertBefore(this.Target);
            this.FromMove.InsertAfter(this.Target);

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            this.ReturnParking.Next.ReplacePreviousTask(this.OriginalParking);
            this.OriginalParking.Next = this.ReturnParking.Next;
            this.ToMove.Remove();
            this.FromMove.Remove();

            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            return false;
        }

        public static IList<ParkingRoutingTemporaryMove> GetMoves(PlanGraph graph)
        {
            List<ParkingRoutingTemporaryMove> moves = new List<ParkingRoutingTemporaryMove>();

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                if (movetask.TaskType == Tasks.MoveTaskType.Departure)
                    continue;

                bool isSplit = movetask.AllNext.Count > 1;

                foreach (Tasks.TrackTask task in movetask.AllNext)
                {
                    if (task.TaskType != Tasks.TrackTaskType.Parking)
                        continue;

                    if (!graph.Cost.ProblemTracks[task.Track.Index] && !graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits))
                        continue;

                    if (task.Next.TaskType == Tasks.MoveTaskType.Departure && task.Next.AllNext.Count > 1)
                        continue;

                    for (var nextmovetask = movetask.NextMove.NextMove; nextmovetask != null && nextmovetask.MoveOrder < task.Next.MoveOrder; nextmovetask = nextmovetask.NextMove)
                    {
                        if (!graph.Cost.ProblemTrains.Intersects(nextmovetask.Train.UnitBits))
                            continue;

                        if (!nextmovetask.DepartureCrossingTracks[task.Track.Index] && !nextmovetask.CrossingTracks[task.Track.Index] && nextmovetask.ToTrack != task.Track)
                            continue;

                        foreach (var track in task.Train.ParkingLocations)
                        {
                            if (track == task.Track/* || !graph.HasSufficientSpace(task.Train, track, nextmovetask.MoveOrder, task.Next.MoveOrder)*/)
                                continue;

                            if (track.Access.HasFlag(Side.A))
                            {
                                if (task.Track.Access.HasFlag(Side.A))
                                    moves.Add(new ParkingRoutingTemporaryMove(graph, task as Tasks.ParkingTask, track, Side.A, Side.A, nextmovetask));
                                if (task.Track.Access.HasFlag(Side.B))
                                    moves.Add(new ParkingRoutingTemporaryMove(graph, task as Tasks.ParkingTask, track, Side.A, Side.B, nextmovetask));
                            }
                            if (track.Access.HasFlag(Side.B))
                            {
                                if (task.Track.Access.HasFlag(Side.A))
                                    moves.Add(new ParkingRoutingTemporaryMove(graph, task as Tasks.ParkingTask, track, Side.B, Side.A, nextmovetask));
                                if (task.Track.Access.HasFlag(Side.B))
                                    moves.Add(new ParkingRoutingTemporaryMove(graph, task as Tasks.ParkingTask, track, Side.B, Side.B, nextmovetask));
                            }
                        }
                    }
                }
            }

            return moves;
        }
    }
}
