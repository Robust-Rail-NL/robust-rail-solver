
namespace ServiceSiteScheduling.Tasks
{
    class POSMoveTask
    {
        // public Solutions.PartialOrderSchedule POSPlanGraph { get; set; }

        // A POSMoveTask has a MoveTask that is used in the Totaly Ordered Solution
        // even if the order and linking of the POS moves are changing it will not have an effect
        // on the MoveTasks' order (Solutions.PlanGraph) and vica versa, nevertheless this reference is needed
        // because the MoveTasks moves contain important relations with other tasks. @CorrespondingMoveTask is
        // basically a pointer to the corresponding MoveTask.
        public MoveTask CorrespondingMoveTask { get; }

        // Specified according to the order of the Totaly Ordered Solution
        public int ID { get; set; }
        public List<POSMoveTask> LinkedMoves { get; set; }


        public List<POSMoveTask> SuccessorMovesByTrainUnits {get; set;}

        public List<POSMoveTask> PredecessorMovesByTrainUnits {get; set;}

        public List<POSMoveTask> SuccessorMovesByInfrastructure {get; set;}

        public List<POSMoveTask> PredecessorMovesByInfrastructure {get; set;}

        public List<POSTrackTask> SuccessorTrackTasks {get; set;}

        public List<POSTrackTask> PredecessorTrackTasks {get; set;}


        public POSMoveTask(MoveTask correspondingMoveTask, int id)
        {
            // this.POSPlanGraph = posGraph;
            this.CorrespondingMoveTask = correspondingMoveTask;
            this.ID = id;
            this.LinkedMoves = new  List<POSMoveTask>();
            this.SuccessorMovesByTrainUnits = new List<POSMoveTask>();
            this.PredecessorMovesByTrainUnits = new List<POSMoveTask>();
            this.SuccessorMovesByInfrastructure = new List<POSMoveTask>();
            this.PredecessorMovesByInfrastructure = new List<POSMoveTask>();
            
            this.SuccessorTrackTasks = new List<POSTrackTask>();
            this.PredecessorTrackTasks = new List<POSTrackTask>();
        }   

        public void InsertAfter(POSMoveTask posMoveTask)
        {
            
        }
        public void AddNewSuccessorByTrainUnits(POSMoveTask successor)
        {
            this.SuccessorMovesByTrainUnits.Add(successor);
        }

        public void AddNewPredecessorByTrainUnits(POSMoveTask predeccessor)
        {
            this.PredecessorMovesByTrainUnits.Add(predeccessor);
        }

        public void AddNewSuccessorByInfrastructure(POSMoveTask successor)
        {
            this.SuccessorMovesByInfrastructure.Add(successor);
        }

        public void AddNewPredecessorByInfrastructure(POSMoveTask predeccessor)
        {
            this.PredecessorMovesByInfrastructure.Add(predeccessor);
        }
        public override string ToString()
        {
            string str = "POSMove " + this.ID + ":\n";
            str = str + "Movement Links by same Train Unit used :\n";
            str = str + "|Direct successors: [";

            foreach(POSMoveTask successor in SuccessorMovesByTrainUnits)
            {
                str = str + "Move " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach(POSMoveTask predeccessor in PredecessorMovesByTrainUnits)
            {
                str = str + "Move " + predeccessor.ID + ", ";

            }
            str = str + "]\n";

            str = str + "Movement Links by same Infrastructure used :\n";
            str = str + "|Direct successors: [";

            foreach(POSMoveTask successor in SuccessorMovesByInfrastructure)
            {
                str = str + "Move " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach(POSMoveTask predeccessor in PredecessorMovesByInfrastructure)
            {
                str = str + "Move " + predeccessor.ID + ", ";

            }
            str = str + "]\n";

            str = str + "Track Task Links :\n";
            str = str + "|Direct successors: [";

            foreach(POSTrackTask successor in SuccessorTrackTasks)
            {
                str = str + "Track Task " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach(POSTrackTask predeccessor in PredecessorTrackTasks)
            {
                str = str + "Track Task " + predeccessor.ID + ", ";

            }
            str = str + "]\n";

            return str;
        }
    }

}

