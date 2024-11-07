using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Solutions;

namespace ServiceSiteScheduling.LocalSearch
{
    class ParkingInsertMove : LocalSearchMove
    {
        public Tasks.ParkingTask OriginalParking { get; }
        public Tasks.ParkingTask InsertedParking { get; }
        public Tasks.MoveTask Successor { get; }
        public Tasks.RoutingTask InsertedMove { get; }
        public bool InsertAfterOriginal { get; }

        public ParkingInsertMove(PlanGraph graph, Tasks.ParkingTask parking, TrackParts.Track track, Side side, Tasks.MoveTask successor, bool after) : base(graph)
        {
            this.OriginalParking = parking;
            this.Successor = successor;
            this.InsertAfterOriginal = after;

            this.InsertedParking = new Tasks.ParkingTask(parking.Train, track, true);
            this.InsertedParking.ArrivalSide = side;
            this.InsertedMove = new Tasks.RoutingTask(parking.Train);
            this.InsertedMove.Graph = graph;
            this.InsertedMove.IsRemoved = true;

            if (after)
            {
                this.InsertedParking.Next = parking.Next;

                this.InsertedMove.Previous = this.OriginalParking;
                this.InsertedMove.FromTrack = this.OriginalParking.Track;
                this.InsertedMove.Next.Add(this.InsertedParking);
                this.InsertedMove.ToTrack = track;
                this.InsertedMove.ToSide = side;

                this.InsertedParking.Previous = this.InsertedMove;
            }
            else
            {
                this.InsertedParking.Previous = parking.Previous;

                this.InsertedMove.Previous = this.InsertedParking;
                this.InsertedMove.FromTrack = track;
                this.InsertedMove.Next.Add(this.OriginalParking);
                this.InsertedMove.ToTrack = this.OriginalParking.Track;
                this.InsertedMove.ToSide = this.OriginalParking.ArrivalSide;

                this.InsertedParking.Next = this.InsertedMove;
            }
        }

        public override SolutionCost Execute()
        {
            if (this.InsertAfterOriginal)
            {
                this.OriginalParking.Next.ReplacePreviousTask(this.InsertedParking);
                this.OriginalParking.Next = this.InsertedMove;
                this.InsertedMove.InsertBefore(this.Successor);
                if (this.InsertedParking.Next.TaskType == Tasks.MoveTaskType.Standard)
                    this.InsertedParking.Next.FromTrack = this.InsertedParking.Track;
            }
            else
            {
                this.OriginalParking.Previous.ReplaceNextTask(this.InsertedParking);
                this.OriginalParking.Previous.ToSide = this.InsertedParking.ArrivalSide;
                this.OriginalParking.Previous.ToTrack = this.InsertedParking.Track;
                this.OriginalParking.Previous = this.InsertedMove;
                this.InsertedMove.InsertBefore(this.Successor);
            }

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            if (this.InsertAfterOriginal)
            {
                this.InsertedParking.Next.ReplacePreviousTask(this.OriginalParking);
                this.OriginalParking.Next = this.InsertedParking.Next;
                this.InsertedMove.Remove();
                if (this.OriginalParking.Next.TaskType == Tasks.MoveTaskType.Standard)
                    this.OriginalParking.Next.FromTrack = this.OriginalParking.Track;
            }
            else
            {
                this.InsertedParking.Previous.ReplaceNextTask(this.OriginalParking);
                this.OriginalParking.Previous = this.InsertedParking.Previous;
                this.InsertedMove.Remove();
                this.OriginalParking.Previous.ToSide = this.OriginalParking.ArrivalSide;
                this.OriginalParking.Previous.ToTrack = this.OriginalParking.Track;
            }
            return base.Revert();
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            return false;
        }

        public static IList<ParkingInsertMove> GetMoves(PlanGraph graph)
        {
            List<ParkingInsertMove> moves = new List<ParkingInsertMove>();

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
                                moves.Add(new ParkingInsertMove(graph, task as Tasks.ParkingTask, track, Side.A, nextmovetask, true));
                                if (!isSplit)
                                    moves.Add(new ParkingInsertMove(graph, task as Tasks.ParkingTask, track, Side.A, nextmovetask, false));
                            }
                            if (track.Access.HasFlag(Side.B))
                            {
                                moves.Add(new ParkingInsertMove(graph, task as Tasks.ParkingTask, track, Side.B, nextmovetask, true));
                                if (!isSplit)
                                    moves.Add(new ParkingInsertMove(graph, task as Tasks.ParkingTask, track, Side.B, nextmovetask, false));
                            }
                        }
                    }
                }
            }

            return moves;
        }
    }
}
