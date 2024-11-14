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


namespace ServiceSiteScheduling.Solutions
{
    class PartialOrderSchedule
    {
        public ShuntTrainUnit[] ShuntUnits { get; private set; }

        public ArrivalTask[] ArrivalTasks { get; private set; }
        DepartureTask[] DepartureTasks;

        public ArrivalTask FirstArrival { get { return this.ArrivalTasks.First(arrival => arrival.Next.PreviousMove == null); } }

        public MoveTask First { get; set; }
        public MoveTask Last { get; set; }

        public List<MoveTask> ListOfMoves { get; set; }

        public Dictionary<ulong, Infrastructure> DictOfInrastructure { get; set; }
        public PartialOrderSchedule(MoveTask first, ShuntTrainUnit[] shuntunits, ArrivalTask[] arrivals, DepartureTask[] departures)
        {
            this.First = first;
            this.ShuntUnits = shuntunits;
            this.ArrivalTasks = arrivals;
            this.DepartureTasks = departures;
        }

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

        // Returns list of the IDs of the Infrastructure used by a movement
        public List<ulong> GetIDListOfInfraUsed(MoveTask move)
        {
            List<ulong> IDListOfInstraUsed = new List<ulong>();

            var routing = move as RoutingTask;

            if (routing != null)
            {
                var tracks = routing.Route.Tracks;
                var lastTrack = tracks.Last();

                foreach (Track track in tracks)
                {

                    IDListOfInstraUsed.Add(track.ASide.ID);
                    IDListOfInstraUsed.Add(track.ID);
                    IDListOfInstraUsed.Add(track.BSide.ID);

                }
            }

            return IDListOfInstraUsed;
        }


        public bool InfraConflict(Dictionary<Infrastructure, int> InfraOccupiedByMovesID, List<ulong> IDListOfInfraUsed, int moveID, ref int conflictingMoveId, bool revisit)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            bool ConflictOccured = false;

            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != moveID && InfraOccupiedByMovesID[DictOfInfrastrucure[id]] == 999 && revisit == false)
                {
                    ConflictOccured = false;
                    conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                }
                else if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != moveID && InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != 999 && revisit == true)
                {
                    ConflictOccured = true;
                    conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                    return true;
                }
                else if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != moveID && InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != 999 && revisit == false)
                {
                    conflictingMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                    return true;
                }
            }


            return ConflictOccured;
        }

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
            // var conflictingMove = listOfMoves[conflictingMoveId];

            // List<ulong> IDListOfInfraUsedByConflictingMove = GetIDListOfInfraUsed(conflictingMove);

            // foreach (ulong infraID in IDListOfInfraUsedByConflictingMove)
            // {
            //     InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = null;
            //     InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = 999;
            // }


            foreach (KeyValuePair<Infrastructure, int> pair in InfraOccupiedByMovesID)
            {
                if (pair.Value == conflictingMoveId)
                {
                    InfraOccupiedByMoves[pair.Key] = null;
                    InfraOccupiedByMovesID[pair.Key] = 999;
                }
            }
        }

        public void UpdatePOS()
        {

            // Index of the list is the ID assigned to a move
            List<MoveTask> listOfMoves = this.ListOfMoves;

            // Maybe use later:
            // Key of dictionary corresponds to the move ID, the value specifies three states:
            // "pending" - move ID has not yet been assigned to an infrastructure
            // "assigned" - move ID is currently assigned to an infrastructure
            // "removed" - move ID has been previously been assigned to an infrastrucure, but since all
            // movments were studied (all the movements were iterated) a reiteration is happening in the list
            // of movments therefore we the "assigned" movement is linked to the new movment that wants to use
            // the same infrastructure the "assigned" movment is now "removed"
            // this dictionary is useful to skip movemnts that were already assigned or removed
            Dictionary<int, string> listOfMovesStates = new Dictionary<int, string>();
            // Init all moveStates as pending, since they were not yet assigned to an infrasturcture
            int moveID = 0;
            foreach (MoveTask move in listOfMoves)
            {
                listOfMovesStates[moveID] = "pending";
                moveID++;
            }

            // Key: movement ID (parent node), value: list of linked movements (IDs child nodes)  
            // 0:{1,3} (arrival movement) 1:{2}
            // Idea is to ontain multiple directed graphs -> dependency between linked movements 
            Dictionary<int, List<int>> MovementLinks = new Dictionary<int, List<int>>();

            // for(int i=0; i<listOfMoves.Count; i++)
            // {
            //     MovementLinks[i] = new List<int>();
            // }

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
            bool revisit = false;
            int revisitIndex = 0;

            while (ok != 0)
            {
               

                // If the movement has alredy been assigned to an infrastructure or it was removed (it was linked to another move)
                // it is not necessary to be studied 
                while (true)
                {
                    if(listOfMovesStates[moveIndex] == "pending" || (moveIndex ==  listOfMovesStates.Count-1))
                    {
                        break;
                    }                  
                    else{
                        moveIndex++;
                    }
                }

                // for(int i=0; i < listOfMovesStates.Count; i++)
                // {
                //     if(listOfMovesStates[i] == "pending")
                //     {
                //         moveIndex = i;
                //         Console.WriteLine("Here");
                //         break;
                //     }
                //     else{
                //         ok = 0;
                //     }

                // }

                var currentMove = listOfMoves[moveIndex];
                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove); // used by the movement


                if ((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveId, revisit) != true) && (revisit == false))
                {
                    // No conflict occured
                    foreach (ulong infraID in IDListOfInfraUsed)
                    {
                        InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                    }
                    // The given movement is assigned to an infrastructure
                    listOfMovesStates[moveIndex] = "assigned";
                }
                else if((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveId, revisit) == true) && (revisit == false))
                {
                    listOfMovesStates[moveIndex] = "pending";

                }
                else if (revisit == true)
                {

                    if (InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveId, revisit) == true)
                    {
                        // 1st: link movements -> conflictingMoveId is now linked with the moveIndex (current move id)
                        // Console.WriteLine($"Remove {conflictingMoveId} link with {moveIndex}");
                        LinkMovmentsByID(MovementLinks, conflictingMoveId, moveIndex);


                        MergeMovements(InfraOccupiedByMoves, InfraOccupiedByMovesID, DictOfInfrastrucure, moveIndex);

                        // 2nd: remove the conflicting movement that was previously assigned -> InfraOccupiedByMovesID 
                        // be set to 999 for the move that once has alredy been assigned and now it is in conflict 

                        RemoveAssignedMoveFromInfrastructureByID(listOfMoves, InfraOccupiedByMoves, InfraOccupiedByMovesID, DictOfInfrastrucure, conflictingMoveId);
                        listOfMovesStates[conflictingMoveId] = "removed";

                        // Assign current movement to the required infrastructure
                        foreach (ulong infraID in IDListOfInfraUsed)
                        {
                            InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                            InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                        }
                        // The given movement is assigned to an infrastructure
                        listOfMovesStates[moveIndex] = "assigned";
                        revisit = false;
                        // var conflictingMove = listOfMoves[conflictingMoveId];

                        // List<ulong> IDListOfInfraUsedByConflictingMove = GetIDListOfInfraUsed(conflictingMove);

                        // foreach (ulong infraID in IDListOfInfraUsedByConflictingMove)
                        // {
                        //     InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = null;
                        //     InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = 999;
                        // }
                    }
                    else
                    {
                        // No conflict occured
                        foreach (ulong infraID in IDListOfInfraUsed)
                        {
                            InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                            InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                        }
                        // The given movement is assigned to an infrastructure
                        listOfMovesStates[moveIndex] = "assigned";
                        revisit = true;

                    }




                    // Conflict occured
                    // TODO: Replace the conflictingMoveId with in the list by 999 and do the same steps like in no conflict occured step
                    //  Console.WriteLine($" ****** ******* ***** **** **** Conflicting ID: {conflictingMoveId}  ****** ******* ***** **** ****");
                }
                else if ((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref conflictingMoveId, revisit) == true))
                {
                    Console.WriteLine($" ****** ******* ***** **** **** Conflicting ID: {conflictingMoveId} with move {moveIndex}  ****** ******* ***** **** ****");

                }


                moveIndex++;

                if (moveIndex >= listOfMoves.Count)
                {
                    // A reiteration of listOfMoves has to happend, to see which moves assigned to a given
                    // infrastructure must be replaced
                    moveIndex = 0;
                    revisit = true;
                    revisitIndex++;

                    if (Test)
                    {
                        Console.WriteLine("-----------------------------------------------------------");

                        Console.WriteLine($"------ Infrastructure - Move - Revisit {revisitIndex} -----");

                        Console.WriteLine("-----------------------------------------------------------");

                        foreach (KeyValuePair<Infrastructure, int> pairInraMove in InfraOccupiedByMovesID)
                        {
                            Console.WriteLine($"Inrastrucure : {pairInraMove.Key} - {pairInraMove.Value}");
                        }
                        DisplayPartialResults(MovementLinks);

                        int move_ID = 0;
                        Console.WriteLine("\n");
                        foreach (MoveTask move in listOfMoves)
                        {
                            if(move_ID < listOfMoves.Count)
                            {
                                Console.Write($"State {move_ID} - {listOfMovesStates[move_ID]}");
                            }
                            move_ID++;
                            
                        }
                        // foreach (KeyValuePair<int, string> pair in listOfMovesStates)
                        // {
                        //     if (pair.Value == "assigned")
                        //     {
                        //         Console.WriteLine($"Move: {pair.Key}");
                        //     }
                        // }

                    }
                }

                // if (Test)
                // {
                //     if (revisitIndex > 7)
                //         ok = 0;
                // }

                // When all the movements are in "pending" sate the POS creation is done
                if (listOfMovesStates.Values.Any(state => state == "pending") == false)
                {
                    ok = 0;

                }

            }


        }

        public void DisplayPartialResults(Dictionary<int, List<int>> MovementLinks)
        {

            foreach (KeyValuePair<int, List<int>> pair in MovementLinks)
            {






                if (pair.Value.Count != 0)
                {
                    string movesStr = "";

                    if (pair.Value.Count == 1)
                    {
                        Console.Write($" Move:{pair.Key} ----> Move: {pair.Value[0]} ----> ");

                    }
                    else
                    {
                        foreach (int id in pair.Value)
                        {
                            movesStr = movesStr + id + " , ";
                        }
                        Console.Write($" Move:{pair.Key} ----> Move: {movesStr} ----> ");

                    }





                }
                else if (pair.Value.Count == 0)
                {
                    Console.Write($"Move:{pair.Key}");
                    Console.Write("\n");


                }
            }
        }

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




            MoveTask move = this.First;
            int i = 0;
            while (move != null)
            {
                Console.WriteLine($"Move: {i}");
                Console.WriteLine($"From : {move.FromTrack} -> To : {move.ToTrack}");

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


                // if (move.TaskType == MoveTaskType.Departure)
                // {
                //     Console.WriteLine("Routing:");
                //     // var routes = ((DepartureRoutingTask)move).GetRoutes();

                //     // foreach (Route route in routes)
                //     // {
                //     //     Console.WriteLine($"{route}");
                //     // }
                //     Console.WriteLine($"{(DepartureRoutingTask)move}");
                // }   

                // var departure = move as DepartureRoutingTask;
                // if (departure != null)
                // {
                //     Console.WriteLine("Routing:");

                //     var routes = departure.routes;

                //     Console.WriteLine($"{departure.routes}");
                // }


                i++;
                move = move.NextMove;
            }
        }

        public void DisplayMovements()
        {
            // TODO   
        }
        public void EvaluatePOS()
        {
            // TODO
        }
    }

    // class POSMoveTask : MoveTask
    // {
    //     public POSMoveTask(Trains.ShuntTrain train, MoveTaskType tasktype) : base(train, tasktype) {}

    //     public override MoveTask DeepCopy()
    //     {
    //         return new POSMoveTask(this.Train, this.TaskType);
    //     }


    //     public override bool IsParkingSkipped(Trains.ShuntTrain train){
    //         return true;
    //     }
    //     public override ParkingTask GetSkippedParking(Trains.ShuntTrain train);
    //     public override RoutingTask GetRouteToSkippedParking(Trains.ShuntTrain train);

    //     public override void SkipParking(ParkingTask parking);
    //     public override void UnskipParking(Trains.ShuntTrain train);
    // }

}