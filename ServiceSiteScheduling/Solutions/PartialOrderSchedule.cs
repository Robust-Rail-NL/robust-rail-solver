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


        public bool InfraConflict(Dictionary<Infrastructure, int> InfraOccupiedByMovesID, List<ulong> IDListOfInfraUsed, int moveID, ref int newMoveId, bool revisit)
        {

            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            bool ConflictOccured = false;

            foreach (ulong id in IDListOfInfraUsed)
            {
                if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != moveID && InfraOccupiedByMovesID[DictOfInfrastrucure[id]] == 999)
                {
                    ConflictOccured = false;
                    newMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                }
                else if (InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != moveID && InfraOccupiedByMovesID[DictOfInfrastrucure[id]] != 999 && revisit == true)
                {
                    ConflictOccured = true;
                    newMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                }
                else
                {
                    newMoveId = InfraOccupiedByMovesID[DictOfInfrastrucure[id]];
                    return true;
                }
            }


            return ConflictOccured;
        }


        public void UpdatePOS()
        {

            List<MoveTask> listOfMoves = this.ListOfMoves;
            Dictionary<Infrastructure, MoveTask> InfraOccupiedByMoves = new Dictionary<Infrastructure, MoveTask>();

            Dictionary<Infrastructure, int> InfraOccupiedByMovesID = new Dictionary<Infrastructure, int>();

            Dictionary<ulong, Infrastructure> DictOfInfrastrucure = this.DictOfInrastructure;

            // Init dictionary for infrastructures occupied by moves

            bool Test = true;

            foreach (KeyValuePair<ulong, Infrastructure> infra in DictOfInfrastrucure)
            {
                InfraOccupiedByMoves[infra.Value] = null;
                InfraOccupiedByMovesID[infra.Value] = 999;
            }


            int ok = 1;
            int moveIndex = 0;
            int newMoveId = 0;
            bool revisit = false;

            while (ok != 0)
            {
                var currentMove = listOfMoves[moveIndex];
                List<ulong> IDListOfInfraUsed = GetIDListOfInfraUsed(currentMove); // used by the movement



                if ((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref newMoveId , revisit) != true) && (revisit == false))
                {
                    // No conflict occured
                    foreach (ulong infraID in IDListOfInfraUsed)
                    {

                        InfraOccupiedByMoves[DictOfInfrastrucure[infraID]] = currentMove;
                        InfraOccupiedByMovesID[DictOfInfrastrucure[infraID]] = moveIndex;
                    }
                }
                else if((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref newMoveId , revisit) == true) && (revisit == true))
                {
                    // Conflict occured
                    // TODO: Replace the newMoveId with in the list by 999 and do the same steps like in no conflict occured step
                   //  Console.WriteLine($" ****** ******* ***** **** **** Conflicting ID: {newMoveId}  ****** ******* ***** **** ****");
                }
                else if((InfraConflict(InfraOccupiedByMovesID, IDListOfInfraUsed, moveIndex, ref newMoveId , revisit) == true))
                {
                    Console.WriteLine($" ****** ******* ***** **** **** Conflicting ID: {newMoveId} with move {moveIndex}  ****** ******* ***** **** ****");

                }
     

                moveIndex++;

                if (Test)
                {
                    if (moveIndex > 2)
                        ok = 0;
                }
            }

            if (Test)
            {
                Console.WriteLine("----------------------------------");

                Console.WriteLine("------ Infrastructure - Move -----");

                Console.WriteLine("----------------------------------");

                foreach (KeyValuePair<Infrastructure, int> pairInraMove in InfraOccupiedByMovesID)
                {
                    Console.WriteLine($"Inrastrucure : {pairInraMove.Key} - {pairInraMove.Value}");
                }

                //  A side 42 Sein70 --> 25 Spoor906a (0 B) -->  B side 58 Wissel963 --> A side 57 Wissel961 --> 39 Spoor52 (473 Both) --> B side 70 Engels974975 


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