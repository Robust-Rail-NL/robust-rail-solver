#nullable enable

using System.Diagnostics;

namespace ServiceSiteScheduling.Tasks
{
    public enum MoveTaskType
    {
        Standard,
        Departure,
    }

    abstract class MoveTask
    {
        public Solutions.PlanGraph Graph { get; set; } = null!;

        public Trains.ShuntTrain Train { get; set; }
        public abstract IList<TrackTask> AllPrevious { get; }
        public abstract IList<TrackTask> AllNext { get; }

        public Utilities.Time Start { get; set; }
        public Utilities.Time End { get; set; }

        public abstract Utilities.Time Duration { get; }
        public abstract int Crossings { get; }
        public abstract Utilities.BitSet CrossingTracks { get; }
        public abstract int DepartureCrossings { get; }
        public abstract Utilities.BitSet DepartureCrossingTracks { get; }
        public abstract int NumberOfRoutes { get; }

        public MoveTask? PreviousMove { get; set; }
        public MoveTask? NextMove { get; set; }
        public double MoveOrder { get; set; }
        public bool IsRemoved { get; set; }

        public abstract bool SkipsParking { get; }

        public TrackParts.Track ToTrack { get; set; } = null!;
        public Side? ToSide { get; set; }
        public TrackParts.Track FromTrack { get; set; } = null!;
        public abstract Side? FromSide { get; }

        public MoveTaskType TaskType
        {
            get => this.tasktype;
        }
        protected readonly MoveTaskType tasktype;

        public MoveTask(Trains.ShuntTrain train, MoveTaskType tasktype)
        {
            this.Train = train;
            this.tasktype = tasktype;
        }

        public abstract bool IsParkingSkipped(Trains.ShuntTrain train);
        public abstract ParkingTask GetSkippedParking(Trains.ShuntTrain train);
        public abstract RoutingTask GetRouteToSkippedParking(Trains.ShuntTrain train);

        public abstract void SkipParking(ParkingTask parking);
        public abstract void UnskipParking(Trains.ShuntTrain train);

        public virtual void Remove()
        {
            // In this method, don't assert that
            //    this.Graph.{First,Last} == this
            // because we may be in the process of re-adding this MoveTask, and the structural invariants temporarily don't hold.
            // Possible fix: extract a private method that does the "dirty work" and assert these things in public-facing methods.

            if (this.IsRemoved)
                return;

            this.IsRemoved = true;

            if (this.PreviousMove == null)
            {
                if (this.NextMove == null)
                    throw new InvalidOperationException(
                        "Cannot remove the last MoveTask in the PlanGraph"
                    );
                this.Graph.First = this.NextMove;
            }
            else
                this.PreviousMove.NextMove = this.NextMove;

            if (this.NextMove == null)
            {
                Debug.Assert(this.PreviousMove != null); // Otherwise, would have thrown InvalidOperationException above
                this.Graph.Last = this.PreviousMove;
            }
            else
                this.NextMove.PreviousMove = this.PreviousMove;
        }

        public virtual void InsertAfter(MoveTask? position)
        {
            if (this == position)
                return;

            if (this == this.Graph.First)
            {
                Debug.Assert(this.NextMove != null);
                this.Graph.First = this.NextMove;
            }
            if (this == this.Graph.Last)
            {
                Debug.Assert(this.PreviousMove != null);
                this.Graph.Last = this.PreviousMove;
            }

            if (position == null)
            {
                if (this != this.Graph.First)
                    this.InsertBefore(this.Graph.First);
                return;
            }

            this.Remove();

            MoveTask? next = position.NextMove;
            position.NextMove = this;
            this.PreviousMove = position;
            if (next != null)
            {
                next.PreviousMove = this;
                this.MoveOrder = (position.MoveOrder + next.MoveOrder) / 2;
            }
            else
                this.MoveOrder = position.MoveOrder + 1;
            this.NextMove = next;
            this.IsRemoved = false;

            if (position == this.Graph.Last)
                this.Graph.Last = this;
        }

        public virtual void InsertBefore(MoveTask? position)
        {
            if (this == position)
                return;

            this.Remove();

            if (position == null)
            {
                this.InsertAfter(this.Graph.Last);
                return;
            }

            MoveTask? previous = position.PreviousMove;
            position.PreviousMove = this;
            this.NextMove = position;
            if (previous != null)
            {
                previous.NextMove = this;
                this.MoveOrder = (position.MoveOrder + previous.MoveOrder) / 2;
            }
            else
                this.MoveOrder = position.MoveOrder - 1;
            this.PreviousMove = previous;
            this.IsRemoved = false;

            if (position == this.Graph.First)
                this.Graph.First = this;
        }

        public abstract IEnumerable<TrackTask> GetPrevious(Func<TrackTask, bool> selector);
        public abstract IEnumerable<TrackTask> GetNext(Func<TrackTask, bool> selector);

        public abstract bool AllPreviousSatisfy(Func<TrackTask, bool> predicate);
        public abstract bool AllNextSatisfy(Func<TrackTask, bool> predicate);

        public abstract void AddRoute(Routing.Route route);

        public abstract void ReplacePreviousTask(TrackTask task);
        public abstract void ReplaceNextTask(TrackTask task);

        public virtual TrackTask? FindFirstNext(
            Func<TrackTask, bool> predicate,
            Func<TrackTask, Utilities.Time> value
        )
        {
            Utilities.Time time = int.MaxValue;
            TrackTask? first = null;
            foreach (TrackTask task in this.AllNext)
            {
                if (predicate(task) && value(task) < time)
                {
                    time = value(task);
                    first = task;
                }
                else
                {
                    TrackTask? result = task.Next?.FindFirstNext(predicate, value);
                    if (result != null && predicate(result) && value(result) < time)
                    {
                        time = value(result);
                        first = result;
                    }
                }
            }
            return first;
        }

        public virtual TrackTask? FindLastPrevious(
            Func<TrackTask, bool> predicate,
            Func<TrackTask, Utilities.Time> value
        )
        {
            Utilities.Time time = int.MinValue;
            TrackTask? first = null;
            foreach (TrackTask task in this.AllPrevious)
            {
                if (predicate(task) && value(task) > time)
                {
                    time = value(task);
                    first = task;
                }
                else
                {
                    TrackTask? result = task.Previous?.FindLastPrevious(predicate, value);
                    if (result != null && predicate(result) && value(result) > time)
                    {
                        time = value(result);
                        first = result;
                    }
                }
            }
            return first;
        }

        public virtual TrackTask? FindFirstNext(Func<TrackTask, Utilities.Time> value)
        {
            Utilities.Time time = int.MaxValue;
            TrackTask? first = null;
            foreach (TrackTask task in this.AllNext)
            {
                if (value(task) < time)
                {
                    time = value(task);
                    first = task;
                }
                else
                {
                    TrackTask? result = task.Next?.FindFirstNext(value);
                    if (result != null && value(result) < time)
                    {
                        time = value(result);
                        first = result;
                    }
                }
            }
            return first;
        }

        public virtual TrackTask? FindLastPrevious(Func<TrackTask, Utilities.Time> value)
        {
            Utilities.Time time = int.MinValue;
            TrackTask? first = null;
            foreach (TrackTask task in this.AllPrevious)
            {
                if (value(task) > time)
                {
                    time = value(task);
                    first = task;
                }
                else
                {
                    TrackTask? result = task.Previous?.FindLastPrevious(value);
                    if (result != null && value(result) > time)
                    {
                        time = value(result);
                        first = result;
                    }
                }
            }
            return first;
        }

        public virtual void FindAllNext(Func<TrackTask, bool> predicate, List<TrackTask> output)
        {
            foreach (TrackTask task in this.AllNext)
            {
                if (predicate(task))
                    output.Add(task);
                if (task is not DepartureTask)
                    task.Next.FindAllNext(predicate, output);
            }
        }

        public virtual void FindAllPrevious(Func<TrackTask, bool> predicate, List<TrackTask> output)
        {
            foreach (TrackTask task in this.AllPrevious)
            {
                if (predicate(task))
                    output.Add(task);
                if (task is not ArrivalTask)
                    task.Previous.FindAllPrevious(predicate, output);
            }
        }

        public virtual MoveTask LatestPrevious()
        {
            Utilities.Time time = int.MinValue;
            MoveTask result = null!;
            foreach (TrackTask task in this.AllPrevious)
            {
                if (task.Start > time)
                {
                    time = task.Start;
                    result = task.Previous;
                }
            }
            Debug.Assert(result != null);
            return result;
        }

        public virtual MoveTask EarliestNext()
        {
            Utilities.Time time = int.MaxValue;
            MoveTask result = null!;
            foreach (TrackTask task in this.AllNext)
            {
                if (task.End < time)
                {
                    time = task.End;
                    result = task.Next;
                }
            }
            Debug.Assert(result != null);
            return result;
        }

        public virtual MoveTask EarliestPrevious()
        {
            Utilities.Time time = int.MaxValue;
            MoveTask result = null!;
            foreach (TrackTask task in this.AllPrevious)
            {
                if (task.Start < time)
                {
                    time = task.Start;
                    result = task.Previous;
                }
            }
            Debug.Assert(result != null);
            return result;
        }

        public virtual MoveTask LatestNext()
        {
            Utilities.Time time = int.MinValue;
            MoveTask result = null!;
            foreach (TrackTask task in this.AllNext)
            {
                if (task.End > time)
                {
                    time = task.End;
                    result = task.Next;
                }
            }
            Debug.Assert(result != null);
            return result;
        }
    }
}
