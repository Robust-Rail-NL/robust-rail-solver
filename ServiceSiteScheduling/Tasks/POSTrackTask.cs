using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    public enum POSTrackTaskType { Arrival, Departure, Service, Parking, Split, Combine}

    class POSTrackTask
    {
        public TrackTask CorrespondingTrackTask {get; }

        public int ID {get; set;}

        // public POSTrackTask nextTasks {get; set;}

        public List<POSMoveTask> previousMoves { get; set; }

        public List<POSMoveTask> nextMoves { get; set; }
        
        public POSTrackTaskType TaskType { get { return this.tasktype; } }

        protected readonly POSTrackTaskType tasktype;


        public POSTrackTask(POSTrackTaskType type, TrackTask correspondingTrackTask)
        {
    
            this.CorrespondingTrackTask = correspondingTrackTask;
            this.tasktype = type;
        }

    }
}
