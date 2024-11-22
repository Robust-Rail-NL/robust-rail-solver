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

        // Returns list of the IDs of the Infrastructure used by a movement
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

        public void UpdatePOS()
        {

            // Index of the list is the ID assigned to a move
            List<MoveTask> listOfMoves = this.ListOfMoves;

                  
               
            // Dictionary with move IDs as Key, and value as List of all the linked moves
            // Key: movement ID (parent move) this move was conflicting since it previously used the same infrastructure as the
            // the current move, Value: list of linked movements (IDs child moves)
            // 0:{1,3} OR 1:{2,3}
            // Idea is to onbtain multiple directed graphs -> dependency between linked movements
            Dictionary<int, List<int>> MovementLinks = new Dictionary<int, List<int>>();

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
                    Console.WriteLine($"-|Move {moveIndex} - is assigned|");


                }
                else
                {

                    foreach (int MoveId in conflictingMoveIds)
                    {
                        Console.WriteLine($"-|Conflicting move id {MoveId}|");
                        
                        // 1st: link movements -> conflictingMoveId is now linked with the moveIndex (current move id)

                        LinkMovmentsByID(MovementLinks, MoveId, moveIndex);

                        // 2nd Assign current movement to the required infrastructure
                        foreach (ulong infraID in IDListOfInfraUsed)
                        {
                            InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                            InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                        }

                    }
                    Console.WriteLine($"|Move {moveIndex} - is assigned|");



                }
                moveIndex++;
                if(moveIndex == listOfMoves.Count)
                    ok = 0;
            }

            // Show connections per Move
            foreach(KeyValuePair<int, List<int>> pair in MovementLinks)
            {
                Console.Write($"Move{pair.Key} -->");
                foreach(int element in pair.Value)
                {
                    Console.Write($"Move:{element} ");
                    
                }
                Console.Write("\n");
            }
            // DisplayPartialResults(MovementLinks);


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

    // if (conflictingMoveId > moveIndex)
    // {
    //     if (listOfMovesStates[moveIndex] != "removed")
    //     {

    //         RemoveAssignedMoveFromInfrastructureByID(listOfMoves, InfraOccupiedByMoves, InfraOccupiedByMovesID, DictOfInfrastrucure, conflictingMoveId);
    //         listOfMovesStates[conflictingMoveId] = "pending";

    //         foreach (ulong infraID in IDListOfInfraUsed)
    //         {
    //             InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
    //             InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
    //         }
    //         // The given movement is assigned to an infrastructure

    //         listOfMovesStates[moveIndex] = "assigned";
    //         Console.WriteLine($"|Move {moveIndex} - is assigned|");
    //     }

    // }


}