namespace ServiceSiteScheduling.Tasks
{
    class UnusedPOSMoveTask
    {
        // public Solutions.UnusedPartialOrderSchedule POSPlanGraph { get; set; }

        // A UnusedPOSMoveTask has a MoveTask that is used in the Totaly Ordered Solution
        // even if the order and linking of the POS moves are changing it will not have an effect
        // on the MoveTasks' order (Solutions.PlanGraph) and vica versa, nevertheless this reference is needed
        // because the MoveTasks moves contain important relations with other tasks. @CorrespondingMoveTask is
        // basically a pointer to the corresponding MoveTask.
        public MoveTask CorrespondingMoveTask { get; }

        // Specified according to the order of the Totaly Ordered Solution
        public int ID { get; set; }
        public List<UnusedPOSMoveTask> LinkedMoves { get; set; }

        public List<UnusedPOSMoveTask> SuccessorMovesByTrainUnits { get; set; }

        public List<UnusedPOSMoveTask> PredecessorMovesByTrainUnits { get; set; }

        public List<UnusedPOSMoveTask> SuccessorMovesByInfrastructure { get; set; }

        public List<UnusedPOSMoveTask> PredecessorMovesByInfrastructure { get; set; }

        public List<UnusedPOSTrackTask> SuccessorTrackTasks { get; set; }

        public List<UnusedPOSTrackTask> PredecessorTrackTasks { get; set; }

        public UnusedPOSMoveTask(MoveTask correspondingMoveTask, int id)
        {
            // this.POSPlanGraph = posGraph;
            this.CorrespondingMoveTask = correspondingMoveTask;
            this.ID = id;
            this.LinkedMoves = [];
            this.SuccessorMovesByTrainUnits = [];
            this.PredecessorMovesByTrainUnits = [];
            this.SuccessorMovesByInfrastructure = [];
            this.PredecessorMovesByInfrastructure = [];

            this.SuccessorTrackTasks = [];
            this.PredecessorTrackTasks = [];
        }

        public static void InsertAfter(UnusedPOSMoveTask posMoveTask) { }

        public void AddNewSuccessorByTrainUnits(UnusedPOSMoveTask successor)
        {
            this.SuccessorMovesByTrainUnits.Add(successor);
        }

        public void AddNewPredecessorByTrainUnits(UnusedPOSMoveTask predeccessor)
        {
            this.PredecessorMovesByTrainUnits.Add(predeccessor);
        }

        public void AddNewSuccessorByInfrastructure(UnusedPOSMoveTask successor)
        {
            this.SuccessorMovesByInfrastructure.Add(successor);
        }

        public void AddNewPredecessorByInfrastructure(UnusedPOSMoveTask predeccessor)
        {
            this.PredecessorMovesByInfrastructure.Add(predeccessor);
        }

        public override string ToString()
        {
            string str = "POSMove " + this.ID + ":\n";
            str = str + "Movement Links by same Train Unit used :\n";
            str = str + "|Direct successors: [";

            foreach (UnusedPOSMoveTask successor in SuccessorMovesByTrainUnits)
            {
                str = str + "Move " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (UnusedPOSMoveTask predeccessor in PredecessorMovesByTrainUnits)
            {
                str = str + "Move " + predeccessor.ID + ", ";
            }
            str = str + "]\n";

            str = str + "Movement Links by same Infrastructure used :\n";
            str = str + "|Direct successors: [";

            foreach (UnusedPOSMoveTask successor in SuccessorMovesByInfrastructure)
            {
                str = str + "Move " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (UnusedPOSMoveTask predeccessor in PredecessorMovesByInfrastructure)
            {
                str = str + "Move " + predeccessor.ID + ", ";
            }
            str = str + "]\n";

            str = str + "Track Task Links :\n";
            str = str + "|Direct successors: [";

            foreach (UnusedPOSTrackTask successor in SuccessorTrackTasks)
            {
                str = str + "Track Task " + successor.ID + " , ";
            }
            str = str + "]\n|Direct predeccessors|: [";

            foreach (UnusedPOSTrackTask predeccessor in PredecessorTrackTasks)
            {
                str = str + "Track Task " + predeccessor.ID + ", ";
            }
            str = str + "]\n";

            return str;
        }
    }
}
