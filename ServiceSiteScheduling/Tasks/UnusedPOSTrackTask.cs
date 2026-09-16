namespace ServiceSiteScheduling.Tasks
{
    public enum POSTrackTaskType
    {
        Arrival,
        Departure,
        Service,
        Parking,
        Split,
        Combine,
    }

    class UnusedPOSTrackTask
    {
        public TrackTask CorrespondingTrackTask { get; set; }

        public int ID { get; set; }

        public List<UnusedPOSMoveTask> previousMoves { get; set; }

        public List<UnusedPOSMoveTask> nextMoves { get; set; }

        public POSTrackTaskType TaskType { get; set; }

        public Trains.ShuntTrain Train { get; set; }

        public TrackParts.Track Track { get; set; }

        public List<UnusedPOSTrackTask> SuccessorTrackTaskByTrainUnits { get; set; }

        public List<UnusedPOSTrackTask> PredecessorTrackTaskByTrainUnits { get; set; }

        public List<UnusedPOSTrackTask> SuccessorTrackTaskByInfrastructure { get; set; }

        public List<UnusedPOSTrackTask> PredecessorTrackTaskByInfrastructure { get; set; }

        public UnusedPOSTrackTask(TrackTask correspondingTrackTask)
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

                // StandIn/StandOut are parking tasks that happen to bookend an
                // inStanding/outStanding train; in a POS plan they are parking.
                case TrackTaskType.Parking:
                case TrackTaskType.StandIn:
                case TrackTaskType.StandOut:
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

            this.previousMoves = [];

            this.nextMoves = [];

            this.Train = correspondingTrackTask.Train;

            this.Track = correspondingTrackTask.Track;

            this.SuccessorTrackTaskByTrainUnits = [];

            this.PredecessorTrackTaskByTrainUnits = [];

            this.SuccessorTrackTaskByInfrastructure = [];

            this.PredecessorTrackTaskByInfrastructure = [];
        }

        public void displayLinksByInfrastructure()
        {
            Console.Write($"UnusedPOSTrackTask {this.ID}\n");
            Console.Write("|  Direct Sucessors | ");
            Console.Write("[ ");

            foreach (UnusedPOSTrackTask item in SuccessorTrackTaskByInfrastructure)
            {
                Console.Write($"UnusedPOSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]");

            Console.Write("|  Direct Predeccessors | ");
            Console.Write("[ ");

            foreach (UnusedPOSTrackTask item in PredecessorTrackTaskByInfrastructure)
            {
                Console.Write($"UnusedPOSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]\n");
        }

        public void displayLinksByTrainUnits()
        {
            Console.Write($"UnusedPOSTrackTask {this.ID}\n");
            Console.Write("|  Direct Sucessors | ");
            Console.Write("[ ");

            foreach (UnusedPOSTrackTask item in SuccessorTrackTaskByTrainUnits)
            {
                Console.Write($"UnusedPOSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]");

            Console.Write("|  Direct Predeccessors | ");
            Console.Write("[ ");

            foreach (UnusedPOSTrackTask item in PredecessorTrackTaskByTrainUnits)
            {
                Console.Write($"UnusedPOSTrackTask {item.ID}, ");
            }
            Console.WriteLine(" ]\n");
        }

        public string GetInfoLinksByTrainUnits()
        {
            string str = "";
            str = str + "TrackTask Links by same Train Unit used :\n";
            str = str + "|Direct successors|: [";
            foreach (UnusedPOSTrackTask item in SuccessorTrackTaskByInfrastructure)
            {
                str = str + "UnusedPOSTrackTask " + item.ID + ", ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (UnusedPOSTrackTask item in PredecessorTrackTaskByInfrastructure)
            {
                str = str + "UnusedPOSTrackTask " + item.ID + ", ";
            }

            str = str + "]\n";
            return str;
        }

        public string GetInfoLinksByInfrastructure()
        {
            string str = "";
            str = str + "TrackTask Links by same Infrastructure used :\n";
            str = str + "|Direct successors: [";
            foreach (UnusedPOSTrackTask item in SuccessorTrackTaskByTrainUnits)
            {
                str = str + "UnusedPOSTrackTask " + item.ID + ", ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (UnusedPOSTrackTask item in PredecessorTrackTaskByTrainUnits)
            {
                str = str + "UnusedPOSTrackTask " + item.ID + ", ";
            }

            str = str + "]\n";
            return str;
        }

        public void AddNewSuccessorByTrainUnits(UnusedPOSTrackTask successor)
        {
            this.SuccessorTrackTaskByTrainUnits.Add(successor);
        }

        public void AddNewPredecessorByTrainUnits(UnusedPOSTrackTask predeccessor)
        {
            this.PredecessorTrackTaskByTrainUnits.Add(predeccessor);
        }

        public void AddNewSuccessorByInfrastructure(UnusedPOSTrackTask successor)
        {
            this.SuccessorTrackTaskByInfrastructure.Add(successor);
        }

        public void AddNewPredecessorByInfrastructure(UnusedPOSTrackTask predeccessor)
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

            string str =
                "UnusedPOSTrackTask "
                + this.ID
                + " - "
                + POStype
                + "Train: "
                + Train
                + " at "
                + Track.ID
                + ":\n";
            str = str + "|UnusedPOSMoveTask Successors: [";

            foreach (UnusedPOSMoveTask successor in nextMoves)
            {
                str = str + "Move " + successor.ID + " , ";
            }

            str = str + "]\n|UnusedPOSMoveTask Predeccessors|: [";

            foreach (UnusedPOSMoveTask predeccessor in previousMoves)
            {
                str = str + "Move " + predeccessor.ID + ", ";
            }
            str = str + "]\n";

            return str;
        }
    }
}
