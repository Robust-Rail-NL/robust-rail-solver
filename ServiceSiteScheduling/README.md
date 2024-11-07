# Description 


## ProtoBuffers

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

In [database/fix](database/fix) a selection of readable scenario and location data is available.


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