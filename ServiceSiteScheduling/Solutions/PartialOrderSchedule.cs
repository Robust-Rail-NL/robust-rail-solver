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

        public PartialOrderSchedule(MoveTask first, ShuntTrainUnit[] shuntunits, ArrivalTask[] arrivals, DepartureTask[] departures)
        {
            this.First = first;
            this.ShuntUnits = shuntunits;
            this.ArrivalTasks = arrivals;
            this.DepartureTasks = departures;
        }

        public void UpdatePOS(MoveTask first)
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
        }
        public void DisplayInfrastructure()
        {
            MoveTask move = this.First;
            int i = 0;
            while (move != null)
            {
                Console.WriteLine($"Move: {i}");
                Console.WriteLine($"From : {move.FromTrack} -> To : {move.ToTrack}");

                var routing = move as RoutingTask;

                if (routing != null)
                {
                    Console.WriteLine("Routing in tracks:");
                    var tracks = routing.Route.Tracks;
                    var lastTrack = tracks.Last(); 

                    // TODO: display more infrastructure

                    foreach (Track track in tracks)
                    {
                        if(track != lastTrack)
                        {
                            Console.Write($"{track} --> ");

                        }else 
                        {
                            Console.Write($"{track}");
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