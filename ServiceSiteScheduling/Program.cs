using System.Diagnostics;
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
                        else if (config.Mode == "DeepLook")
                        {
                            TestCasesDeepLook(config);
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
                Test_Location_Scenario_Parsing(
                    $"./database/TUSS-Instance-Generator/scenario_settings/{directory}/location_solver.json",
                    $"./database/TUSS-Instance-Generator/scenario_settings/{directory}/scenario_solver.json"
                );
                Console.WriteLine("***************** CreatePlan() *****************");
                CreatePlan(
                    $"./database/TUSS-Instance-Generator/scenario_settings/{directory}/location_solver.json",
                    $"./database/TUSS-Instance-Generator/scenario_settings/{directory}/scenario_solver.json",
                    $"./database/TUSS-Instance-Generator/scenario_settings/{directory}/plan.json"
                );
            }
        }

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
            if (
                config != null
                && config.Mode == "DeepLook"
                && config.DeepLook.DeterministicPlanning != null
                && config.DeepLook.DeterministicPlanning.Seed != 0
            )
            {
                random = new Random(config.DeepLook.DeterministicPlanning.Seed);
            }
            else if (config != null && config.Seed > 0)
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

        // Input:   @currentInstance: an already existing problem instance with scenario and location -
        //          useful in tests when scenarios have to be modifed at code level not directly in the .json file
        //          @config: service site scheduling config to creat the plan from
        // Output:  @plan_path: path to where the plan (.json) file will be written
        // Method: First it calls a Tabu Search method to find an initial plan (Graph) that is used by
        //         a Simulated Annealing method to find the final schedle plan (Totally Ordered Graph)
        static void CreatePlanFromExisting(
            ProblemInstance currentInstance,
            string plan_path,
            Config config = null
        )
        {
            // If a seed was specified in the config file and it's value is not 0, then we can use the seed for deterministic plan creation
            Random random;
            if (
                config.DeepLook.DeterministicPlanning != null
                && config.DeepLook.DeterministicPlanning.Seed != 0
            )
            {
                random = new Random(config.DeepLook.DeterministicPlanning.Seed);
            }
            else
            {
                random = new Random();
            }

            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;

            ProblemInstance.Current = currentInstance;

            int solved = 0;
            for (int i = 0; i < 1; i++)
            {
                Console.WriteLine($"Create Plan Iteration: {i}");
                LocalSearch.TabuSearch ts = new(random);
                if (config != null)
                {
                    ts.Run(
                        config.TabuSearch.Iterations,
                        config.TabuSearch.IterationsUntilReset,
                        config.TabuSearch.TabuListLength,
                        config.TabuSearch.Bias,
                        config.DebugLevel
                    );
                }
                else
                {
                    ts.Run(40, 100, 16, 0.5);
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
                        config.DebugLevel,
                        config.SimulatedAnnealing.IntensifyOnImprovement
                    );
                }
                else
                {
                    sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2);
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
                Console.WriteLine($"solved: {solved}");
                Console.WriteLine($"best = {best}");
                Console.WriteLine("------------------------------");
                Console.WriteLine($"Generate JSON format plan");
                Console.WriteLine("------------------------------");
                sa.Graph.WriteJSONFile(plan_path);
                Console.WriteLine(
                    "----------------------------------------------------------------------"
                );
                sa.Graph.DisplayMovements();
                Solutions.PlanGraph.Clear();
            }
            Console.WriteLine("------------ OVERALL BEST --------------");
            Console.WriteLine(best);
        }

        static void TestCasesDeepLook(Config config)
        {
            Console.WriteLine(
                "***************** Test_Location_Scenario_Parsing() *****************"
            );

            Test_Location_Scenario_Parsing(
                config.LocationPath,
                config.ScenarioPath,
                config.DebugLevel
            );

            // Contains all the tested scenario cases and the plan evaluation results [valid, not valid]
            Dictionary<string, string> ResultSummary = [];

            Dictionary<string, string[]> ResultSummaryWithSeed = [];
            string scenarioTestCase = "";

            int testCases = config.DeepLook.TestCases;

            ulong timeToAjustCase0 = 0;
            ulong timeToAjustCase1 = 0;
            ulong timeToAjustCase2 = 0;
            ulong timeToAjustCase3 = 0;

            ulong consAmount = 300;

            // If testCases is 0 and LookForSeed is true the scenario is not modified, but actually the seed is modified
            // it might be the case that a plan cannot be found because of the choosen random number and not
            // not because of the constraints in the scenarion
            if (testCases > 0 || config.DeepLook.DeterministicPlanning.LookForSeed)
            {
                bool validPlanFound = false;
                int itTest = 0;
                while ((validPlanFound == false) && (itTest < config.DeepLook.MaxTest))
                {
                    for (int testCase = -1; testCase < testCases; testCase++)
                    {
                        Console.WriteLine("***************** CreatePlan() *****************");
                        ProblemInstance.Current = ProblemInstance.ParseJson(
                            config.LocationPath,
                            config.ScenarioPath
                        );

                        int numberOfArrivals = ProblemInstance.Current.ArrivalsOrdered.Length;

                        // It is a simple example in which the arrival and departure times are modified
                        if (testCase != -1)
                        {
                            switch (testCase % (numberOfArrivals + 1))
                            {
                                case 0:
                                    // Test case: Increase the time of arrival trains, but not the departure trains'
                                    // timeToAjustCase0 = timeToAjustCase0 + consAmount;
                                    foreach (
                                        var arrivalTrain in ProblemInstance
                                            .Current
                                            .InterfaceScenario
                                            .In
                                            .Trains
                                    )
                                    {
                                        arrivalTrain.Arrival =
                                            arrivalTrain.Arrival + timeToAjustCase0;
                                        arrivalTrain.Departure =
                                            arrivalTrain.Departure + timeToAjustCase0;
                                    }
                                    break;
                                case 1:
                                    // Test case: Increase the time of departure trains, but not the arrival trains'
                                    timeToAjustCase1 = timeToAjustCase1 + consAmount;
                                    ProblemInstance.Current.InterfaceScenario.EndTime =
                                        ProblemInstance.Current.InterfaceScenario.EndTime
                                        + timeToAjustCase1;

                                    foreach (
                                        var departureTrain in ProblemInstance
                                            .Current
                                            .InterfaceScenario
                                            .Out
                                            .TrainRequests
                                    )
                                    {
                                        departureTrain.Arrival =
                                            departureTrain.Arrival + timeToAjustCase1;
                                        departureTrain.Departure =
                                            departureTrain.Departure + timeToAjustCase1;
                                    }

                                    break;
                                case 2:
                                    // Test case: Increase the time of arrival trains, and the departure trains'
                                    timeToAjustCase2 = timeToAjustCase2 + consAmount;
                                    ProblemInstance.Current.InterfaceScenario.EndTime =
                                        ProblemInstance.Current.InterfaceScenario.EndTime
                                        + timeToAjustCase2;

                                    foreach (
                                        var arrivalTrain in ProblemInstance
                                            .Current
                                            .InterfaceScenario
                                            .In
                                            .Trains
                                    )
                                    {
                                        arrivalTrain.Arrival =
                                            arrivalTrain.Arrival + timeToAjustCase1;
                                        arrivalTrain.Departure =
                                            arrivalTrain.Departure + timeToAjustCase1;
                                    }

                                    foreach (
                                        var departureTrain in ProblemInstance
                                            .Current
                                            .InterfaceScenario
                                            .Out
                                            .TrainRequests
                                    )
                                    {
                                        departureTrain.Arrival =
                                            departureTrain.Arrival + timeToAjustCase2;
                                        departureTrain.Departure =
                                            departureTrain.Departure + timeToAjustCase2;
                                    }
                                    break;

                                case 3:
                                    // Test case when instanding and outstanding trains are involved
                                    timeToAjustCase3 = timeToAjustCase3 + consAmount;
                                    ProblemInstance.Current.InterfaceScenario.EndTime =
                                        ProblemInstance.Current.InterfaceScenario.EndTime
                                        + timeToAjustCase2;

                                    foreach (
                                        var outStandingTrain in ProblemInstance
                                            .Current
                                            .InterfaceScenario
                                            .OutStanding
                                            .TrainRequests
                                    )
                                    {
                                        outStandingTrain.Arrival =
                                            outStandingTrain.Arrival + timeToAjustCase3;
                                        outStandingTrain.Departure =
                                            outStandingTrain.Departure + timeToAjustCase3;
                                    }

                                    break;
                                default:

                                    break;
                            }

                            // The current instance has to be reinitialized since the scenario parameters were changend in a Protobuf object level according to the test cases
                            ProblemInstance.Current = ProblemInstance.Parse(
                                ProblemInstance.Current.InterfaceLocation,
                                ProblemInstance.Current.InterfaceScenario
                            );
                        }

                        // Convert the scenario into an evaluator type scenario
                        // and also store it with the prefix scenario_evaluator.json
                        Converter converter = new(
                            ProblemInstance.Current,
                            config.DeepLook.ConversionAndStorage.PathScenarioEval
                        );

                        if (converter.ConvertScenario())
                        {
                            Console.WriteLine(
                                "----------------------------------------------------------------------"
                            );
                            Console.WriteLine("Conversion done with success");
                            Console.WriteLine(
                                "----------------------------------------------------------------------"
                            );

                            // Store converted scenario, this will be read by the evaluator
                            var fileNameEvalScenario = "scenario_evaluator";
                            if (!converter.StoreScenarioEvaluator(fileNameEvalScenario))
                                Console.WriteLine(
                                    "Error while storage of evaluator format scenario"
                                );

                            // Store evaluator format scenarion per test case
                            var fileNameEvalScenarioCase =
                                "scenario_evaluator" + "_case_" + testCase + "_it_" + itTest;
                            if (!converter.StoreScenarioEvaluator(fileNameEvalScenarioCase))
                                Console.WriteLine(
                                    "Error while storage of evaluator format scenario"
                                );

                            // Store the modified solver format scenario in the same folder but under a different file name per test case
                            var fileNameSolverScenario =
                                "scenario_solver" + "_case_" + testCase + "_it_" + itTest;
                            if (!converter.StoreScenarioSolver(fileNameSolverScenario))
                                Console.WriteLine("Error while storage of solver format scenario");

                            scenarioTestCase = fileNameSolverScenario;
                        }
                        // Create a plan corresponding to the scenario per test case
                        CreatePlanFromExisting(ProblemInstance.Current, config.PlanPath, config);

                        // Store the plan per test case
                        var fileNameToStorePlam = "plan" + "_case_" + testCase + "_it_" + itTest;
                        if (
                            !Converter.StorePlan(
                                fileNameToStorePlam,
                                config.DeepLook.EvaluatorInput.PathPlan
                            )
                        )
                            Console.WriteLine(
                                $"Plan Path {config.DeepLook.EvaluatorInput.PathPlan}"
                            );

                        bool evaluatorResult;
                        if (Call_Evaluator(config))
                        {
                            Console.WriteLine("The plan is valid");
                            evaluatorResult = true;
                        }
                        else
                        {
                            Console.WriteLine("The plan is not valid");
                            evaluatorResult = false;
                        }
                        if (evaluatorResult)
                        {
                            validPlanFound = true;
                        }

                        if (
                            StoreScenarioAndEvaluationResults(
                                config.DeepLook.EvaluatorInput.PathScenario,
                                config.DeepLook.ConversionAndStorage.PathScenarioEval,
                                config.DeepLook.ConversionAndStorage.PathEvalResult,
                                testCase,
                                itTest,
                                evaluatorResult
                            )
                        )
                        {
                            Console.WriteLine(
                                "Scenario for Evaluator and the Results are successfully stored"
                            );
                        }
                        else
                        {
                            Console.WriteLine(
                                "Problemes during the storage of Scenario for Evaluator and the Results"
                            );
                        }

                        // Add summary of the test cases and evaluation results
                        ResultSummary[scenarioTestCase] = evaluatorResult
                            ? "Valid ✅"
                            : "Not Valid ❌";

                        // If the seed should be displayed
                        if (config.DeepLook.DeterministicPlanning.DisplaySeed)
                        {
                            string[] resultReport = new string[2];

                            resultReport[0] = evaluatorResult ? "Valid ✅" : "Not Valid ❌";

                            resultReport[1] = $"{config.DeepLook.DeterministicPlanning.Seed}";
                            if (config.DeepLook.DeterministicPlanning.LookForSeed)
                            {
                                Console.WriteLine(
                                    $">>> Seed: {config.DeepLook.DeterministicPlanning.Seed}"
                                );
                            }

                            ResultSummaryWithSeed[scenarioTestCase] = resultReport;
                        }
                    }
                    if (config.DeepLook.DeterministicPlanning.LookForSeed)
                    {
                        config.DeepLook.DeterministicPlanning.Seed++;
                    }

                    timeToAjustCase0 = 0;
                    timeToAjustCase1 = 0;
                    timeToAjustCase2 = 0;
                    itTest++;
                }

                // Print the sumarry of the test cases and their evaluation results
                PrintSummary(ResultSummary);

                // Print the results with the seed values
                if (config.DeepLook.DeterministicPlanning.DisplaySeed)
                    PrintSummaryWithSeeds(ResultSummaryWithSeed);

                if (ResultSummary.ContainsValue("Valid ✅"))
                {
                    Console.WriteLine($"Valid plan found in {itTest} iterations");
                }
                else
                {
                    Console.WriteLine($"No valid pland was found");
                }
            }
            else
            {
                // The current instance has to be reinitialized since the scenario parameters were changend in a Protobuf object level according to the test cases
                ProblemInstance.Current = ProblemInstance.Parse(
                    ProblemInstance.Current.InterfaceLocation,
                    ProblemInstance.Current.InterfaceScenario
                );

                var testCase = 0;
                Converter converter = new(
                    ProblemInstance.Current,
                    config.DeepLook.ConversionAndStorage.PathScenarioEval
                );

                if (converter.ConvertScenario())
                {
                    Console.WriteLine(
                        "----------------------------------------------------------------------"
                    );
                    Console.WriteLine("Conversion done with success");
                    Console.WriteLine(
                        "----------------------------------------------------------------------"
                    );

                    // Store converted scenario, this will be read by the evaluator
                    var fileNameEvalScenario = "scenario_evaluator";
                    if (!converter.StoreScenarioEvaluator(fileNameEvalScenario))
                        Console.WriteLine("Error while storage of evaluator format scenario");

                    // Store evaluator format scenarion per test case
                    var fileNameEvalScenarioCase = "scenario_evaluator" + "_case_" + testCase;
                    if (!converter.StoreScenarioEvaluator(fileNameEvalScenarioCase))
                        Console.WriteLine("Error while storage of evaluator format scenario");

                    // Store the modified solver format scenario in the same folder but under a different file name per test case
                    var fileNameSolverScenario = "scenario_solver" + "_case_" + testCase;
                    if (!converter.StoreScenarioSolver(fileNameSolverScenario))
                        Console.WriteLine("Error while storage of solver format scenario");

                    scenarioTestCase = fileNameSolverScenario;
                }

                // Create a plan corresponding to the scenario per test case
                CreatePlanFromExisting(ProblemInstance.Current, config.PlanPath, config);
                if (config.DeepLook.DeterministicPlanning.LookForSeed)
                {
                    config.DeepLook.DeterministicPlanning.Seed++;
                }
                // Store the plan per test case
                var fileNameToStorePlam = "plan" + "_case_" + testCase;
                if (
                    !Converter.StorePlan(
                        fileNameToStorePlam,
                        config.DeepLook.EvaluatorInput.PathPlan
                    )
                )
                    Console.WriteLine($"Plan Path {config.DeepLook.EvaluatorInput.PathPlan}");

                bool evaluatorResult;
                if (Call_Evaluator(config))
                {
                    Console.WriteLine("The plan is valid");
                    evaluatorResult = true;
                }
                else
                {
                    Console.WriteLine("The plan is not valid");
                    evaluatorResult = false;
                }
                if (
                    StoreScenarioAndEvaluationResults(
                        config.DeepLook.EvaluatorInput.PathScenario,
                        config.DeepLook.ConversionAndStorage.PathScenarioEval,
                        config.DeepLook.ConversionAndStorage.PathEvalResult,
                        testCase,
                        0,
                        evaluatorResult
                    )
                )
                {
                    Console.WriteLine(
                        "Scenario for Evaluator and the Results are successfully stored"
                    );
                }
                else
                {
                    Console.WriteLine(
                        "Problemes during the storage of Scenario for Evaluator and the Results"
                    );
                }

                // Add summary of the test cases and evaluation results

                ResultSummary[scenarioTestCase] = evaluatorResult ? "Valid ✅" : "Not Valid ❌";
                // Print the sumarry of the test cases and their evaluation results

                // If the seed should be displayed
                if (config.DeepLook.DeterministicPlanning.DisplaySeed)
                {
                    string[] resultReport = new string[2];

                    resultReport[0] = evaluatorResult ? "Valid ✅" : "Not Valid ❌";

                    int seed = config.DeepLook.DeterministicPlanning.Seed;
                    if (config.DeepLook.DeterministicPlanning.LookForSeed)
                    {
                        seed -= 1;
                    }
                    resultReport[1] = $"{seed}";
                    ResultSummaryWithSeed[scenarioTestCase] = resultReport;
                }

                // Print the sumarry of the test cases and their evaluation results
                PrintSummary(ResultSummary);
                // Print the results with the seed values
                if (config.DeepLook.DeterministicPlanning.DisplaySeed)
                    PrintSummaryWithSeeds(ResultSummaryWithSeed);
            }
        }

        static bool StoreScenarioAndEvaluationResults(
            string PathScenarioForEval,
            string PathToStoreEvalScenario,
            string PathToEvaluationResult,
            int TestNum,
            int iterationStep,
            bool valid
        )
        {
            try
            {
                string destinationPath = PathToStoreEvalScenario;

                destinationPath =
                    destinationPath
                    + "/"
                    + "scenario_case_"
                    + TestNum
                    + "_it_"
                    + iterationStep
                    + (valid ? "_valid" : "_not_valid")
                    + Path.GetExtension(PathScenarioForEval);
                File.Copy(PathScenarioForEval, destinationPath, overwrite: true);

                destinationPath = PathToStoreEvalScenario;
                destinationPath =
                    destinationPath
                    + "/"
                    + "evaluator_result_case_"
                    + TestNum
                    + TestNum
                    + "_it_"
                    + iterationStep
                    + (valid ? "_valid" : "_not_valid")
                    + Path.GetExtension(PathToEvaluationResult);

                // destinationPath = Path.GetFileNameWithoutExtension(PathToEvaluationResult);
                // destinationPath = destinationPath + "evaluator_result_case_" + TestNum + (valid ? "valid" : "not_valid") + Path.GetExtension(PathToStoreEvalScenario);
                File.Copy(PathToEvaluationResult, destinationPath, overwrite: true);
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error copying file: " + ex.Message);
                return false;
            }

            return true;
        }

        static bool Call_Evaluator(Config config)
        {
            Process process = new();
            process.StartInfo.FileName = config.DeepLook.EvaluatorInput.Path;

            if (config.DeepLook.EvaluatorInput.Mode == "EVAL")
            {
                process.StartInfo.Arguments =
                    "--mode "
                    + config.DeepLook.EvaluatorInput.Mode
                    + " --path_location "
                    + config.DeepLook.EvaluatorInput.PathLocation
                    + " --path_scenario "
                    + config.DeepLook.EvaluatorInput.PathScenario
                    + " --path_plan "
                    + config.DeepLook.EvaluatorInput.PathPlan
                    + " --plan_type "
                    + config.DeepLook.EvaluatorInput.PlanType
                    + " --departure_delay "
                    + config.DeepLook.EvaluatorInput.DepartureDelay;
            }
            else if (config.DeepLook.EvaluatorInput.Mode == "EVAL_AND_STORE")
            {
                process.StartInfo.Arguments =
                    "--mode "
                    + config.DeepLook.EvaluatorInput.Mode
                    + " --path_location "
                    + config.DeepLook.EvaluatorInput.PathLocation
                    + " --path_scenario "
                    + config.DeepLook.EvaluatorInput.PathScenario
                    + " --path_plan "
                    + config.DeepLook.EvaluatorInput.PathPlan
                    + " --plan_type "
                    + config.DeepLook.EvaluatorInput.PlanType
                    + " --path_eval_result "
                    + config.DeepLook.ConversionAndStorage.PathEvalResult
                    + " --departure_delay "
                    + config.DeepLook.EvaluatorInput.DepartureDelay;
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

            Console.WriteLine("Output from Evaluator:");
            Console.WriteLine(output);

            // Check the evaluator's result if it is valid plan or not only in case the resutls were stored

            if (output.Contains("The plan is valid", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
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
                            $"    Location with {ProblemInstance.Current.Tracks.Length} tracks and {ProblemInstance.Current.InterfaceLocation.TrackParts.Count} track parts, including {ProblemInstance.Current.InterfaceLocation.TrackParts.Count(tp => tp.Type == NoProto.TrackPartType.RailRoad && tp.ParkingAllowed)} parking tracks, {ProblemInstance.Current.InterfaceLocation.TrackParts.Count(tp => tp.Type != NoProto.TrackPartType.RailRoad && tp.Type != NoProto.TrackPartType.Bumper)} crossings and {ProblemInstance.Current.InterfaceLocation.Facilities.Count} servicing tracks"
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
                            $"    Scenario with {scenario_in.Trains.Count} incoming trains {scenario_out.TrainRequests.Count} outgoing trains, {ProblemInstance.Current.InterfaceScenario.InStanding.Trains.Count} instanding trains {ProblemInstance.Current.InterfaceScenario.OutStanding.TrainRequests.Count} outstanding trains."
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

                List<NoProto.IncomingTrain> incomingTrains = new(scenario_in.Trains);
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

                List<NoProto.TrainRequest> outgoingTrains = new(scenario_out.TrainRequests);
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

        // Prints out all the scenario test cases and their evaluation results
        static void PrintSummary(Dictionary<string, string> summary)
        {
            Console.WriteLine("+-----------------------------------------------------------+");
            Console.WriteLine("|                      Test summary                         |");
            Console.WriteLine("+-----------------------------------------------------------+");

            foreach (var item in summary)
            {
                Console.WriteLine($"|       {item.Key}        |  {item.Value}  ");
                Console.WriteLine("+___________________________________________________________+");
            }
            Console.WriteLine("+-----------------------------------------------------------+");
        }

        // Prints out all the scenario test cases and their evaluation results with the seed values found
        static void PrintSummaryWithSeeds(Dictionary<string, string[]> summary)
        {
            Console.WriteLine(
                "+--------------------------------------------------------------------------+"
            );
            Console.WriteLine(
                "|                               Test summary                               |"
            );
            Console.WriteLine(
                "+--------------------------------------------------------------------------+"
            );
            Console.WriteLine(
                $"|                 File name                |     Result     |      Seed    |"
            );
            Console.WriteLine(
                "+__________________________________________________________________________+"
            );
            foreach (var item in summary)
            {
                Console.WriteLine(
                    $"|       {item.Key}        |  {item.Value[0]}  | {item.Value[1]} "
                );
                Console.WriteLine(
                    "+__________________________________________________________________________+"
                );
            }
            Console.WriteLine(
                "+--------------------------------------------------------------------------+"
            );
        }
    }

    internal class Config
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

        public class ConfigEvaluatorInput
        {
            // Path to the evaluator's executable
            public string Path { get; set; }

            // Mode to choose, simple evaluation or evaluation with storage of the results (recommended)
            public string Mode { get; set; }

            // # Folder where the evaluator format location file can be found (TODO: later the solver format location should be converted to evaluator format)
            public string PathLocation { get; set; }

            // Path to scenario_evaluator.json file ! Important ! scenario_evaluator.json is generated by the Deep Look mode from the solver format scenarion specified by ScenarioPath in the config.yaml
            public string PathScenario { get; set; }

            // The path to the plan generated by the solver
            public string PathPlan { get; set; }

            // To tell the evaluator that a solver formated plan must be evaluated
            public string PlanType { get; set; }

            // In certain scenarios the departure delay might be allowed, if no delay introduced this parameter should be set to "0"
            public string DepartureDelay { get; set; }
        }

        public class ConfigConversionAndStorage
        {
            // The path where the evaluation results (.txt) should be stored
            public string PathScenarioEval { get; set; }

            // The path where the scenario_evaluator.json will be stored after the conversion. Note that the name "scenario_evaluator" is a default name it can be changed in the code, but in that case the PathScenario should point to the "renamed" evaluator format scenarion file. This path also serves for serving the evaluator format scenarios after the evaluation as scenario_case_x_valid/not_valid.json - needed to summaize the run cases.
            public string PathEvalResult { get; set; }
        }

        public class ConfigDeepLook
        {
            public int TestCases { get; set; }

            public int MaxTest { get; set; }
            public ConfigEvaluatorInput EvaluatorInput { get; set; }

            public ConfigConversionAndStorage ConversionAndStorage { get; set; }

            public ConfigDeterministicPlanning DeterministicPlanning { get; set; }
        }

        public class ConfigDeterministicPlanning
        {
            public bool LookForSeed { get; set; }

            public bool DisplaySeed { get; set; } // To display the seed found
            public int Seed { get; set; }
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
