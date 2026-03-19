using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using ServiceSiteScheduling.NoProto;
using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling
{
    class Converter
    {
        EvaluatorScenario InterfaceScenarioEvaluator;
        ProblemInstance ProblemInstanceSolver;

        public string PathToStoreEvalScenario;

        public Converter(ProblemInstance problemInstanceSolver, string pathScenarioEval)
        {
            this.ProblemInstanceSolver = problemInstanceSolver;
            this.PathToStoreEvalScenario = pathScenarioEval;
            this.InterfaceScenarioEvaluator = new EvaluatorScenario()
            {
                StartTime = problemInstanceSolver.ScenarioStartTime,
                EndTime = problemInstanceSolver.ScenarioEndTime,
            };
        }

        public bool StoreScenarioEvaluator(string FileName)
        {
            string json_scenario_evaluator = InterfaceScenarioEvaluator.SerializeJson();

            // string json_scenario_evaluator = JsonFormatter.Default.Format(InterfaceScenarioEvaluator);

            if (!Directory.Exists(PathToStoreEvalScenario) && PathToStoreEvalScenario != null)
            {
                Directory.CreateDirectory(PathToStoreEvalScenario);
                Console.WriteLine($"Directory created: {PathToStoreEvalScenario}");
            }
            if (PathToStoreEvalScenario != null)
            {
                Console.WriteLine(
                    "----------------------------------------------------------------------"
                );
                string saveTo = PathToStoreEvalScenario + "/" + FileName + ".json";
                Console.WriteLine($" Save scenario for Evaluator to {saveTo}");

                File.WriteAllText(saveTo, json_scenario_evaluator);
                Console.WriteLine(
                    "----------------------------------------------------------------------"
                );
            }
            else
            {
                Console.WriteLine(" Path cannot be found");

                return false;
            }

            return true;
        }

        public static bool StorePlan(string FileName, string pathToPlan)
        {
            if (!File.Exists(pathToPlan) && pathToPlan != null)
            {
                Console.WriteLine($"Directory does not exist: {pathToPlan}");
                return false;
            }

            var planDirectory = Path.GetDirectoryName(pathToPlan);

            var newPlanToStore = planDirectory + "/" + FileName + Path.GetExtension(pathToPlan);

            if (pathToPlan != null)
            {
                File.Copy(pathToPlan, newPlanToStore, overwrite: true);
            }
            else
            {
                return false;
            }
            Console.WriteLine(
                "----------------------------------------------------------------------"
            );
            Console.WriteLine($" Save modifed plan to {newPlanToStore}");
            Console.WriteLine(
                "----------------------------------------------------------------------"
            );

            return true;
        }

        // During the test phase the Solver formated scenario is also modified
        // it is stored in the same directory as the Evaluator formated scenario, but
        // under a differnet name
        public bool StoreScenarioSolver(string FileName)
        {
            var formatter = new JsonFormatter(
                JsonFormatter.Settings.Default.WithIndentation("\t").WithFormatDefaultValues(true)
            );
            string json_scenario_solver = ProblemInstanceSolver.InterfaceScenario.SerializeJson();

            String PathToStoreSolverScenario = PathToStoreEvalScenario;

            if (!Directory.Exists(PathToStoreSolverScenario) && PathToStoreSolverScenario != null)
            {
                Directory.CreateDirectory(PathToStoreSolverScenario);
                Console.WriteLine($"Directory created: {PathToStoreSolverScenario}");
            }
            if (PathToStoreSolverScenario != null)
            {
                Console.WriteLine(
                    "----------------------------------------------------------------------"
                );
                string saveTo = PathToStoreSolverScenario + "/" + FileName + ".json";
                Console.WriteLine($" Save scenario for Evaluaor to {saveTo}");

                File.WriteAllText(saveTo, json_scenario_solver);
                Console.WriteLine(
                    "----------------------------------------------------------------------"
                );
            }
            else
            {
                Console.WriteLine(" Path cannot be found");

                return false;
            }

            return true;
        }

        public void PrintScenarioEvaluator()
        {
            string json_parsed = InterfaceScenarioEvaluator.SerializeJson();
            // string json_parsed = JsonFormatter.Default.Format(InterfaceScenarioEvaluator);

            Console.WriteLine("******* The Evaluator's scenario *******");
            Console.WriteLine(json_parsed);
        }

        public bool ConvertScenario()
        {
            Console.WriteLine("******* From ConvertScenario *******");
            CreateTrainUnitTypes(InterfaceScenarioEvaluator.TrainUnitTypes);

            // Convert all the Solver format arrivals to Evaluator format
            var inComingTrains = InterfaceScenarioEvaluator.In;

            foreach (var arrivalTrain in ProblemInstanceSolver.InterfaceScenario.In.Trains)
            {
                Train train = new();

                train.Id = arrivalTrain.Id;
                train.Time = arrivalTrain.Departure;
                train.SideTrackPart = arrivalTrain.EntryTrackPart;
                train.ParkingTrackPart = arrivalTrain.FirstParkingTrackPart;
                train.CanDepartFromAnyTrack = false;
                train.StandingIndex = arrivalTrain.StandingIndex;

                if (arrivalTrain.Members.Count > 0)
                {
                    foreach (var member in arrivalTrain.Members)
                    {
                        NoProto.TrainUnit trainUnit = new();
                        trainUnit.Id = member.TrainUnit.Id;
                        trainUnit.TypeDisplayName =
                            member.TrainUnit.Type.DisplayName
                            + "-"
                            + member.TrainUnit.Type.Carriages;

                        if (member.Tasks.Count > 0)
                        {
                            TaskSpec tasksEvaluator = new();
                            foreach (var taskSolver in member.Tasks)
                            {
                                string requiredskill = "";
                                if (taskSolver.Type.Other != null)
                                {
                                    Debug.Assert(taskSolver.Type.Predefined == null);
                                    TaskType taskTypeEvaluator = new(null, taskSolver.Type.Other);
                                    tasksEvaluator.Type = taskTypeEvaluator;

                                    if (taskSolver.Type.Other == "Reinigingsperron")
                                    {
                                        requiredskill = "inwendige_reiniging";
                                    }
                                    else
                                    {
                                        requiredskill = "";
                                    }
                                }

                                tasksEvaluator.Duration = taskSolver.Duration;
                                tasksEvaluator.Priority = 1;
                                tasksEvaluator.RequiredSkills.Add(requiredskill);
                            }
                            trainUnit.Tasks.Add(tasksEvaluator);
                        }
                        train.Members.Add(trainUnit);
                    }
                }

                inComingTrains.Add(train);
            }

            // If instanding trains are also defined, the should also be converted
            // Convert all the Solver format instanding trains to Evaluator format
            if (ProblemInstanceSolver.InterfaceScenario.InStanding != null)
            {
                var inStandingTrains = InterfaceScenarioEvaluator.InStanding;

                foreach (
                    var arrivalTrain in ProblemInstanceSolver.InterfaceScenario.InStanding.Trains
                )
                {
                    Train train = new();

                    train.Id = arrivalTrain.Id;
                    // train.Time = arrivalTrain.Departure;
                    train.Time = ProblemInstanceSolver.ScenarioStartTime;
                    train.SideTrackPart = arrivalTrain.EntryTrackPart;
                    train.ParkingTrackPart = arrivalTrain.FirstParkingTrackPart;
                    train.CanDepartFromAnyTrack = false;
                    train.StandingIndex = arrivalTrain.StandingIndex;

                    if (arrivalTrain.Members.Count > 0)
                    {
                        foreach (var member in arrivalTrain.Members)
                        {
                            NoProto.TrainUnit trainUnit = new();
                            trainUnit.Id = member.TrainUnit.Id;
                            trainUnit.TypeDisplayName =
                                member.TrainUnit.Type.DisplayName
                                + "-"
                                + member.TrainUnit.Type.Carriages;

                            if (member.Tasks.Count > 0)
                            {
                                TaskSpec tasksEvaluator = new();
                                foreach (var taskSolver in member.Tasks)
                                {
                                    string requiredskill = "";
                                    if (taskSolver.Type.Other != null)
                                    {
                                        Debug.Assert(taskSolver.Type.Predefined == null);
                                        TaskType taskTypeEvaluator = new(
                                            null,
                                            taskSolver.Type.Other
                                        );
                                        tasksEvaluator.Type = taskTypeEvaluator;

                                        if (taskSolver.Type.Other == "Reinigingsperron")
                                        {
                                            requiredskill = "inwendige_reiniging";
                                        }
                                        else
                                        {
                                            requiredskill = "";
                                        }
                                    }

                                    tasksEvaluator.Duration = taskSolver.Duration;
                                    tasksEvaluator.Priority = 1;
                                    tasksEvaluator.RequiredSkills.Add(requiredskill);
                                }
                                trainUnit.Tasks.Add(tasksEvaluator);
                            }
                            train.Members.Add(trainUnit);
                        }
                    }

                    inStandingTrains.Add(train);
                }
            }

            // Convert all the Solver format departure trains to Evaluator format
            var outgoingTrains = InterfaceScenarioEvaluator.Out;
            foreach (
                var departureTrain in ProblemInstanceSolver.InterfaceScenario.Out.TrainRequests
            )
            {
                Train train = new();

                train.Id = departureTrain.DisplayName;
                train.Time = departureTrain.Arrival;
                train.SideTrackPart = departureTrain.LeaveTrackPart;
                train.ParkingTrackPart = departureTrain.LastParkingTrackPart;
                train.CanDepartFromAnyTrack = false;
                train.StandingIndex = departureTrain.StandingIndex;

                if (departureTrain.TrainUnits.Count > 0)
                {
                    foreach (var member in departureTrain.TrainUnits)
                    {
                        NoProto.TrainUnit trainUnit = new();
                        trainUnit.Id = "****";
                        trainUnit.TypeDisplayName =
                            member.Type.DisplayName + "-" + member.Type.Carriages;
                        train.Members.Add(trainUnit);
                    }
                }

                outgoingTrains.Add(train);
            }

            // If there are any outstanding trains, they should also be converted
            // Convert all the Solver format outstanding trains to Evaluator format
            if (ProblemInstanceSolver.InterfaceScenario.OutStanding != null)
            {
                var outStandingTrains = InterfaceScenarioEvaluator.OutStanding;
                foreach (
                    var outStandingTrain in ProblemInstanceSolver
                        .InterfaceScenario
                        .OutStanding
                        .TrainRequests
                )
                {
                    Train train = new();

                    train.Id = outStandingTrain.DisplayName;
                    // train.Time = outStandingTrain.Arrival;
                    train.Time = ProblemInstanceSolver.ScenarioEndTime;
                    train.SideTrackPart = outStandingTrain.LeaveTrackPart;
                    train.ParkingTrackPart = outStandingTrain.LastParkingTrackPart;
                    train.CanDepartFromAnyTrack = false;
                    train.StandingIndex = outStandingTrain.StandingIndex;

                    if (outStandingTrain.TrainUnits.Count > 0)
                    {
                        foreach (var member in outStandingTrain.TrainUnits)
                        {
                            NoProto.TrainUnit trainUnit = new();
                            trainUnit.Id = "****";
                            trainUnit.TypeDisplayName =
                                member.Type.DisplayName + "-" + member.Type.Carriages;
                            train.Members.Add(trainUnit);
                        }
                    }

                    outStandingTrains.Add(train);
                }
            }

            return true;
        }

        public static void CreateTrainUnitTypes(IList<TrainUnitType> trainUnitTypes)
        {
            trainUnitTypes.Add(
                new()
                {
                    // SLT-4
                    DisplayName = "SLT-4",
                    Carriages = 4,
                    Length = 69.36,
                    CombineDuration = 180,
                    SplitDuration = 120,
                    NeedsElectricity = true,
                    TypePrefix = "SLT",
                    NeedsLoco = false,
                    IsLoco = false,
                    BackNormTime = 120,
                    BackAdditionTime = 16,
                }
            );

            trainUnitTypes.Add(
                new()
                {
                    // SLT-6
                    DisplayName = "SLT-6",
                    Carriages = 6,
                    Length = 100.54,
                    CombineDuration = 180,
                    SplitDuration = 120,
                    NeedsElectricity = true,
                    TypePrefix = "SLT",
                    NeedsLoco = false,
                    IsLoco = false,
                    BackNormTime = 120,
                    BackAdditionTime = 15,
                }
            );

            trainUnitTypes.Add(
                new()
                {
                    // SNG-3
                    DisplayName = "SNG-3",
                    Carriages = 3,
                    Length = 59.50,
                    CombineDuration = 180,
                    SplitDuration = 120,
                    NeedsElectricity = true,
                    TypePrefix = "SNG",
                    NeedsLoco = false,
                    IsLoco = false,
                }
            );

            trainUnitTypes.Add(
                new()
                {
                    // SNG-4
                    DisplayName = "SNG-4",
                    Carriages = 4,
                    Length = 75.70,
                    CombineDuration = 180,
                    SplitDuration = 120,
                    NeedsElectricity = true,
                    TypePrefix = "SNG",
                    NeedsLoco = false,
                    IsLoco = false,
                }
            );
        }
    }
}
