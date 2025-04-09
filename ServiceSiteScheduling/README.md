# Description 

## Building process - Dev-Container
### Dev-Container setup
The usage of **[Dev-Container](https://code.visualstudio.com/docs/devcontainers/tutorial)** is highly recommanded in macOS environment. Running **VS Code** inside a Docker container is useful, since it allows to compile and use cTORS without plaform dependencies. In addition, **Dev-Container** allows to an easy to use dockerized development since the mounted `ctors` code base can be modified real-time in a docker environment via **VS Code**.

* 1st - Install **Docker**

* 2nd - Install **VS Code** with the **Dev-Container** extension. 

* 3rd - Open the project in **VS Code**

* 4th - `Ctrl+Shif+P` → Dev Containers: Rebuild Container (it can take a few minutes) - this command will use the [Dockerfile](.devcontainer/Dockerfile) and [devcontainer.json](.devcontainer/devcontainer.json) definitions unde [.devcontainer](.devcontainer).

* 5th - Build process of the tool is below: 
Note: all the dependencies are alredy contained by the Docker instance.

## Building process - Native support (Linux)
## Dependencies

To ensure that the code compiles, **dotnet net8.0 framework is required**. The code was tested with `dotnet v8.0.404`.

If you are a Ubuntu user, please go to [Install .NET SDK]("https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?pivots=os-linux-ubuntu-2204&tabs=dotnet9") and chooes your Ubuntu version.


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


Other packages might also needed to be installed on the system:
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


## Issue with input data

Some of the input location and scenarios (scenario.data and location.data) cannot be read in the main program by parsing with the protobuffres. 

* In [database/fix](database/fix) a selection of readable scenario and location data is available.

* In [database/TUSS-Instance-Generator/featured](database/TUSS-Instance-Generator/featured) a collection of scenarios can be found run on the [Kleine Binckhorst shunting yard](Kleine_Binckhorst.png). The standard location file of Kleine Binckhorst is described as [location.json](database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json).

    * [scenario_kleineBinckhorst_HIP_dump](database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json) is a working scenarion on Kleine Binckhorst location.


## How To Use ?


The [main program](Program.cs) contains several functions with different features.

### Location Scenario Parsing

It is advised to first call `Test_Location_Scenario_Parsing(string location_path, string scenario_path)` function:
* It will test if the given location and scenario (json format) files can be parsed correctly into protobuf objects (`ProblemInstance`). As part of the test, the overall infrastructure of the location (e.g.,track parts) will be displayed. If the parsing from `location.json` `->` `protobuf location object` is successfull, the json format location will be displayed. When the the parsing from `sceenario.json` `->` `protobuf scenario object` is successfull, the json format scenario will be displayed and some detalis about the Incoming and Outgoing trains.

Usage of the parsing test:
```bash
Test_Location_Scenario_Parsing(string location_path, string scenario_path)
```
Example: 

```bash
Test_Location_Scenario_Parsing("./database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json", "./database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json");
```

### Create Plan with Tabu and Local Search methods - from configuration file

Usage: 
```bash 
dotnet run -- --config=./config.yaml
```
Where [config.yaml](./config.yaml) contains all the parameters needed to specify path to the `location file`, `scenario file` and to define path of the `plan file`. Morevoer, the configuration parameters for the Tabu Search and Simulated Annealing are also included in this config file. 

**Details about the parameters**: Explained below (Create Plan with Tabu and Local Search methods).


### Create Plan with Tabu and Local Search methods

* This function takse as input the path to location file `location_path` and the path to the scenario file `scenario_path`. 
    * E.g., of the location is shunting yard - [location.json](database/TUSS-Instance-Generator/featured/location_kleineBinckhorst_HIP_dump.json). 
    * E.g., of the sceenario is the time of arrivals & departures, train types/composition - [scenario.json](database/TUSS-Instance-Generator/featured/scenario_kleineBinckhorst_HIP_dump.json).

* The function returns a schedule plan as solution to the scenario. The function uses Tabu Search and Simulated Annealing mehods to find a Totally Ordered Graph which is finally converted into a schedule plan.
    *  The plan is stored in json format and the path/name of the plan defined by `plan_path` input argument (e.g., database/plans/plan.json).  

```bash
CreatePlan(string location_path, string scenario_path, string plan_path)
```

*Note*: default the parameters are used for the Tabu Search and Simulated Annealing methods. However, these parameters can be modified.

* **Tabu Search parameters**:
    * **iterations**: maximum iterations in the searching algorithm if it is achieved the search ends
    * **iterationsUntilReset**: the current solution should be improved until that number of iteration if this number is hit, the current solution  cannot be improved -> the current solution is reverted to the original solution
    * **tabuListLength**: length of the tabu search list conaining LocalSerachMoves -> solution graphs
    * **bias**: restricted probability (e.g., 0.75)
    * **suppressConsoleOutput**: enables extra logs


* Example of usage: `ts.Run(40, 100, 16, 0.5);`



* **Simulated Annealing parameters**:

    * **maxduration**: maximum duration of the serach in seconds (e.g., Time.Hour is 3600 seconds)
    * **stopWhenFeasible**: stops serach when it is feaseable (bool)
    * **iterations**: maximum iterations in the searching algorithm if it is achieved the search ends
    * **t**: the T parameter in the equation P = exp([cost(a') - cost(b')]/T), where e T is a control parameter that will be decreased  during the search to accept less deterioration in solution quality later on in the process
    * **a**: the rate of the decrease of T (e.g., a=0.97 -> 3% of decrease every time q iteration has been achieved)
    * **q**: number of iterations until the next decrease of T (e.g., 2000)
    * **reset**: the current solution should be improved until that number of iteration if this number is hit, the current solution cannot be improved -> the current solution is reverted to the original solution (e.g., 2000)
    * **bias**: restricted probability (e.g., 0.4)
    * **suppressConsoleOutput**: enables extra logs
    * **intensifyOnImprovement**: enables further improvments

* Example of usage: `sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);`




## POS - can we find POS or everithing is Totally Ordred ?

```bash
LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts,Graph);
sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);
```
`SimulatedAnnealing` -> `Run`

* `Run:`
Before `while(true)` -> `this.Graph.ComputeModel();`

* `Graph.ComputeModel()`: 

```bash
public SolutionCost ComputeModel()
{
    foreach (var departure in this.DepartureTasks)
        (departure.Previous as DepartureRoutingTask).UpdatePreviousTaskOrder();

    return this.ComputeModel(this.First, this.Last);
}
```

```bash
 ComputeLocation(MoveTask start, MoveTask recomputestart, MoveTask recomputeend)
```


```bash
public SolutionCost ComputeModel(MoveTask recomputestart, MoveTask recomputeend)
{
    for (int i = 0; i < this.TrackOccupations.Length; i++)
        if (this.TrackOccupations[i] != null)
            this.TrackOccupations[i].Reset();

        this.ComputeLocation(this.First, recomputestart, recomputeend);
        this.ComputeTime(recomputestart, recomputestart.PreviousMove?.End ?? 0);
        return this.ComputeCost();
}
```


```bash
    Track Task - Types: Service, Parking, Arrival, Departure
        * Previous Move Task
        * Next Move Task
        * Train (shunt train)

    Inheriting classes:
        * Service Task
        * Parking Task
        * Arrival Task
        * Departure Task

       

    Move (Move Task) - Standard, Departure Type
        --> Train Unit
        --> Relation Precedence Activities 
        --> Movement from Track i->j

    Ingeriting classes:
        * Departure Routing Task
        * Routing Task/     
```    

```bash
public virtual void FindAllPrevious(Func<TrackTask, bool> predicate, List<TrackTask> output)
{
    foreach (TrackTask task in this.AllPrevious)
    {
        if (predicate(task))
            output.Add(task);
        if (!(task is ArrivalTask))
            task.Previous.FindAllPrevious(predicate, output);
    }
}

Call it as:

task.Previous.FindAllPrevious(t => t == task, tasks);

Avoids cyclic references to task itself

```



```bash

Parking State --
State -> conatins a Task (TrackTask) -> can give information where the given train is situating
  
public override string ToString()
{
    return $"{this.Task.Train} at {this.Task.Track.ID}";
}
```

* RoutingGraph

```bash
SuperVertex[];
Vertex[] Vertices;
public Arc[,] ArcMatrix;
```

```bash
Vertex aa = new Vertex(Side.A, Side.A); // by definition Vertex(Side trackside, Side arrivalside)
```