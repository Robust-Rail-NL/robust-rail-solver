using ServiceSiteScheduling.Utilities;
using YamlDotNet.Serialization;
using Google.Protobuf;
using AlgoIface;
using System.Text.Json;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;


namespace ServiceSiteScheduling
{
    class Program
    {

        // Method: Run the program from a config file. This is the entry point of the application
        static void Main(string[] args)
        {

            if (args.Length != 0)
            {
                string config_file = "";
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--config="))
                    {
                        config_file = arg.Substring("--config=".Length);


                        if (!File.Exists(config_file))
                        {
                            Console.Error.WriteLine($"Error: Config file '{config_file}' not found.");
                            Environment.Exit(1);
                        }

                        string yaml = File.ReadAllText(config_file);

                        var deserializer = new Deserializer();
                        Config config = deserializer.Deserialize<Config>(new StringReader(yaml));
                        if (config.Mode == "Standard")
                        {
                            Console.WriteLine("***************** Test_Location_Scenario_Parsing() *****************");

                            Test_Location_Scenario_Parsing(config.LocationPath, config.ScenarioPath);

                            Console.WriteLine("***************** CreatePlan() *****************");
                            CreatePlan(config.LocationPath, config.ScenarioPath, config.PlanPath, config);

                        }
                        else if (config.Mode == "DeepLook")
                        {
                            Console.WriteLine("***************** Test_Location_Scenario_Parsing() *****************");

                            Test_Location_Scenario_Parsing(config.LocationPath, config.ScenarioPath);

                            Converter converter = new Converter(ProblemInstance.Current, config.DeepLook.PathScenarioEval);

                            if (converter.ConvertScenario())
                            {
                                Console.WriteLine("----------------------------------------------------------------------");
                                Console.WriteLine("Conversion done with success");
                                Console.WriteLine("----------------------------------------------------------------------");
                               

                                converter.StoreScenarioEvaluator("scenario_evaluator");

                            }

                            // Console.WriteLine("***************** CreatePlan() *****************");
                            // CreatePlan(config.LocationPath, config.ScenarioPath, config.PlanPath, config);

                            Call_Evaluator(config);
                        }
                        else
                        {
                            Console.WriteLine("Unknown parameter for Mode");
                        }

                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown --parameter name");
                        Environment.Exit(1);
                    }
                }
            }

            else
            {
                Test_Location_Scenario_Parsing("./database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json", "./database/TUSS-Instance-Generator/setting_A/scenario_solver.json");
                Console.WriteLine("***************** CreatePlan() *****************");
                CreatePlan("./database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json", "/database/TUSS-Instance-Generator/setting_A/scenario_solver.json", "./database/TUSS-Instance-Generator/plan.json");
            }



        }

        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        //          @config: service site scheduling config to creat the plan from
        // Output:  @plan_path: path to where the plan (.json) file will be written
        // Method: First it calls a Tabu Search method to find an initial plan (Graph) that is used by 
        //         a Simulated Annealing method to find the final schedle plan (Totally Ordered Graph)
        static void CreatePlan(string location_path, string scenario_path, string plan_path, Config config = null)
        {

            Random random = new Random();
            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;

            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);

            int solved = 0;
            for (int i = 0; i < 1; i++)
            {
                Console.WriteLine(i);
                LocalSearch.TabuSearch ts = new LocalSearch.TabuSearch(random);
                if (config != null)
                {
                    ts.Run(config.TabuSearch.Iterations, config.TabuSearch.IterationsUntilReset, config.TabuSearch.TabuListLength, config.TabuSearch.Bias, config.TabuSearch.SuppressConsoleOutput);
                }
                else
                {
                    ts.Run(40, 100, 16, 0.5);
                }
                LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts.Graph);
                if (config != null)
                {
                    sa.Run(new Time(config.SimulatedAnnealing.MaxDuration), config.SimulatedAnnealing.StopWhenFeasible, config.SimulatedAnnealing.IterationsUntilReset, config.SimulatedAnnealing.T, config.SimulatedAnnealing.A, config.SimulatedAnnealing.Q, config.SimulatedAnnealing.Reset, config.SimulatedAnnealing.Bias, config.SimulatedAnnealing.SuppressConsoleOutput, config.SimulatedAnnealing.IintensifyOnImprovement);
                }
                else
                {
                    sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);

                }

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

                // Console.WriteLine(jsonPlan);

                string directoryPath = Path.GetDirectoryName(plan_path);

                if (!Directory.Exists(directoryPath) && directoryPath != null)
                {

                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Directory created: {directoryPath}");
                }

                File.WriteAllText(plan_path, jsonPlan);

                Console.WriteLine("----------------------------------------------------------------------");


                sa.Graph.DisplayMovements();

                sa.Graph.Clear();

            }

            Console.WriteLine("------------ OVERALL BEST --------------");
            Console.WriteLine(best);

            // Console.ReadLine();
        }

        static void Test()
        {

            try
            {

                AlgoIface.Location location;
                using (var input = File.OpenRead("database/TUSS-Instance-Generator/location.json"))
                    location = AlgoIface.Location.Parser.ParseFrom(input);

                Console.WriteLine("Location:");

                string json = JsonFormatter.Default.Format(location);

                // Console.WriteLine("JSON: \n " + json);


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

        }

        static void Call_Evaluator(Config config)
        {
            Process process = new Process();
            process.StartInfo.FileName = config.DeepLook.Path;

            if (config.DeepLook.Mode == "EVAL")
            {
                process.StartInfo.Arguments = "--mode " + config.DeepLook.Mode + " --path_location " + config.DeepLook.PathLocation + " --path_scenario " + config.DeepLook.PathScenario + " --path_plan " + config.DeepLook.PathPlan + " --plan_type " + config.DeepLook.PlanType;
            }
            else if (config.DeepLook.Mode == "EVAL_AND_STORE")
            {
                process.StartInfo.Arguments = "--mode " + config.DeepLook.Mode + " --path_location " + config.DeepLook.PathLocation + " --path_scenario " + config.DeepLook.PathScenario + " --path_plan " + config.DeepLook.PathPlan + " --plan_type " + config.DeepLook.PlanType + " --path_eval_result " + config.DeepLook.PathEvalResult;

            }
            else
            {
                Console.WriteLine("Warning ! Mode is unknown");
            }
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine("Output from C++:");
            Console.WriteLine(output);

        }

        // Tests if the given location and scenario (json format) files can be parsed correctly int protobuf objects (ProblemInstance)
        // As partial results, the function displays the details about the infrstructure of the location, and the incoming and outgoing trains of the scenario
        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        static void Test_Location_Scenario_Parsing(string location_path, string scenario_path)
        {

            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);
            try
            {
                var location_TrackParts = ProblemInstance.Current.InterfaceLocation.TrackParts;

                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed location is null.");

                }

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceLocation);

                string json_original = ProblemInstance.ParseJsonToString(location_path);


                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Location file parsing was successful !");
                    // Console.WriteLine("JSON: \n " + json_parsed);
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

            try
            {

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceScenario);

                var scenario_in = ProblemInstance.Current.InterfaceScenario.In;
                var scenario_out = ProblemInstance.Current.InterfaceScenario.Out;

                if (scenario_in == null)
                {
                    throw new NullReferenceException("Parsed scenario in filed is null.");
                }


                if (scenario_out == null)
                {
                    throw new NullReferenceException("Parsed scenario out field is null.");
                }

                string json_original = ProblemInstance.ParseJsonToString(scenario_path);

                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Scenario file parsing was successful !");
                    // Console.WriteLine("JSON: \n " + json_parsed);
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
                foreach (AlgoIface.TrainRequest train in scenario_out.TrainRequests)
                {
                    outgoingTrains.Add(train);
                }

                foreach (AlgoIface.TrainRequest train in outgoingTrains)
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
        public ConfigTabuSearch TabuSearch { get; set; }
        public ConfigSimulatedAnnealing SimulatedAnnealing { get; set; }

        public ConfigDeepLook DeepLook { get; set; }
        public class ConfigTabuSearch
        {
            public int Iterations { get; set; }
            public int IterationsUntilReset { get; set; }
            public int TabuListLength { get; set; }
            public float Bias { get; set; }
            public bool SuppressConsoleOutput { get; set; }

        }
        public class ConfigSimulatedAnnealing
        {
            public int MaxDuration { get; set; }
            public bool StopWhenFeasible { get; set; }
            public int IterationsUntilReset { get; set; }
            public int T { get; set; }
            public float A { get; set; }
            public int Q { get; set; }
            public int Reset { get; set; }
            public float Bias { get; set; }
            public bool SuppressConsoleOutput { get; set; }
            public bool IintensifyOnImprovement { get; set; }

        }

        public class ConfigDeepLook
        {
            public string Path { get; set; }
            public string Mode { get; set; }
            public string PathLocation { get; set; }
            public string PathScenario { get; set; }
            public string PathPlan { get; set; }
            public string PlanType { get; set; }
            // Path where the converted scenario for 
            // the evaluator has to be stored
            public string PathScenarioEval { get; set; }
            // Path to store the Evaluator's results
            public string PathEvalResult { get; set; }

        }
        public int Seed { get; set; }
        public int MaxDuration { get; set; }
        public bool StopWhenFeasible { get; set; }
        public string LocationPath { get; set; }
        public string ScenarioPath { get; set; }
        public string PlanPath { get; set; }
        public string OutputPath { get; set; }
        public string Mode { get; set; }

    }
}
