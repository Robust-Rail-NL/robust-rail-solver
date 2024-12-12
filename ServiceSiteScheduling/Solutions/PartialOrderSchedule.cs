using ServiceSiteScheduling.Parking;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using ServiceSiteScheduling.TrackParts;
using Google.Protobuf;
using System.ComponentModel.DataAnnotations;
using System.Transactions;
using System.Runtime.CompilerServices;
using ServiceSiteScheduling.Servicing;
using System.Xml;
using System.Security;
using YamlDotNet.Core.Tokens;
using System.Linq.Expressions;
using AlgoIface;
using System.Net.Http.Metrics;
using System.Security.Cryptography.X509Certificates;


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
        public Dictionary<POSTrackTask, List<POSTrackTask>> POSTrackTaskadjacencyListForTrainUsed {get; set;}

        // This is the Adjacency List for POS TrackTask using the same Infrastructure: Each POSTrackTask maps to a list of connected POSTrackTask
        // (dotted arcs links)
        public Dictionary<POSTrackTask, List<POSTrackTask>> POSTrackTaskadjacencyListForInfrastructure {get; set;}



        // First movement of the POS
        public POSMoveTask FirstPOS { get; set; }

        // Last movement of the POS
        public POSMoveTask LastPOS { get; set; }


        // Reated to Total Ordered Solution
        public MoveTask First { get; set; }
        public MoveTask Last { get; set; }

        // List of moves got from the Totaly Ordered Solution
        // the moves already contain the relations with the tasks
        // this list should be initiated only once and the order of moves should not be changed
        public List<MoveTask> ListOfMoves { get; set; }

        public List<POSTrackTask> ListOfPOSTrackTasks { get; set; }

        // Dictionary that contains the overall Infrastructure used in the scenario
        public Dictionary<ulong, Infrastructure> DictOfInrastructure { get; set; }
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

        // Get the infrastrucure describing the shunting yard
        // this information is obtained from the ProblemInstance, created from the `location.data` file
        public Dictionary<ulong, Infrastructure> GetInfrasturcture()
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
                        // tracks.Add(track);
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
                        // gateways.Add(gateway);
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

            foreach(ShuntTrainUnit trainUnit in posTrackTask.Train.Units)
            {
                trainUnits.Add(trainUnit.Index);
            }

            return trainUnits.Distinct().ToList();
        }

        // Returns list of the IDs of the Infrastructure used by a POSTrackTask (POSTrackTask)

        public List<ulong> GetIDListOfInfraUsedByTrackTasks(POSTrackTask posTrackTask)
        {
            List<ulong> IDListOfInstraUsed = [
                posTrackTask.Track.ASide.ID,
                posTrackTask.Track.ID,
                posTrackTask.Track.BSide.ID,
            ];

            return IDListOfInstraUsed;
        }


        // Returns list of the IDs of the Infrastructure used by a movement (MoveTask)
        public List<ulong> GetIDListOfInfraUsed(MoveTask move)
        {
            List<ulong> IDListOfInstraUsed = new List<ulong>();

            var routing = move as RoutingTask;

            if (routing != null)
            {
                // Standard MoveTask
                var tracks = routing.Route.Tracks;
                var lastTrack = tracks.Last();

                foreach (Track track in tracks)
                {

                    IDListOfInstraUsed.Add(track.ASide.ID);
                    IDListOfInstraUsed.Add(track.ID);
                    IDListOfInstraUsed.Add(track.BSide.ID);

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
                        int numbrerOfInfraUsed = 0;
                        int k = 0;
                        foreach (Route route in listOfRoutes)
                        {
                            var tracks = route.Tracks;
                            var lastTrack = tracks.Last();

                            List<ulong> IDListOfInstraUsedIntermediate = new List<ulong>();

                            foreach (Track track in tracks)
                            {

                                IDListOfInstraUsedIntermediate.Add(track.ASide.ID);
                                IDListOfInstraUsedIntermediate.Add(track.ID);
                                IDListOfInstraUsedIntermediate.Add(track.BSide.ID);

                            }
                            // TODO: review this, probably a different logic should be used her
                            // maybe calculate the distance for each route ?
                            if (k == 0)
                            {
                                numbrerOfInfraUsed = IDListOfInstraUsedIntermediate.Count;
                                foreach (ulong trackID in IDListOfInstraUsedIntermediate)
                                    IDListOfInstraUsed.Add(trackID);

                            }
                            else
                            {
                                if (IDListOfInstraUsedIntermediate.Count < numbrerOfInfraUsed)
                                {
                                    // Less infrasturcure used
                                    numbrerOfInfraUsed = IDListOfInstraUsedIntermediate.Count;
                                    IDListOfInstraUsed = new List<ulong>();

                                    foreach (ulong trackID in IDListOfInstraUsedIntermediate)
                                        IDListOfInstraUsed.Add(trackID);
                                }

                            }
                            k++;

                        }


                    }
                }
            }

            return IDListOfInstraUsed;
        }

        // Returns true if the same infrastrucure is used by previous moves as the requireied in @IDListOfInfraUsed
        // @IDListOfInfraUsed contains the infrastructure the current move will use
        // @InfraOccupiedByMovesID is a dictionary (Key:Value) contains all the infrastructures (Key) and
        // their occupation by a specific move (Value) - note: the moves are specified by their IDs
        // @conflictingMoveIds contains all the conflicting moves, that use the same infrastrucure the current move requires to occupy
        public bool InfraConflict(Dictionary<Infrastructure, int> InfraOccupiedByMovesID, List<ulong> IDListOfInfraUsed, int moveID, ref List<int> conflictingMoveIds)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            conflictingMoveIds = new List<int>();
            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] == 999)
                {
                    // conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                }
                else
                {
                    if (conflictingMoveIds.Contains(InfraOccupiedByMovesID[DictOfInfrastrucure[id]]) == false)
                        conflictingMoveIds.Add(InfraOccupiedByMovesID[DictOfInfrastrucure[id]]);
                }


            }
            if (conflictingMoveIds.Count != 0)
                return true;

            return false;
        }

        // Returns true if the same train unit is used by previous moves as the requireied in @IDListOfTrainUnitUsed
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


        // Returns true if the same train unit is used by previous POSTrackTask as the requireied in @IDListOfTrainUnitUsed
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
        public void LinkMovmentsByID(Dictionary<int, List<int>> MovementLinks, int parentMovementID, int childMovementID)
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



        public void MergeMovements(Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves, Dictionary<Infrastructure, int> InfraOccupiedByMovesID, Dictionary<ulong, Infrastructure> DictOfInfrastrucure, int currentID)
        {
            List<MoveTask> listOfMoves = this.ListOfMoves;

            var currentMove = listOfMoves[currentID];
            List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove);

            foreach (ulong infraID in IDListOfInfraUsed)
            {
                InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = currentID;
            }
        }

        // Returns true if the same infrastrucure is used by previous POSTrackTask as the requireied in @IDListOfInfraUsed
        // @IDListOfInfraUsed contains the infrastructure the current POSTrackTask will use
        // @InfraOccupiedByTrackTaskID is a dictionary (Key:Value) contains all the infrastructures (Key) and
        // their occupation by a specific POSTrackTask (Value) - note: the POSTrackTask are specified by their IDs
        // @conflictingTrackTaskIds contains all the conflicting moves, that use the same infrastrucure the current POSTrackTask requires to occupy
        public bool InfraConflictByTrackTasks(Dictionary<Infrastructure, int> InfraOccupiedByTrackTaskID, List<ulong> IDListOfInfraUsed, ref List<int> conflictingTrackTaskIds)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            conflictingTrackTaskIds = new List<int>();
            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByTrackTaskID[DictOfInfrastrucure[id]] == 999)
                {
                    // conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                }
                else
                {
                    if (conflictingTrackTaskIds.Contains(InfraOccupiedByTrackTaskID[DictOfInfrastrucure[id]]) == false)
                        conflictingTrackTaskIds.Add(InfraOccupiedByTrackTaskID[DictOfInfrastrucure[id]]);
                }


            }
            if (conflictingTrackTaskIds.Count != 0)
                return true;

            return false;

        }

        // TODO: rename it to CreatePOS - it should pobably be called only once
        // later un update function will basically be called when a recopute of POS
        // is needed
        public void UpdatePOS()
        {

            // Index of the list is the ID assigned to a move
            List<MoveTask> listOfMoves = this.ListOfMoves;



            // Dictionary with move IDs as Key, and value as List of all the linked moves
            // Key: movement ID (parent move) this move was conflicting since it previously used the same infrastructure as the
            // the current move even if it is trivial when the same train unit is used in the previous and the current move,
            // Value: list of linked movements (IDs child moves)
            // 0:{1,3} OR 1:{2,3}
            // Idea is to onbtain multiple directed graphs -> dependency between linked movements
            // To Note: here the links are the links of infrastructure conflicts and same train units per movment
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


            // Dictionary with all infrastrucures, for each infrastructure a movement is assigned
            Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves = new Dictionary<Infrastructure, MoveTask>();

            // Dictionary with all infrastrucures, for each infrastructure a movement ID is assigned, the IDs
            // are used to access a move which is stored in 'listOfMoves' or  'this.ListOfMoves'
            // the InfraOccupiedByMovesID is initialized with 999, meaning that there in no valid movment ID
            // assigned yet to the for the given infrastructure
            Dictionary<Infrastructure, int> InfraOccupiedByMovesID = new Dictionary<Infrastructure, int>();

            Dictionary<Infrastructure, int> InfraOccupiedByTrackTaskID = new Dictionary<Infrastructure, int>();


            //List of al train uinit used the movement present in this scenario
            List<Trains.TrainUnit> ListOfTrainUnits = this.ListOfTrainUnits;

            // Dictionary with all train units, for each train unit a movement ID is assigned, the IDs
            // are used to access a move which is stored in 'listOfMoves' or  'this.ListOfMoves'
            // the TrainUnitsOccupiedByMovesID is initialized with 999, meaning that there in no valid movment ID
            // assigned yet to given train unit
            Dictionary<Trains.TrainUnit, int> TrainUnitsOccupiedByMovesID = new Dictionary<Trains.TrainUnit, int>();

            // Dictionary conatining all the infrasturcutre, index:infrastructure
            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;


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
            foreach (KeyValuePair<ulong, Infrastructure> infra in DictOfInfrastrucure)
            {
                InfraOccupiedByMoves[infra.Value] = null; // Maybe later this can be removed
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
                    // No conflict occured

                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        // Assign move to the infrastructure occupied
                        InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                    }

                }
                else
                {

                    // Contains all the movments that was assigned to the same movements as the train unit of the cuurent move (moveIndex)
                    // this also mean that these movements are conflicting becasue of the same train used assigned to the movement
                    // and not only because of the same infrastructure used
                    List<int> movesUsingSameTrainUnit = CheckIfSameTrainUintUsed(conflictingMoveIds, listOfMoves, moveIndex);

                    foreach (int MoveId in conflictingMoveIds)
                    {
                        // 1st: link movements -> conflictingMoveId is now linked with the moveIndex (current move id)
                        LinkMovmentsByID(MovementLinks, MoveId, moveIndex);


                        if (movesUsingSameTrainUnit.Count != 0)
                        {
                            // This statement is used to link the movements conflicted because of using the same infrastrucure\
                            // and not because of same train unit assigned per movement {aka dashed line dependency}
                            if (!movesUsingSameTrainUnit.Contains(MoveId))
                                LinkMovmentsByID(MovementLinksSameInfrastructure, MoveId, moveIndex);

                            // This statement is used to link the movements conflicted because of using the same train unit\
                            // and not because of same infrastructure assigned per movement {aka solid line dependency}
                            // if (movesUsingSameTrainUnit.Contains(MoveId))
                            //     LinkMovmentsByID(MovementLinksSameTrainUnit, MoveId, moveIndex);
                        }
                        else
                        {
                            LinkMovmentsByID(MovementLinksSameInfrastructure, MoveId, moveIndex);
                        }

                    }
                    // 2nd Assign current movement to the required infrastructure
                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                    }



                }
                // Identify all the conflicting moves related to the same train units used by the movements - and link moves

                List<int> IDListOfTrainUnitUsed = GetIDListOfTrainUnitsUsed(currentMove); // Train units used by the movement

                if (TrainUnitConflict(TrainUnitsOccupiedByMovesID, IDListOfTrainUnitUsed, ref conflictingMoveIds) == false)
                {
                    // No conflict occured. Here the moves are not linked
                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {
                        // Assin the move to the Train Units used
                        TrainUnitsOccupiedByMovesID[ListOfTrainUnits[trainUnitID]] = moveIndex;
                    }
                }
                else
                {
                    // The conflicting moves are linked
                    foreach (int MoveId in conflictingMoveIds)
                    {
                        LinkMovmentsByID(MovementLinksSameTrainUnit, MoveId, moveIndex);

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
            AddInfrastructurePredeccessorSuccessorLinksToPOSMoves();
            this.POSadjacencyListForTrainUint = CreatePOSAdjacencyList(MovementLinksSameTrainUnit);


            AddTrainUnitPredeccessorSuccessorLinksToPOSMoves();


            AddSuccessorsAndPredeccessors();

            // DisplayAllPOSMovementLinks();
            DisplayPOSMovementLinksInfrastructureUsed();
            DisplayPOSMovementLinksTrainUinitUsed();
            // DisplayTrainUnitSuccessorsAndPredeccessors();
            DisplayMovesSuccessorsAndPredeccessors();

            this.ListOfPOSTrackTasks = CreatePOSTrackTaskList();
            DisplayListPOSTrackTracks();


            // TODO: -------------------------------------------------------------------------------------------------------------------------------------------
            ok = 1;
            int TrackTaskIndex = 0;

            List<int> conflictingTrackTaskIds = new List<int>();

            List<POSTrackTask> listOfPOSTrackTasks = this.ListOfPOSTrackTasks;
            while (ok != 0)
            {

                var currentPOSTrakTask = listOfPOSTrackTasks[TrackTaskIndex];

                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsedByTrackTasks(currentPOSTrakTask);

                // Identify all the conflicting POSTrackTask related to the infrastructure used by the POSTrakTask - and links POSTrackTasks
                if (InfraConflictByTrackTasks(InfraOccupiedByTrackTaskID, IDListOfInfraUsed, ref conflictingTrackTaskIds) == false)
                {
                    // No conflict occured

                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        // Assign POSTrackTask to the infrastructure occupied
                        InfraOccupiedByTrackTaskID[DictOfInfrastrucure[infraID]] = TrackTaskIndex;
                    }

                }
                else
                {
                    // Contains all the POSTrackTasks that was assigned to the same POSTrackTask as the train unit of the current POSTrackTask (TrackTaskIndex)
                    // this also mean that these POSTrackTasks are conflicting becasue of the same train used (assigned to the POSTrackTask)
                    // and not only because of the same infrastructure used
                    List<int> trackTaskUsingSameTrainUnit = CheckIfSameTrainUintUsedByPOSTrackTask(conflictingTrackTaskIds, listOfPOSTrackTasks, TrackTaskIndex);


                    // TODO from here:
                    foreach (int trackTaskId in conflictingTrackTaskIds)
                    {
                   

                        if (trackTaskUsingSameTrainUnit.Count != 0)
                        {
                            // This statement is used to link the POSTrackTasks conflicted because of using the same infrastrucure\
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
                        InfraOccupiedByTrackTaskID[DictOfInfrastrucure[infraID]] = TrackTaskIndex;
                    }
                }

                 // Identify all the conflicting POSTrackTask related to the same train units used by the POSTrackTask - and link POSTrackTask

                List<int> IDListOfTrainUnitUsed = GetIDListOfTrainUnitUsedPOSTrackTask(currentPOSTrakTask); // Train units used by the POSTrackTask

                if (TrainUnitConflictByPOSTrackTask(TrainUnitsOccupiedByPOSTrackTaskID, IDListOfTrainUnitUsed, ref conflictingTrackTaskIds) == false)
                {
                    // No conflict occured. Here the POSTrackTasks are not linked
                    foreach (int trainUnitID in IDListOfTrainUnitUsed)
                    {
                        // Assin the POSTrackTask to the Train Units used
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

            foreach(KeyValuePair<int, List<int>> pair in POSTrackTaskLinksSameInfrastructure.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"POSTrackTask {pair.Key} --> ");
                foreach(int linkToPOStrackTask in pair.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask} ");
                }
                Console.WriteLine();
            }

            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Train Unit used)                |");
            Console.WriteLine("-----------------------------------------------------------------------------------");

            foreach(KeyValuePair<int, List<int>> pair in POSTrackTaskLinksSameTrainUnits.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"POSTrackTask {pair.Key} --> ");
                foreach(int linkToPOStrackTask in pair.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask} ");
                }
                Console.WriteLine();
            }

            this.POSTrackTaskadjacencyListForTrainUsed = CreatePOSAdjacencyListTrackTask(POSTrackTaskLinksSameTrainUnits);

            Console.WriteLine("-----------------------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Train Unit used) - AdjacencyList            |");
            Console.WriteLine("-----------------------------------------------------------------------------------------------");

            foreach(KeyValuePair<POSTrackTask, List<POSTrackTask>> Ttask in this.POSTrackTaskadjacencyListForTrainUsed)
            {
                Console.Write($"POSTrackTask {Ttask.Key.ID} --> ");
                foreach(POSTrackTask linkToPOStrackTask in Ttask.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask.ID} ");
                }
                Console.WriteLine();
            }

            // TODO: -------------------------------------------------------------------------------------------------------------------------------------------

            this.POSTrackTaskadjacencyListForInfrastructure = CreatePOSAdjacencyListTrackTask(POSTrackTaskLinksSameInfrastructure);

            Console.WriteLine("--------------------------------------------------------------------------------------------------");
            Console.WriteLine("|              From POSTrackTask inner Links (same Infrastructure used) - AdjacencyList            |");
            Console.WriteLine("--------------------------------------------------------------------------------------------------");

            foreach(KeyValuePair<POSTrackTask, List<POSTrackTask>> Ttask in this.POSTrackTaskadjacencyListForInfrastructure)
            {
                Console.Write($"POSTrackTask {Ttask.Key.ID} --> ");
                foreach(POSTrackTask linkToPOStrackTask in Ttask.Value)
                {
                    Console.Write($"POSTrackTask {linkToPOStrackTask.ID} ");
                }
                Console.WriteLine();
            }

        }

        // Creates a list of POSTrackkTasks. POSTrackTasks are created by using the TrackTasks embedded between 
        // the MoveTasks. 
        // The extraction of POSMTrackTask information is done by using the dependecies (links) between the POSMoveTasks @POSadjacencyList.
        // When a successor of a POSMoveTask has the same TrackTask as predecessor TrackTask a POSTrackTask is created. POSTrackTask is also created
        // in case of arrival TrackTask.
        // The function also links the POSMoveTasks and POSTrackTasks - in several cases between two POSMoveTasks a POSTrackTasks is included (such as
        // service, parking, split, combine task). 
        // POSTrackTask is based on types: {Arrival, Departure, Parking, Service, Split, Combine}
        // Example of linking: POSMoveTask_j -> POSMoveTask_k and POSMoveTask_j -> POSMoveTask_l, and POSMoveTask_j next TrackTask is POSTrackTask_i
        // and POSMoveTask_k previous TrackTask is POSTrackTask_i, and POSMoveTask_l previous POSTrackTask is POSTrackTask_b
        // then it might be the case that:
        // POSMoveTask_j <- POSTrackTask_i -> POSMoveTask_k ,but POSMoveTask_l is not linked with POSTrackTask_i becuase they didn't have 
        // a commun POSTrackTask
        public List<POSTrackTask> CreatePOSTrackTaskList()
        {

            Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyList;
            List<POSTrackTask> listPOSTrackTask = new List<POSTrackTask>();


            int id = 0;

            // Study all the POSMoveTasl moves
            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in POSadjacencyList)
            {
                POSMoveTask POSmove = element.Key;

                MoveTask corrMoveTask = POSmove.CorrespondingMoveTask;

                // Next TrackTask(s) following the movement (MoveTask)
                IList<TrackTask> trackTaskNexts = corrMoveTask.AllNext;

                // Previous TrackTask(s) preceding the movement (MoveTask)
                IList<TrackTask> trackTaskPrevious = corrMoveTask.AllPrevious;


                // Create new POSTrackTask(s) when the POSMoveTask's predeccessor is an arrival task
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

                foreach (TrackTask nextTrackTask in trackTaskNexts)
                {
                    POSTrackTask newTrackTask = new POSTrackTask(nextTrackTask);
                    newTrackTask.ID = id;
                    // More than 1 successor task means that a train unit was splited
                    if (trackTaskNexts.Count > 1)
                    {
                        newTrackTask.setPOSTrackTaskType(POSTrackTaskType.Split);
                        newTrackTask.TaskType = POSTrackTaskType.Split;
                    }

                    POSmove.SuccessorTrackTasks.Add(newTrackTask);
                    newTrackTask.previousMoves.Add(POSmove);

                    // Check for dependencies when same train unit used - successors
                    // if the current POSMoveTask successor's previous TrackTask matches
                    // the next TrackTask of the POSMoveTask successor, then link POSTackTask
                    // with the successor POSMoveTasl 
                    foreach (POSMoveTask successor in POSmove.SuccessorMovesByTrainUnits)
                    {

                        MoveTask corrSucessorMoveTask = successor.CorrespondingMoveTask;
                        IList<TrackTask> previousTrackTasksOfSuccessor = corrSucessorMoveTask.AllPrevious;

                        foreach (TrackTask task in previousTrackTasksOfSuccessor)
                        {
                            string nextTrackTaskCharacteristicks = $"{nextTrackTask.Start} - {nextTrackTask.End} : {nextTrackTask.Train} at {nextTrackTask.Track.ID}";

                            string trackTrackTaskCharacteristicks = $"{task.Start} - {task.End} : {task.Train} at {task.Track.ID}";

                            if (nextTrackTaskCharacteristicks == trackTrackTaskCharacteristicks)
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

                        MoveTask corrSucessorMoveTask = successor.CorrespondingMoveTask;
                        IList<TrackTask> previousTrackTaskOfSuccessor = corrSucessorMoveTask.AllPrevious;

                        foreach (TrackTask task in previousTrackTaskOfSuccessor)
                        {
                            string nextTrackTaskCharacteristicks = $"{nextTrackTask.Start} - {nextTrackTask.End} : {nextTrackTask.Train} at {nextTrackTask.Track.ID}";

                            string trackTrackTaskCharacteristicks = $"{task.Start} - {task.End} : {task.Train} at {task.Track.ID}";

                            if (nextTrackTaskCharacteristicks == trackTrackTaskCharacteristicks)
                            {
                                POSMoveTask explicitSuccessor = GetPOSMoveTaskByID(successor.ID, POSadjacencyList);

                                explicitSuccessor.PredecessorTrackTasks.Add(newTrackTask);

                                // More than 1 pedeccessor task means that the train units were
                                if (previousTrackTaskOfSuccessor.Count > 1)
                                    newTrackTask.setPOSTrackTaskType(POSTrackTaskType.Combine);
                                newTrackTask.nextMoves.Add(explicitSuccessor);


                            }
                            // if(nextTrackTask.Start == task.Start && nextTrackTask.End == task.End && nextTrackTask.Train == task.Train && )   
                        }

                    }


                    listPOSTrackTask.Add(newTrackTask);
                    id++;

                }

            }


            return listPOSTrackTask;

        }

        // Displays the all POSTrackTask list identified in the POS solution
        public void DisplayListPOSTrackTracks()
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
                List<POSMoveTask> PredeccessorsByTrainUnit = POSmove.PredecessorMovesByTrainUnits;

                List<POSMoveTask> SuccessorsByIntfrastructure = POSmove.SuccessorMovesByInfrastructure;
                List<POSMoveTask> PredeccessorsByIntfrastructure = POSmove.PredecessorMovesByInfrastructure;



            }
        }


        public void AddSuccessorsAndPredeccessors()
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

                        List<POSMoveTask> Predeccessors = GetMovePredecessors(POSmoveInfra, posAdjacencyListForInfrastructure);
                        foreach (POSMoveTask predeccessor in Predeccessors)
                        {
                            POSmove.AddNewPredeccessorByInfrastructure(predeccessor);
                        }
                    }
                }

                foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> elementTruinUnit in posAdjacencyListForTrainUint)
                {
                    POSMoveTask POSmoveTrainUint = elementTruinUnit.Key;

                    if (POSmove.ID == POSmoveTrainUint.ID)
                    {
                        List<POSMoveTask> Successors = elementTruinUnit.Value;
                        foreach (POSMoveTask successor in Successors)
                        {
                            POSmove.AddNewSuccessorByTrainUnits(successor);
                        }

                        List<POSMoveTask> Predeccessors = GetMovePredecessors(POSmoveTrainUint, posAdjacencyListForTrainUint);

                        foreach (POSMoveTask predeccessor in Predeccessors)
                        {
                            POSmove.AddNewPredecessorByTrainUnits(predeccessor);

                        }

                    }
                }



            }
        }

        // Takes all the POSMove linked when using the same infrastructure - POSadjacencyListForInfrastructure 
        // and assigns the POSMove successors and predeccessors to the POSMove taken. This function is very useful, since it adds new link information to the POSMoves used
        // in the Partial Order Schedule graph
        public void AddInfrastructurePredeccessorSuccessorLinksToPOSMoves()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForInfrastructure = this.POSadjacencyListForInfrastructure;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in posAdjacencyListForInfrastructure)
            {
                // Add sucessors to each POSMoves contained by the adjacency list
                POSMoveTask POSmove = element.Key;
                List<POSMoveTask> Successors = element.Value;

                foreach (POSMoveTask successor in Successors)
                {
                    POSmove.AddNewSuccessorByInfrastructure(successor);
                }

                // Add predeccessors to each POSMoves contained by the adjacency list
                List<POSMoveTask> Predeccessors = GetMovePredecessors(POSmove, posAdjacencyListForInfrastructure);

                foreach (POSMoveTask predeccessor in Predeccessors)
                {
                    POSmove.AddNewPredeccessorByInfrastructure(predeccessor);
                }

            }
        }


        // Takes all the POSMove linked when using the same train unit - POSadjacencyListForTrainUint and assigns the POSMove
        // successors and predeccessors to the POSMove taken. This function is very useful, since it adds new link information to the POSMoves used
        // in the Partial Order Schedule graph
        public void AddTrainUnitPredeccessorSuccessorLinksToPOSMoves()
        {
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyListForTrainUint = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in posAdjacencyListForTrainUint)
            {
                // Add sucessors to each POSMoves contained by the adjacency list
                POSMoveTask POSmove = element.Key;
                List<POSMoveTask> Successors = element.Value;

                foreach (POSMoveTask successor in Successors)
                {
                    POSmove.AddNewSuccessorByTrainUnits(successor);
                }


                // Add predeccessors to each POSMoves contained by the adjacency list
                List<POSMoveTask> Predeccessors = GetMovePredecessors(POSmove, posAdjacencyListForTrainUint);

                foreach (POSMoveTask predeccessor in Predeccessors)
                {
                    POSmove.AddNewPredecessorByTrainUnits(predeccessor);
                }

                // foreach(var s in Successors)
                // {
                //     if(!posAdjacencyListForTrainUint.ContainsKey(s))
                //     {
                //         posAdjacencyListForTrainUint[s] = new List<POSMoveTask>();
                //         var tmp =  posAdjacencyListForTrainUint[s];

                //     }
                // }
            }

        }

        public void DisplayMovesSuccessorsAndPredeccessors()
        {
            Console.WriteLine("---------------------------------------------------");
            Console.WriteLine("|            From POS Movement Links               |");
            Console.WriteLine("---------------------------------------------------");

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> element in this.POSadjacencyList)
            {
                Console.WriteLine(element.Key);
            }
        }


        // Displays all the POSMove predeccessors and successors - these links are represents the 
        // relations between the moves using the same train unit
        public void DisplayTrainUnitSuccessorsAndPredeccessors()
        {
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine("|       POS Movement predeccessors and successors - TrainUnit (solid arcs) |");
            Console.WriteLine("--------------------------------------------------------------------------");
            // Show connections per Move
            Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyListForTrainUint;

            foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList.OrderBy(pair => pair.Key.ID).ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Console.Write($"Move{pair.Key.ID} --> \n");
                Console.WriteLine("Predeccessors:");
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

            // Console.WriteLine("---------------------------------------------------");
            // Console.WriteLine("|  From POS Movement Links - Same Train Unit used  |");
            // Console.WriteLine("---------------------------------------------------");

            // foreach(KeyValuePair<POSMoveTask, List<POSMoveTask>> element in this.POSadjacencyListForTrainUint)
            // {
            //     Console.WriteLine(element.Key);
            // }
        }


        // public List<POSTrackTask> CreatePOSTrackTask()
        // {
        //     Dictionary<POSMoveTask, List<POSMoveTask>> posAdjacencyList = this.POSadjacencyList;

        //     int id = 0;
        //     foreach (KeyValuePair<POSMoveTask, List<POSMoveTask>> pair in posAdjacencyList)
        //     {
        //         MoveTask move = pair.Key.CorrespondingMoveTask;


        //         if (move.AllPrevious.Count == 1)
        //         {
        //             TrackTask trackTask = move.AllPrevious[0];

        //             POSTrackTask POStask = null;

        //             if (trackTask.TaskType is TrackTaskType.Arrival)
        //             {
        //                 POStask = new POSTrackTask(id, POSTrackTaskType.Arrival, trackTask);

        //             }
        //         }


        //         // foreach (TrackTask task in move.AllPrevious)
        //         //         {
        //         //             Console.WriteLine($"---{task}----");
        //         //             // if (task is ParkingTask parkingTask)
        //         //             //     Console.WriteLine("@It was a ParkingTask");
        //         //             // if (task is ServiceTask serviceTask)
        //         //             //     Console.WriteLine("@It was a ServiceTask");
        //         //             // if (task is ArrivalTask arrivalTask)
        //         //             //     Console.WriteLine("@It was a ArrivalTask");
        //         //             // if (task is DepartureTask departureTask)
        //         //             //     Console.WriteLine("@It was a DepartureTask");
        //         //         }

        //     }
        // }
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

        // Displays all the direct sucessors and predecessors of a given POS move
        // the move is identified by its ID (POSMoveTask POSmove.ID)
        // @linkType specifies the type of the links 'infrastructure' - same inrastructure used - populated from @POSadjacencyListForInfrastructure
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

                    List<POSMoveTask> sucessorPOSMoves = POSadjacencyList[POSmove];

                    List<POSMoveTask> predecessorsPOSMoves = GetMovePredecessors(POSmove, POSadjacencyList);

                    Console.WriteLine("|  Direct Sucessors |");
                    Console.Write("[ ");

                    foreach (POSMoveTask move in sucessorPOSMoves)
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

                    List<POSMoveTask> sucessorPOSMoves = POSadjacencyList[POSmove];

                    List<POSMoveTask> predecessorsPOSMoves = GetMovePredecessors(POSmove, POSadjacencyList);

                    Console.WriteLine("|  Direct Sucessors |");

                    Console.Write("[ ");
                    foreach (POSMoveTask move in sucessorPOSMoves)
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


        // Get all the direct sucessors and predecessors of a given POS move, the move is identified by its ID (POSMoveTask POSmove.ID)
        // Successors stored in @sucessorPOSMoves; Predecessors stored in @predecessorsPOSMoves
        // @linkType specifies the type of the links 'infrastructure' - same inrastructure used - populated from @POSadjacencyListForInfrastructure
        // 'trainUint' - same train unit(s) used - populated from @POSadjacencyListForTrainUint
        public void GetMoveLinksOfPOSMove(int POSId, string linkType, out List<POSMoveTask> sucessorPOSMoves, out List<POSMoveTask> predecessorsPOSMoves)
        {
            sucessorPOSMoves = new List<POSMoveTask>();
            predecessorsPOSMoves = new List<POSMoveTask>();

            if (linkType == "infrastructure")
            {
                Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList = this.POSadjacencyListForInfrastructure;

                // sucessorPOSMoves.Clear();
                // predecessorsPOSMoves.Clear();
                try
                {

                    POSMoveTask POSmove = GetPOSMoveTaskByID(POSId, POSadjacencyList);

                    sucessorPOSMoves.AddRange(POSadjacencyList[POSmove]);

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

                    sucessorPOSMoves.AddRange(POSadjacencyList[POSmove]);

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
        public void DisplayPOSMovementLinksTrainUinitUsed()
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

                List<ShuntTrainUnit> trainUintsOfCurrentMove = currentMove.Train.Units;

                foreach (ShuntTrainUnit shuntTrainOfConflictingMove in trainUnitsOfConflictingMove)
                {
                    foreach (ShuntTrainUnit shuntTrainOfCurrentMove in trainUintsOfCurrentMove)
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

                List<ShuntTrainUnit> trainUintsOfCurrentTrackTask = currentTrackTask.Train.Units;

                foreach (ShuntTrainUnit shuntTrainOfConflictingTrackTask in trainUnitsOfConflictingTrackTask)
                {
                    foreach (ShuntTrainUnit shuntTrainOfCurrentTrackTask in trainUintsOfCurrentTrackTask)
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


            // TODO: It is used for an old version, it most be modified accordingly
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

        // TODO; rename it to Initialize POS since it is a sort of Constructor
        // this function does not generate a POS graph only initialize some values
        public void CreatePOS()
        {
            MoveTask move = this.First;

            List<MoveTask> listOfMoves = new List<MoveTask>();

            while (move != null)
            {
                // TODO : Probably a Clone method will be needed here

                MoveTask move_clone = move;
                listOfMoves.Add(move_clone);

                move = move.NextMove;
            }
            this.ListOfMoves = listOfMoves;
            this.First = listOfMoves.First();

            this.DictOfInrastructure = GetInfrasturcture();

            this.ListOfTrainUnits = GetTrainFleet();

        }
        public void DisplayInfrastructure()
        {
            Console.WriteLine("-------------------------------");

            Console.WriteLine("------Main Infrastructure------");

            Console.WriteLine("-------------------------------");

            Dictionary<ulong, Infrastructure> infrastructuremap = GetInfrasturcture();

            foreach (KeyValuePair<ulong, Infrastructure> infra in infrastructuremap)
            {
                Console.WriteLine($"id: {infra.Key} : Inrastructure {infra.Value}");

            }


            Console.WriteLine("-------------------------------");


        }

        // Shows rich information about the movements and infrastructure used in the Totaly Ordered Solution
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
                    Console.WriteLine("Infrastrucre used (tracks):");
                    var tracks = routing.Route.Tracks;
                    var lastTrack = tracks.Last();

                    Console.WriteLine("All Previous tasks:");
                    foreach (TrackTask task in routing.AllPrevious)
                    {
                        Console.WriteLine($"---{task}----");
                        // if (task is ParkingTask parkingTask)
                        //     Console.WriteLine("@It was a ParkingTask");
                        // if (task is ServiceTask serviceTask)
                        //     Console.WriteLine("@It was a ServiceTask");
                        // if (task is ArrivalTask arrivalTask)
                        //     Console.WriteLine("@It was a ArrivalTask");
                        // if (task is DepartureTask departureTask)
                        //     Console.WriteLine("@It was a DepartureTask");
                    }

                    Console.WriteLine("All Next tasks:");
                    foreach (TrackTask task in routing.AllNext)
                    {
                        Console.WriteLine($"---{task}----");
                        // if (task is ParkingTask parkingTask)
                        //     Console.WriteLine("@It was a ParkingTask");
                        // if (task is ServiceTask serviceTask)
                        //     Console.WriteLine("@It was a ServiceTask");
                        // if (task is ArrivalTask arrivalTask)
                        //     Console.WriteLine("@It was a ArrivalTask");
                        // if (task is DepartureTask departureTask)
                        //     Console.WriteLine("@It was a DepartureTask");
                    }

                    // TODO: display more infrastructure

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
                        // var previousTasks = move.AllPrevious
                        Console.WriteLine("All Previous tasks:");
                        foreach (TrackTask task in move.AllPrevious)
                        {
                            Console.WriteLine($"---{task}----");
                            // if (task is ParkingTask parkingTask)
                            //     Console.WriteLine("@It was a ParkingTask");
                            // if (task is ServiceTask serviceTask)
                            //     Console.WriteLine("@It was a ServiceTask");
                            // if (task is ArrivalTask arrivalTask)
                            //     Console.WriteLine("@It was a ArrivalTask");
                            // if (task is DepartureTask departureTask)
                            //     Console.WriteLine("@It was a DepartureTask");
                        }

                        Console.WriteLine("All Next tasks:");
                        foreach (TrackTask task in move.AllNext)
                        {
                            Console.WriteLine($"---{task}----");
                            // if (task is ParkingTask parkingTask)
                            //     Console.WriteLine("@It was a ParkingTask");
                            // if (task is ServiceTask serviceTask)
                            //     Console.WriteLine("@It was a ServiceTask");
                            // if (task is ArrivalTask arrivalTask)
                            //     Console.WriteLine("@It was a ArrivalTask");
                            // if (task is DepartureTask departureTask)
                            //     Console.WriteLine("@It was a DepartureTask");
                        }



                        var routingDeparture = move as DepartureRoutingTask;
                        if (routingDeparture != null)
                        {
                            var listOfRoutes = routingDeparture.GetRoutes();

                            Console.WriteLine($"Infrastrucre used (tracks) number of routes {listOfRoutes.Count}:");


                            // // TODO: display more infrastructure
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
        public void EvaluatePOS()
        {
            // TODO
        }
    }


}