using ServiceSiteScheduling.Utilities;
using System;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Google.Protobuf;
using ServiceSiteScheduling.Matching;
using System.Runtime.CompilerServices;
using Google.Protobuf.Collections;
using System.Data.Common;
using AlgoIface;

namespace ServiceSiteScheduling
{
    class Program
    {
        static void Main(string[] args)
        {
            /*AlgoIface.Plan plan = null;
            using (StreamReader reader = new StreamReader("test1"))
                plan = AlgoIface.Plan.Parser.Parse
                From(reader.BaseStream);*/
            // RunForStudents();
            TS();
            //Test();
        }

        static void TS()
        {
            // Console.WriteLine("seed?");
            // var line = Console.ReadLine();
            // int seed = line == string.Empty ? 0 : int.Parse(line);
            // Random random = new Random(seed);
            Random random = new Random();
            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;

            // ProblemInstance.Current = ProblemInstance.Parse("database/location.dat", "database/scenario.dat");
            // ProblemInstance.Current = ProblemInstance.Parse("database/other/location-10200.dat", "database/other/scenario-10200.dat");
            
            ProblemInstance.Current = ProblemInstance.ParseJson("database/fix/location-10200.json", "database/fix/scenario-10200.json");

            int solved = 0;
            for (int i = 0; i < 1; i++)
            {
                Console.WriteLine(i);
                LocalSearch.TabuSearch ts = new LocalSearch.TabuSearch(random);
                ts.Run(40, 100, 16, 0.5);
                LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts.Graph);
                sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);
                //ts = new LocalSearch.TabuSearch(random, sa.Graph);
                //ts.Run(100, 100, 16);
                Console.WriteLine("--------------------------");
                Console.WriteLine(" Output Movement Schedule ");
                Console.WriteLine("--------------------------");

                sa.Graph.OutputMovementSchedule();
                Console.WriteLine("--------------------------");
                Console.WriteLine("");
                

                Console.WriteLine("----------------------------");
                Console.WriteLine(" Output Train Unit Schedule ");
                Console.WriteLine("----------------------------");
                Console.WriteLine("");
                sa.Graph.OutputTrainUnitSchedule();
                Console.WriteLine("----------------------------");

                Console.WriteLine("");
                Console.WriteLine("------------------------------");
                Console.WriteLine(" Output Constraint Violations ");
                Console.WriteLine("------------------------------");

                sa.Graph.OutputConstraintViolations();
                Console.WriteLine(sa.Graph.Cost);
                Console.WriteLine("--------------------------");

                if (sa.Graph.Cost.ArrivalDelays + sa.Graph.Cost.DepartureDelays + sa.Graph.Cost.TrackLengthViolations + sa.Graph.Cost.Crossings + sa.Graph.Cost.CombineOnDepartureTrack <= 2)
                {
                    solved++;
                    //sa.Graph.GenerateOutput($"plan-{seed}-{i}.dat");
                }

                if (sa.Graph.Cost.BaseCost < (best?.BaseCost ?? double.PositiveInfinity))
                {
                    best = sa.Graph.Cost;
                    graph = sa.Graph;
                }
                Console.WriteLine($"solved: {solved}");
                Console.WriteLine($"best = {best}");
                Console.WriteLine("------------------------------");
                Console.WriteLine($"Generate JSON format plan");
                Console.WriteLine("------------------------------");
                
                Plan plan_pb = sa.Graph.GenerateOutputPB();

                string jsonPlan = JsonFormatter.Default.Format(plan_pb);
                Console.WriteLine(jsonPlan);

                // Save plan as json
                string filePath = "./database/TUSS-Instance-Generator/plan.json"; 
                File.WriteAllText(filePath, jsonPlan);

                Console.WriteLine("----------------------------------------------------------------------");


                sa.Graph.DisplayMovements();

                // Plan plan_pb_extended = sa.Graph.GenerateOutputPB_extended();
                // string jsonPlan_extended = JsonFormatter.Default.Format(plan_pb_extended);
                // Console.WriteLine(jsonPlan_extended);



                sa.Graph.Clear();

            }

            Console.WriteLine("------------ OVERALL BEST --------------");
            Console.WriteLine(best);

            Console.ReadLine();
        }

        static void RunForStudents()
        {
            try
            {
                var input = new StreamReader("config.yaml");
                var deserializer = new Deserializer(namingConvention: new CamelCaseNamingConvention());
                Config config = deserializer.Deserialize<Config>(input);

                Console.SetIn(new StreamReader(Console.OpenStandardInput(8192), Console.InputEncoding, false, 8192));
                config.OutputPath = Console.ReadLine();
                var location = AlgoIface.Location.Parser.ParseFrom(Convert.FromBase64String(Console.ReadLine()));
                var scenario = AlgoIface.Scenario.Parser.ParseFrom(Convert.FromBase64String(Console.ReadLine()));

                //config.OutputPath = "test.dat";
                //var location = "database/location.dat";
                //var scenario = "database/scenario.dat";

                try
                {
                    ProblemInstance.Current = ProblemInstance.Parse(location, scenario);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("error during parsing", e);
                }


                Random random = null;
                if (config.Seed == -1)
                    random = new Random();
                else
                    random = new Random(config.Seed);

                LocalSearch.TabuSearch ts = null;
                try
                {
                    ts = new LocalSearch.TabuSearch(random);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("error while constructing initial solution", e);
                }
                ts.Run(40, 100, 16, 0.5, true);
                LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts.Graph);
                sa.Run(config.MaxDuration == 0 ? 180 : config.MaxDuration, config.StopWhenFeasible, 250000, 12, 0.97, 2000, 2000, 0.2, true);

                sa.Graph.GenerateOutput(config.OutputPath);
                if (!sa.Graph.Cost.IsFeasible)
                {
                    Environment.ExitCode = 1;
                    Console.WriteLine($"The current solution is not feasible: {sa.Graph.Cost.ArrivalDelays} arrival delays, {sa.Graph.Cost.DepartureDelays} departure delays, {sa.Graph.Cost.Crossings} crossings, {sa.Graph.Cost.TrackLengthViolations} track length violations");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Environment.ExitCode = 2;
            }
        }

        static void Test()
        {
            try
            {

                // Location
                AlgoIface.Location location;
                using (var input = File.OpenRead("database/other/location-10200.dat"))
                    location = AlgoIface.Location.Parser.ParseFrom(input);

                Console.WriteLine("Location:");

                string json = JsonFormatter.Default.Format(location);

                Console.WriteLine("JSON: \n " + json);


                byte[] locationBytes = location.ToByteArray();
                Console.WriteLine("Location :" + locationBytes.Length);

                Console.WriteLine(Convert.ToBase64String(location.ToByteArray()));

                var location_TrackParts = location.TrackParts;

                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed message is null.");

                }

                foreach (AlgoIface.TrackPart trackType in location_TrackParts)
                {
                    Console.WriteLine("ID : " + trackType.Id);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

            // Scenario
            AlgoIface.Scenario scenario;
            using (var inputSecenario = File.OpenRead("database/other/scenario-10200.dat"))
            {
                try
                {
                    Console.WriteLine(" Length : " + inputSecenario.Length);


                    scenario = AlgoIface.Scenario.Parser.ParseFrom(inputSecenario);
                    Console.WriteLine("Scenario :");

                    byte[] scenarioBytes = scenario.ToByteArray();

                    Console.WriteLine(" Length : " + scenarioBytes.Length);


                    Console.WriteLine(Convert.ToBase64String(scenario.ToByteArray()));

                    if (scenario == null)
                    {
                        throw new NullReferenceException("Parsed message is null.");
                    }

                    string json = JsonFormatter.Default.Format(scenario);

                    Console.WriteLine("JSON: \n " + json);


                    var scenario_in = scenario.In;


                    // AlgoIface.ScenarioIn scenario_in = AlgoIface.ScenarioIn.Parser.ParseFrom(inputSecenario);

                    if (scenario_in == null)
                    {
                        throw new NullReferenceException("Parsed message is null.");
                    }

                    List<AlgoIface.IncomingTrain> incomingTrains = new List<AlgoIface.IncomingTrain>(scenario_in.Trains);
                    foreach (AlgoIface.IncomingTrain train in scenario_in.Trains)
                    {
                        incomingTrains.Add(train);
                    }

                    foreach (AlgoIface.IncomingTrain train in incomingTrains)
                    {
                        Console.WriteLine("Parcking track" + train.FirstParkingTrackPart + " for train (id) " + train.Id);
                    }

                }
                catch (Exception e)
                {
                    throw new ArgumentException("error during parsing", e);
                }




            }
        }



    }

    class Config
    {
        public int Seed { get; set; }
        public int MaxDuration { get; set; }
        public bool StopWhenFeasible { get; set; }
        public string OutputPath { get; set; }
    }
}
