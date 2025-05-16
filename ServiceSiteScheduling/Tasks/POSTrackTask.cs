
namespace ServiceSiteScheduling.Tasks
{
    public enum POSTrackTaskType { Arrival, Departure, Service, Parking, Split, Combine }

    class POSTrackTask
    {
        public TrackTask CorrespondingTrackTask { get; set; }

        public int ID { get; set; }

        public List<POSMoveTask> previousMoves { get; set; }

        public List<POSMoveTask> nextMoves { get; set; }

        public POSTrackTaskType TaskType { get; set; }

        public Trains.ShuntTrain Train { get; set; }

        public TrackParts.Track Track { get; set; }

        public List<POSTrackTask> SuccessorTrackTaskByTrainUnits { get; set; }

        public List<POSTrackTask> PredecessorTrackTaskByTrainUnits { get; set; }

        public List<POSTrackTask> SuccessorTrackTaskByInfrastructure { get; set; }

        public List<POSTrackTask> PredecessorTrackTaskByInfrastructure { get; set; }



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


            this.SuccessorTrackTaskByTrainUnits = new List<POSTrackTask>();

            this.PredecessorTrackTaskByTrainUnits = new List<POSTrackTask>();

            this.SuccessorTrackTaskByInfrastructure = new List<POSTrackTask>();

            this.PredecessorTrackTaskByInfrastructure = new List<POSTrackTask>();

        }

        public void displayLinksByInfrastructure()
        {
            Console.Write($"POSTrackTask {this.ID}\n");
            Console.Write("|  Direct Sucessors | ");
            Console.Write("[ ");

            foreach (POSTrackTask item in SuccessorTrackTaskByInfrastructure)
            {
                Console.Write($"POSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]");

            Console.Write("|  Direct Predeccessors | ");
            Console.Write("[ ");

            foreach (POSTrackTask item in PredecessorTrackTaskByInfrastructure)
            {
                Console.Write($"POSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]\n");

        }

        public void displayLinksByTrainUnits()
        {
            Console.Write($"POSTrackTask {this.ID}\n");
            Console.Write("|  Direct Sucessors | ");
            Console.Write("[ ");

            foreach (POSTrackTask item in SuccessorTrackTaskByTrainUnits)
            {
                Console.Write($"POSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]");

            Console.Write("|  Direct Predeccessors | ");
            Console.Write("[ ");

            foreach (POSTrackTask item in PredecessorTrackTaskByTrainUnits)
            {
                Console.Write($"POSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]\n");

        }

        public string GetInfoLinksByTrainUnits()
        {
            string str = "";
            str = str + "TrackTask Links by same Train Unit used :\n";
            str = str + "|Direct successors|: [";
            foreach (POSTrackTask item in SuccessorTrackTaskByInfrastructure)
            {
                str = str + "POSTrackTask " + item.ID + ", ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (POSTrackTask item in PredecessorTrackTaskByInfrastructure)
            {
                str = str + "POSTrackTask " + item.ID + ", ";
            }

            str = str + "]\n";
            return str;
        }

          public string GetInfoLinksByInfrastructure()
        {
            string str = "";
            str = str + "TrackTask Links by same Infrastructure used :\n";
            str = str + "|Direct successors: [";
            foreach (POSTrackTask item in SuccessorTrackTaskByTrainUnits)
            {
                str = str + "POSTrackTask " + item.ID + ", ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (POSTrackTask item in PredecessorTrackTaskByTrainUnits)
            {
                str = str + "POSTrackTask " + item.ID + ", ";
            }

            str = str + "]\n";
            return str;
        }


        public void AddNewSuccessorByTrainUnits(POSTrackTask successor)
        {
            this.SuccessorTrackTaskByTrainUnits.Add(successor);
        }

        public void AddNewPredecessorByTrainUnits(POSTrackTask predeccessor)
        {
            this.PredecessorTrackTaskByTrainUnits.Add(predeccessor);
        }

        public void AddNewSuccessorByInfrastructure(POSTrackTask successor)
        {
            this.SuccessorTrackTaskByInfrastructure.Add(successor);
        }

        public void AddNewPredecessorByInfrastructure(POSTrackTask predeccessor)
        {
            this.PredecessorTrackTaskByInfrastructure.Add(predeccessor);
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
