using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Servicing;


namespace ServiceSiteScheduling.Solutions
{
    class PartialOrderSchedule
    {
        public ShuntTrainUnit[] ShuntUnits { get; private set; }

        public ArrivalTask[] ArrivalTasks { get; private set; }

        public List<Trains.TrainUnit> ListOfTrainUnits { get; set; }
        DepartureTask[] DepartureTasks;

        public ArrivalTask FirstArrival { get { return this.ArrivalTasks.First(arrival => arrival.Next.PreviousMove == null); } }

        // This is the Adjacency List for POS Movements: Each POSMoveTask maps to a list of connected POSMoveTask
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList { get; private set; }

        // This is the Adjacency List for POS Movements using the same infrastructure: Each POSMoveTask maps to a list of connected POSMoveTask
        // (dashed arcs dependency links)
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyListForInfrastructure { get; private set; }

        // This is the Adjacency List for POS Movements using the same Train Unit: Each POSMoveTask maps to a list of connected POSMoveTask
        // (solid arcs dependency links)
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyListForTrainUint { get; private set; }

        // This is the Adjacency List for POS TrackTask using the same Train Unit: Each POSTrackTask maps to a list of connected POSTrackTask
        // (dotted arcs links)
        public Dictionary<POSTrackTask, List<POSTrackTask>> POSTrackTaskadjacencyListForTrainUsed { get; set; }

        // This is the Adjacency List for POS TrackTask using the same Infrastructure: Each POSTrackTask maps to a list of connected POSTrackTask
        // (dotted arcs links)
        public Dictionary<POSTrackTask, List<POSTrackTask>> POSTrackTaskadjacencyListForInfrastructure { get; set; }



        // First movement of the POS
        public POSMoveTask FirstPOS { get; set; }

        // Last movement of the POS
        public POSMoveTask LastPOS { get; set; }


        // Reated to Total Ordered Solution
        public MoveTask First { get; set; }
        public MoveTask Last { get; set; }

        // List of moves got from the Totally Ordered Solution
        // the moves already contain the relations with the tasks
        // this list should be initiated only once and the order of moves should not be changed
        public List<MoveTask> ListOfMoves { get; set; }

        public List<POSTrackTask> ListOfPOSTrackTasks { get; set; }

        // Dictionary that contains the overall Infrastructure used in the scenario
        public Dictionary<ulong, Infrastructure> DictOfInfrastructure { get; set; }
        public PartialOrderSchedule(MoveTask first)
        {
            this.First = first;

        }

        // Get all the train units (shunt train units) used by the movements in this scenario
        // this information is obtained from the ProblemInstance, created from the `scenario.data` file
        public List<Trains.TrainUnit> GetTrainFleet()
        {
            ProblemInstance instance = ProblemInstance.Current;

            List<Trains.TrainUnit> trains = new List<Trains.TrainUnit>();



            foreach (var train in instance.TrainUnits)
            {
                trains.Add(train);
            }

            return trains;
        }

        // Get the infrastructure describing the shunting yard
        // this information is obtained from the ProblemInstance, created from the `location.json` file
        public Dictionary<ulong, Infrastructure> GetInfrastructure()
        {
            ProblemInstance instance = ProblemInstance.Current;

            Dictionary<ulong, Infrastructure> infrastructuremap = new Dictionary<ulong, Infrastructure>();
            int index = 0;

            foreach (var part in instance.InterfaceLocation.TrackParts)
            {
                switch (part.Type)
                {
                    case AlgoIface.TrackPartType.RailRoad:
                        Track track = new Track(part.Id, part.Name, ServiceType.None, (int)part.Length, Side.None, part.ParkingAllowed, part.SawMovementAllowed);
                        track.Index = index++;
                        infrastructuremap[part.Id] = track;
                        break;
                    case AlgoIface.TrackPartType.Switch:
                        infrastructuremap[part.Id] = new Switch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.EnglishSwitch:
                        infrastructuremap[part.Id] = new EnglishSwitch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.HalfEnglishSwitch:
                        infrastructuremap[part.Id] = new HalfEnglishSwitch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.Intersection:
                        infrastructuremap[part.Id] = new Intersection(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.Bumper:
                        var gateway = new GateWay(part.Id, part.Name);
                        infrastructuremap[part.Id] = gateway;
                        break;
                    default:
                        break;
                }
            }

            return infrastructuremap;

        }

        // Returns list of the IDs of the train units used by a movement (MoveTask)

        public List<int> GetIDListOfTrainUnitsUsed(MoveTask move)
        {
            List<int> trainUnits = new List<int>();

            foreach (TrackTask task in move.AllNext)
            {
                foreach (ShuntTrainUnit trainUnit in task.Train.Units)
                    trainUnits.Add(trainUnit.Index);
            }

            foreach (TrackTask task in move.AllPrevious)
            {
                foreach (ShuntTrainUnit trainUnit in task.Train.Units)
                    trainUnits.Add(trainUnit.Index);
            }

            return trainUnits.Distinct().ToList();

        }

        // Returns list of the IDs of the train units used by a POSTrackTask (POSTrackTask)
        public List<int> GetIDListOfTrainUnitUsedPOSTrackTask(POSTrackTask posTrackTask)
        {
            List<int> trainUnits = new List<int>();

            foreach (ShuntTrainUnit trainUnit in posTrackTask.Train.Units)
            {
                trainUnits.Add(trainUnit.Index);
            }

            return trainUnits.Distinct().ToList();
        }

        // Returns list of the IDs of the Infrastructure used by a POSTrackTask (POSTrackTask)

        public List<ulong> GetIDListOfInfraUsedByTrackTasks(POSTrackTask posTrackTask)
        {
            List<ulong> IDListOfInfraUsed = [
                posTrackTask.Track.ASide.ID,
                posTrackTask.Track.ID,
                posTrackTask.Track.BSide.ID,
            ];

            return IDListOfInfraUsed;
        }


        // Returns list of the IDs of the Infrastructure used by a movement (MoveTask)
        public List<ulong> GetIDListOfInfraUsed(MoveTask move)
        {
            List<ulong> IDListOfInfraUsed = new List<ulong>();

            var routing = move as RoutingTask;

            if (routing != null)
            {
                // Standard MoveTask
                var tracks = routing.Route.Tracks;
                var lastTrack = tracks.Last();

                foreach (Track track in tracks)
                {

                    IDListOfInfraUsed.Add(track.ASide.ID);
                    IDListOfInfraUsed.Add(track.ID);
                    IDListOfInfraUsed.Add(track.BSide.ID);

                }
            }
            else
            {
                // Departure MoveTask
                if (move.TaskType == MoveTaskType.Departure)
                {
                    // Cast as DepartureRoutingTask
                    var routingDeparture = move as DepartureRoutingTask;
                    if (routingDeparture != null)
                    {
                        var listOfRoutes = routingDeparture.GetRoutes();

                        // // TODO: display more infrastructure
                        int numberOfInfraUsed = 0;
                        int k = 0;
                        foreach (Route route in listOfRoutes)
                        {
                            var tracks = route.Tracks;
                            var lastTrack = tracks.Last();

                            List<ulong> IDListOfInfraUsedIntermediate = new List<ulong>();

                            foreach (Track track in tracks)
                            {

                                IDListOfInfraUsedIntermediate.Add(track.ASide.ID);
                                IDListOfInfraUsedIntermediate.Add(track.ID);
                                IDListOfInfraUsedIntermediate.Add(track.BSide.ID);

                            }
               
                            if (k == 0)
                            {
                                numberOfInfraUsed = IDListOfInfraUsedIntermediate.Count;
                                foreach (ulong trackID in IDListOfInfraUsedIntermediate)
                                    IDListOfInfraUsed.Add(trackID);

                            }
                            else
                            {
                                if (IDListOfInfraUsedIntermediate.Count < numberOfInfraUsed)
                                {
                                    // Less infrastructure used
                                    numberOfInfraUsed = IDListOfInfraUsedIntermediate.Count;
                                    IDListOfInfraUsed = new List<ulong>();

                                    foreach (ulong trackID in IDListOfInfraUsedIntermediate)
                                        IDListOfInfraUsed.Add(trackID);
                                }

                            }
                            k++;

                        }


                    }
                }
            }

            return IDListOfInfraUsed;
        }

        // Returns true if the same infrastructure is used by previous moves as the required in @IDListOfInfraUsed
        // @IDListOfInfraUsed contains the infrastructure the current move will use
        // @InfraOccupiedByMovesID is a dictionary (Key:Value) contains all the infrastructures (Key) and
        // their occupation by a specific move (Value) - note: the moves are specified by their IDs
        // @conflictingMoveIds contains all the conflicting moves, that use the same infrastructure the current move requires to occupy
        public bool InfraConflict(Dictionary<Infrastructure, int> InfraOccupiedByMovesID, List<ulong> IDListOfInfraUsed, int moveID, ref List<int> conflictingMoveIds)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastructure = this.DictOfInfrastructure;

            conflictingMoveIds = new List<int>();
            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByMovesID[DictOfInfrastructure[id]] == 999)
                {
                    // conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastructure[id]];
                }
                else
                {
                    if (conflictingMoveIds.Contains(InfraOccupiedByMovesID[DictOfInfrastructure[id]]) == false)
                        conflictingMoveIds.Add(InfraOccupiedByMovesID[DictOfInfrastructure[id]]);
                }


            }
            if (conflictingMoveIds.Count != 0)
                return true;

            return false;
        }

        // Returns true if the same train unit is used by previous moves as the required in @IDListOfTrainUnitUsed
        // @IDListOfTrainUnitUsed contains the train units of the current move
        // @TrainUnitsOccupiedByMovesID is a dictionary (Key:Value) contains all the train units (Key) and
        // their appearance by a specific move (Value) - note: the moves are specified by their IDs
        // @conflictingMoveIds contains all the conflicting moves, with the same train unit as the current move has
        public bool TrainUnitConflict(Dictionary<Trains.TrainUnit, int> TrainUnitsOccupiedByMovesID, List<int> IDListOfTrainUnitUsed, ref List<int> conflictingMoveIds)
        {
            List<Trains.TrainUnit> listOfTrainUnits = this.ListOfTrainUnits;

            conflictingMoveIds = new List<int>();

            foreach (int id in IDListOfTrainUnitUsed)
            {
                if (TrainUnitsOccupiedByMovesID[listOfTrainUnits[id]] == 999)
                {

                }
                else
                {
                    if (conflictingMoveIds.Contains(TrainUnitsOccupiedByMovesID[listOfTrainUnits[id]]) == false)
                    {
                        conflictingMoveIds.Add(TrainUnitsOccupiedByMovesID[listOfTrainUnits[id]]);
                    }
                }
            }
            if (conflictingMoveIds.Count != 0)
                return true;

            return false;

        }

        // Returns true if the same train unit is used by previous POSTrackTask as the required in @IDListOfTrainUnitUsed
        // @IDListOfTrainUnitUsed contains the train units of the current POSTrackTask
        // @TrainUnitsOccupiedByTrackTaskID is a dictionary (Key:Value) contains all the train units (Key) and
        // their appearance by a specific POSTrackTask (Value) - note: the POSTrackTasks are specified by their IDs
        // @conflictingTrackTaskIds contains all the conflicting POSTrackTask, with the same train unit as the current POSTrackTask has
        public bool TrainUnitConflictByPOSTrackTask(Dictionary<Trains.TrainUnit, int> TrainUnitsOccupiedByTrackTaskID, List<int> IDListOfTrainUnitUsed, ref List<int> conflictingTrackTaskIds)
        {
            List<Trains.TrainUnit> listOfTrainUnits = this.ListOfTrainUnits;

            conflictingTrackTaskIds = new List<int>();

            foreach (int id in IDListOfTrainUnitUsed)
            {
                if (TrainUnitsOccupiedByTrackTaskID[listOfTrainUnits[id]] == 999)
                {

                }
                else
                {
                    if (conflictingTrackTaskIds.Contains(TrainUnitsOccupiedByTrackTaskID[listOfTrainUnits[id]]) == false)
                    {
                        conflictingTrackTaskIds.Add(TrainUnitsOccupiedByTrackTaskID[listOfTrainUnits[id]]);
                    }
                }
            }
            if (conflictingTrackTaskIds.Count != 0)
                return true;

            return false;

        }

        // Links the previous move -@parentMovementID- this move was conflicting since it previously used the same infrastructure/train unit as the
        // the current move -@childMovementID-
        // @MovementLinks is a dictionary with move IDs as Key, and value as List of all the linked moves
        public void LinkMovementsByID(Dictionary<int, List<int>> MovementLinks, int parentMovementID, int childMovementID)
        {

            if (!MovementLinks.ContainsKey(parentMovementID))
            {
                MovementLinks[parentMovementID] = new List<int>();

            }
            MovementLinks[parentMovementID].Add(childMovementID);
        }


        // Links the previous POSTrackTask -@parentPOSTrackTaskID- this POSTrackTask was conflicting since it previously used the same infrastructure/train unit
        // as the current POSTrackTask -@childPOSTrackTaskID-
        // @POSTrackTaskLinks is a dictionary with POSTrackTask IDs as Key, and value as List of all the linked POSTrackTasks
        public void LinkTrackTaskByID(Dictionary<int, List<int>> POSTrackTaskLinks, int parentPOSTrackTaskID, int childPOSTrackTaskID)
        {

            if (!POSTrackTaskLinks.ContainsKey(parentPOSTrackTaskID))
            {
                POSTrackTaskLinks[parentPOSTrackTaskID] = new List<int>();

            }
            POSTrackTaskLinks[parentPOSTrackTaskID].Add(childPOSTrackTaskID);
        }



        public void MergeMovements(Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves, Dictionary<Infrastructure, int> InfraOccupiedByMovesID, Dictionary<ulong, Infrastructure> DictOfInfrastructure, int currentID)
        {
            List<MoveTask> listOfMoves = this.ListOfMoves;

            var currentMove = listOfMoves[currentID];
            List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove);

            foreach (ulong infraID in IDListOfInfraUsed)
            {
                InfraOccupiedByMoves[DictOfInfrastructure[infraID]] = currentMove;
                InfraOccupiedByMovesID[DictOfInfrastructure[infraID]] = currentID;
            }
        }

        // Returns true if the same infrastructure is used by previous POSTrackTask as the required in @IDListOfInfraUsed
        // @IDListOfInfraUsed contains the infrastructure the current POSTrackTask will use
        // @InfraOccupiedByTrackTaskID is a dictionary (Key:Value) contains all the infrastructures (Key) and
        // their occupation by a specific POSTrackTask (Value) - note: the POSTrackTask are specified by their IDs
        // @conflictingTrackTaskIds contains all the conflicting moves, that use the same infrastructure the current POSTrackTask requires to occupy
        public bool InfraConflictByTrackTasks(Dictionary<Infrastructure, int> InfraOccupiedByTrackTaskID, List<ulong> IDListOfInfraUsed, ref List<int> conflictingTrackTaskIds)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastructure = this.DictOfInfrastructure;

            conflictingTrackTaskIds = new List<int>();
            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByTrackTaskID[DictOfInfrastructure[id]] == 999)
                {
                    // conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastructure[id]];
                }
                else
                {
                    if (conflictingTrackTaskIds.Contains(InfraOccupiedByTrackTaskID[DictOfInfrastructure[id]]) == false)
                        conflictingTrackTaskIds.Add(InfraOccupiedByTrackTaskID[DictOfInfrastructure[id]]);
                }


            }
            if (conflictingTrackTaskIds.Count != 0)
                return true;

            return false;

        }

        public void CreatePOS()
        {

            // Index of the list is the ID assigned to a move
            List<MoveTask> listOfMoves = this.ListOfMoves;

            // Dictionary with move IDs as Key, and value as List of all the linked moves
            // Key: movement ID (parent move) this move was conflicting since it previously used the same infrastructure as the
            // the current move even if it is trivial when the same train unit is used in the previous and the current move,
            // Value: list of linked movements (IDs child moves)
            // 0:{1,3} OR 1:{2,3}
            // Idea is to obtain multiple directed graphs -> dependency between linked movements
            // To Note: here the links are the links of infrastructure conflicts and same train units per movement
            // @MovementLinksSameInfrastructure contains the linked moves related to the same infrastructure used
            // @MovementLinksSameTrainUnit contains the linked moves related to the same train unit used
            Dictionary<int, List<int>> MovementLinks = new Dictionary<int, List<int>>();

            // Dictionary with move IDs as Key, and value as List linked moves using the same infrastructure,
            // in this dictionary a movement is linked to another movement (parent move) if and only if they used the same
            // infrastructure aka dashed arcs Move_i---> Move_j
            Dictionary<int, List<int>> MovementLinksSameInfrastructure = new Dictionary<int, List<int>>();

            // Dictionary with move IDs as Key, and value as List of linked moves using the same train unit,
            // in this dictionary a movement is linked to another movement (parent move) if and only if they used the same
            // train unit aka solid arcs Move_i -> Move_j
            Dictionary<int, List<int>> MovementLinksSameTrainUnit = new Dictionary<int, List<int>>();

            // Dictionary with all infrastructures, for each infrastructure a movement is assigned
            Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves = new Dictionary<Infrastructure, MoveTask>();

            // Dictionary with all infrastructures, for each infrastructure a movement ID is assigned, the IDs
            // are used to access a move which is stored in 'listOfMoves' or  'this.ListOfMoves'
            // the InfraOccupiedByMovesID is initialized with 999, meaning that there in no valid movement ID
            // assigned yet to the for the given infrastructure
            Dictionary<Infrastructure, int> InfraOccupiedByMovesID = new Dictionary<Infrastructure, int>();

            Dictionary<Infrastructure, int> InfraOccupiedByTrackTaskID = new Dictionary<Infrastructure, int>();

            //List of al train uint used the movement present in this scenario
            List<Trains.TrainUnit> ListOfTrainUnits = this.ListOfTrainUnits;

            // Dictionary with all train units, for each train unit a movement ID is assigned, the IDs
            // are used to access a move which is stored in 'listOfMoves' or  'this.ListOfMoves'
            // the TrainUnitsOccupiedByMovesID is initialized with 999, meaning that there in no valid movement ID
            // assigned yet to given train unit
            Dictionary<Trains.TrainUnit, int> TrainUnitsOccupiedByMovesID = new Dictionary<Trains.TrainUnit, int>();

            // Dictionary containing all the infrastructure, index:infrastructure
            Dictionary<ulong, Infrastructure> DictOfInfrastructure = this.DictOfInfrastructure;

            // Dictionary with POSTrackTask IDs as Key, and value as List of linked POSTrackTask using the same train unit,
            // in this dictionary a POSTrackTask is linked to another POSTrackTask (parent POSTrackTask) if and only if they used the same
            // train unit aka dotted arcs (version 2) Task_i...> Task_j
            Dictionary<int, List<int>> POSTrackTaskLinksSameInfrastructure = new Dictionary<int, List<int>>();


            // Dictionary with POSTrackTask IDs as Key, and value as List linked POSTrackTask using the same infrastructure,
            // in this dictionary a POSTrackTask is linked to another POSTrackTask (parent POSTrackTask) if and only if they used the same
            // infrastructure aka dotted arcs (version 1) Task_i...> Task_j
            Dictionary<int, List<int>> POSTrackTaskLinksSameTrainUnits = new Dictionary<int, List<int>>();

            // Dictionary with all train units, for each train unit a POSTrackTask ID is assigned, the IDs
            // are used to access a POSTrackTask which is stored in 'this.ListOfPOSTrackTasks'
            // the TrainUnitsOccupiedByPOSTrackTaskID is initialized with 999, meaning that there in no valid POSTrackTask ID
            // assigned yet to given train unit
            Dictionary<Trains.TrainUnit, int> TrainUnitsOccupiedByPOSTrackTaskID = new Dictionary<Trains.TrainUnit, int>();

            // Init dictionary for infrastructures occupied by moves
            bool Test = true;

            // Initialize dictionaries
            foreach (KeyValuePair<ulong, Infrastructure> infra in DictOfInfrastructure)
            {
                InfraOccupiedByMoves[infra.Value] = null;
                InfraOccupiedByMovesID[infra.Value] = 999;
                InfraOccupiedByTrackTaskID[infra.Value] = 999;
            }


            foreach (Trains.TrainUnit train in ListOfTrainUnits)
            {
                TrainUnitsOccupiedByMovesID[train] = 999;
                TrainUnitsOccupiedByPOSTrackTaskID[train] = 999;
            }


            int ok = 1;
            int moveIndex = 0;

            List<int> conflictingMoveIds = new List<int>();


            // Example of the using @InfraOccupiedByMovesID to link moves using the same infrastructure: (x in this example is 999 in @InfraOccupiedByMovesID)
            // Scenario:
            // Move 0: 0 -> 2 -> 4  (infrastructure)
            // Move 1: 0 -> 2 -> 1  (infrastructure)
            // Move 2: 4 -> 2 -> 3  (infrastructure)

            // Evolution of @InfraOccupiedByMovesID:
            // | Move 0 | => iteration 0
            // 0; 1; 2; 3; 4; (infrastructure)
            // 0; x; 0; x; 0; (occupation by move)
            // No link

            // | Move 1 | => iteration 1
            // 0; 1; 2; 3; 4; (infrastructure)
            // 1; 1; 1; x; 0; (occupation by move) 
            // Move 1 is in conflict with Move 0 => link Move 0 and Move 1; Move 0-> Move 1

            // | Move 2 | => iteration 2
            // 0; 1; 2; 3; 4; (infrastructure)
            // 1; 1; 2; 2; 2; (occupation by move)
            // Move 1 is in conflict with Move 0 and Move 1 => link Move 1 and Move 2 and Move 0 and Move 2; Move 0-> Move 1-> Move 2
            //                                                                                                     ----------> Move 2 



            while (ok != 0)
            {

                var currentMove = listOfMoves[moveIndex];
                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove); // infrastructure used by the movement


                // Identify all the conflicting moves related to the infrastructure used by the movements - and link moves
                if (InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveIds) == false)
                {
                    // No conflict occurred

                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        // Assign move to the infrastructure occupied
                        InfraOccupiedByMoves[DictOfInfrastructure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastructure[infraID]] = moveIndex;
                    }

                }
                else
                {

                    // Contains all the movements that was assigned to the same movements as the train unit of the current move (moveIndex)
                    // this also mean that these movements are conflicting because of the same train used assigned to the movement
                    // and not only because of the same infrastructure used
                    List<int> movesUsingSameTrainUnit = CheckIfSameTrainUintUsed(conflictingMoveIds, listOfMoves, moveIndex);

                    foreach (int MoveId in conflictingMoveIds)
                    {
                        // 1st: link movements -> conflictingMoveId is now linked with the moveIndex (current move id)
                        LinkMovementsByID(MovementLinks, MoveId, moveIndex);


                        if (movesUsingSameTrainUnit.Count != 0)
                        {
                            // This statement is used to link the movements conflicted because of using the same infrastructure\
                            // and not because of same train unit assigned per movement {aka dashed line dependency}
                            if (!movesUsingSameTrainUnit.Contains(MoveId))
                                LinkMovementsByID(MovementLinksSameInfrastructure, MoveId, moveIndex);

                            // This statement is used to link the movements conflicted because of using the same train unit\
                            // and not because of same infrastructure assigned per movement {aka solid line dependency}
                            // if (movesUsingSameTrainUnit.Contains(MoveId))
                            //     LinkMovementsByID(MovementLinksSameTrainUnit, MoveId, moveIndex);
                        }
                        else
                        {
                            LinkMovementsByID(MovementLinksSameInfrastructure, MoveId, moveIndex);
                        }

                    }
                    // 2nd Assign current movement to the required infrastructure
                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        InfraOccupiedByMoves[DictOfInfrastructure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastructure[infraID]] = moveIndex;
                    }
                }
                // Identify all the conflicting moves related to the same train units used by the movements - and link moves

                List<int> IDListOfTrainUnitUsed = GetIDListOfTrainUnitsUsed(currentMove); // Train units used by the movement

                if (TrainUnitConflict(TrainUnitsOccupiedByMovesID, IDListOfTrainUnitUsed, ref conflictingMoveIds) == false)
                {
                    // No conflict occurred. Here the moves are not linked
                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {
                        // Assign the move to the Train Units used
                        TrainUnitsOccupiedByMovesID[ListOfTrainUnits[trainUnitID]] = moveIndex;
                    }
                }
                else
                {
                    // The conflicting moves are linked
                    foreach (int MoveId in conflictingMoveIds)
                    {
                        LinkMovementsByID(MovementLinksSameTrainUnit, MoveId, moveIndex);

                    }

                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {

                        TrainUnitsOccupiedByMovesID[ListOfTrainUnits[trainUnitID]] = moveIndex;
                    }
                }

                moveIndex++;
                if (moveIndex == listOfMoves.Count)
                {
                    ok = 0;
                    // The last movement is not linked, it contains an empty list
                    MovementLinks.Add(moveIndex - 1, new List<int>());


                    MovementLinksSameInfrastructure.Add(moveIndex - 1, new List<int>());
                    MovementLinksSameTrainUnit.Add(moveIndex - 1, new List<int>());

                }
            }


            this.POSadjacencyList = CreatePOSAdjacencyList(MovementLinks);
            this.FirstPOS = POSadjacencyList.First().Key;
            this.LastPOS = POSadjacencyList.Last().Key;

            this.POSadjacencyListForInfrastructure = CreatePOSAdjacencyList(MovementLinksSameInfrastructure);
            AddInfrastructurePredecessorSuccessorLinksToPOSMoves();
            this.POSadjacencyListForTrainUint = CreatePOSAdjacencyList(MovementLinksSameTrainUnit);


            AddTrainUnitPredecessorSuccessorLinksToPOSMoves();


            AddSuccessorsAndPredecessors();

            DisplayPOSMovementLinksInfrastructureUsed();
            DisplayPOSMovementLinksTrainUnitUsed();

            this.ListOfPOSTrackTasks = CreatePOSTrackTaskList();
            DisplayListPOSTrackTask();

            ok = 1;
            int TrackTaskIndex = 0;

            List<int> conflictingTrackTaskIds = new List<int>();

            List<POSTrackTask> listOfPOSTrackTasks = this.ListOfPOSTrackTasks;
            while (ok != 0)
            {

                var currentPOSTrackTask = listOfPOSTrackTasks[TrackTaskIndex];

                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsedByTrackTasks(currentPOSTrackTask);

                // Identify all the conflicting POSTrackTask related to the infrastructure used by the POSTrackTask - and links POSTrackTasks
                if (InfraConflictByTrackTasks(InfraOccupiedByTrackTaskID, IDListOfInfraUsed, ref conflictingTrackTaskIds) == false)
                {
                    // No conflict occurred

                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        // Assign POSTrackTask to the infrastructure occupied
                        InfraOccupiedByTrackTaskID[DictOfInfrastructure[infraID]] = TrackTaskIndex;
                    }

                }
                else
                {
                    // Contains all the POSTrackTasks that was assigned to the same POSTrackTask as the train unit of the current POSTrackTask (TrackTaskIndex)
                    // this also mean that these POSTrackTasks are conflicting because of the same train used (assigned to the POSTrackTask)
                    // and not only because of the same infrastructure used
                    List<int> trackTaskUsingSameTrainUnit = CheckIfSameTrainUintUsedByPOSTrackTask(conflictingTrackTaskIds, listOfPOSTrackTasks, TrackTaskIndex);


                    // TODO from here:
                    foreach (int trackTaskId in conflictingTrackTaskIds)
                    {


                        if (trackTaskUsingSameTrainUnit.Count != 0)
                        {
                            // This statement is used to link the POSTrackTasks conflicted because of using the same infrastructure\
                            // and not because of same train unit assigned per POSTrackTask {aka dashed line dependency}
                            if (!trackTaskUsingSameTrainUnit.Contains(trackTaskId))
                                LinkTrackTaskByID(POSTrackTaskLinksSameInfrastructure, trackTaskId, TrackTaskIndex);

                        }
                        else
                        {
                            LinkTrackTaskByID(POSTrackTaskLinksSameInfrastructure, trackTaskId, TrackTaskIndex);

                        }

                    }
                    // 2nd Assign current POSTrackTask to the required infrastructure
                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        InfraOccupiedByTrackTaskID[DictOfInfrastructure[infraID]] = TrackTaskIndex;
                    }
                }

                // Identify all the conflicting POSTrackTask related to the same train units used by the POSTrackTask - and link POSTrackTask

                List<int> IDListOfTrainUnitUsed = GetIDListOfTrainUnitUsedPOSTrackTask(currentPOSTrackTask); // Train units used by the POSTrackTask

                if (TrainUnitConflictByPOSTrackTask(TrainUnitsOccupiedByPOSTrackTaskID, IDListOfTrainUnitUsed, ref conflictingTrackTaskIds) == false)
                {
                    // No conflict occurred. Here the POSTrackTasks are not linked
                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {
                        // Assign the POSTrackTask to the Train Units used
                        TrainUnitsOccupiedByPOSTrackTaskID[ListOfTrainUnits[trainUnitID]] = TrackTaskIndex;
                    }
                }
                else
                {
                    // The conflicting POSTrackTasks are linked
                    foreach (int TrackId in conflictingTrackTaskIds)
                    {
                        LinkTrackTaskByID(POSTrackTaskLinksSameTrainUnits, TrackId, TrackTaskIndex);

                    }

                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {

                        TrainUnitsOccupiedByPOSTrackTaskID[ListOfTrainUnits[trainUnitID]] = TrackTaskIndex;
                    }
                }



                TrackTaskIndex++;
                if (TrackTaskIndex == listOfPOSTrackTasks.Count)
                {
                    ok = 0;



                }
            }

            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("|            From POSTrackTask inner Links (same Infrastructure used)              |");
            Console.WriteLine("-----------------------------------------------------------------------------------");

            foreach (KeyValuePair<int, List<int>> pair in POSTrackTaskLinksSameInfrastructure.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"POSTrackTask {pair.Key} --> ");
                foreach (int linkToPOStrackTask in pair.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Train Unit used)                |");
            Console.WriteLine("-----------------------------------------------------------------------------------");

            foreach (KeyValuePair<int, List<int>> pair in POSTrackTaskLinksSameTrainUnits.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"POSTrackTask {pair.Key} --> ");
                foreach (int linkToPOStrackTask in pair.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask} ");
                }
                Console.WriteLine();
            }

            this.POSTrackTaskadjacencyListForTrainUsed = CreatePOSAdjacencyListTrackTask(POSTrackTaskLinksSameTrainUnits);

            Console.WriteLine("-----------------------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Train Unit used) - AdjacencyList            |");
            Console.WriteLine("-----------------------------------------------------------------------------------------------");

            foreach (KeyValuePair<POSTrackTask, List<POSTrackTask>> Task in this.POSTrackTaskadjacencyListForTrainUsed)
            {
                Console.Write($"POSTrackTask {Task.Key.ID} --> ");
                foreach (POSTrackTask linkToPOStrackTask in Task.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask.ID} ");
                }
                Console.WriteLine();
            }


            this.POSTrackTaskadjacencyListForInfrastructure = CreatePOSAdjacencyListTrackTask(POSTrackTaskLinksSameInfrastructure);

            Console.WriteLine("--------------------------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Infrastructure used) - AdjacencyList            |");
            Console.WriteLine("--------------------------------------------------------------------------------------------------");

            foreach (KeyValuePair<POSTrackTask, List<POSTrackTask>> Task in this.POSTrackTaskadjacencyListForInfrastructure)
            {
                Console.Write($"POSTrackTask {Task.Key.ID} --> ");
                foreach (POSTrackTask linkToPOStrackTask in Task.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask.ID} ");
                }
                Console.WriteLine();
            }

            AddSuccessorsAndPredecessorsPOSTrackTasks();

            Console.WriteLine("----------------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("|  From POSTrackTask inner Links (same train unit used) - AdjacencyList  - AddSuccessorsAndPredecessorsPOSTrackTasks |");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------");

            foreach (POSTrackTask item in this.ListOfPOSTrackTasks)
            {
                item.displayLinksByTrainUnits();
            }
            DisplayMovesSuccessorsAndPredecessors();
            ShowAllInfoAboutMove(7);
            ShowAllInfoAboutTrackTask(17);
        }

        // Creates a list of POSTrackTasks. POSTrackTasks are created by using the TrackTasks embedded between 
        // the MoveTasks. 
        // The extraction of POSTrackTask information is done by using the dependencies (links) between the POSMoveTasks @POSadjacencyList.
        // When a successor of a POSMoveTask has the same TrackTask as predecessor TrackTask a POSTrackTask is created. POSTrackTask is also created
        // in case of arrival TrackTask.
        // The function also links the POSMoveTasks and POSTrackTasks - in several cases between two POSMoveTasks a POSTrackTasks is included (such as
        // service, parking, split, combine task). 
        // POSTrackTask is based on types: {Arrival, Departure, Parking, Service, Split, Combine}
        // Example of linking: POSMoveTask_j -> POSMoveTask_k and POSMoveTask_j -> POSMoveTask_l, and POSMoveTask_j next TrackTask is POSTrackTask_i
        // and POSMoveTask_k previous TrackTask is POSTrackTask_i, and POSMoveTask_l previous POSTrackTask is POSTrackTask_b
        // then it might be the case that:
        // POSMoveTask_j <- POSTrackTask_i -> POSMoveTask_k ,but POSMoveTask_l is not linked with POSTrackTask_i because they didn't have 
        // a common POSTrackTask
        public List<POSTrackTask> CreatePOSTrackTaskList()
        {

            Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyList;
            List<POSTrackTask> listPOSTrackTask = new List<POSTrackTask>();


            int id = 0;

            // Study all the POSMoveTask moves
            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in POSadjacencyList)
            {
                POSMoveTask POSmove = element.Key;

                MoveTask corrMoveTask = POSmove.CorrespondingMoveTask;

                // Next TrackTask(s) following the movement (MoveTask)
                IList<TrackTask> trackTaskNext = corrMoveTask.AllNext;

                // Previous TrackTask(s) preceding the movement (MoveTask)
                IList<TrackTask> trackTaskPrevious = corrMoveTask.AllPrevious;


                // Create new POSTrackTask(s) when the POSMoveTask's predecessor is an arrival task
                if (trackTaskPrevious.Count == 1)
                {
                    TrackTask previousTrackTask = trackTaskPrevious[0];
                    if (previousTrackTask.TaskType is TrackTaskType.Arrival)
                    {
                        POSTrackTask newArrival = new POSTrackTask(previousTrackTask);
                        newArrival.ID = id;
                        newArrival.nextMoves.Add(POSmove);
                        newArrival.TaskType = POSTrackTaskType.Arrival;
                        listPOSTrackTask.Add(newArrival);

                        POSmove.PredecessorTrackTasks.Add(newArrival);

                        id++;

                    }
                }

                // When the TrackTask is not an arrival

                foreach (TrackTask nextTrackTask in trackTaskNext)
                {
                    POSTrackTask newTrackTask = new POSTrackTask(nextTrackTask);
                    newTrackTask.ID = id;
                    // More than 1 successor task means that a train unit was spited
                    if (trackTaskNext.Count > 1)
                    {
                        newTrackTask.setPOSTrackTaskType(POSTrackTaskType.Split);
                        newTrackTask.TaskType = POSTrackTaskType.Split;
                    }

                    POSmove.SuccessorTrackTasks.Add(newTrackTask);
                    newTrackTask.previousMoves.Add(POSmove);

                    // Check for dependencies when same train unit used - successors
                    // if the current POSMoveTask successor's previous TrackTask matches
                    // the next TrackTask of the POSMoveTask successor, then link POSTackTask
                    // with the successor POSMoveTask 
                    foreach (POSMoveTask successor in POSmove.SuccessorMovesByTrainUnits)
                    {

                        MoveTask corrSuccessorMoveTask = successor.CorrespondingMoveTask;
                        IList<TrackTask> previousTrackTasksOfSuccessor = corrSuccessorMoveTask.AllPrevious;

                        foreach (TrackTask task in previousTrackTasksOfSuccessor)
                        {
                            string nextTrackTaskCharacteristics = $"{nextTrackTask.Start} - {nextTrackTask.End} : {nextTrackTask.Train} at {nextTrackTask.Track.ID}";

                            string trackTrackTaskCharacteristics = $"{task.Start} - {task.End} : {task.Train} at {task.Track.ID}";

                            if (nextTrackTaskCharacteristics == trackTrackTaskCharacteristics)
                            {
                                POSMoveTask explicitSuccessor = GetPOSMoveTaskByID(successor.ID, POSadjacencyList);

                                explicitSuccessor.PredecessorTrackTasks.Add(newTrackTask);

                                // More than 1 pedeccessor task means that the train units were combined
                                if (previousTrackTasksOfSuccessor.Count > 1)
                                    newTrackTask.setPOSTrackTaskType(POSTrackTaskType.Combine);
                                newTrackTask.nextMoves.Add(explicitSuccessor);

                            }
                        }

                    }
                    // Check for dependencies when  same infrastructure used - successors

                    foreach (POSMoveTask successor in POSmove.SuccessorMovesByInfrastructure)
                    {

                        MoveTask corrSuccessorMoveTask = successor.CorrespondingMoveTask;
                        IList<TrackTask> previousTrackTaskOfSuccessor = corrSuccessorMoveTask.AllPrevious;

                        foreach (TrackTask task in previousTrackTaskOfSuccessor)
                        {
                            string nextTrackTaskCharacteristics = $"{nextTrackTask.Start} - {nextTrackTask.End} : {nextTrackTask.Train} at {nextTrackTask.Track.ID}";

                            string trackTrackTaskCharacteristics = $"{task.Start} - {task.End} : {task.Train} at {task.Track.ID}";

                            if (nextTrackTaskCharacteristics == trackTrackTaskCharacteristics)
                            {
                                POSMoveTask explicitSuccessor = GetPOSMoveTaskByID(successor.ID, POSadjacencyList);

                                explicitSuccessor.PredecessorTrackTasks.Add(newTrackTask);

                                // More than 1 pedeccessor task means that the train units were
                                if (previousTrackTaskOfSuccessor.Count > 1)
                                    newTrackTask.setPOSTrackTaskType(POSTrackTaskType.Combine);
                                newTrackTask.nextMoves.Add(explicitSuccessor);


                            }
                        }

                    }


                    listPOSTrackTask.Add(newTrackTask);
                    id++;

                }

            }


            return listPOSTrackTask;

        }

        // Displays the all POSTrackTask list identified in the POS solution
        public void DisplayListPOSTrackTask()
        {
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine("|            From POS TrackTask Links with POSMoves              |");
            Console.WriteLine("-----------------------------------------------------------------");

            List<POSTrackTask> listPOSTrackTask = this.ListOfPOSTrackTasks;

            foreach (POSTrackTask trackTask in listPOSTrackTask)
            {
                Console.WriteLine($"{trackTask}");
            }
        }
        public void LinkPOSMovesWithPOSTrackTasks()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyList;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in POSadjacencyList)
            {
                POSMoveTask POSmove = element.Key;

                List<POSMoveTask> SuccessorsByTrainUnit = POSmove.SuccessorMovesByTrainUnits;
                List<POSMoveTask> PredecessorsByTrainUnit = POSmove.PredecessorMovesByTrainUnits;

                List<POSMoveTask> SuccessorsByInfrastructure = POSmove.SuccessorMovesByInfrastructure;
                List<POSMoveTask> PredecessorsByInfrastructure = POSmove.PredecessorMovesByInfrastructure;



            }
        }


        public void AddSuccessorsAndPredecessors()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyList;
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForInfrastructure = this.POSadjacencyListForInfrastructure;
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForTrainUint = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in posAdjacencyList)
            {
                POSMoveTask POSmove = element.Key;
                foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> elementInfra in posAdjacencyListForInfrastructure)
                {
                    POSMoveTask POSmoveInfra = elementInfra.Key;

                    if (POSmove.ID == POSmoveInfra.ID)
                    {
                        List<POSMoveTask> Successors = elementInfra.Value;
                        foreach (POSMoveTask successor in Successors)
                        {
                            POSmove.AddNewSuccessorByInfrastructure(successor);
                        }

                        List<POSMoveTask> Predecessors = GetMovePredecessors(POSmoveInfra, posAdjacencyListForInfrastructure);
                        foreach (POSMoveTask predecessor in Predecessors)
                        {
                            POSmove.AddNewPredecessorByInfrastructure(predecessor);
                        }
                    }
                }

                foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> elementTrainUnit in posAdjacencyListForTrainUint)
                {
                    POSMoveTask POSmoveTrainUint = elementTrainUnit.Key;

                    if (POSmove.ID == POSmoveTrainUint.ID)
                    {
                        List<POSMoveTask> Successors = elementTrainUnit.Value;
                        foreach (POSMoveTask successor in Successors)
                        {
                            POSmove.AddNewSuccessorByTrainUnits(successor);
                        }

                        List<POSMoveTask> Predecessors = GetMovePredecessors(POSmoveTrainUint, posAdjacencyListForTrainUint);

                        foreach (POSMoveTask predecessor in Predecessors)
                        {
                            POSmove.AddNewPredecessorByTrainUnits(predecessor);

                        }

                    }
                }



            }
        }


        public void AddSuccessorsAndPredecessorsPOSTrackTasks()
        {

            List<POSTrackTask> listOfPOSTrackTasks = this.ListOfPOSTrackTasks;

            Dictionary<POSTrackTask, List<POSTrackTask>> posAdjacencyListForInfrastructure = this.POSTrackTaskadjacencyListForInfrastructure;
            Dictionary<POSTrackTask, List<POSTrackTask>> posAdjacencyListForTrainUint = this.POSTrackTaskadjacencyListForTrainUsed;

            foreach (POSTrackTask POStrackTask in listOfPOSTrackTasks)
            {

                foreach (KeyValuePair<POSTrackTask, List<POSTrackTask>> elementInfra in posAdjacencyListForInfrastructure)
                {
                    POSTrackTask POStrackTaskInfra = elementInfra.Key;

                    if (POStrackTask.ID == POStrackTaskInfra.ID)
                    {
                        List<POSTrackTask> Successors = elementInfra.Value;
                        foreach (POSTrackTask successor in Successors)
                        {
                            POStrackTask.AddNewSuccessorByInfrastructure(successor);
                        }

                        List<POSTrackTask> Predecessors = GetTrackTaskPredecessors(POStrackTaskInfra, posAdjacencyListForInfrastructure);
                        foreach (POSTrackTask predecessor in Predecessors)
                        {
                            POStrackTask.AddNewPredecessorByInfrastructure(predecessor);
                        }
                    }
                }

                foreach (KeyValuePair<POSTrackTask, List<POSTrackTask>> elementTrainUnit in posAdjacencyListForTrainUint)
                {
                    POSTrackTask POStrackTaskTrainUint = elementTrainUnit.Key;

                    if (POStrackTask.ID == POStrackTaskTrainUint.ID)
                    {
                        List<POSTrackTask> Successors = elementTrainUnit.Value;
                        foreach (POSTrackTask successor in Successors)
                        {
                            POStrackTask.AddNewSuccessorByTrainUnits(successor);
                        }

                        List<POSTrackTask> Predecessors = GetTrackTaskPredecessors(POStrackTaskTrainUint, posAdjacencyListForTrainUint);

                        foreach (POSTrackTask predecessor in Predecessors)
                        {
                            POStrackTask.AddNewPredecessorByTrainUnits(predecessor);

                        }

                    }
                }



            }
        }
        // Takes all the POSMove linked when using the same infrastructure - POSadjacencyListForInfrastructure 
        // and assigns the POSMove successors and Predecessors to the POSMove taken. This function is very useful, since it adds new link information to the POSMoves used
        // in the Partial Order Schedule graph
        public void AddInfrastructurePredecessorSuccessorLinksToPOSMoves()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForInfrastructure = this.POSadjacencyListForInfrastructure;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in posAdjacencyListForInfrastructure)
            {
                // Add successors to each POSMoves contained by the adjacency list
                POSMoveTask POSmove = element.Key;
                List<POSMoveTask> Successors = element.Value;

                foreach (POSMoveTask successor in Successors)
                {
                    POSmove.AddNewSuccessorByInfrastructure(successor);
                }

                // Add Predecessors to each POSMoves contained by the adjacency list
                List<POSMoveTask> Predecessors = GetMovePredecessors(POSmove, posAdjacencyListForInfrastructure);

                foreach (POSMoveTask predecessor in Predecessors)
                {
                    POSmove.AddNewPredecessorByInfrastructure(predecessor);
                }

            }
        }


        // Takes all the POSMove linked when using the same train unit - POSadjacencyListForTrainUint and assigns the POSMove
        // successors and Predecessors to the POSMove taken. This function is very useful, since it adds new link information to the POSMoves used
        // in the Partial Order Schedule graph
        public void AddTrainUnitPredecessorSuccessorLinksToPOSMoves()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForTrainUint = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in posAdjacencyListForTrainUint)
            {
                // Add successors to each POSMoves contained by the adjacency list
                POSMoveTask POSmove = element.Key;
                List<POSMoveTask> Successors = element.Value;

                foreach (POSMoveTask successor in Successors)
                {
                    POSmove.AddNewSuccessorByTrainUnits(successor);
                }


                // Add Predecessors to each POSMoves contained by the adjacency list
                List<POSMoveTask> Predecessors = GetMovePredecessors(POSmove, posAdjacencyListForTrainUint);

                foreach (POSMoveTask predecessor in Predecessors)
                {
                    POSmove.AddNewPredecessorByTrainUnits(predecessor);
                }

            }

        }

        public void DisplayMovesSuccessorsAndPredecessors()
        {
            Console.WriteLine("---------------------------------------------------");
            Console.WriteLine("|            From POS Movement Links               |");
            Console.WriteLine("---------------------------------------------------");

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in this.POSadjacencyList)
            {
                Console.WriteLine(element.Key);
            }
        }


        // Displays all the POSMove Predecessors and successors - these links are represents the 
        // relations between the moves using the same train unit
        public void DisplayTrainUnitSuccessorsAndPredecessors()
        {
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine("|       POS Movement Predecessors and successors - TrainUnit (solid arcs) |");
            Console.WriteLine("--------------------------------------------------------------------------");
            // Show connections per Move
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"Move{pair.Key.ID} --> \n");
                Console.WriteLine("Predecessors:");
                foreach (POSMoveTask element in pair.Key.PredecessorMovesByTrainUnits)
                {
                    Console.Write($"Move:{element.ID} ");

                }
                Console.Write("\n");
                Console.WriteLine("Successors:");
                foreach (POSMoveTask element in pair.Key.SuccessorMovesByTrainUnits)
                {
                    Console.Write($"Move:{element.ID} ");

                }
                Console.Write("\n");

            }

        }


        public POSMoveTask GetPOSMoveTaskByID(int ID, Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList)
        {

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in POSadjacencyList)
            {
                if (pair.Key.ID == ID)
                    return pair.Key;
            }
            throw new KeyNotFoundException($"The move '{ID}' was not found in the POSadjacencyList.");
        }

        public List<POSMoveTask> GetMovePredecessors(POSMoveTask POSmove, Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList)
        {
            List<POSMoveTask> PredecessorsOfPOSMove = new List<POSMoveTask>();

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in POSadjacencyList)
            {
                foreach (POSMoveTask move in pair.Value)
                {
                    if (move.ID == POSmove.ID)
                        PredecessorsOfPOSMove.Add(pair.Key);
                }

            }

            if (PredecessorsOfPOSMove.Count > 1)
            {
                return PredecessorsOfPOSMove.OrderBy(element => element.ID).ToList();
            }
            return PredecessorsOfPOSMove;
        }


        public List<POSTrackTask> GetTrackTaskPredecessors(POSTrackTask POStrackTask, Dictionary<POSTrackTask, List<POSTrackTask>> POSadjacencyList)
        {
            List<POSTrackTask> PredecessorsOfPOStrackTask = new List<POSTrackTask>();

            foreach (KeyValuePair<POSTrackTask, List<POSTrackTask>> pair in POSadjacencyList)
            {
                foreach (POSTrackTask trackTask in pair.Value)
                {
                    if (trackTask.ID == POStrackTask.ID)
                        PredecessorsOfPOStrackTask.Add(pair.Key);
                }

            }

            if (PredecessorsOfPOStrackTask.Count > 1)
            {
                return PredecessorsOfPOStrackTask.OrderBy(element => element.ID).ToList();
            }
            return PredecessorsOfPOStrackTask;
        }


         public void ShowAllInfoAboutTrackTask(int trackTaskID)
        {
            Console.WriteLine("-----------------------------------------------------------------------");
            Console.WriteLine($"|         All information about- track task id : {trackTaskID}        |");
            Console.WriteLine("-----------------------------------------------------------------------");
            foreach (POSTrackTask item in this.ListOfPOSTrackTasks)
            {
                if(item.ID == trackTaskID)
                {
                    Console.Write(item);
                    Console.Write(item.GetInfoLinksByTrainUnits());
                    Console.Write(item.GetInfoLinksByInfrastructure());
                }
            }
        }

        public void ShowAllInfoAboutMove(int moveID)
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine($"|         All information about- move id : {moveID}        |");
            Console.WriteLine("------------------------------------------------------------");
            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> item in this.POSadjacencyList)
            {
                if(item.Key.ID == moveID)
                    Console.Write(item.Key);
            }
        }
        // Displays all the direct successors and predecessors of a given POS move
        // the move is identified by its ID (POSMoveTask POSmove.ID)
        // @linkType specifies the type of the links 'infrastructure' - same infrastructure used - populated from @POSadjacencyListForInfrastructure
        // 'trainUint' - same train unit(s) used - populated from @POSadjacencyListForTrainUint
        public void DisplayMoveLinksOfPOSMove(int POSId, string linkType)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine($"|         POS Movement Links - move id : {POSId}        |");
            Console.WriteLine("----------------------------------------------------------");

            if (linkType == "infrastructure")
            {
                Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyListForInfrastructure;

                try
                {
                    POSMoveTask POSmove = GetPOSMoveTaskByID(POSId, POSadjacencyList);

                    List<POSMoveTask> successorPOSMoves = POSadjacencyList[POSmove];

                    List<POSMoveTask> predecessorsPOSMoves = GetMovePredecessors(POSmove, POSadjacencyList);

                    Console.WriteLine("|  Direct Successors |");
                    Console.Write("[ ");

                    foreach (POSMoveTask move in successorPOSMoves)
                    {
                        Console.Write($"Move {move.ID}, ");
                    }
                    Console.WriteLine(" ]");

                    Console.WriteLine("|  Direct Predecessors |");
                    Console.Write("[ ");

                    foreach (POSMoveTask move in predecessorsPOSMoves)
                    {
                        Console.Write($"Move {move.ID}, ");
                    }
                    Console.WriteLine(" ]");

                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine(ex.Message);
                }




            }
            else if (linkType == "trainUint")
            {
                Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyListForTrainUint;

                try
                {
                    POSMoveTask POSmove = GetPOSMoveTaskByID(POSId, POSadjacencyList);

                    List<POSMoveTask> successorPOSMoves = POSadjacencyList[POSmove];

                    List<POSMoveTask> predecessorsPOSMoves = GetMovePredecessors(POSmove, POSadjacencyList);

                    Console.WriteLine("|  Direct Successors |");

                    Console.Write("[ ");
                    foreach (POSMoveTask move in successorPOSMoves)
                    {
                        Console.Write($"Move {move.ID}, ");
                    }
                    Console.WriteLine(" ]");

                    Console.WriteLine("|  Direct Predecessors |");
                    Console.Write("[ ");

                    foreach (POSMoveTask move in predecessorsPOSMoves)
                    {
                        Console.Write($"Move {move.ID}, ");
                    }
                    Console.WriteLine(" ]");

                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine(ex.Message);
                }


            }
            else
            {
                Console.WriteLine("--- Unknown linkType for GetLinksOfPOSMove() ---");
            }

        }


        // Get all the direct successors and predecessors of a given POS move, the move is identified by its ID (POSMoveTask POSmove.ID)
        // Successors stored in @successorPOSMoves; Predecessors stored in @predecessorsPOSMoves
        // @linkType specifies the type of the links 'infrastructure' - same infrastructure used - populated from @POSadjacencyListForInfrastructure
        // 'trainUint' - same train unit(s) used - populated from @POSadjacencyListForTrainUint
        public void GetMoveLinksOfPOSMove(int POSId, string linkType, out List<POSMoveTask> successorPOSMoves, out List<POSMoveTask> predecessorsPOSMoves)
        {
            successorPOSMoves = new List<POSMoveTask>();
            predecessorsPOSMoves = new List<POSMoveTask>();

            if (linkType == "infrastructure")
            {
                Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyListForInfrastructure;

                try
                {

                    POSMoveTask POSmove = GetPOSMoveTaskByID(POSId, POSadjacencyList);

                    successorPOSMoves.AddRange(POSadjacencyList[POSmove]);

                    predecessorsPOSMoves.AddRange(GetMovePredecessors(POSmove, POSadjacencyList));


                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine(ex.Message);
                }


            }
            else if (linkType == "trainUint")
            {
                Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyListForTrainUint;

                try
                {
                    POSMoveTask POSmove = GetPOSMoveTaskByID(POSId, POSadjacencyList);

                    successorPOSMoves.AddRange(POSadjacencyList[POSmove]);

                    predecessorsPOSMoves.AddRange(GetMovePredecessors(POSmove, POSadjacencyList));

                }
                catch (KeyNotFoundException ex)
                {
                    Console.WriteLine(ex.Message);
                }


            }
            else
            {
                Console.WriteLine("--- Unknown linkType for GetLinksOfPOSMove() ---");
            }
        }

        // Shows train unit relations between the POS movements, meaning that
        // links per move using the same train unit are displayed - links by train unit
        public void DisplayPOSMovementLinksTrainUnitUsed()
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("|      POS Movement Links - Train Unit (solid arcs)     |");
            Console.WriteLine("----------------------------------------------------------");

            // Show connections per Move
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"Move{pair.Key.ID} --> ");
                foreach (POSMoveTask element in pair.Value)
                {
                    Console.Write($"Move:{element.ID} ");

                }
                Console.Write("\n");
            }
        }

        // Shows infrastructure relations between the POS movements, meaning that
        // links per move using the same infrastructure are displayed - links by infrastructure
        public void DisplayPOSMovementLinksInfrastructureUsed()
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("|   POS Movement Links - Infrastructure (dashed arcs)   |");
            Console.WriteLine("----------------------------------------------------------");
            // Show connections per Move
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyListForInfrastructure;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"Move{pair.Key.ID} --> ");
                foreach (POSMoveTask element in pair.Value)
                {
                    Console.Write($"Move:{element.ID} ");

                }
                Console.Write("\n");
            }
        }

        public List<int> CheckIfSameTrainUintUsed(List<int> conflictingMoveIds, List<MoveTask> listOfMoves, int moveIndex)
        {

            List<int> movesUsingSameTrainUnit = new List<int>();
            // moveIndex is the ID of the current move that has conflicting moves: conflictingMoveIds
            // since they use the same infrastructure than the current move
            MoveTask currentMove = listOfMoves[moveIndex];

            foreach (int moveInConflictID in conflictingMoveIds)
            {
                MoveTask moveInConflict = listOfMoves[moveInConflictID];

                // Train Units used in the move in conflict
                List<ShuntTrainUnit> trainUnitsOfConflictingMove = moveInConflict.Train.Units;

                List<ShuntTrainUnit> trainUnitsOfCurrentMove = currentMove.Train.Units;

                foreach (ShuntTrainUnit shuntTrainOfConflictingMove in trainUnitsOfConflictingMove)
                {
                    foreach (ShuntTrainUnit shuntTrainOfCurrentMove in trainUnitsOfCurrentMove)
                    {
                        // if the one of the train units are the same, that means that there is a conflict in using
                        // the same train unit between the conflicting moves by the infrastructure used
                        if (shuntTrainOfConflictingMove.Index == shuntTrainOfCurrentMove.Index)
                        {
                            movesUsingSameTrainUnit.Add(moveInConflictID);
                        }
                    }
                }


            }


            if (movesUsingSameTrainUnit.Count != 0)
            {
                // First the repeating IDs are removed
                return movesUsingSameTrainUnit.Distinct().ToList();
            }
            else
            {
                return movesUsingSameTrainUnit;
            }
        }


        public List<int> CheckIfSameTrainUintUsedByPOSTrackTask(List<int> conflictingTrackTaskIds, List<POSTrackTask> listOfPOSTrackTasks, int TrackTaskIndex)
        {

            List<int> trackTasksUsingSameTrainUnit = new List<int>();
            // TrackTaskIndex is the ID of the current POSTrackTask that has conflicting POSTrackTask: conflictingTrackTaskIds
            // since they use the same infrastructure than the current POSTrackTask
            POSTrackTask currentTrackTask = listOfPOSTrackTasks[TrackTaskIndex];

            foreach (int trackTaskInConflictID in conflictingTrackTaskIds)
            {
                POSTrackTask taskInConflict = listOfPOSTrackTasks[trackTaskInConflictID];

                // Train Units used in the track task in conflict
                List<ShuntTrainUnit> trainUnitsOfConflictingTrackTask = taskInConflict.Train.Units;

                List<ShuntTrainUnit> trainUnitsOfCurrentTrackTask = currentTrackTask.Train.Units;

                foreach (ShuntTrainUnit shuntTrainOfConflictingTrackTask in trainUnitsOfConflictingTrackTask)
                {
                    foreach (ShuntTrainUnit shuntTrainOfCurrentTrackTask in trainUnitsOfCurrentTrackTask)
                    {
                        // if the one of the train units are the same, that means that there is a conflict in using
                        // the same train unit between the conflicting track task by the same infrastructure used
                        if (shuntTrainOfConflictingTrackTask.Index == shuntTrainOfCurrentTrackTask.Index)
                        {
                            trackTasksUsingSameTrainUnit.Add(trackTaskInConflictID);
                        }
                    }
                }


            }


            if (trackTasksUsingSameTrainUnit.Count != 0)
            {
                // First the repeating IDs are removed
                return trackTasksUsingSameTrainUnit.Distinct().ToList();
            }
            else
            {
                return trackTasksUsingSameTrainUnit;
            }
        }


        // Shows all the relations between the POS movements, meaning that
        // all kind of links per move are displayed - links by infrastructure
        // links by same train unit used
        public void DisplayAllPOSMovementLinks()
        {
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("|          All POS Movement Links          |");
            Console.WriteLine("--------------------------------------------");

            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyList;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList)
            {
                Console.Write($"Move{pair.Key.ID} --> ");
                foreach (POSMoveTask element in pair.Value)
                {
                    Console.Write($"Move:{element.ID} ");

                }
                Console.Write("\n");
            }

        }


        // POS Adjacency list is used to track the links between the movement nodes
        // of the POS graph. The POS Adjacency list is actually a dictionary
        // => {POSMove : List[POSMove, ...]}
        public Dictionary<POSMoveTask, List<POSMoveTask>> CreatePOSAdjacencyList(Dictionary<int, List<int>> MovementLinks)
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = new Dictionary<POSMoveTask, List<POSMoveTask>>();

            List<MoveTask> listOfMoves = this.ListOfMoves;

            // Order Dictionary
            var orderedMovementLinks = MovementLinks.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            List<POSMoveTask> POSMoveList = new List<POSMoveTask>();


            int id = 0;
            foreach (MoveTask moveTask in listOfMoves)
            {

                POSMoveTask POSmove = new POSMoveTask(moveTask, id);
                POSMoveList.Add(POSmove);
                posAdjacencyList[POSmove] = new List<POSMoveTask>();
                id++;
            }

            foreach (KeyValuePair<int, List<int>> pair in orderedMovementLinks)
            {
                // Console.Write($"Move{pair.Key} -->");
                POSMoveTask POSmove = POSMoveList[pair.Key];

                posAdjacencyList[POSmove] = new List<POSMoveTask>();
                foreach (int linkedMoveID in pair.Value)
                {

                    posAdjacencyList[POSmove].Add(POSMoveList[linkedMoveID]);
                }
            }

            return posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value); ;


        }


        // POS Adjacency list is used to track the links between the POSTrackTask nodes
        // of the POS graph. The POS Adjacency list is actually a dictionary
        // => {POSTrackTask : List[POSTrackTask, ...]}
        public Dictionary<POSTrackTask, List<POSTrackTask>> CreatePOSAdjacencyListTrackTask(Dictionary<int, List<int>> POSTrackTaskLinks)
        {
            Dictionary<POSTrackTask, List<POSTrackTask>> posAdjacencyList = new Dictionary<POSTrackTask, List<POSTrackTask>>();

            List<POSTrackTask> listOfPOSTrackTasks = this.ListOfPOSTrackTasks;

            // Order Dictionary
            var orderedPOStrackTaskLinks = POSTrackTaskLinks.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);



            foreach (POSTrackTask trackTask in listOfPOSTrackTasks)
            {
                posAdjacencyList[trackTask] = new List<POSTrackTask>();
            }

            foreach (KeyValuePair<int, List<int>> pair in orderedPOStrackTaskLinks)
            {
                foreach (int linkedTrackTaskID in pair.Value)
                {

                    posAdjacencyList[listOfPOSTrackTasks[pair.Key]].Add(listOfPOSTrackTasks[linkedTrackTaskID]);

                }
            }



            return posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value); ;


        }

        public void DisplayPartialResults(Dictionary<int, List<int>> MovementLinks)
        {
            foreach (KeyValuePair<int, List<int>> pair in MovementLinks)
            {

                if (pair.Value.Count != 0)
                {
                    Console.Write($" Move:{pair.Key} -->");

                    if (pair.Value.Count == 1)
                    {
                        Console.Write($" Move:{pair.Value[0]}");
                    }
                    else
                    {
                        foreach (int id in pair.Value)
                        {
                            if (pair.Value.Last() == pair.Value.Count - 1)
                            {
                                Console.Write($" Move:{id}");
                            }
                            else
                            {
                                Console.Write($" Move:{id} -->");

                            }

                        }
                    }


                }
                else
                {
                    Console.Write($"Move:{pair.Key}");


                }
                Console.Write("\n");

            }
        }
        public void InitializePOS()
        {
            MoveTask move = this.First;

            List<MoveTask> listOfMoves = new List<MoveTask>();

            while (move != null)
            {
                MoveTask move_clone = move;
                listOfMoves.Add(move_clone);

                move = move.NextMove;
            }
            this.ListOfMoves = listOfMoves;
            this.First = listOfMoves.First();

            this.DictOfInfrastructure = GetInfrastructure();

            this.ListOfTrainUnits = GetTrainFleet();

        }
        public void DisplayInfrastructure()
        {
            Console.WriteLine("-------------------------------");

            Console.WriteLine("------Main Infrastructure------");

            Console.WriteLine("-------------------------------");

            Dictionary<ulong, Infrastructure> infrastructuremap = GetInfrastructure();

            foreach (KeyValuePair<ulong, Infrastructure> infra in infrastructuremap)
            {
                Console.WriteLine($"id: {infra.Key} : Infrastructure {infra.Value}");

            }


            Console.WriteLine("-------------------------------");


        }

        // Shows rich information about the movements and infrastructure used in the Totally Ordered Solution
        public void DisplayMovements()
        {
            MoveTask move = this.First;
            int i = 0;
            while (move != null)
            {
                Console.WriteLine($"Move: {i} --- {move.TaskType}");
                Console.WriteLine($"From : {move.FromTrack} -> To : {move.ToTrack} ({move.Train})");

                var routing = move as RoutingTask;

                if (routing != null)
                {
                    Console.WriteLine("Infrastructure used (tracks):");
                    var tracks = routing.Route.Tracks;
                    var lastTrack = tracks.Last();

                    Console.WriteLine("All Previous tasks:");
                    foreach (TrackTask task in routing.AllPrevious)
                    {
                        Console.WriteLine($"---{task}----");
                    }

                    Console.WriteLine("All Next tasks:");
                    foreach (TrackTask task in routing.AllNext)
                    {
                        Console.WriteLine($"---{task}----");
                    }

                    foreach (Track track in tracks)
                    {
                        if (track != lastTrack)
                        {
                            Console.Write($" A side {track.ASide} -->");
                            Console.Write($" {track} --> ");
                            Console.Write($" B side {track.BSide} -->");
                        }
                        else
                        {
                            Console.Write($" A side {track.ASide} -->");
                            Console.Write($" {track} -->");

                            Console.Write($" B side {track.BSide} ");
                        }

                    }
                    Console.WriteLine("");

                }
                else
                {
                    if (move.TaskType is MoveTaskType.Departure)
                    {
                        Console.WriteLine("All Previous tasks:");
                        foreach (TrackTask task in move.AllPrevious)
                        {
                            Console.WriteLine($"---{task}----");
                        }

                        Console.WriteLine("All Next tasks:");
                        foreach (TrackTask task in move.AllNext)
                        {
                            Console.WriteLine($"---{task}----");
                        }



                        var routingDeparture = move as DepartureRoutingTask;
                        if (routingDeparture != null)
                        {
                            var listOfRoutes = routingDeparture.GetRoutes();

                            Console.WriteLine($"Infrastructure used (tracks) number of routes {listOfRoutes.Count}:");

                            foreach (Route route in listOfRoutes)
                            {
                                var Tracks = route.Tracks;
                                var lastTrack = Tracks.Last();

                                foreach (Track track in Tracks)
                                {

                                    if (track != lastTrack)
                                    {
                                        Console.Write($" A side {track.ASide} -->");
                                        Console.Write($" {track} --> ");
                                        Console.Write($" B side {track.BSide} -->");
                                    }
                                    else
                                    {
                                        Console.Write($" A side {track.ASide} -->");
                                        Console.Write($" {track} -->");

                                        Console.Write($" B side {track.BSide} ");
                                    }


                                }
                                Console.WriteLine("");
                            }
                            Console.WriteLine("");
                        }
                    }
                }
                i++;
                move = move.NextMove;
            }
        }
    }
}