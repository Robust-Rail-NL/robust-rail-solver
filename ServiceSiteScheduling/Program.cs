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
using Microsoft.VisualBasic;
using System.Text.Json;

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
            // TS();
            Test_Location_Scenario_Parsing("./database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json", "./database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json");
            Console.WriteLine("***************** Test TS() *****************");
            // TS();

            // Test();
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
            // ProblemInstance.Current = ProblemInstance.Parse("./database/TUSS-Instance-Generator/location.dat", "./database/TUSS-Instance-Generator/scenario.dat");



            ProblemInstance.Current = ProblemInstance.ParseJson("./database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json", "./database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json");

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
                using (var input = File.OpenRead("database/TUSS-Instance-Generator/location.dat"))
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
            using (var inputSecenario = File.OpenRead("database/Kleine_binckhorst/scenario.dat"))
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
        static void Test_Location_Scenario_Parsing(string location_path, string scenario_path)
        {
            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);

            // ProblemInstance.Current = ProblemInstance.ParseJson("./database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json", "./database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json");

            try
            {



                var location_TrackParts = ProblemInstance.Current.InterfaceLocation.TrackParts;

                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed location is null.");

                }

                // foreach (AlgoIface.TrackPart trackType in location_TrackParts)
                // {
                //     Console.WriteLine("ID : " + trackType.Id);
                // }

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceLocation);

                string json_original = ProblemInstance.ParseJsonToString(location_path);

                // Test the parsing - if the the json file converted from the protobuf object
                // is the same as the original input location, then the parsing was successful
                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Location file parsing was successful !");
                    Console.WriteLine("JSON: \n " + json_parsed);
                }
                else
                {
                    Console.WriteLine("The Location file parsing was not successful! ");
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

            // Scenario
            try
            {

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceScenario);

                var scenario_in = ProblemInstance.Current.InterfaceScenario.In;
                var scenario_out = ProblemInstance.Current.InterfaceScenario.Out;

                if (scenario_in == null)
                {
                    // Scenario.In is probably null or not well formated
                    throw new NullReferenceException("Parsed scenario in filed is null.");
                }


                if (scenario_out == null)
                {
                    // Scenario.Out is probably null or not well formated
                    throw new NullReferenceException("Parsed scenario out field is null.");
                }

                string json_original = ProblemInstance.ParseJsonToString(scenario_path);

                // Test the parsing - if the the json file converted from the protobuf object
                // is the same as the original input scenario, then the parsing was successful
                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Scenario file parsing was successful !");
                    Console.WriteLine("JSON: \n " + json_parsed);
                }
                else
                {
                    Console.WriteLine("The Location file parsing was not successful! ");
                }
                
                Console.WriteLine("Scenario details: ");
                Console.WriteLine("---- Incoming Trains ----");
                List<AlgoIface.IncomingTrain> incomingTrains = new List<AlgoIface.IncomingTrain>(scenario_in.Trains);
                foreach (AlgoIface.IncomingTrain train in scenario_in.Trains)
                {
                    incomingTrains.Add(train);
                }

                Console.WriteLine("---- Outgoing Trains ----");
                foreach (AlgoIface.IncomingTrain train in incomingTrains)
                {
                    Console.WriteLine("Parcking track " + train.FirstParkingTrackPart + " for train (id) " + train.Id);
                }

                List<AlgoIface.TrainRequest> outgoingTrains = new List<AlgoIface.TrainRequest>(scenario_out.TrainRequests);
                foreach(AlgoIface.TrainRequest train in scenario_out.TrainRequests)
                {
                    outgoingTrains.Add(train);
                }

                foreach(AlgoIface.TrainRequest train in outgoingTrains)
                {
                    Console.WriteLine("Parcking track " + train.LastParkingTrackPart + " for train (id) " + train.DisplayName);

                }


            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
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
