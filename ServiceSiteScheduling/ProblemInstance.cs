using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using Switch = ServiceSiteScheduling.TrackParts.Switch;

namespace ServiceSiteScheduling
{
    class ProblemInstance
    {
        public static ProblemInstance Current;

        public TrainType[] TrainTypes;
        public TrainUnit[] TrainUnits;
        public Dictionary<TrainType, TrainUnit[]> TrainUnitsByType;

        public Track[] Tracks;
        public ServiceType[] ServiceTypes;
        public ServiceLocation[] ServiceLocations;

        public Dictionary<Time, ArrivalTrain> ArrivalsByTime;
        public ArrivalTrain[] ArrivalsOrdered;

        public Dictionary<Time, DepartureTrain> DeparturesByTime;
        public Dictionary<TrainType, List<DepartureTrain>> DeparturesByType;
        public DepartureTrain[] DeparturesOrdered;

        public NoProto.Location InterfaceLocation;
        public NoProto.Scenario InterfaceScenario;
        public Dictionary<TrainUnit, NoProto.IncomingTrainUnit> TrainUnitConversion;
        public Dictionary<ServiceType, NoProto.Facility> FacilityConversion;
        public Dictionary<ulong, TrackSwitchContainer> GatewayConversion;

        public Service[][] FreeServices;

        public ulong ScenarioStartTime;
        public ulong ScenarioEndTime;

        public void FillTrains()
        {
            this.TrainUnitsByType = [];
            foreach (TrainType type in this.TrainTypes)
                this.TrainUnitsByType[type] = this
                    .TrainUnits.Where(unit => unit.Type == type)
                    .ToArray();
        }

        public void FillArrivals()
        {
            this.ArrivalsByTime = [];
            foreach (ArrivalTrain arrival in this.ArrivalsOrdered)
                this.ArrivalsByTime[arrival.Time] = arrival;
        }

        public void FillDepartures()
        {
            this.DeparturesByTime = [];
            this.DeparturesByType = [];
            foreach (DepartureTrain departure in this.DeparturesOrdered)
            {
                this.DeparturesByTime[departure.Time] = departure;
                foreach (
                    var type in departure
                        .Units.Select(unit => unit.IsFixed ? unit.Unit.Type : unit.Type)
                        .Distinct()
                )
                {
                    if (!this.DeparturesByType.ContainsKey(type))
                        this.DeparturesByType[type] = [];
                    this.DeparturesByType[type].Add(departure);
                }
            }
        }

        public static string ParseJsonToString(string path)
        {
            string jsonContent;
            using (var input = File.OpenRead(path))

            using (StreamReader reader = new(input))
            {
                jsonContent = reader.ReadToEnd();
            }
            return jsonContent;
        }

        public static ProblemInstance ParseJson(string locationpath, string scenariopath)
        {
            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                Converters = { new JsonStringEnumConverter() },
            };

            Console.WriteLine($"Parsing JSON location from {locationpath}");
            NoProto.Location location;
            using (var input = File.OpenRead(locationpath))
            using (StreamReader reader = new(input))
            {
                string jsonContent = reader.ReadToEnd();
                location = JsonSerializer.Deserialize<NoProto.Location>(jsonContent, options);
            }

            Console.WriteLine($"Parsing JSON scenario from {scenariopath}");
            NoProto.Scenario scenario;
            using (var input = File.OpenRead(scenariopath))
            using (StreamReader reader = new(input))
            {
                string jsonContent = reader.ReadToEnd();
                scenario = JsonSerializer.Deserialize<NoProto.Scenario>(jsonContent, options);
            }

            return Parse(location, scenario);
        }

        public static ProblemInstance Parse(string locationpath, string scenariopath)
        {
            NoProto.Location location;
            using (var input = File.OpenRead(locationpath))
                location = JsonSerializer.Deserialize<NoProto.Location>(input);

            NoProto.Scenario scenario;
            using (var input = File.OpenRead(scenariopath))
                scenario = JsonSerializer.Deserialize<NoProto.Scenario>(input);

            return Parse(location, scenario);
        }

        // Warn-and-continue: a missing or unexpected schemaVersion is logged,
        // never a hard reject. Internal (rather than private) so tests can
        // exercise it directly without going through a full Parse().
        //
        // The logger is a parameter so that tests can read back what was
        // written instead of only checking that nothing threw. Passing one
        // rather than swapping the static factory keeps it safe under xUnit's
        // parallelism: there is no shared state to race on.
        internal static void WarnOnSchemaVersionMismatch(
            string modelName,
            int? version,
            ILogger logger = null
        )
        {
            logger ??= Logging.GetLogger();
            if (version is null)
            {
                logger.LogWarning(
                    "{ModelName}: schemaVersion is missing; assuming {Expected}.",
                    modelName,
                    NoProto.InterchangeSchema.ExpectedVersion
                );
            }
            else if (version != NoProto.InterchangeSchema.ExpectedVersion)
            {
                logger.LogWarning(
                    "{ModelName}: schemaVersion {Actual} does not match expected {Expected}.",
                    modelName,
                    version,
                    NoProto.InterchangeSchema.ExpectedVersion
                );
            }
        }

        public static ProblemInstance Parse(
            NoProto.Location location,
            NoProto.Scenario scenario,
            int debugLevel = 0
        )
        {
            WarnOnSchemaVersionMismatch("Location", location.SchemaVersion);
            WarnOnSchemaVersionMismatch("Scenario", scenario.SchemaVersion);

            //debugLevel = 2;
            ProblemInstance instance = new();

            // Get start and end time of the scenario
            // this is needed sice the start time will be associated with the arrival time
            // of the instanding trains (which were already on the shunting yard "before"
            // the scenarion), the end time is associated with the departure time of the
            // outstanding trains which "exits" the scenario but in real they stays on the yard
            instance.ScenarioStartTime = scenario.StartTime;
            instance.ScenarioEndTime = scenario.EndTime;

            instance.InterfaceLocation = location;
            instance.InterfaceScenario = scenario;

            List<Track> tracks = [];
            List<GateWay> gateways = [];
            Dictionary<ulong, Infrastructure> infrastructuremap = [];
            int index = 0;

            // Construct the track parts
            foreach (var part in location.TrackParts)
            {
                switch (part.Type)
                {
                    case NoProto.TrackPartType.RailRoad:
                        Track track = new(
                            part.Id,
                            part.Name,
                            ServiceType.None,
                            (int)(part.Length ?? 0),
                            Side.None,
                            part.ParkingAllowed,
                            part.SawMovementAllowed,
                            index++
                        );
                        tracks.Add(track);
                        infrastructuremap[part.Id] = track;
                        break;
                    case NoProto.TrackPartType.Switch:
                        infrastructuremap[part.Id] = new Switch(part.Id, part.Name);
                        break;
                    case NoProto.TrackPartType.EnglishSwitch:
                        infrastructuremap[part.Id] = new EnglishSwitch(part.Id, part.Name);
                        break;
                    case NoProto.TrackPartType.HalfEnglishSwitch:
                        infrastructuremap[part.Id] = new HalfEnglishSwitch(part.Id, part.Name);
                        break;
                    case NoProto.TrackPartType.Intersection:
                        infrastructuremap[part.Id] = new Intersection(part.Id, part.Name);
                        break;
                    case NoProto.TrackPartType.Bumper:
                        var gateway = new GateWay(part.Id, part.Name);
                        infrastructuremap[part.Id] = gateway;
                        gateways.Add(gateway);
                        break;
                    default:
                        Logging
                            .GetLogger()
                            .LogWarning(
                                $"No TrackPartType set for TrackPart {part.Name} ({part.Id}); assuming RailRoad"
                            );
                        goto case NoProto.TrackPartType.RailRoad;
                }
            }
            instance.Tracks = tracks.ToArray();
            // Connect the parts
            foreach (var part in location.TrackParts)
            {
                if (debugLevel > 1)
                {
                    Console.WriteLine($"Track Parts : {part}");
                }

                switch (part.Type)
                {
                    case NoProto.TrackPartType.RailRoad:
                        Track track = infrastructuremap[part.Id] as Track;
                        Infrastructure A = null,
                            B = null;
                        if (part.ASide.Length > 0)
                        {
                            infrastructuremap.TryGetValue(part.ASide[0], out A);
                        }
                        if (part.BSide.Length > 0)
                            infrastructuremap.TryGetValue(part.BSide[0], out B);
                        track.Connect(A, B);

                        break;
                    case NoProto.TrackPartType.Switch:
                        Switch @switch = infrastructuremap[part.Id] as Switch;
                        if (part.ASide.Length == 1)
                        { // A side is connected to two B side infrastructure
                            Debug.Assert(part.BSide.Length == 2);
                            @switch.Connect(
                                infrastructuremap[part.ASide[0]],
                                new Infrastructure[2]
                                {
                                    infrastructuremap[part.BSide[0]],
                                    infrastructuremap[part.BSide[1]],
                                }
                            );
                        }
                        else
                        { // B side is connected to two A side infrastructure
                            Debug.Assert(part.ASide.Length == 2);
                            @switch.Connect(
                                infrastructuremap[part.BSide[0]],
                                new Infrastructure[2]
                                {
                                    infrastructuremap[part.ASide[0]],
                                    infrastructuremap[part.ASide[1]],
                                }
                            );
                        }

                        break;
                    case NoProto.TrackPartType.EnglishSwitch:
                        EnglishSwitch englishswitch = infrastructuremap[part.Id] as EnglishSwitch;
                        englishswitch.Connect(
                            part.ASide.Select(neighbor => infrastructuremap[neighbor]).ToList(),
                            part.BSide.Select(neighbor => infrastructuremap[neighbor]).ToList()
                        );
                        break;
                    case NoProto.TrackPartType.HalfEnglishSwitch:
                        Debug.Assert(part.ASide.Length == 2);
                        Debug.Assert(part.BSide.Length == 2);
                        HalfEnglishSwitch halfenglishswitch =
                            infrastructuremap[part.Id] as HalfEnglishSwitch;
                        halfenglishswitch.Connect(
                            infrastructuremap[part.ASide[0]],
                            infrastructuremap[part.ASide[1]],
                            infrastructuremap[part.BSide[0]],
                            infrastructuremap[part.BSide[1]]
                        );
                        break;
                    case NoProto.TrackPartType.Intersection:
                        Debug.Assert(part.ASide.Length == 2);
                        Debug.Assert(part.BSide.Length == 2);
                        Intersection intersection = infrastructuremap[part.Id] as Intersection;
                        intersection.Connect(
                            infrastructuremap[part.ASide[0]],
                            infrastructuremap[part.BSide[1]],
                            infrastructuremap[part.ASide[1]],
                            infrastructuremap[part.BSide[0]]
                        );
                        break;
                    case NoProto.TrackPartType.Bumper:
                        GateWay gateway = infrastructuremap[part.Id] as GateWay;

                        if (debugLevel > 1)
                        {
                            Console.WriteLine($"gateway : {gateway}");
                        }
                        if (part.ASide.Length != 0)
                        {
                            if (debugLevel > 1)
                            {
                                Console.WriteLine($"Track part A: {part.Id}");
                                Console.WriteLine($"Infra: {infrastructuremap[part.Id]}");
                            }
                            gateway.Connect(infrastructuremap[part.ASide[0]]);
                        }
                        else if (part.BSide.Length != 0)
                        {
                            if (debugLevel > 1)
                            {
                                Console.WriteLine($"Track part B: {part.Id}");
                                Console.WriteLine($"Infra: {infrastructuremap[part.Id]}");
                            }
                            gateway.Connect(infrastructuremap[part.BSide[0]]);
                        }
                        else
                        { // Remove gateway from gateways dict
                            gateways.Remove(gateway);
                        }
                        break;
                    default:
                        goto case NoProto.TrackPartType.RailRoad;
                }
            }
            Dictionary<GateWay, TrackSwitchContainer> gatewayconnections = [];
            foreach (var gateway in gateways)
            {
                List<Infrastructure> path = [gateway];

                gatewayconnections[gateway] = gateway
                    .EndPoint.GetTracksConnectedTo(gateway, 0, path, false)
                    .First();
                if (debugLevel > 1)
                {
                    Console.WriteLine(
                        $"gatewayconnections[gateway]: {gateway}:{gatewayconnections[gateway]}"
                    );
                }
            }

            // LP FIXME shouldn't we also iterate over InStanding here?
            Dictionary<NoProto.TaskType, ServiceType> taskmap = [];
            var tasktypes = scenario
                .In.Aggregate(
                    new List<NoProto.TaskType>(),
                    (list, train) =>
                    {
                        list.AddRange(
                            train.Members.Aggregate(
                                new List<NoProto.TaskType>(),
                                (l, unit) =>
                                {
                                    l.AddRange(unit.Tasks.Select(task => task.Type));
                                    return l;
                                }
                            )
                        );
                        return list;
                    }
                )
                .Distinct()
                .ToList();

            instance.ServiceTypes = new ServiceType[tasktypes.Count];
            for (int i = 0; i < tasktypes.Count; i++)
            {
                var type = tasktypes[i];
                ServiceType service = new(i, type.Other, ServiceLocationType.Fixed);
                instance.ServiceTypes[i] = service;
                taskmap[type] = service;
                if (debugLevel > 1)
                {
                    Console.WriteLine($">>>>Service : {service}");
                }
            }

            // Facilities
            // Connect the tracks to the services
            instance.FacilityConversion = [];
            var servicetracks = new HashSet<Track>();
            var freetracks = new HashSet<Track>();
            var crews = new List<ServiceCrew>();
            foreach (var facility in location.Facilities)
            {
                if (debugLevel > 1)
                {
                    Console.WriteLine($">>>>Facility : {facility}");
                }

                var facilitytracks = new List<Track>();
                foreach (var part in facility.RelatedTrackPartIDs)
                {
                    if (infrastructuremap.TryGetValue(part, out Infrastructure infra))
                    {
                        var track = infra as Track;
                        facilitytracks.Add(track);
                        if (facility.Type != "Unknown")
                            servicetracks.Add(track);
                        else
                        {
                            freetracks.Add(track);
                        }
                    }
                }

                if (facility.Type == "Unknown")
                {
                    for (int i = 0; i < 30; i++)
                    {
                        var crew = new ServiceCrew(
                            "crew " + i,
                            facility
                                .TaskTypes.AsQueryable<NoProto.TaskType>()
                                .Select(type => taskmap[type])
                        );
                        crews.Add(crew);
                    }
                }

                foreach (var type in facility.TaskTypes)
                {
                    if (taskmap.TryGetValue(type, out ServiceType service))
                    {
                        foreach (var track in facilitytracks)
                            service.Tracks.Add(track);
                        instance.FacilityConversion[service] = facility;
                        if (facility.Type == "Unknown")
                        {
                            service.LocationType = ServiceLocationType.Free;
                            service.Resources.AddRange(crews);
                        }
                    }
                }
            }

            instance.ServiceLocations = new ServiceLocation[instance.Tracks.Length];
            foreach (var track in servicetracks)
                instance.ServiceLocations[track.Index] = new ServiceLocation(
                    track.ID.ToString(),
                    instance.ServiceTypes.Where(type => type.Tracks.Contains(track)),
                    track
                );
            foreach (
                var service in instance.ServiceTypes.Where(s =>
                    s.LocationType == ServiceLocationType.Fixed
                )
            )
            foreach (var track in service.Tracks)
                service.Resources.Add(instance.ServiceLocations[track.Index]);

            if (debugLevel > 1)
            {
                foreach (var track in servicetracks)
                    Console.WriteLine(
                        $"Service location: {instance.ServiceLocations[track.Index]}"
                    );
            }

            // Determine train types
            List<TrainType> traintypes = [];
            List<TrainUnit> trainunits = [];
            List<ArrivalTrain> arrivals = [];
            Dictionary<(string TypePrefix, uint Carriages), TrainType> traintypemap = [];
            Dictionary<uint, TrainUnit> trainunitmap = [];
            instance.TrainUnitConversion = [];
            instance.GatewayConversion = [];
            var freeservicelists = new List<Service[]>();

            foreach (var tut in scenario.TrainUnitTypes)
            {
                var name = tut.TypePrefix;
                TrainType type = new(
                    traintypes.Count,
                    name,
                    (int)tut.Length,
                    instance.Tracks.Where(t => t.CanPark).ToArray(),
                    (int)tut.BackNormTime,
                    (int)tut.BackAdditionTime * (int)tut.Carriages,
                    (int)tut.CombineDuration,
                    (int)tut.SplitDuration
                );
                traintypes.Add(type);
                traintypemap[tut.TypeDisplayName()] = type;
            }

            foreach (var arrivaltrain in scenario.In)
            {
                var currenttrainunits = GetTrainUnits(arrivaltrain);

                if (debugLevel > 1)
                {
                    Console.WriteLine(
                        $"Track part : {infrastructuremap[arrivaltrain.EntryTrackPart]}"
                    );
                }

                if (infrastructuremap[arrivaltrain.EntryTrackPart] is GateWay gateway)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(
                        connection.Path[connection.Path.Length - 2]
                    );
                    var train = new ArrivalTrain(
                        currenttrainunits.ToArray(),
                        connection.Track,
                        side,
                        (int)arrivaltrain.Departure
                    );
                    arrivals.Add(train);

                    if (debugLevel > 1)
                    {
                        Console.WriteLine($"connection :{connection}");
                        Console.WriteLine($"gateway :{gateway}");
                        Console.WriteLine($"side :{side}");
                        Console.WriteLine($"connection.Track :{connection.Track}");
                    }
                }
                if (debugLevel > 1)
                {
                    foreach (var arrival in arrivals)
                    {
                        Console.WriteLine($"Arrival train : {arrival}");
                    }
                }
            }

            // Consider the instanding trains as arrival (incoming) trains
            // the time of arrival of these trains is set to the start time of the scenario

            // The solver does not yet order multiple inStanding trains sharing a track
            // (see solver#18); reject such scenarios rather than silently guessing an
            // order that may not match the one the scenario intends.
            foreach (var group in (scenario.InStanding ?? []).GroupBy(t => t.FirstParkingTrackPart))
            {
                if (group.Count() > 1)
                {
                    throw new NotSupportedException(
                        $"Track part {group.Key} has {group.Count()} inStanding trains. "
                            + "The solver does not yet support multiple inStanding trains on the same track (see solver#18)."
                    );
                }
            }

            foreach (var arrivaltrain in scenario.InStanding ?? [])
            {
                var currenttrainunits = GetTrainUnits(arrivaltrain);

                if (debugLevel > 1)
                {
                    Console.WriteLine(
                        $"Track part : {infrastructuremap[arrivaltrain.EntryTrackPart]}"
                    );
                }

                if (infrastructuremap[arrivaltrain.EntryTrackPart] is GateWay gateway)
                {
                    if (debugLevel > 1)
                    {
                        Console.WriteLine(
                            $"************ Start time: {(int)instance.ScenarioStartTime}"
                        );
                    }
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(
                        connection.Path[connection.Path.Length - 2]
                    );
                    var train = new ArrivalTrain(
                        currenttrainunits.ToArray(),
                        connection.Track,
                        Side.None,
                        (int)instance.ScenarioStartTime,
                        true,
                        arrivaltrain.StandingIndex ?? 0.0
                    );
                    arrivals.Add(train);

                    if (debugLevel > 1)
                    {
                        Console.WriteLine($"connection :{connection}");
                        Console.WriteLine($"gateway :{gateway}");
                        Console.WriteLine($"side :{side}");
                        Console.WriteLine($"connection.Track :{connection.Track}");
                    }
                }
                else
                {
                    var infra = infrastructuremap[arrivaltrain.FirstParkingTrackPart] as Track;

                    // Switch @switch = infrastructuremap[departuretrain.LeaveTrackPart] as Switch;
                    // TODO : add switch statement for more infratype
                    if (debugLevel > 1)
                    {
                        Console.WriteLine($">>>>> Arrival Infra Access: {infra} - {infra.Access}");
                    }

                    var train = new ArrivalTrain(
                        currenttrainunits.ToArray(),
                        infra,
                        infra.Access,
                        (int)instance.ScenarioStartTime,
                        true,
                        arrivaltrain.StandingIndex ?? 0.0
                    );
                    arrivals.Add(train);
                }
            }

            if (debugLevel > 1)
            {
                foreach (var arrival in arrivals)
                    Console.WriteLine($"Arrival train : {arrival}");
            }

            instance.TrainTypes = traintypes.ToArray();
            instance.TrainUnits = trainunits.ToArray();
            instance.FillTrains();

            instance.ArrivalsOrdered = arrivals.OrderBy(arrival => arrival.Time).ToArray();

            if (debugLevel > 1)
            {
                Console.WriteLine("Arrivals Ordered: ");
                foreach (var item in instance.ArrivalsOrdered)
                {
                    Console.WriteLine(item);
                }
            }

            // Change order when the standingInedex is lower!
            // var tmpArrivals = instance.ArrivalsOrdered;

            // for (int i = 0; i < tmpArrivals.Length - 1; i++)
            // {
            //     var tmpArrival = tmpArrivals[i];
            //     if (tmpArrival.IsItInStanding())
            //     {

            //         for (int j = i + 1; j < tmpArrivals.Length; j++)
            //         {
            //             if (!tmpArrivals[j].IsItInStanding())
            //             {
            //                 // swap
            //                 var tmp = tmpArrivals[j];
            //                 tmpArrivals[j] = tmpArrivals[i];
            //                 tmpArrivals[i] = tmp;
            //                 break;
            //             }

            //         }
            //     }

            // }

            // // for (int i = 0; i < tmpArrivals.Length-1; i++)
            // // {
            // //     var tmpArrival = tmpArrivals[i];
            // //     for (int j = i + 1; j < tmpArrivals.Length; j++)
            // //     {
            // //         if (tmpArrivals[i].Track == tmpArrivals[j].Track)
            // //         {
            // //             if (tmpArrivals[i].StandingIndex > tmpArrivals[j].StandingIndex)
            // //             {
            // //                 var tmp = tmpArrivals[j];
            // //                 tmpArrivals[j] = tmpArrivals[i];
            // //                 tmpArrivals[i] = tmp;
            // //             }
            // //         }
            // //     }
            // // }

            // Console.WriteLine("Arrivals Re-Ordered: ");
            // foreach (var item in instance.ArrivalsOrdered)
            // {
            //     Console.WriteLine(item);
            // }
            // foreach (var item in tmpArrivals)
            // {
            //     Console.WriteLine(item);
            // }

            instance.FillArrivals();

            instance.FreeServices = freeservicelists.ToArray();

            var departures = new List<DepartureTrain>();
            foreach (var departuretrain in scenario.Out)
            {
                var units = departuretrain.TrainUnits.Select(unit =>
                    unit.Id is null
                        ? new DepartureTrainUnit(traintypemap[unit.TypeDisplayName()])
                        : new DepartureTrainUnit(trainunitmap[unit.Id.Value])
                );

                if (infrastructuremap[departuretrain.LeaveTrackPart] is GateWay gateway)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(
                        connection.Path[connection.Path.Length - 2]
                    );
                    var train = new DepartureTrain(
                        (int)departuretrain.Arrival,
                        units.ToArray(),
                        connection.Track,
                        side
                    );
                    departures.Add(train);

                    foreach (var unit in units)
                        unit.Train = train;
                }

                // foreach (var departure in departures)
                //     Console.WriteLine($"Departure train : {departure}");
            }

            // Consider the outstanding trains as departure (outgoing) trains
            // the time of departure of these trains is set to the end time of the scenario
            // TODO: check scenario of two outstanding trains maybe conflict will happen
            // because of the same departure times ?
            // FIXME LP: the below code sometimes does a lookup by unit.Id, but I believe this is always (and should be) null for outstanding trains.
            if (scenario.OutStanding != null)
            {
                foreach (var departuretrain in scenario.OutStanding ?? [])
                {
                    var units = departuretrain.TrainUnits.Select(unit =>
                        unit.Id is null
                            ? new DepartureTrainUnit(traintypemap[unit.TypeDisplayName()])
                            : new DepartureTrainUnit(trainunitmap[unit.Id.Value])
                    );

                    if (infrastructuremap[departuretrain.LeaveTrackPart] is GateWay gateway)
                    {
                        var connection = gatewayconnections[gateway];
                        instance.GatewayConversion[connection.Track.ID] = connection;
                        var side = connection.Track.GetSide(
                            connection.Path[connection.Path.Length - 2]
                        );
                        var train = new DepartureTrain(
                            (int)instance.ScenarioEndTime,
                            units.ToArray(),
                            connection.Track,
                            side,
                            true
                        );
                        departures.Add(train);

                        foreach (var unit in units)
                            unit.Train = train;
                    }
                    else
                    {
                        var infra = infrastructuremap[departuretrain.LastParkingTrackPart] as Track;
                        if (debugLevel > 1)
                        {
                            Console.WriteLine(
                                $">>>>> Departure Infra Access: {infra} - {infra.Access}"
                            );
                        }

                        var train = new DepartureTrain(
                            (int)instance.ScenarioEndTime,
                            units.ToArray(),
                            infra,
                            infra.Access,
                            true
                        );
                        departures.Add(train);
                    }
                }
            }

            if (debugLevel > 1)
            {
                foreach (var departure in departures)
                    Console.WriteLine($"Departure train : {departure}");
            }

            instance.DeparturesOrdered = departures.OrderBy(departure => departure.Time).ToArray();
            instance.FillDepartures();

            foreach (
                var t in instance
                    .Tracks.Where(track =>
                        arrivals.Any(train => train.Track == track)
                        || departures.Any(train => train.Track == track)
                    )
                    .ToArray()
            )
                t.IsActive = true;

            int id = 0;
            foreach (var train in instance.DeparturesOrdered)
            foreach (var unit in train.Units)
                unit.ID = id++;

            return instance;

            /// <summary>
            /// Extracts the train unit types in the arrival train and returns the train units.
            /// </summary>
            List<TrainUnit> GetTrainUnits(NoProto.IncomingTrain arrivaltrain)
            {
                var currenttrainunits = new List<TrainUnit>();
                foreach (var unit in arrivaltrain.Members)
                {
                    TrainUnit trainunit = new(
                        unit.Id.ToString(),
                        trainunits.Count,
                        traintypemap[unit.TypeDisplayName()],
                        unit.Tasks.Where(task =>
                                taskmap[task.Type].LocationType == ServiceLocationType.Fixed
                            )
                            .Select(task => new Service(taskmap[task.Type], (int)task.Duration))
                            .ToArray(),
                        instance.ServiceTypes
                    );
                    trainunits.Add(trainunit);
                    currenttrainunits.Add(trainunit);
                    trainunitmap[unit.Id] = trainunit;
                    instance.TrainUnitConversion[trainunit] = unit;
                    freeservicelists.Add(
                        unit.Tasks.Where(task =>
                                taskmap[task.Type].LocationType == ServiceLocationType.Free
                            )
                            .Select(task => new Service(taskmap[task.Type], (int)task.Duration))
                            .ToArray()
                    );
                }
                return currenttrainunits;
            }
        }
    }
}
