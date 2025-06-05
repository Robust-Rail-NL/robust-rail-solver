using ServiceSiteScheduling.Solutions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ServiceSiteScheduling.Utilities;
using ServiceSiteScheduling.Tasks;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.Protobuf;
using AlgoIface;

namespace ServiceSiteScheduling.LocalSearch
{
    class SimulatedAnnealing
    {
        Random random;
        public PlanGraph Graph { get; private set; }

        public SimulatedAnnealing(Random random)
        {
            var graph = Initial.SimpleHeuristic.Construct(random);
            graph.Cost = graph.ComputeModel();

            this.Graph = graph;
            this.random = random;
        }

        public SimulatedAnnealing(Random random, PlanGraph graph)
        {
            this.Graph = graph;
            this.random = random;
        }

        public void showAllPrevious(MoveTask gMove)
        {
            List<TrackTask> previousTasks = new List<TrackTask>();


            if (gMove.AllPrevious.Count == 0)
            {
                Console.WriteLine("AllPrevious list is empty");
            }
            else
            {
                Console.WriteLine("AllPrevious list is not empty");

                foreach (TrackTask task in gMove.AllPrevious)
                {
                    Console.WriteLine(task);
                }
            }

            Console.WriteLine("**** PREVIOUS TrackTask****");
            try
            {
                foreach (TrackTask task in gMove.AllPrevious)
                {
                    task.Previous.FindAllPrevious(t => t == task, previousTasks);

                }
                if (previousTasks.Count == 0)
                {
                    Console.WriteLine("Tasks list is empty");
                }
                else
                {
                    Console.WriteLine("Tasks list is not empty");

                    foreach (TrackTask task in previousTasks)
                    {
                        Console.WriteLine(task);
                    }

                }
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        public void showAllNex(MoveTask gMove)
        {
            List<TrackTask> nextTasks = new List<TrackTask>();


            if (gMove.AllNext.Count == 0)
            {
                Console.WriteLine("AllNext list is empty");
            }
            else
            {
                Console.WriteLine("AllNext list is not empty");

                foreach (TrackTask task in gMove.AllNext)
                {
                    Console.WriteLine(task);
                }
            }

            Console.WriteLine("**** NEXT TrackTask****");

            foreach (TrackTask task in gMove.AllNext)
            {
                task.Previous.FindAllNext(t => t == task, nextTasks);
            }

            if (nextTasks.Count == 0)
            {
                Console.WriteLine("Tasks list is empty");
            }
            else
            {
                Console.WriteLine("Tasks list is not empty");

                foreach (TrackTask task in nextTasks)
                {
                    Console.WriteLine(task);
                }

            }
        }

        public void diplayGraphDetails(string specifyMoveType, string specifyListType)
        {
            Console.WriteLine("****************************************");
            Console.WriteLine("*              Graph Details           *");
            Console.WriteLine("****************************************");

            if (specifyMoveType == "Last")
            {
                Console.WriteLine("**** LAST MoveTask****");

                MoveTask gLast = this.Graph.Last;


                if (specifyListType == "Previous")
                {
                    showAllPrevious(gLast);
                }
                else if (specifyListType == "Next")
                {
                    showAllNex(gLast);

                }

            }
            else if (specifyMoveType == "First")
            {
                Console.WriteLine("**** FIRST MoveTask****");

                MoveTask gFirst = this.Graph.First;

                if (specifyListType == "Previous")
                {
                    showAllPrevious(gFirst);
                }
                else if (specifyListType == "Next")
                {
                    showAllNex(gFirst);
                }

            }
            else if (specifyMoveType == "All")
            {
                Console.WriteLine("**** All MoveTask****");

                MoveTask gMove = this.Graph.First;
                int i = 0;

                while (gMove != null)
                {

                    Console.WriteLine($"MoveTask =====> {i}");
                    showAllPrevious(gMove);

                    showAllNex(gMove);

                    i++;
                    gMove = gMove.NextMove;

                }


            }
        }

        // Core function to find a solution graph => schedule plan
        // @maxduration: Maximum duration of the serach in seconds (e.g., Time.Hour is 3600 seconds)
        // @stopWhenFeasible: stop serach when it is feaseable (bool)
        // @iterations: maximum iterations in the searching algorithm if it is achieved the search ends
        // @t: the T parameter in the equation P = exp([cost(a') - cost(b')]/T), where e T is a control parameter that will be decreased 
        // during the search to accept less deterioration in solution quality later on in the process
        // @a: the rate of the decrease of T (e.g., a=0.97 -> 3% of decrease every time q iteration has been achieved)
        // @q: number of iterations until the next decrease of T
        // @reset: the current solution should be improved until that number of iteration if this number is hit, the current solution 
        // cannot be improved -> the current solution is reverted to the original solution
        // @bias: Restricted probability (e.g., 0.4)
        // @suppressConsoleOutput: enables extra logs
        // @intensifyOnImprovement: enables further improvments
        public void Run(Time maxduration, bool stopWhenFeasible, int iterations, double t, double a, int q, int reset, double bias = 0.4, bool suppressConsoleOutput = false, bool intensifyOnImprovement = false, bool allowDelayedPlans = false, string plan_path = "default_temp_path.json")
        {
            double T = t, alpha = a;
            int Q = q, iterationsUntilReset = reset;

            List<LocalSearchMove> moves = new List<LocalSearchMove>();
            moves.Add(new IdentityMove(this.Graph));
            int noimprovement = 0, iteration = 0, neighbors = 0;
            bool previousimproved = false;
            SolutionCost bestcost = this.Graph.ComputeModel();
            SolutionCost current = bestcost;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int localsearchmovetypes = 9;

            while (true)
            {

                bool fullcost = this.Graph.Cost.IsFeasible;

                List<LocalSearchMove> selectedmoves = new List<LocalSearchMove>();
                double movetype = random.NextDouble();
                double restricted = random.NextDouble();
                double restrictedprobability = bias;
                if (movetype < 1.2 / localsearchmovetypes)
                {
                    var currentmoves = ServiceMachineSwitchMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.Selected.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Selected.Train.UnitBits));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 2.4 / localsearchmovetypes)
                {
                    var currentmoves = ServiceTrainOrderMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 3.2 / localsearchmovetypes)
                {
                    var currentmoves = MatchingSwapMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Matching.GetShuntTrain(move.First.Train).UnitBits) ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Second.Matching.GetShuntTrain(move.Second.Train).UnitBits));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 4.4 / localsearchmovetypes)
                {
                    var currentmoves = ParkingSwitchMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.Track.Index] ||
                                move.RelatedTasks.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits)));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 5.7 / localsearchmovetypes)
                {
                    var currentmoves = ParkingSwapMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.ParkingFirst.First().Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.ParkingSecond.First().Track.Index] ||
                                move.ParkingFirst.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits)) ||
                                move.ParkingSecond.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits)));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 6.8 / localsearchmovetypes)
                {
                    var currentmoves = ServiceMachineOrderMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits) ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Second.Train.UnitBits));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 7.9 / localsearchmovetypes)
                {
                    var currentmoves = RoutingMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                        {
                            var shift = move as RoutingShiftMove;
                            if (shift != null)
                            {
                                return
                                    this.Graph.Cost.ProblemTracks[shift.Selected.ToTrack.Index] ||
                                    shift.Selected.AllNext.Any(task => this.Graph.Cost.ProblemTracks[task.Track.Index]) ||
                                    this.Graph.Cost.ProblemTrains.Intersects(shift.Selected.Train.UnitBits);
                            }
                            else
                            {
                                var merge = move as RoutingMergeMove;
                                return
                                    this.Graph.Cost.ProblemTracks[merge.To.ToTrack.Index] ||
                                    this.Graph.Cost.ProblemTrains.Intersects(merge.From.Train.UnitBits);
                            }
                        });
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 8.8 / localsearchmovetypes)
                {
                    var currentmoves = ServiceMachineSwapMove.GetMoves(this.Graph);
                    if (restricted < restrictedprobability)
                    {
                        var restrictedmoves = currentmoves.Where(move =>
                            this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                            this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                            this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits) ||
                            this.Graph.Cost.ProblemTrains.Intersects(move.Second.Train.UnitBits));
                        if (restrictedmoves.Count() > 0)
                            currentmoves = restrictedmoves.ToList();
                    }
                    selectedmoves.AddRange(currentmoves);
                }
                else if (movetype < 8.9 / localsearchmovetypes)
                {
                    var currentmoves = ParkingRoutingTemporaryMove.GetMoves(this.Graph);
                    selectedmoves.AddRange(currentmoves);
                }
                else
                {
                    var currentmoves = ParkingInsertMove.GetMoves(this.Graph);
                    selectedmoves.AddRange(currentmoves);
                }

                if (selectedmoves.Count > 0)
                {
                    for (int i = 0; i < Math.Min(10, selectedmoves.Count); i++)
                    {
                        int selectedindex = random.Next(selectedmoves.Count - i);
                        var move = selectedmoves[selectedindex];
                        this.Graph.Cost = move.Execute();
                        neighbors++;

                        if (move.Cost.Cost(fullcost) < current.Cost(fullcost) || random.NextDouble() < Math.Exp((current.Cost(fullcost) - move.Cost.Cost(fullcost)) / T))
                        {
                            moves.Add(move);
                            current = move.Cost;

                            if (move.Cost.Cost(fullcost) < bestcost.Cost(fullcost))
                            {
                                bestcost = move.Cost;
                                noimprovement = 0;
                                if (!suppressConsoleOutput)
                                    Console.WriteLine(move.Cost);
                                previousimproved = true;
                            }
                            break;
                        }
                        else
                        {
                            this.Graph.Cost = move.Revert();
                            selectedmoves[selectedindex] = selectedmoves[selectedmoves.Count - i - 1];
                        }
                    }
                }

                if (intensifyOnImprovement && (previousimproved || random.NextDouble() < 0.001))
                {
                    while (random.NextDouble() > 0.5)
                    {
                        List<LocalSearchMove> currentmoves = new List<LocalSearchMove>();

                        var parkingroutingtemporarymoves = ParkingRoutingTemporaryMove.GetMoves(this.Graph);
                        currentmoves.AddRange(parkingroutingtemporarymoves);
                        var servicemachineswapmoves = ServiceMachineSwapMove.GetMoves(this.Graph);
                        currentmoves.AddRange(servicemachineswapmoves);
                        var servicemachineordermoves = ServiceMachineOrderMove.GetMoves(this.Graph);
                        currentmoves.AddRange(servicemachineordermoves);
                        var servicemachineswitchmoves = ServiceMachineSwitchMove.GetMoves(this.Graph);
                        currentmoves.AddRange(servicemachineswitchmoves);
                        var servicetrainordermoves = ServiceTrainOrderMove.GetMoves(this.Graph);
                        currentmoves.AddRange(servicetrainordermoves);
                        var matchingswapmoves = MatchingSwapMove.GetMoves(this.Graph);
                        currentmoves.AddRange(matchingswapmoves);
                        var parkingshiftmoves = ParkingSwitchMove.GetMoves(this.Graph);
                        currentmoves.AddRange(parkingshiftmoves);
                        var parkingswapmoves = ParkingSwapMove.GetMoves(this.Graph);
                        currentmoves.AddRange(parkingswapmoves);
                        var routingshiftmoves = RoutingMove.GetMoves(this.Graph);
                        currentmoves.AddRange(routingshiftmoves);
                        var parkinginsertmoves = ParkingInsertMove.GetMoves(this.Graph);
                        currentmoves.AddRange(parkinginsertmoves);

                        foreach (var move in currentmoves)
                        {
                            move.Execute();
                            move.Revert();
                            neighbors++;
                        }

                        LocalSearchMove selected = currentmoves.Min();

                        if (selected != null && selected.Cost.Cost(fullcost) < current.Cost(fullcost))
                        {
                            moves.Add(selected);
                            current = selected.Cost;
                            this.Graph.Cost = selected.Execute();

                            if (current.Cost(fullcost) < bestcost.Cost(fullcost))
                            {
                                bestcost = current;
                                noimprovement = 0;
                                if (!suppressConsoleOutput)
                                    Console.WriteLine(selected.Cost);
                            }
                        }
                        else
                            Console.WriteLine("++++++++++++++++++++++++++++ BREAK 3 ++++++++++++++++++++++");
                        break;
                    }
                }
                previousimproved = false;

                noimprovement++;
                if (noimprovement >= iterationsUntilReset || current.Cost(fullcost) > 5 * bestcost.Cost(fullcost))
                {
                    this.Revert(moves, fullcost);
                    current = bestcost;
                    noimprovement = 0;
                }
                Plan plan_pb = this.Graph.GenerateOutputPB();

                string jsonPlan = JsonFormatter.Default.Format(plan_pb);

                string directoryPath = Path.GetDirectoryName(plan_path) + "_temp_SA";

                if (!Directory.Exists(directoryPath) && directoryPath != null)
                {
                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Directory created: {directoryPath}");
                }

                File.WriteAllText(Path.Join(directoryPath, $"temp_plan_{iteration}.json"), jsonPlan);
                File.WriteAllText(Path.Join(directoryPath, $"temp_plan_{iteration}.txt"), this.Graph.OutputTrainUnitSchedule());



                if (iteration >= iterations || stopwatch.ElapsedMilliseconds > 1000 * maxduration || (stopWhenFeasible && (this.Graph.Cost.IsFeasible || this.Graph.Cost.IsDelayFeasible && allowDelayedPlans)))
                {
                    Console.WriteLine("++++++++++++++++++++++++++++ BREAK 2 ++++++++++++++++++++++");
                    Console.WriteLine($" iteration {iteration}, graph cost is feasible {this.Graph.Cost.IsFeasible}");
                    break;
                }

                if (++iteration % Q == 0)
                    T *= alpha;

                if (iteration % 1000 == 0 && !suppressConsoleOutput)
                    Console.WriteLine($"{iteration} {T}");
            }

            stopwatch.Stop();

            this.Revert(moves, this.Graph.Cost.IsFeasible);
            if (!suppressConsoleOutput)
            {
                Console.WriteLine("-----------------------");
                Console.WriteLine($"{this.Graph.ComputeModel()}");
                Console.WriteLine("-----------------------");
                Console.WriteLine($"Finished after {(stopwatch.ElapsedMilliseconds / (double)1000).ToString("N2")} seconds");
                Console.WriteLine($"Neighbors visited = {neighbors}");
            }
        }

        protected void Revert(List<LocalSearchMove> moves, bool fullcost, SolutionCost best = null)
        {
            int min = moves.MinIndex(move => move.Cost.Cost(fullcost));
            if (best != null && best.Cost(fullcost) != moves[min].Cost.Cost(fullcost))
                throw new ArgumentException($"{best} should be {moves[min].Cost}");

            for (int i = moves.Count - 1; i > min; i--)
            {
                this.Graph.Cost = moves[i].Revert();
                if (this.Graph.Cost.Cost(fullcost) != moves[i - 1].Cost.Cost(fullcost))
                    throw new ArgumentException($"{this.Graph.Cost.Cost(fullcost)} should be {moves[i - 1].Cost.Cost(fullcost)}......");
            }
            moves.RemoveRange(min + 1, moves.Count - min - 1);

            if (best != null && best.Cost(fullcost) != this.Graph.Cost.Cost(fullcost))
                throw new ArgumentException($"{best} should be {moves[min].Cost}");
        }
    }
}
