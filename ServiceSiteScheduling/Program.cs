using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ServiceSiteScheduling.Utilities;
using YamlDotNet.Serialization;

namespace ServiceSiteScheduling
{
    class Program
    {
        // Method: Run the program from a config file. This is the entry point of the application
        static void Main(string[] args)
        {
            Console.WriteLine($"HIP {Version}");

            if (args.Length != 0)
            {
                string config_file = "";
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--config="))
                    {
                        config_file = arg.Substring("--config=".Length);
                        Console.WriteLine("Using config file: " + config_file);
                        Config config = Config.ReadFrom(config_file);

                        string directoryPath = Path.GetDirectoryName(config.PlanPath);
                        if (!Directory.Exists(directoryPath) && directoryPath != null)
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        string tmpPathPlan = "";
                        if (config.TemporaryPlanPath is null or "")
                        {
                            string currentDirectory = Directory.GetCurrentDirectory();
                            tmpPathPlan = Path.Combine(currentDirectory, "tmp_plans") + "/";
                        }
                        else
                        {
                            tmpPathPlan = config.TemporaryPlanPath + "/";
                        }

                        if (config.Mode == "Standard")
                        {
                            if (config.DebugLevel > 1)
                            {
                                Console.WriteLine(
                                    "***************** Reading Location and Scenario *****************"
                                );
                            }
                            Test_Location_Scenario_Parsing(
                                config.LocationPath,
                                config.ScenarioPath,
                                config.DebugLevel
                            );
                            if (config.DebugLevel > 1)
                                Console.WriteLine(
                                    "***************** Creating a Plan *****************"
                                );
                            CreatePlan(
                                config.LocationPath,
                                config.ScenarioPath,
                                config.PlanPath,
                                config,
                                config.DebugLevel,
                                tmpPathPlan
                            );
                        }
                        else
                        {
                            Console.WriteLine("Unknown parameter for Mode");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown --parameter name: " + arg);
                        Environment.Exit(1);
                    }
                }
            }
            else
            {
                string directory = "setting_A";
                Console.WriteLine(
                    $"No config file provided, running with default test files: {directory}"
                );
                string prefix =
                    "/home/leon/Projects/Robust-Rail-NL/robust-rail-solver/ServiceSiteScheduling";
                Test_Location_Scenario_Parsing(
                    $"{prefix}/database/TUSS-Instance-Generator/scenario_settings/{directory}/location.json",
                    $"{prefix}/database/TUSS-Instance-Generator/scenario_settings/{directory}/scenario.json",
                    2
                );
                Console.WriteLine("***************** CreatePlan() *****************");
                CreatePlan(
                    $"{prefix}/database/TUSS-Instance-Generator/scenario_settings/{directory}/location.json",
                    $"{prefix}/database/TUSS-Instance-Generator/scenario_settings/{directory}/scenario.json",
                    $"{prefix}/database/TUSS-Instance-Generator/scenario_settings/{directory}/plan.json",
                    debugLevel: 0
                );
            }
        }

        // The single source of truth is HIP.csproj's <Version> element; the SDK
        // embeds it (including any prerelease suffix) as the assembly's
        // AssemblyInformationalVersionAttribute. Source-linked builds append a
        // "+<git-hash>" build-metadata suffix, which is stripped here.
        internal static string Version =>
            Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0]
            ?? "unknown";

        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        //          @config: service site scheduling config to creat the plan from
        // Output:  @plan_path: path to where the plan (.json) file will be written
        // Method: First it calls a Tabu Search method to find an initial plan (Graph) that is used by
        //         a Simulated Annealing method to find the final schedle plan (Totally Ordered Graph)
        internal static void CreatePlan(
            string location_path,
            string scenario_path,
            string plan_path,
            Config config = null,
            int debugLevel = 0,
            string tmp_plan_path = "./tmp_plans/"
        )
        {
            if (!Directory.Exists(tmp_plan_path))
            {
                Directory.CreateDirectory(tmp_plan_path);
            }
            foreach (var file in Directory.GetFiles(tmp_plan_path, "*.json"))
            {
                File.Delete(file);
            }
            // If a seed was specified in the config file and its value is not 0, then we can use the seed for deterministic plan creation
            Random random;
            if (config != null && config.Seed > 0)
            {
                Console.WriteLine($"Using random seed <{config.Seed}> from config.");
                random = new Random(config.Seed);
            }
            else
            {
                int seed = Guid.NewGuid().GetHashCode();
                random = new Random(seed);
                Console.WriteLine($"Using randomly generated seed <{seed}>.");
            }

            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;
            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);

            int solved = 0;
            // TODO how many iterations should be used here?
            for (int i = 0; i < 1; i++)
            {
                if (debugLevel > 0)
                {
                    Console.WriteLine($"Create Plan Iteration: {i}");
                }
                LocalSearch.TabuSearch ts = new(random, debugLevel);
                if (config != null)
                {
                    ts.Run(
                        config.TabuSearch.Iterations,
                        config.TabuSearch.IterationsUntilReset,
                        config.TabuSearch.TabuListLength,
                        config.TabuSearch.Bias,
                        debugLevel,
                        tmp_plan_path
                    );
                }
                else
                {
                    ts.Run(40, 100, 16, 0.5, debugLevel, tmp_plan_path);
                }
                LocalSearch.SimulatedAnnealing sa = new(random, ts.Graph);
                if (config != null)
                {
                    sa.Run(
                        new Time(config.SimulatedAnnealing.MaxDuration),
                        config.SimulatedAnnealing.StopWhenFeasible,
                        config.SimulatedAnnealing.IterationsUntilReset,
                        config.SimulatedAnnealing.T,
                        config.SimulatedAnnealing.A,
                        config.SimulatedAnnealing.Q,
                        config.SimulatedAnnealing.Reset,
                        config.SimulatedAnnealing.Bias,
                        debugLevel,
                        config.SimulatedAnnealing.IntensifyOnImprovement,
                        tmp_plan_path
                    );
                }
                else
                {
                    sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2);
                }
                if (debugLevel > 0)
                {
                    Console.WriteLine("--------------------------");
                    Console.WriteLine(" Output Movement Schedule ");
                    Console.WriteLine("--------------------------");
                    sa.Graph.OutputMovementSchedule();
                    Console.WriteLine("--------------------------");
                    Console.WriteLine(" Output Train Unit Schedule ");
                    Console.WriteLine("----------------------------");
                    sa.Graph.OutputTrainUnitSchedule();
                    Console.WriteLine("----------------------------");
                    Console.WriteLine(" Output Constraint Violations ");
                    Console.WriteLine("------------------------------");
                    sa.Graph.OutputConstraintViolations();
                    Console.WriteLine(sa.Graph.Cost);
                    Console.WriteLine("--------------------------");
                }

                if (
                    sa.Graph.Cost.ArrivalDelays
                        + sa.Graph.Cost.DepartureDelays
                        + sa.Graph.Cost.TrackLengthViolations
                        + sa.Graph.Cost.Crossings
                        + sa.Graph.Cost.CombineOnDepartureTrack
                    <= 2
                )
                {
                    solved++;
                }

                if (sa.Graph.Cost.BaseCost < (best?.BaseCost ?? double.PositiveInfinity))
                {
                    best = sa.Graph.Cost;
                    graph = sa.Graph;
                }
                if (debugLevel > 1)
                {
                    Console.WriteLine($"solved: {solved}");
                    Console.WriteLine($"best = {best}");
                    Console.WriteLine("------------------------------");
                    Console.WriteLine($"Generate JSON format plan");
                    Console.WriteLine("------------------------------");
                }

                // Write JSON plan to file
                sa.Graph.WriteJSONFile(plan_path);
                Console.WriteLine("Plan written to: " + plan_path);

                File.WriteAllText(
                    Path.ChangeExtension(plan_path, ".txt"),
                    sa.Graph.OutputTrainUnitSchedule()
                );
                Console.WriteLine(
                    "Wrote resulting schedule for train units to text file: "
                        + Path.ChangeExtension(plan_path, ".txt")
                );
                if (debugLevel > 1)
                {
                    Console.WriteLine(
                        "----------------------------------------------------------------------"
                    );
                    sa.Graph.DisplayMovements();
                }
                Solutions.PlanGraph.Clear();
                Console.WriteLine("------------------ Found a plan ---------------------------");
                sa.Graph.GetShortPlanStatistics();
            }
            if (debugLevel > 0)
            {
                Console.WriteLine("------------ OVERALL BEST --------------");
                Console.WriteLine(best);
            }
        }

        // Tests if the given location and scenario (json format) files can be parsed correctly int protobuf objects (ProblemInstance)
        // As partial results, the function displays the details about the infrstructure of the location, and the incoming and outgoing trains of the scenario
        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        //          @debugLevel: 0 - no debug, 1 - some debug, 2 - full debug
        // Output:  Prints out the details about the location and scenario, and if the parsing was successful or not
        static void Test_Location_Scenario_Parsing(
            string location_path,
            string scenario_path,
            int debugLevel = 2
        )
        {
            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);
            try
            {
                var location_TrackParts = ProblemInstance.Current.InterfaceLocation.TrackParts;
                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed location is null.");
                }

                string json_parsed = ProblemInstance.Current.InterfaceLocation.SerializeJson();
                string json_original = ProblemInstance.ParseJsonToString(location_path);

                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_original.ToString() == token_parsed.ToString())
                {
                    if (debugLevel > 0)
                    {
                        Console.WriteLine("The Location file parsing was successful");
                        Console.WriteLine(
                            $"    Location with {ProblemInstance.Current.Tracks.Length} tracks and {ProblemInstance.Current.InterfaceLocation.TrackParts.Length} track parts, including {ProblemInstance.Current.InterfaceLocation.TrackParts.Count(tp => tp.Type == NoProto.TrackPartType.RailRoad && tp.ParkingAllowed)} parking tracks, {ProblemInstance.Current.InterfaceLocation.TrackParts.Count(tp => tp.Type != NoProto.TrackPartType.RailRoad && tp.Type != NoProto.TrackPartType.Bumper)} crossings and {ProblemInstance.Current.InterfaceLocation.Facilities.Length} servicing tracks"
                        );
                    }
                }
                else
                {
                    Console.WriteLine("***The Location file parsing was not successful***");
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

            try
            {
                string json_parsed = ProblemInstance.Current.InterfaceScenario.SerializeJson();

                var scenario_in = ProblemInstance.Current.InterfaceScenario.In;
                var scenario_out = ProblemInstance.Current.InterfaceScenario.Out;

                if (scenario_in == null)
                {
                    throw new NullReferenceException("Parsed scenario in field is null.");
                }
                if (scenario_out == null)
                {
                    throw new NullReferenceException("Parsed scenario out field is null.");
                }
                string json_original = ProblemInstance.ParseJsonToString(scenario_path);

                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_original.ToString() == token_parsed.ToString())
                {
                    if (debugLevel > 0)
                    {
                        Console.WriteLine("The Scenario file parsing was successful");
                        Console.WriteLine(
                            $"    Scenario with {scenario_in.Count} incoming trains {scenario_out.Count} outgoing trains, {ProblemInstance.Current.InterfaceScenario.InStanding.Count} instanding trains {ProblemInstance.Current.InterfaceScenario.OutStanding.Count} outstanding trains."
                        );
                        Console.WriteLine(
                            $"    Number of train units {ProblemInstance.Current.TrainUnits.Length} of different train unit types {ProblemInstance.Current.TrainUnitsByType.Count}: "
                                + string.Join(
                                    ", ",
                                    ProblemInstance.Current.TrainUnitsByType.Select(t =>
                                        t.Key.Name + " (" + t.Value.Length + " units)"
                                    )
                                )
                        );
                    }
                }
                else
                {
                    Console.WriteLine("***The Scenario file parsing was not successful***");
                }

                List<NoProto.IncomingTrain> incomingTrains = new(scenario_in);
                if (debugLevel > 1)
                {
                    Console.WriteLine("Scenario details: ");
                    Console.WriteLine("---- Incoming Trains ----");
                    foreach (NoProto.IncomingTrain train in incomingTrains)
                    {
                        Console.WriteLine(
                            "Arrival track "
                                + train.FirstParkingTrackPart
                                + " for train (id) "
                                + train.Id
                                + " at time "
                                + train.Arrival
                        );
                    }
                }

                List<NoProto.TrainRequest> outgoingTrains = new(scenario_out);
                if (debugLevel > 1)
                {
                    Console.WriteLine("---- Outgoing Trains ----");
                    foreach (NoProto.TrainRequest train in outgoingTrains)
                    {
                        Console.WriteLine(
                            "Departure track "
                                + train.LastParkingTrackPart
                                + " for train (id) "
                                + train.DisplayName
                                + " at time "
                                + train.Departure
                        );
                    }
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }
        }
    }

    internal class Config
    {
        public ConfigTabuSearch TabuSearch { get; set; }

        public ConfigSimulatedAnnealing SimulatedAnnealing { get; set; }

        public class ConfigTabuSearch
        {
            public int Iterations { get; set; }
            public int IterationsUntilReset { get; set; }
            public int TabuListLength { get; set; }
            public float Bias { get; set; }
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
            public bool IntensifyOnImprovement { get; set; }
        }

        public int Seed { get; set; }
        public int MaxDuration { get; set; }
        public int DebugLevel { get; set; } // 0 - no debug, 1 - some information given, 2 - all information given
        public bool StopWhenFeasible { get; set; }
        public string LocationPath { get; set; }
        public string ScenarioPath { get; set; }
        public string PlanPath { get; set; }
        public string TemporaryPlanPath { get; set; }
        public string Mode { get; set; }

        internal static Config ReadFrom(string config_file)
        {
            if (!File.Exists(config_file))
            {
                Console.Error.WriteLine($"Error: Config file '{config_file}' not found.");
                Environment.Exit(1);
            }

            string yaml = File.ReadAllText(config_file);
            var deserializer = new Deserializer();
            // The config has a debugLevel value: 0=only important info, 1=some info, 2=all info
            return deserializer.Deserialize<Config>(new StringReader(yaml));
        }
    }
}
