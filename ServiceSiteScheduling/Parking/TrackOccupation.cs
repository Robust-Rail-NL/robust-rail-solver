using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Parking
{
    abstract class TrackOccupation
    {
        public Track Track;
        public Deque<State> StateDeque;
        public List<State> States;
        public int TrackLengthViolations = 0;
        public int TrackLengthViolationSum = 0;
        public List<State> ViolatingStates;

        public TrackOccupation(Track track)
        {
            this.Track = track;
            this.StateDeque = new Deque<State>();
            this.States = new List<State>();
            this.ViolatingStates = new List<State>();
        }

        public virtual void Arrive(TrackTask task)
        {
            task.State.TrackOccupation = this;
            task.State.HasArrived = true;
            this.StateDeque.Add(task.State, task.ArrivalSide);
            this.States.Add(task.State);
        }

        public virtual void Depart(TrackTask task)
        {
            task.State.ComputeCrossings();
            this.StateDeque.Remove(task.State);
            task.State.HasDeparted = true;
        }

        public virtual void UpdateDepartureOrder(IList<TrackTask> tasks, ShuntTrain train, Side side)
        {
            
            int unitindex = 0, taskindex = 0;
            foreach (State state in (side == Side.A ? this.StateDeque.A2B : this.StateDeque.B2A))
            {
                TrackTask task = state.Task;
                if (tasks.Contains(task))
                {
                    tasks[tasks.IndexOf(task)] = tasks[taskindex];
                    tasks[taskindex++] = task;
                    foreach (ShuntTrainUnit unit in (side == task.ArrivalSide ? task.Train.B2A : task.Train.A2B))
                        if (train.UnitBits[unit.Index])
                            train.Units[unitindex++] = unit;
                }

                if (taskindex == tasks.Count)
                    break;
            }
        }

        public abstract int CountCrossingsIfTurning(ShuntTrain train, Side side);

        public abstract bool HasSufficientSpace(ShuntTrain train, double start, double end);
    
        public virtual void Reset()
        {
            this.StateDeque.Clear();
            foreach (var state in this.States)
                state.Reset();
            this.States.Clear();
            this.ViolatingStates.Clear();
            this.TrackLengthViolations = this.TrackLengthViolationSum = 0;
        }
    }

    class LIFOTrackOccupation : TrackOccupation
    {
        protected Side side;
        int crossings = 0;

        public int Crossings { get { return this.crossings; } }

        public LIFOTrackOccupation(Track track) : base(track)
        {
            this.side = track.Access;
        }

        public override void Arrive(TrackTask task)
        {
            int distance = (this.StateDeque.Head(this.side)?.GetDistance(this.side) ?? this.Track.Length) - task.Train.Length;

            task.State.SetDistance(this.side, distance);
            if (distance < 0)
                this.TrackLengthViolations++;

            base.Arrive(task);
        }

        public int CountCrossingsIfTurning(ShuntTrain train)
        {
            if (this.StateDeque.Count == 0)
                return 0;

            int crossings = 0;
            State state = this.StateDeque.Head(this.side);
            Side otherside = this.side.Flip;
            while (state != null && state.GetDistance(this.side) < train.Length)
            {
                crossings++;
                state = state.Next(otherside) as State;
            }
            return crossings;
        }

        public override int CountCrossingsIfTurning(ShuntTrain train, Side side)
        {
            return this.CountCrossingsIfTurning(train);
        }

        public override void Reset()
        {
            this.crossings = 0;
            base.Reset();
        }

        public override bool HasSufficientSpace(ShuntTrain train, double start, double end)
        {
            throw new NotImplementedException();
        }
    }

    class FreeTrackOccupation : TrackOccupation
    {
        public FreeTrackOccupation(Track track) : base(track) { }

        public override void Arrive(TrackTask task)
        {
            if (this.StateDeque.Count > 0)
            {
                if (task.ArrivalSide == Side.A)
                {
                    this.StateDeque.A.StatesA.Add(task.State);
                    task.State.StatesB.Add(this.StateDeque.A);
                }
                else
                {
                    this.StateDeque.B.StatesB.Add(task.State);
                    task.State.StatesA.Add(this.StateDeque.B);
                }
            }

            base.Arrive(task);
        }

        public override void Depart(TrackTask task)
        {
            base.Depart(task);
            this.ComputeLongestPath(task.State, Side.Both);
        }

        protected void ComputeLongestPath(State state, Side side)
        {
            int length = state.Task.Train.Length;

            if (side.HasFlag(Side.A) && (state.StatesA.Count == 0 || state.StatesA.Last().HasDeparted))
            {
                if (state.StatesA.Count > 0)
                    state.DistanceA = state.StatesA.Max(neighbor => neighbor.DistanceA + neighbor.Task.Train.Length);
                foreach (State neighbor in state.StatesB)
                {
                    if (neighbor.HasDeparted)
                        this.ComputeLongestPath(neighbor, Side.A);
                    else
                        neighbor.DistanceA = Math.Max(state.DistanceA + length, neighbor.DistanceA);
                }
            }

            if (side.HasFlag(Side.B) && (state.StatesB.Count == 0 || state.StatesB.Last().HasDeparted))
            {
                if (state.StatesB.Count > 0)
                    state.DistanceB = state.StatesB.Max(neighbor => neighbor.DistanceB + neighbor.Task.Train.Length);
                foreach (State neighbor in state.StatesA)
                {
                    if (neighbor.HasDeparted)
                        this.ComputeLongestPath(neighbor, Side.B);
                    else
                        neighbor.DistanceB = Math.Max(state.DistanceB + length, neighbor.DistanceB);
                }
            }

            if (!state.ExceedsTrackLength && (state.DistanceA + length > this.Track.Length || state.DistanceB + length > this.Track.Length))
            {
                state.ExceedsTrackLength = true;
                this.TrackLengthViolations++;
            }
        }

        public override int CountCrossingsIfTurning(ShuntTrain train, Side side)
        {
            int count = 0;
            if (side == Side.A)
            {
                var state = this.StateDeque.A;
                while (state != null)
                {
                    if (state.DistanceA > train.Length)
                        break;

                    count++;
                    state = state.B as State;
                }
            }
            else
            {
                var state = this.StateDeque.B;
                while (state != null)
                {
                    if (state.DistanceB > train.Length)
                        break;

                    count++;
                    state = state.A as State;
                }
            }
            return count;
        }

        public override bool HasSufficientSpace(ShuntTrain train, double start, double end)
        {
            throw new NotImplementedException();
        }
    }

    class SimpleTrackOccupation : TrackOccupation
    {
        int occupation = 0;
        List<double> order = new List<double>();
        List<int> space = new List<int>();

        public SimpleTrackOccupation(Track track) : base(track)
        {
        }

        public override void Arrive(TrackTask task)
        {
            this.occupation += task.Train.Length;
            if (task.Previous != null && (this.space.Count == 0 || this.order[this.space.Count - 1] != task.Previous.MoveOrder))
            {
                this.order.Add(task.Previous.MoveOrder);
                this.space.Add(this.Track.Length - this.occupation);
            }
            if (this.occupation > this.Track.Length)
            {
                this.TrackLengthViolations++;
                this.ViolatingStates.Add(task.State);
                this.TrackLengthViolationSum = Math.Max(this.TrackLengthViolationSum, this.occupation - this.Track.Length);
            }
            base.Arrive(task);
        }

        public override void Depart(TrackTask task)
        {
            this.occupation -= task.Train.Length;
            if (task.Next != null && (this.space.Count == 0 || this.order[this.space.Count - 1] != task.Next.MoveOrder))
            {
                this.order.Add(task.Next.MoveOrder);
                this.space.Add(this.Track.Length - this.occupation);
            }
            base.Depart(task);
        }

        public override void Reset()
        {
            this.occupation = 0;
            this.space.Clear();
            this.order.Clear();
            base.Reset();
        }

        public override int CountCrossingsIfTurning(ShuntTrain train, Side side)
        {
            return this.Track.Length - this.occupation >= train.Length ? 0 : 1;
        }

        public override bool HasSufficientSpace(ShuntTrain train, double start, double end)
        {
            int index = this.order.BinarySearchClosestIndexOf(start);
            for (; index < this.space.Count; index++)
            {
                if (this.space[index] < train.Length)
                    return false;
                if (this.order[index] > end)
                    break;
            }

            return true;
        }
    }
}
