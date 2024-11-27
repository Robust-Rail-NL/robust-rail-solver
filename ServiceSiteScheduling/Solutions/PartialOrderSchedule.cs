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


namespace ServiceSiteScheduling.Solutions
{
    class PartialOrderSchedule
    {
        public ShuntTrainUnit[] ShuntUnits { get; private set; }

        public ArrivalTask[] ArrivalTasks { get; private set; }
        DepartureTask[] DepartureTasks;

        public ArrivalTask FirstArrival { get { return this.ArrivalTasks.First(arrival => arrival.Next.PreviousMove == null); } }

        // This is the Adjacency List for POS Movements: Each POSMoveTask maps to a list of connected POSMoveTask
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyList { get; private set; }

        // This is the Adjacency List for POS Movements using the same infrastructure: Each POSMoveTask maps to a list of connected POSMoveTask
        // (dashed line dependency links)
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyListForInfrastructure { get; private set; }

        // This is the Adjacency List for POS Movements using the same Train Unit: Each POSMoveTask maps to a list of connected POSMoveTask
        // (solid line dependency links)
        public Dictionary<POSMoveTask, List<POSMoveTask>> POSadjacencyListForTrainUint { get; private set; }


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

        // Dictionary that contains the overall Infrastructure used in the scenario
        public Dictionary<ulong, Infrastructure> DictOfInrastructure { get; set; }
        public PartialOrderSchedule(MoveTask first)
        {
            this.First = first;

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
        // @conflictingMoveIds contains all the conflicting moves, that use the same infrastrucure tas the current move requires to occupy
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

        // Links the previous move -@parentMovementID- this move was conflicting since it previously used the same infrastructure as the
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
        public void RemoveAssignedMoveFromInfrastructureByID(List<MoveTask> listOfMoves, Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves, Dictionary<Infrastructure, int> InfraOccupiedByMovesID, Dictionary<ulong, Infrastructure> DictOfInfrastrucure, int conflictingMoveId)
        {


            foreach (KeyValuePair<Infrastructure, int> pair in InfraOccupiedByMovesID)
            {
                if (pair.Value == conflictingMoveId)
                {
                    InfraOccupiedByMoves[pair.Key] = null;
                    InfraOccupiedByMovesID[pair.Key] = 999;
                }
            }
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
            // infrastructure aka dashed lines Move_i---> Move_j
            Dictionary<int, List<int>> MovementLinksSameInfrastructure = new Dictionary<int, List<int>>();


            // Dictionary with move IDs as Key, and value as List of linked moves using the same train unit,
            // in this dictionary a movement is linked to another movement (parent move) if and only if they used the same 
            // train unit aka solid lines Move_i -> Move_j
            Dictionary<int, List<int>> MovementLinksSameTrainUnit = new Dictionary<int, List<int>>();



            // Dictionary with all infrastrucures, for each infrastructure a movement is assigned
            Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves = new Dictionary<Infrastructure, MoveTask>();

            // Dictionary with all infrastrucures, for each infrastructure a movement ID is assigned, the IDs
            // are used to access a move which is stored in 'listOfMoves' or  'this.ListOfMoves'
            // the InfraOccupiedByMovesID is initialized with 999, meaning that there in no valid movment ID
            // assigned yet to the for the given infrastructure
            Dictionary<Infrastructure, int> InfraOccupiedByMovesID = new Dictionary<Infrastructure, int>();

            // Dictionary conatining all the infrasturcutre, index:infrastructure
            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            // Init dictionary for infrastructures occupied by moves

            bool Test = true;

            // Initialize dictionaries
            foreach (KeyValuePair<ulong, Infrastructure> infra in DictOfInfrastrucure)
            {
                InfraOccupiedByMoves[infra.Value] = null; // Maybe later this can be removed
                InfraOccupiedByMovesID[infra.Value] = 999;
            }


            int ok = 1;
            int moveIndex = 0;
            int conflictingMoveId = 0;

            List<int> conflictingMoveIds = new List<int>();
            bool revisit = false;
            int revisitIndex = 0;



            while (ok != 0)
            {


                var currentMove = listOfMoves[moveIndex];
                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove); // used by the movement


                if (InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveIds) == false)
                {
                    // No conflict occured

                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                    }
                    // The given movement is assigned to an infrastructure


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
                            if (movesUsingSameTrainUnit.Contains(MoveId))
                                LinkMovmentsByID(MovementLinksSameTrainUnit, MoveId, moveIndex);
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

            this.POSadjacencyListForTrainUint = CreatePOSAdjacencyList(MovementLinksSameTrainUnit);

            DisplayAllPOSMovementLinks();
            DisplayPOSMovementLinksInfrastructureUsed();
            DisplayPOSMovementLinksTrainUinitUsed();


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
            Console.WriteLine("|      POS Movement Links - Train Unit (solid lines)     |");
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
            Console.WriteLine("|   POS Movement Links - Infrastructure (dashed lines)   |");
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
            // Dictionary<int, List<int>> orderedMovementLinks = new Dictionary<int, List<int>>();

            var orderedMovementLinks = MovementLinks.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);

            List<POSMoveTask> POSMoveList = new List<POSMoveTask>();

            // foreach (KeyValuePair<int, List<int>> pair in orderedMovementLinks)
            // {

            //     POSMoveTask POSmove = new POSMoveTask(listOfMoves[pair.Key], pair.Key);
            //     POSMoveList.Add(POSmove);

            // }
            int id = 0;
            foreach (MoveTask moveTask in listOfMoves)
            {

                POSMoveTask POSmove = new POSMoveTask(moveTask, id);
                POSMoveList.Add(POSmove);
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
        public void CreatePOS(MoveTask first)
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
                            Console.WriteLine($"---{task}----");

                        Console.WriteLine("All Next tasks:");
                        foreach (TrackTask task in move.AllNext)
                            Console.WriteLine($"---{task}----");



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