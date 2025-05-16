
namespace ServiceSiteScheduling.Tasks
{
    class ServiceTask : TrackTask
    {
        public Servicing.ServiceType Type { get; private set; }
        public Utilities.Time MinimumDuration {
            get
            {
                if (this.minimumDuration < 0)
                    this.minimumDuration = this.Train.Units.Sum(unit => unit.ServiceDurations[this.Type.Index]);
                return this.minimumDuration;
            }
        }
        public ServiceTask PreviousServiceTask { get; set; }
        public ServiceTask NextServiceTask { get; set; }
        public Servicing.ServiceResource Resource { get; set; }
        public bool IsRemoved { get; private set; } = false;

        private Utilities.Time minimumDuration = -1;

        public ServiceTask(Trains.ShuntTrain train, TrackParts.Track track, Servicing.ServiceType type, Servicing.ServiceResource resource) : base(train, track, TrackTaskType.Service)
        {
            this.Type = type;
            this.Resource = resource;
        }

        public void SwapBefore(ServiceTask position, Servicing.ServiceResource resource)
        {
            if (this == position)
                return;

            if (position == null || position.Resource == resource)
            {
                this.Remove();

                this.Resource = resource;

                if (position == null)
                {
                    if (resource.Last == null)
                    {
                        this.Resource.First = this.Resource.Last = this;
                        this.PreviousServiceTask = this.NextServiceTask = null;
                    }
                    else
                        this.SwapAfter(resource.Last, resource);
                }
                else
                {
                    if (position.Resource == resource)
                    {
                        ServiceTask previous = position.PreviousServiceTask;
                        position.PreviousServiceTask = this;
                        this.NextServiceTask = position;
                        this.PreviousServiceTask = previous;
                        if (previous != null)
                            previous.NextServiceTask = this;
                        else
                            this.Resource.First = this;
                    }
                }
            }

            if (this.Train.Equals(position.Train))
                sameTrainSwap(position, this);

            this.IsRemoved = false;
        }

        public void SwapAfter(ServiceTask position, Servicing.ServiceResource resource)
        {
            if (this == position)
                return;

            if (position == null || position.Resource == resource)
            {
                this.Remove();

                this.Resource = resource;

                if (position == null)
                {
                    if (resource.First == null)
                    {
                        this.Resource.First = this.Resource.Last = this;
                        this.PreviousServiceTask = this.NextServiceTask = null;
                    }
                    else
                        this.SwapBefore(resource.First, resource);
                }
                else
                {

                    if (position.Resource == resource)
                    {
                        ServiceTask next = position.NextServiceTask;
                        position.NextServiceTask = this;
                        this.PreviousServiceTask = position;
                        this.NextServiceTask = next;
                        if (next != null)
                            next.PreviousServiceTask = this;
                        else
                            this.Resource.Last = this;
                    }
                }
            }

            if (this.Train.Equals(position?.Train))
                sameTrainSwap(this, position);

            this.IsRemoved = false;
        }

        public void Remove()
        {
            if (this.IsRemoved)
                return;

            this.IsRemoved = true;

            if (this.PreviousServiceTask == null)
                this.Resource.First = this.NextServiceTask;
            else
                this.PreviousServiceTask.NextServiceTask = this.NextServiceTask;

            if (this.NextServiceTask == null)
                this.Resource.Last = this.PreviousServiceTask;
            else
                this.NextServiceTask.PreviousServiceTask = this.PreviousServiceTask;

            this.NextServiceTask = this.PreviousServiceTask = null;

        }

        private static void sameTrainSwap(ServiceTask first, ServiceTask second)
        {
            RoutingTask firstprev = first.Previous as RoutingTask,
                        firstnext = first.Next as RoutingTask,
                        secondprev = second.Previous as RoutingTask,
                        secondnext = second.Next as RoutingTask;

            foreach (var task in firstnext.Next)
                task.Previous = secondnext;
            firstprev.Previous.Next = secondprev;
            foreach (var task in secondnext.Next)
                task.Previous = firstnext;
            secondprev.Previous.Next = firstprev;

            var prev = secondprev.Previous;
            secondprev.Previous = firstprev.Previous;
            firstprev.Previous = prev;

            var next = secondnext.Next;
            secondnext.Next = firstnext.Next;
            firstnext.Next = next;

            var track = secondprev.FromTrack;
            secondprev.FromTrack = firstprev.FromTrack;
            firstprev.FromTrack = track;

            track = secondnext.ToTrack;
            var side = secondnext.ToSide;
            secondnext.ToTrack = firstnext.ToTrack;
            secondnext.ToSide = firstnext.ToSide;
            firstnext.ToTrack = track;
            firstnext.ToSide = side;
        }
    }
}
