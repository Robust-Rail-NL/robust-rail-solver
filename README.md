# Robust Rail Solver 
Also known as Baseline HIP. 

Table of contents
- [Description](#description)
    - [How to use?](#how-to-use)
        - [Input parsing](#location-scenario-parsing)
        - [Run solver with a configuration file](#create-plan-with-tabu-and-local-search-methods---from-configuration-file)
        - [Run solver with command line arguments](#create-plan-with-tabu-and-local-search-methods)
    - [Validated scenarios](#validated-scenarios)
    - [Partial Order Schedule](#partial-order-schedule-pos---other-helper-functions)
    - [ProtoBuffers](#protobuffers)
    - [Deep Look Mode](#deep-look-mode---unit-test-like-mode)
- [Known Problems](#known-problems)
- [Building Process](#build-as-standalone-tool)
    - [Build as `.devcontainer`](#building-process---dev-container)
    - [Build in Linux](#building-process---native-support-linux)
    - [Known issues](#issues)
    - [Publishing the HIP image](#publishing-the-hip-image)


# Description 
This tool is the `baseline version` of the research outcome of a published paper by Roel van den Broek: [A Local Search Algorithm for Train Unit Shunting with Service Scheduling](https://pubsonline.informs.org/doi/10.1287/trsc.2021.1090).
The paper considers train unit shunting problem extended with service task scheduling. This problem originates from Dutch Railways, which is the main railway operator in the Netherlands. The study presents the first solution method covering all aspects of the shunting and scheduling problem. The problem consists of matching train units arriving at a shunting yard to departing trains, scheduling service tasks such as cleaning and maintenance on the available resources, and parking the trains on the available tracks such that the shunting yard can operate conflict-free. Partial order schedule representation that captures the full problem is also elaborated, and local search algorithm that utilizes the partial ordering has been applied. 
An earlier contribution to that research paper is [Train Shunting and Service Scheduling: an integrated local search approach](https://studenttheses.uu.nl/handle/20.500.12932/24118).

### robust-rail-solver 
- Input:
    * Location (e.g., shunting yard)
    * Scenario (e.g., train arrivals/departures - time/train types) 
- Output:
    * Plan - scheduled actions with the respect of service tasks done and the departure times


### Evaluation of the Plan
The plan produced by the solver can be further evaluated by [robust-rail-evaluator](https://github.com/Robust-Rail-NL/robust-rail-evaluator), which verifies if all the actions taken in the plan are valid and respecting the corresponding scenario and location.

### Scenario generation
[robust-rail-generator](https://github.com/Robust-Rail-NL/robust-rail-generator) tool helps to make scenario generation easier. The generated scenario respects the format used by [robust-rail-evaluator](https://github.com/Robust-Rail-NL/robust-rail-evaluator) and [robust-rail-solver](https://github.com/Robust-Rail-NL/robust-rail-solver).


## How To Use?


The [main program](Program.cs) contains several functions with different features.

### Create Plan with Tabu and Local Search methods - from configuration file

Usage: 
```bash
cd ServiceSiteScheduling
dotnet run -- --config=./config.yaml
```
Where [config.yaml](./ServiceSiteScheduling/config.yaml) contains all the parameters needed to specify path to the `location file`, `scenario file` and to define path of the `plan file`. Moreover, the configuration parameters for the Tabu Search and Simulated Annealing are also included in this config file. 

**Details about the parameters**: Explained below (Create Plan with Tabu and Local Search methods).


### Create Plan with Tabu and Local Search methods

* This function takes as input the path to location file `location_path` and the path to the scenario file `scenario_path`. 
    * E.g., of the location is shunting yard - [location_solver.json](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json). 
    * E.g., of the scenario is the time of arrivals & departures, train types/composition - [scenario_solver.json](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_A/scenario_solver.json).

* The function returns a schedule plan as solution to the scenario. The function uses Tabu Search and Simulated Annealing methods to find a Totally Ordered Graph which is finally converted into a schedule plan.
    *  The plan is stored in JSON format and the path/name of the plan defined by `plan_path` input argument (e.g., database/plans/plan.json).  

```bash
CreatePlan(string location_path, string scenario_path, string plan_path)
```

*Note*: default the parameters are used for the Tabu Search and Simulated Annealing methods. However, these parameters can be modified.

* **Tabu Search parameters**:
    * **iterations**: maximum iterations in the searching algorithm if it is achieved the search ends
    * **iterationsUntilReset**: the current solution should be improved until that number of iteration if this number is hit, the current solution cannot be improved → the current solution is reverted to the original solution
    * **tabuListLength**: length of the tabu search list containing LocalSearchMoves → solution graphs
    * **bias**: restricted probability (e.g., 0.75)
    * **suppressConsoleOutput**: enables extra logs


* Example of usage: `ts.Run(40, 100, 16, 0.5);`

* **Simulated Annealing parameters**:

    * **maxduration**: maximum duration of the search in seconds (e.g., Time.Hour is 3600 seconds)
    * **stopWhenFeasible**: stops search when it is feasible (bool)
    * **iterations**: maximum iterations in the searching algorithm if it is achieved the search ends
    * **t**: the T parameter in the equation P = exp([cost(a') - cost(b')]/T), where e T is a control parameter that will be decreased during the search to accept less deterioration in solution quality later on in the process
    * **a**: the rate of the decrease of T (e.g., a=0.97 → 3% of decrease every time q iteration has been achieved)
    * **q**: number of iterations until the next decrease of T (e.g., 2000)
    * **reset**: the current solution should be improved until that number of iteration if this number is hit, the current solution cannot be improved → the current solution is reverted to the original solution (e.g., 2000)
    * **bias**: restricted probability (e.g., 0.4)
    * **suppressConsoleOutput**: enables extra logs
    * **intensifyOnImprovement**: enables further improvements

* Example of usage: `sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);`

Usage: 
```bash
cd ServiceSiteScheduling
dotnet run
```

### Location Scenario Parsing

It is advised to first call `Test_Location_Scenario_Parsing(string location_path, string scenario_path)` function:
* It will test if the given location and scenario (json format) files can be parsed correctly into protobuf objects (`ProblemInstance`). As part of the test, the overall infrastructure of the location (e.g., track parts) will be displayed. If the parsing from `location_solver.json` `->` `protobuf location object` is successful, the json format location will be displayed. When the parsing from `scenario_solver.json` `->` `protobuf scenario object` is successful, the json format scenario will be displayed and some details about the Incoming and Outgoing trains.

Usage of the parsing test:
```bash
Test_Location_Scenario_Parsing(string location_path, string scenario_path)
```
Example: 

```bash
Test_Location_Scenario_Parsing("./database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json", "./database/TUSS-Instance-Generator/setting_A/scenario_solver.json");
```


## Validated scenarios
Some of the scenarios were successfully solved by [robust-rail-solver](https://github.com/Robust-Rail-NL/robust-rail-solver) and the plans were validated by [robust-rail-evaluator](https://github.com/Robust-Rail-NL/robust-rail-evaluator). All the validated scenarios and location files are collected under [scenario-planning-inputs](https://github.com/Robust-Rail-NL/scenario-planning-inputs) repository. Nevertheless, some of those plans are available in the `robust-rail-solver` as well. 


* [**Scenarios:**](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/)

### `Scenario_settings/`
- **`setting_A/`** - scenario at Kleine Binckhorst 6 trains custom config v2
    - **location_solver.json** - Kleine Binckhorst solver format
    - **scenario_solver.json** - 6 trains custom config solver format
    - **plan.json** - plan corresponding to the scenario

- **`setting_B/`** - scenario at Kleine Binckhorst 6 trains custom config v3
    - **location_solver.json** - Kleine Binckhorst solver format
    - **scenario_solver.json** - 6 trains custom config solver format
    - **plan.json** - plan corresponding to the scenario

- **`setting_C/`** - scenario at Kleine Binckhorst 10 trains random 42 seed distribution 1
    - **location_solver.json** - Kleine Binckhorst solver format
    - **scenario_solver.json** - 10 trains custom config solver format
    - **plan.json** - plan corresponding to the scenario
    
- **`setting_D/`** - scenario at Kleine Binckhorst 10 trains random 42 seed distribution 2
    - **location_solver.json** - Kleine Binckhorst solver format
    - **scenario_solver.json** - 10 trains custom config solver format
    - **plan.json** - plan corresponding to the scenario

- **`setting_deep_look`**
    - **clean.sh** - script to clean the results
    - **config.json** - config for the evaluator
    - **location.json** - Kleine Binckhorst evaluator format
    - **location_solver.json** - Kleine Binckhorst solver format
    - **plan.json** - plan corresponding to the scenario
    - **scenario_evaluator.json** - 6 trains custom config evaluator format
    - **scenario_solver.json** - 6 trains custom config solver format
    - **vis_config.json** - emulator config for visualization (this is not functional)

- **`setting_issue`**
    - **clean.sh** - script to clean the results
    - **config.json** - config for the evaluator
    - **location.json** - [Small Yard - switch matter](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/switch.jpg) - switch declaration will result in different plans (valid/not valid) 
    - **location_solver.json** - [Small Yard - switch matter](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/switch.jpg) - switch declaration will result in different plans (valid/not valid) 
    - **plan.json** - plan corresponding to the scenario
    - **scenario_evaluator.json** - 2 trains custom config evaluator format
    - **scenario_solver.json** - 2 trains custom config solver format
    - **vis_config.json** - emulator config for visualization (this is not functional)

- **`setting_known_problems`** - read more about these known problems in **Known Problems** section
    - **setting_invalid_endmove**
    - **setting_multiple_instanding**
    - **setting_occupation_error**

## Partial Order Schedule (POS) - Other helper functions 

There are optional functions to display movement actions and other Partial Order Schedule graphs (relations among the actions i.e., moves using the same infrastructure, service resource, activities that require the same train unit). Example of the partial order schedule of a shunting plan. ![Partial Order Schedule](./ServiceSiteScheduling/POS.png). Reference to the figure: [A Local Search Algorithm for Train Unit Shunting with Service Scheduling](https://pubsonline.informs.org/doi/10.1287/trsc.2021.1090), by Roel van den Broek.

### Helper functions: 
These functions are optional they help to display the current partial order schedule and look for relations between the actions during the Local Search.
* `InitializePOS`: Initialize some values needed to create the POS structure
* `CreatePOS`: Creates a Partial Order Schedule representation from the Totally ordered Solution

Usage:
```bash
POS = new PartialOrderSchedule(start);
POS.InitializePOS();
POS.CreatePOS();
```
Where `start` is the first MoveTask of the totally ordered solution in the `PlanGraph`. Typically, it should be called in the end of the `ComputeLocation()` see `SolutionCost ComputeModel()` -> `ComputeLocation()` in `PlanGraph.cs`.
 
 After the POS is created many functions can be called: 


* `ShowAllInfoAboutTrackTask`: Shows all kind of information about a specific track task

* `ShowAllInfoAboutMove`: Shows all kind of information about a specific Move

* `GetMoveLinksOfPOSMove`: Get all the direct successors and predecessors of a given POS move, the move is identified by its ID (POSMoveTask POSmove.ID). Successors stored in @successorPOSMoves; Predecessors stored in @predecessorsPOSMoves @linkType specifies the type of the links 'infrastructure' - same infrastructure used - populated from @POSadjacencyListForInfrastructure 'trainUnit' - same train unit(s) used - populated from @POSadjacencyListForTrainUnit
      
* `DisplayListPOSTrackTask`: Displays the all POSTrackTask list identified in the POS solution


* `DisplayTrainUnitSuccessorsAndPredeccessors`: Displays all the POSMove predecessors and successors - these links are represents the relations between the moves using the same train unit


* `DisplayMoveLinksOfPOSMove`: Displays all the direct successors and predecessors of a given POS move the move is identified by its ID (POSMoveTask POSmove.ID) @linkType specifies the type of the links 'infrastructure' - same infrastructure used - populated from @POSadjacencyListForInfrastructure 'trainUnit' - same train unit(s) used - populated from @POSadjacencyListForTrainUnit

* `DisplayPOSMovementLinksTrainUnitUsed`: Shows train unit relations between the POS movements, meaning that links per move using the same train unit are displayed - links by train unit
 

* `DisplayPOSMovementLinksInfrastructureUsed`: Shows infrastructure relations between the POS movements, meaning that links per move using the same infrastructure are displayed - links by infrastructure


* `DisplayAllPOSMovementLinks`: Shows all the relations between the POS movements, meaning that all kind of links per move are displayed - links by infrastructure links by same train unit used


* `DisplayInfrastructure`: Shows the Infrastructure of the given location (e.g., shunting yard)


* `DisplayMovements`: Shows rich information about the movements and infrastructure used in the Totally Ordered Solution

## ProtoBuffers

* **Optional step** - all the protobufers used are pre-compiled. Nevertheless, when modifications must be added a proper compilation of protobufs is required.
* New version of protobufers are used to create scenario, location and plan structures. 
* `protoc-28.3-linux-x86_64` (libprotoc 28.3) contains the `protoc` compiler and other proto files.

* `Usage:`

```bash
protoc --proto_path=protos --csharp_out=generated protos/Scenario.proto
protoc --proto_path=protos --csharp_out=generated protos/Location.proto
protoc --proto_path=protos --csharp_out=generated protos/Plan.proto
``` 

* `HIP.csproj has to contain`
```bash
<PackageReference Include="Google.Protobuf" Version="3.28.3" />
```


## Deep Look mode - unit test-like mode
In this mode it is possible to process a unit-like test. It works as follows: 
A default solver format scenario is provided, which can be modified with custom parameter setting and modification, e.g., increment the departure time by a certain amount of constant time. This part of the mode can call customized test cases which are described with a switch statement and each case can represent a different logic of testing (the `@TestCases` parameter is used for these iterations). The tests cases are wrapped in a loop that implies that the cases are repeating until a valid scenario is found, or a maximum number of tries achieved (`@MaxTest` parameter is used). In this same loop, the seed value might also be incremented, since it happens that a given scenario can be solved by modifying the constraints (e.g., modification of the departure time), but it cannot the solver is not able to find a solution became of the randomness (i.e., local search is based on some random parameters).   

When the custom test part of the test is done, the (solver format) scenario is converted to an evaluator enabled format. After the conversion, the evaluator is called (evaluator's executable with the converted scenario and plan as input). The evaluator will store or return the results in the terminal.
The solver saves all the evaluation results, plans and the corresponding scenario files. In the end of the program a summary is generated.

**DeterministicPlanning**:
* This mode also contains a sub-mode `DeterministicPlanning`. This mode allows to set a specific seed (with the `Seed` field under the `DeterministicPlanning` field) for the random number generation, so the plan solving can be reproduced in a deterministic way. 

* In addition, when `LookForSeed` mode is `true` than the initial seed value is incremented for each iteration of a test case block (`testCase`). Basically, a `testCase` block (switch case) is executed with the same seed, and the seed value is increased for each iteration (`itTest`) until a valid result is found or the maximum value of tests is achieved. When the `TestCases` parameter is set to `0` and `LookForSeed` is set to `true`, the scenario is not modified in the test cases only the seed value is incremented - this mode is basically intended to look for a seed (without modifying the constraints in the scenario) which results in a valid or invalid plan for the corresponding scenario.

* To reproduce a plan with a specific seed value the best way is to set `TestCases: -1` and `LookForSeed: false` and specify the seed value `Seed: your_seed_value`.


### Usage

Since the evaluator's execution requires dynamic protobuf libraries, the entire execution must take place in the evaluator's conda environment, which already contains the requested libraries. Call `dotnet run -- --config=./config.yaml`, the configuration for this mode (DeepLook) in `config.yaml` is described below. 


#### Parameters

* `TestCases`: Number of customized test cases which are described with a switch statement and each case can represent a different logic of testing
* `MaxTest`: The testing repeats until a valid scenario is found, or a maximum number of tries achieved
* `EvaluatorInput`: Parameters for the **Evaluator**
    * `Path`: path to the executable of the **Evaluator** (recommended to not modify)
    * `Mode`: **EVAL_AND_STORE** evaluation and storage of the results, **EVAL** evaluation of the results
    * `PathLocation`: path to the folder where the location file take place. Note: this location file is an **Evaluator** format location file
    * `PathScenario`: Path to the scenario which is converted by the **Solver** to an **Evaluator** format, in this mode the **Solver** converts the solver format scenario (specified by `ScenarioPath`) to an **Evaluator** format scenario - Note that the name of the Evaluator format scenario is hard-coded as `scenario_evaluator.json`. The reason is that for each iteration the modified scenario (modified according to the test cases) is stored under this hard-coded name for the evaluation process. After, the evaluation, this scenario is stored with a name highlighting the test cases and if the plan was valid or not valid.   
    * `PathPlan`: Path to the plan that was issued by the **Solver**
    * `PlanType`: it is fixed to "Solver" (the plan is a Solver format plan)
    * `DepartureDelay`: If different from 0, it means that a certain delay of the departure is allowed
* `ConversionAndStorage`:
    * `PathEvalResult`: this parameter is need to specify where the evaluation results (issued by the **Evaluator**) should be stored. Again the file name `Evaluation_Results.txt` is hard-coded because this file is further copied by the **Solver** under another name highlighting the test cases and if the plan was valid or not valid.
    * `PathScenarioEval`: path to the folder where the evaluation results will be stored
* `DeterministicPlanning`:
    * `LookForSeed`: When true the mode will look for a seed value by incrementing `Seed` with the goal of finding a valid plan, note that the upper limit for this loop must be specified by `MaxTest`.
    * `DisplaySeed`: `true/false` to display the seed values with the summary of the results.
    * `Seed`: Initial value of the seed, this value can be used to reproduce results, note: `LookForSeed` should be set to `false` in that case. If the `LookForSeed` is set to `true` the `Seed` value is incremented for each iteration in the loop `MaxTest`.

**Note**: to use this mode the new version of [.devcontainer](https://github.com/Robust-Rail-NL/.devcontainer) is needed. Also, please follow the step described in the [README.md](https://github.com/Robust-Rail-NL/.devcontainer/README.md) before the first usage. 

```bash
conda env list
```
If the list is empty: (or it does not contain my_proto_env)
```bash
cd /workspace/robust-rail-evaluator
conda env create -f env.yml
source ~/.bashrc
conda activate my_proto_env 
cd /workspace/robust-rail-solver/ServiceSiteScheduling
```
Else:
```bash
conda activate my_proto_env 
```

An example of the DeepLook mode in the [config.yaml](./ServiceSiteScheduling/config.yaml) config file: 

```bash
DeepLook:
    TestCases: 10 # Number of test cases to be done
    MaxTest: 10 #  The maximum number of time the loop can be iterated until a valid plan is found
    EvaluatorInput:
        Path: "/workspace/robust-rail-evaluator/build/TORS" # DO NOT Modify ! 
        Mode: "EVAL_AND_STORE" #EVAL_AND_STORE or EVAL - mode to choose, simple evaluation or evaluation with storage of the results (recommended)
        PathLocation: "/workspace/robust-rail-solver/ServiceSiteScheduling/database/" # folder where the evaluator format location file can be found (TODO: later the solver format location should be converted to evaluator format)
        PathScenario: "/workspace/robust-rail-solver/ServiceSiteScheduling/database/scenario_evaluator.json" # Important ! scenario_evaluator.json is generated by the Deep Look mode from the solver format scenarion specified by ScenarioPath in the config.yaml  
        PathPlan: "/workspace/robust-rail-solver/ServiceSiteScheduling/database/plan.json" # plan.json is generated by the solver
        DepartureDelay: "0" # In certain scenarios the departure delay might be allowed, if no delay introduced this parameter should be set to "0" - this parameter is used in the Evaluator only!
        PlanType: "Solver" # To tell the evaluator that a solver formated plan must be evaluated
    ConversionAndStorage:    
        PathEvalResult: "/workspace/robust-rail-solver/ServiceSiteScheduling/database/Evaluation_Results.txt" # The path where the evaluation results (.txt) should be stored 
        PathScenarioEval: "/workspace/robust-rail-solver/ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_deep_look" # The path where the scenario_evaluator.json will be stored after the conversion. Note that the name "scenario_evaluator" is a default name it can be changed in the code, but in that case the PathScenario should point to the "renamed" evaluator format scenarion file. This path also serves for serving the evaluator format scenarios after the evaluation as scenario_case_x_valid/not_valid.json - needed to summaize the run cases.
    DeterministicPlanning:
        LookForSeed: false # looking for a seed, so the seed value is modified for each iteration, if TestCases is 0 and LookForSeed is true the scenario is not modified only the seed value is modified in the loop of @MaxTest
        DisplaySeed: true
        Seed: 11298
```

Run the program: 

```bash
cd ServiceSiteScheduling
dotnet run -- --config=./config.yaml
```

* [config_seed.yaml](./ServiceSiteScheduling/config_seed.yaml) provides an example with a fixed seed value `11298` that will result in a valid plan **[Deeplook mode]**. 
    * If the `Seed` is set to 11297 and the `LookForSeed` is set to true the config will result in one valid and one not valid plan.
* [config_issue.yaml](./ServiceSiteScheduling/config_issue.yaml) provides an example that is looking for a solution by modifying the initial seed value and the test cases **[Deeplook mode]**.

# Known Problems
Several scenario results in an invalid plan. Sometimes these results are due to some constraints in the scenario, some of them are due to suspicious errors/handling tasks in the solver (e.g., same track occupation by multiple train) or in the evaluator (e.g., invalid end move action). These latter should be addressed in future development phases. The following descriptions and configurations help to reproduce the known problems/errors/suspected errors. The configuration, scenario and location files can be found in [setting_known_problems](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/).



| ID | Files  | Config file  | Expected Errors | Log file |
| :------------ |:------------|:------------|:------------|:------------|
| **[Solver issue]** Track occupation issue | [setting_occupation_error](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_occupation_error) | [config_occupation_error.yaml](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_occupation_error/config_occupation_error.yaml) | The train occupation is not always handled in a straightforward way, there are scenarios when train A is parked on a specific track, and later train B is parked on the same track. It used to happen when the deadlines of departure times are tight that train B goes through train A. | [occupation_error.txt](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_occupation_error/occupation_error.txt)|
| **[Evaluator issue]** Invalid EndMove action | [setting_invalid_endmove](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_occupation_error/) | [config_setting_invalid_endmove.yaml](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/config_invalid_endmove.yaml) |  There might be an issue with the train activity checking: when the “move” action followed by an “endmove” action it used to happen that the evaluator states that action is not valid because the train action is already active. To reproduce an error use **seed: 5**. To reproduce a valid plan use the **seed: 6**, departure time 2300 and 2600 respectively. | [invalid_endmove_error.txt](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/invalid_endmove_error.txt) |
| **[Solver issue]** Multiple Instanding Trains | [setting_multiple_instanding](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_multiple_instanding/) | [config_multiple_instanding.yaml](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_multiple_instanding/config_multiple_instanding.yaml) | When the scenario contains multiple instanding trains it happens that the Solver parks too many trains on the departure track, and finally the departure trains start blocking each other movements | [instandning_error.txt](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_multiple_instanding/instandning_error.txt) |

### Specific "structural" issue
| ID | Files  | Config file  | Expected Errors | Log file |
| :------------ |:------------|:------------|:------------|:------------|
| Switch matter | [Definition of a Switch](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/switch.jpg) | Not Specified | Switch definition might affect the solving complexity. Reversing the switch will result in a different location structure which affect directly the plan solving. In as the figure shows, a switch with Bside{5} Aside{4,1} is not the sane as switch Bside{1} Aside{4,5}, however, switch with Bside{4,5} Aside{1} is the same as switch with Bside{4,5} Aside{1} | Not Specified | 

![Switch](./ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_invalid_endmove/switch.jpg)
Figure: Switch




# Build as standalone tool
In principle the robust-rail tools are built in a single Docker do ease the development and usage. Nevertheless, it is possible to use/build `robust-rail-solver` as a standalone tool.


## Building process - Dev-Container
### Dev-Container set up
The usage of **[Dev-Container](https://code.visualstudio.com/docs/devcontainers/tutorial)** is highly recommended in macOS environment. Running **VS Code** inside a Docker container is useful, since it allows compiling and use cTORS without platform dependencies. In addition, **Dev-Container** allows to an easy to use dockerized development since the mounted `ctors` code base can be modified real-time in a docker environment via **VS Code**.

* 1st - Install **Docker**

* 2nd - Install **VS Code** with the **Dev-Container** extension. 

* 3rd - Open the project in **VS Code**

* 4th - `Ctrl+Shif+P` → Dev Containers: Rebuild Container (it can take a few minutes) - this command will use the [Dockerfile](.devcontainer/Dockerfile) and [devcontainer.json](.devcontainer/devcontainer.json) definitions under [.devcontainer](.devcontainer).

* 5th - Build process of the tool is below: 
Note: all the dependencies are already contained by the Docker instance.

## Building process - Native support (Linux)
## Dependencies

To ensure that the code compiles, **dotnet net8.0 framework is required**. The code was tested with `dotnet v8.0.404`.

If you are an Ubuntu user, please go to [Install .NET SDK]("https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?pivots=os-linux-ubuntu-2204&tabs=dotnet9") and choose your Ubuntu version.


### First step:
Example of installation on Ubuntu 20.04:

```bash
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
```
After the packages are located:

```bash
sudo apt-get install -y dotnet-sdk-8.0
```


Other packages might also be needed to be installed on the system:
* ca-certificates
* libc6
* libgcc-s1
* libgssapi-krb5-2
* libicu70
* liblttng-ust1
* libssl3
* libstdc++6
* libunwind8
* zlib1g


```bash
sudo apt install name-of-the-package
```

## Compile ProtoBuf
In case the ProtoBuf structures must be modified (they can be found under [ProtoBuf](./ServiceSiteScheduling/ProtoBuf/)), then they must be compiled, so the main program can call their functionalities.

If first usage:

```bash
conda env create -f env.yml
source ~/.bashrc
```

Activate the environment:

```bash
conda activate my_proto_env_solver
protoc --proto_path="/workspace/robust-rail-solver/ServiceSiteScheduling/ProtoBuf" --csharp_out="/workspace/robust-rail-solver/ServiceSiteScheduling/ProtoBuf" /workspace/robust-rail-solver/ServiceSiteScheduling/ProtoBuf/name_of_the_file_to_compile.proto
```


## Issues
There is a known issue when using the new `.devcontainer` of the project. It might happen that after switching to the new version, the following error will be raised when running the solver:

### Issue 1
```bash
/usr/share/dotnet/sdk/8.0.411/Microsoft.Common.CurrentVersion.targets(3829,5): error MSB3491: Could not write lines to file "obj/Debug/net8.0/HIP.csproj.CoreCompileInputs.cache". Access to the path '/workspace/robust-rail-solver/ServiceSiteScheduling/obj/Debug/net8.0/HIP.csproj.CoreCompileInputs.cache' is denied.  [/workspace/robust-rail-solver/ServiceSiteScheduling/HIP.csproj]
```

Solution:

```bash
cd /workspace/robust-rail-solver/ServiceSiteScheduling
rm -rf obj/
rm -rf bin/
```

### Issue 2
```bash
./build/TORS: error while loading shared libraries: libprotobuf.so.26: cannot open shared object file: No such file or directory
```
In that case the `robust-rail-evaluator` project should be rebuilt. 

If it was already rebuilt, then:
```bash
cd /workspace/robust-rail-evaluator
conda env create -f env.yml # if the env has not yet been build
source ~/.bashrc
conda activate my_proto_env 
cd /workspace/robust-rail-solver/ServiceSiteScheduling
```

## Publishing the HIP image
`HIP.csproj`'s `<Version>` element is the single source of truth for the image version — it is baked into the Docker image (via build-arg) as the `org.opencontainers.image.version` label, and used as the image tag, so it never needs updating in more than one place.

To bump it, use [`bump-version.sh`](ServiceSiteScheduling/bump-version.sh):
```bash
cd ServiceSiteScheduling
./bump-version.sh patch        # 1.4.1 -> 1.4.2
./bump-version.sh minor        # 1.4.1 -> 1.5.0
./bump-version.sh major        # 1.4.1 -> 2.0.0
./bump-version.sh prerelease   # 2.0.0-alpha.1 -> 2.0.0-alpha.2
./bump-version.sh 3.0.0-beta.2 # set an explicit version
```

Then [`docker-push.sh`](ServiceSiteScheduling/docker-push.sh) builds and pushes the multi-arch (`linux/amd64,linux/arm64`) HIP image to `ghcr.io/robust-rail-nl/hip`, tagged with the current `HIP.csproj` version:

```bash
./docker-push.sh
```

The `:latest` tag is only applied for final `1.x.y` releases — prerelease versions (including the `noproto` branch's `2.0.0-alpha.*` line) are pushed under their own tag only, so they never shadow the current stable image.

The script creates a dedicated `buildx` builder (`robust-rail-builder`) using the `docker-container` driver with `network=host`. This is required on some machines: the default `docker-container` driver runs BuildKit in an isolated network namespace whose DNS resolution can fail to reach private/LAN DNS servers, causing errors like:

```
failed to resolve source metadata for mcr.microsoft.com/dotnet/sdk:8.0: ... dial tcp: lookup mcr.microsoft.com on <lan-ip>:53: i/o timeout
```

This typically shows up as `docker build` working fine while `docker buildx build` times out. `network=host` makes the builder container share the host's network stack, sidestepping the issue. If you already have a stale builder without this option, remove it first with `docker buildx rm robust-rail-builder`.

The builder is shared across sibling `Robust-Rail-NL` projects that also need multi-arch/`network=host` builds (e.g. `robust-rail-evaluator`) — a `buildx` builder isn't tied to a specific repo, so there's no need for a separate one per project.