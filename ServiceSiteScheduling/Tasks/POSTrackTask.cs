using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    public enum POSTrackTaskType { Arrival, Departure, Service, Parking, Split, Combine }

    class POSTrackTask
    {
        public TrackTask CorrespondingTrackTask { get; set; }

        public int ID { get; set; }

        // public POSTrackTask nextTasks {get; set;}

        public List<POSMoveTask> previousMoves { get; set; }

        public List<POSMoveTask> nextMoves { get; set; }

        public POSTrackTaskType TaskType { get; set; }

        public Trains.ShuntTrain Train { get; set; }

        public TrackParts.Track Track { get; set; }




        public POSTrackTask(TrackTask correspondingTrackTask)
        {
            this.CorrespondingTrackTask = correspondingTrackTask;


            switch (CorrespondingTrackTask.TaskType)
            {
                case TrackTaskType.Arrival:
                    this.TaskType = POSTrackTaskType.Arrival;
                    break;

                case TrackTaskType.Departure:
                    this.TaskType = POSTrackTaskType.Departure;
                    break;

                case TrackTaskType.Parking:
                    this.TaskType = POSTrackTaskType.Parking;
                    break;

                case TrackTaskType.Service:
                    this.TaskType = POSTrackTaskType.Service;
                    break;

                // Default value
                default:
                    this.TaskType = POSTrackTaskType.Service;
                    break;
            }

            this.previousMoves = new List<POSMoveTask>();

            this.nextMoves = new List<POSMoveTask>();

            this.Train = correspondingTrackTask.Train;

            this.Track = correspondingTrackTask.Track;

        }

        public void setPOSTrackTaskType(POSTrackTaskType POSTrackTaskType)
        {

            this.TaskType = POSTrackTaskType;
        }

        public override string ToString()
        {

            string POStype = "";

            switch (this.TaskType)
            {
                case POSTrackTaskType.Arrival:
                    POStype = "Arrival";
                    break;

                case POSTrackTaskType.Departure:
                    POStype = "Departure";
                    break;

                case POSTrackTaskType.Parking:
                    POStype = "Parking";
                    break;

                case POSTrackTaskType.Service:
                    POStype = "Service";
                    break;

                case POSTrackTaskType.Split:
                    POStype = "Split";
                    break;

                 case POSTrackTaskType.Combine:
                    POStype = "Combine";
                    break;
                // Default value
                default:
                    POStype = "Service";
                    break;
            }




            string str = "POSTrackTask " + this.ID + " - " + POStype + "Train: " + Train + " at " + Track.ID + ":\n";
            str = str + "|POSMoveTask Successors: [";

            foreach (POSMoveTask successor in nextMoves)
            {
                str = str + "Move " + successor.ID + " , ";
            }

            str = str + "]\n|POSMoveTask Predeccessors|: [";

            foreach (POSMoveTask predeccessor in previousMoves)
            {
                str = str + "Move " + predeccessor.ID + ", ";

            }
            str = str + "]\n";


            return str;
        }

    }
}
