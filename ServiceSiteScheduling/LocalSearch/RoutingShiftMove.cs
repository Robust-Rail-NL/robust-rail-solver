using System;
using System.Linq;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.LocalSearch
{
    class RoutingShiftMove : RoutingMove
    {
        public Tasks.MoveTask Selected { get; private set; }
        public Tasks.MoveTask Position { get; private set; }
        public bool BeforePosition { get; private set; }

        protected Tasks.MoveTask originalprevious;

        public RoutingShiftMove(PlanGraph graph, Tasks.MoveTask selected, Tasks.MoveTask position, bool before) : base(graph)
        {
            this.Selected = selected;
            this.Position = position;
            this.BeforePosition = before;
            this.originalprevious = selected.PreviousMove;

            /*if (this.BeforePosition)
            {
                this.executestart = selected;
                this.executeend = selected.PreviousMove;
                this.revertstart = position;
                this.revertend = selected;
            }
            else
            {
                this.executestart = selected.NextMove;
                this.executeend = selected;
                this.revertstart = selected;
                this.revertend = position;
            }*/

            /*this.AffectedTracks = new BitSet(ProblemInstance.Current.Tracks.Length);
            this.AffectedTracks[this.Selected.ToTrack.Index] = true;
            this.AffectedTracks[this.Position.ToTrack.Index] = true;
            foreach (var task in this.Selected.AllPrevious)
                this.AffectedTracks[task.Track.Index] = true;
            foreach (var task in this.Position.AllPrevious)
                this.AffectedTracks[task.Track.Index] = true;*/
        }

        public override SolutionCost Execute()
        { 
            if (this.BeforePosition)
                this.Selected.InsertBefore(this.Position);
            else
                this.Selected.InsertAfter(this.Position);

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            this.Selected.InsertAfter(this.originalprevious);
            
            return base.Revert();
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: positioned route {this.Selected} {(this.BeforePosition ? "before" : "after")} {this.Position}";
        }

        public static bool Allowed(Tasks.MoveTask first, Tasks.MoveTask second)
        {
            if (first == null || second == null)
                return false;

            // Allow non-intersecting trains
            if (first.Train.UnitBits.Intersects(second.Train.UnitBits))
                return false;

            // Allowed if there are no service resource conflicts
            Servicing.ServiceResource resource = null;
            foreach (var task in first.AllPrevious)
                if (task.TaskType == Tasks.TrackTaskType.Service)
                {
                    resource = (task as Tasks.ServiceTask).Resource;
                    break;
                }
            if (resource != null)
                foreach (var task in second.AllNext)
                    if (task.TaskType == Tasks.TrackTaskType.Service && (task as Tasks.ServiceTask).Resource == resource)
                        return false;

            // Allowed if there are no service resource conflicts
            //if (first.GetPrevious(task => task is Tasks.ServiceTask).Select(task  => (task as Tasks.ServiceTask).Resource).Intersect(second.GetNext(task => task is Tasks.ServiceTask).Select(task => (task as Tasks.ServiceTask).Resource)).Count() > 0)
            //    return false;

            //if (second.GetPrevious(task => task is Tasks.ServiceTask).Select(task => (task as Tasks.ServiceTask).Resource).Intersect(first.GetNext(task => task is Tasks.ServiceTask).Select(task => (task as Tasks.ServiceTask).Resource)).Count() > 0)
            //    return false;

            // Allowed if not both tasks have a fixed time schedule
            if (first.AllPreviousSatisfy(task => !(task is Tasks.IFixedSchedule)) && first.AllNextSatisfy(task => !(task is Tasks.IFixedSchedule)))
                return true;

            if (second.AllPreviousSatisfy(task => !(task is Tasks.IFixedSchedule)) && second.AllNextSatisfy(task => !(task is Tasks.IFixedSchedule)))
                return true;

            // Allowed if fixed time schedules are in the wrong order
            Time firstscheduled = first.GetPrevious(task => task is Tasks.IFixedSchedule).Select(task => task as Tasks.IFixedSchedule).DefaultIfEmpty().Max(task => task?.ScheduledTime ?? 0);
            firstscheduled = Math.Max(firstscheduled, first.GetNext(task => task is Tasks.IFixedSchedule).Select(task => task as Tasks.IFixedSchedule).DefaultIfEmpty().Max(task => task?.ScheduledTime ?? 0));

            Time secondscheduled = second.GetPrevious(task => task is Tasks.IFixedSchedule).Select(task => task as Tasks.IFixedSchedule).DefaultIfEmpty().Max(task => task?.ScheduledTime ?? 0);
            secondscheduled = Math.Max(secondscheduled, second.GetNext(task => task is Tasks.IFixedSchedule).Select(task => task as Tasks.IFixedSchedule).DefaultIfEmpty().Max(task => task?.ScheduledTime ?? 0));
            return firstscheduled >= secondscheduled;
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var shiftmove = move as RoutingShiftMove;
            if (shiftmove == null)
            {
                var mergemove = move as RoutingMergeMove;
                if (mergemove == null)
                    return false;

                return this.Selected == mergemove.From || this.Position == mergemove.From || this.Selected == mergemove.To || this.Position == mergemove.From;
            }

            return this.Selected == shiftmove.Selected || this.Position == shiftmove.Position || this.Selected == shiftmove.Position || this.Position == shiftmove.Selected;
        }
    }
}
