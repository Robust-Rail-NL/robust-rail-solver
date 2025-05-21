using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

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


        public AlgoIface.Location InterfaceLocation;
        public AlgoIface.Scenario InterfaceScenario;
        public Dictionary<TrainUnit, AlgoIface.TrainUnit> TrainUnitConversion;
        public Dictionary<ServiceType, AlgoIface.Facility> FacilityConversion;
        public Dictionary<ulong, TrackSwitchContainer> GatewayConversion;

        public Service[][] FreeServices;

        public ulong ScenarioStartTime;
        public ulong ScenarioEndTime;

        public void FillTrains()
        {
            this.TrainUnitsByType = new Dictionary<TrainType, TrainUnit[]>();
            foreach (TrainType type in this.TrainTypes)
                this.TrainUnitsByType[type] = this.TrainUnits.Where(unit => unit.Type == type).ToArray();
        }

        public void FillArrivals()
        {
            this.ArrivalsByTime = new Dictionary<Time, ArrivalTrain>();
            foreach (ArrivalTrain arrival in this.ArrivalsOrdered)
                this.ArrivalsByTime[arrival.Time] = arrival;
        }

        public void FillDepartures()
        {
            this.DeparturesByTime = new Dictionary<Time, DepartureTrain>();
            this.DeparturesByType = new Dictionary<TrainType, List<DepartureTrain>>();
            foreach (DepartureTrain departure in this.DeparturesOrdered)
            {
                this.DeparturesByTime[departure.Time] = departure;
                foreach (var type in departure.Units.Select(unit => unit.IsFixed ? unit.Unit.Type : unit.Type).Distinct())
                {
                    if (!this.DeparturesByType.ContainsKey(type))
                        this.DeparturesByType[type] = new List<DepartureTrain>();
                    this.DeparturesByType[type].Add(departure);
                }
            }
        }

        public static string ParseJsonToString(string path)
        {
            string jsonContent;
            using (var input = File.OpenRead(path))

            using (StreamReader reader = new StreamReader(input))
            {
                jsonContent = reader.ReadToEnd();
            }
            return jsonContent;

        }


        public static ProblemInstance ParseJson(string locationpath, string scenariopath)
        {
            AlgoIface.Location location;
            using (var input = File.OpenRead(locationpath))
            using (StreamReader reader = new StreamReader(input))
            {
                string jsonContent = reader.ReadToEnd();
                location = AlgoIface.Location.Parser.ParseJson(jsonContent);

            }

            AlgoIface.Scenario scenario;
            using (var input = File.OpenRead(scenariopath))
            using (StreamReader reader = new StreamReader(input))
            {
                string jsonContent = reader.ReadToEnd();
                scenario = AlgoIface.Scenario.Parser.ParseJson(jsonContent);

            }

            return Parse(location, scenario);
        }

        public static ProblemInstance Parse(string locationpath, string scenariopath)
        {
            AlgoIface.Location location;
            using (var input = File.OpenRead(locationpath))
                location = AlgoIface.Location.Parser.ParseFrom(input);

            AlgoIface.Scenario scenario;
            using (var input = File.OpenRead(scenariopath))
                scenario = AlgoIface.Scenario.Parser.ParseFrom(input);

            return Parse(location, scenario);
        }

        public static ProblemInstance Parse(AlgoIface.Location location, AlgoIface.Scenario scenario)
        {
            ProblemInstance instance = new ProblemInstance();

            // only for database/*.dat
            bool include94139414 = false;
            bool include24082409 = false;
            bool include2610 = false;
            bool include2611 = false;
            bool noservices = false;

            // Get start and end time of the scenario
            // this is needed sice the start time will be associated with the arrival time
            // of the instanding trains (which were already on the shunting yard "before"
            // the scenarion), the end time is associated with the departure time of the
            // outstanding trains which "exits" the scenario but in real they stays on the yard 
            instance.ScenarioStartTime = scenario.StartTime;
            instance.ScenarioEndTime = scenario.EndTime;

            instance.InterfaceLocation = location;
            instance.InterfaceScenario = scenario;

            List<Track> tracks = new List<Track>();
            List<GateWay> gateways = new List<GateWay>();
            Dictionary<ulong, Infrastructure> infrastructuremap = new Dictionary<ulong, Infrastructure>();
            int index = 0;

            // Construct the track parts
            foreach (var part in location.TrackParts)
            {
                switch (part.Type)
                {
                    case AlgoIface.TrackPartType.RailRoad:
                        Track track = new Track(part.Id, part.Name, ServiceType.None, (int)part.Length, Side.None, part.ParkingAllowed, part.SawMovementAllowed);
                        track.Index = index++;
                        tracks.Add(track);
                        infrastructuremap[part.Id] = track;
                        break;
                    case AlgoIface.TrackPartType.Switch:
                        infrastructuremap[part.Id] = new Switch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.EnglishSwitch:
                        infrastructuremap[part.Id] = new EnglishSwitch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.HalfEnglishSwitch:
                        infrastructuremap[part.Id] = new HalfEnglishSwitch(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.Intersection:
                        infrastructuremap[part.Id] = new Intersection(part.Id, part.Name);
                        break;
                    case AlgoIface.TrackPartType.Bumper:
                        var gateway = new GateWay(part.Id, part.Name);
                        infrastructuremap[part.Id] = gateway;
                        gateways.Add(gateway);
                        break;
                    default:
                        break;
                }
            }
            instance.Tracks = tracks.ToArray();
            // Connect the parts
            foreach (var part in location.TrackParts)
            {
                Console.WriteLine($"Track Parts : {part}");
                switch (part.Type)
                {
                    case AlgoIface.TrackPartType.RailRoad:
                        Track track = infrastructuremap[part.Id] as Track;
                        Infrastructure A = null, B = null;
                        if (part.ASide.Count > 0)
                            infrastructuremap.TryGetValue(part.ASide.First(), out A);
                        if (part.BSide.Count > 0)
                            infrastructuremap.TryGetValue(part.BSide.First(), out B);
                        track.Connect(A, B);

                        break;
                    case AlgoIface.TrackPartType.Switch:
                        Switch @switch = infrastructuremap[part.Id] as Switch;
                        if (part.ASide.Count() == 1)
                        {   // A side is connected to two B side infrastructure
                            @switch.Connect(infrastructuremap[part.ASide.First()], new Infrastructure[2] { infrastructuremap[part.BSide.First()], infrastructuremap[part.BSide.Last()] });
                        }
                        else
                        {   // B side is connected to two A side infrastructure
                            @switch.Connect(infrastructuremap[part.BSide.First()], new Infrastructure[2] { infrastructuremap[part.ASide.First()], infrastructuremap[part.ASide.Last()] });

                        }

                        break;
                    case AlgoIface.TrackPartType.EnglishSwitch:
                        EnglishSwitch englishswitch = infrastructuremap[part.Id] as EnglishSwitch;
                        englishswitch.Connect(part.ASide.Select(neighbor => infrastructuremap[neighbor]).ToList(), part.BSide.Select(neighbor => infrastructuremap[neighbor]).ToList());
                        break;
                    case AlgoIface.TrackPartType.HalfEnglishSwitch:
                        HalfEnglishSwitch halfenglishswitch = infrastructuremap[part.Id] as HalfEnglishSwitch;
                        halfenglishswitch.Connect(
                            infrastructuremap[part.ASide.First()],
                            infrastructuremap[part.ASide.Last()],
                            infrastructuremap[part.BSide.First()],
                            infrastructuremap[part.BSide.Last()]);
                        break;
                    case AlgoIface.TrackPartType.Intersection:
                        Intersection intersection = infrastructuremap[part.Id] as Intersection;
                        intersection.Connect(
                            infrastructuremap[part.ASide.First()],
                            infrastructuremap[part.BSide.Last()],
                            infrastructuremap[part.ASide.Last()],
                            infrastructuremap[part.BSide.First()]);
                        break;
                    case AlgoIface.TrackPartType.Bumper:
                        GateWay gateway = infrastructuremap[part.Id] as GateWay;

                        Console.WriteLine($"gateway : {gateway}");
                        if (part.ASide.Count() != 0)
                        {
                            Console.WriteLine($"Track part A: {part.Id}");
                            Console.WriteLine($"Infra: {infrastructuremap[part.Id]}");

                            gateway.Connect(infrastructuremap[part.ASide.First()]);

                        }
                        else if (part.BSide.Count() != 0)
                        {
                            Console.WriteLine($"Track part B: {part.Id}");
                            Console.WriteLine($"Infra: {infrastructuremap[part.Id]}");
                            gateway.Connect(infrastructuremap[part.BSide.First()]);
                        }
                        else
                        { // Remove gateway from gateways dict
                            gateways.Remove(gateway);
                        }
                        break;
                    default:
                        break;
                }
            }
            Dictionary<GateWay, TrackSwitchContainer> gatewayconnections = new Dictionary<GateWay, TrackSwitchContainer>();
            foreach (var gateway in gateways)
            {
                List<Infrastructure> path = new List<Infrastructure>();
                path.Add(gateway);

                gatewayconnections[gateway] = gateway.EndPoint.GetTracksConnectedTo(gateway, 0, path, false).First();
                Console.WriteLine($"gatewayconnections[gateway]: {gateway}:{gatewayconnections[gateway]}");

            }




            Dictionary<AlgoIface.TaskType, ServiceType> taskmap = new Dictionary<AlgoIface.TaskType, ServiceType>();
            var tasktypes = scenario.In.Trains.Aggregate(
                new List<AlgoIface.TaskType>(),
                (list, train) =>
                {
                    list.AddRange(train.Members.Aggregate(
                        new List<AlgoIface.TaskType>(),
                        (l, unit) => { l.AddRange(unit.Tasks.Select(task => task.Type)); return l; }));
                    return list;
                }).Distinct().ToList();

            instance.ServiceTypes = new ServiceType[tasktypes.Count];
            for (int i = 0; i < tasktypes.Count; i++)
            {
                var type = tasktypes[i];
                ServiceType service = new ServiceType(i, type.Other, ServiceLocationType.Fixed);
                instance.ServiceTypes[i] = service;
                taskmap[type] = service;
                Console.WriteLine($">>>>Service : {service}");
            }


            // Facilities
            // Connect the tracks to the services
            instance.FacilityConversion = new Dictionary<ServiceType, AlgoIface.Facility>();
            var servicetracks = new HashSet<Track>();
            var freetracks = new HashSet<Track>();
            var crews = new List<ServiceCrew>();
            foreach (var facility in location.Facilities)
            {
                Console.WriteLine($">>>>Facility : {facility}");

                var facilitytracks = new List<Track>();
                foreach (var part in facility.RelatedTrackParts)
                {
                    if (infrastructuremap.ContainsKey(part))
                    {
                        var track = infrastructuremap[part] as Track;
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
                        var crew = new ServiceCrew("crew " + i, facility.TaskTypes.Select(type => taskmap[type]));
                        crews.Add(crew);
                    }
                }

                foreach (var type in facility.TaskTypes)
                {
                    if (taskmap.ContainsKey(type))
                    {
                        var service = taskmap[type];
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
                instance.ServiceLocations[track.Index] = new ServiceLocation(track.ID.ToString(), instance.ServiceTypes.Where(type => type.Tracks.Contains(track)), track);
            foreach (var service in instance.ServiceTypes.Where(s => s.LocationType == ServiceLocationType.Fixed))
                foreach (var track in service.Tracks)
                    service.Resources.Add(instance.ServiceLocations[track.Index]);


            foreach (var track in servicetracks)
                Console.WriteLine($"Service location: {instance.ServiceLocations[track.Index]}");

            // Determine train types
            List<TrainType> traintypes = new List<TrainType>();
            List<TrainUnit> trainunits = new List<TrainUnit>();
            List<ArrivalTrain> arrivals = new List<ArrivalTrain>();
            Dictionary<AlgoIface.TrainUnitType, TrainType> traintypemap = new Dictionary<AlgoIface.TrainUnitType, TrainType>();
            Dictionary<string, TrainUnit> trainunitmap = new Dictionary<string, TrainUnit>();
            instance.TrainUnitConversion = new Dictionary<TrainUnit, AlgoIface.TrainUnit>();
            instance.GatewayConversion = new Dictionary<ulong, TrackSwitchContainer>();
            var freeservicelists = new List<Service[]>();
            foreach (var arrivaltrain in scenario.In.Trains)
            {
                var currenttrainunits = new List<TrainUnit>();
                foreach (var unit in arrivaltrain.Members)
                {
                    if (!traintypemap.ContainsKey(unit.TrainUnit.Type))
                    {
                        var name = $"{unit.TrainUnit.Type.DisplayName}-{unit.TrainUnit.Type.Carriages}";
                        TrainType type = new TrainType(
                            traintypes.Count,
                            name,
                            (int)unit.TrainUnit.Type.Length,
                            instance.Tracks.Where(t => t.CanPark).ToArray(),
                            (int)unit.TrainUnit.Type.BackNormTime,
                            (int)unit.TrainUnit.Type.BackAdditionTime * (int)unit.TrainUnit.Type.Carriages,
                            (int)unit.TrainUnit.Type.CombineDuration,
                            (int)unit.TrainUnit.Type.SplitDuration);
                        traintypes.Add(type);
                        traintypemap[unit.TrainUnit.Type] = type;
                    }
                    TrainUnit trainunit = new TrainUnit(
                        unit.TrainUnit.Id,
                        trainunits.Count,
                        traintypemap[unit.TrainUnit.Type],
                        unit.Tasks.Where(task => taskmap[task.Type].LocationType == ServiceLocationType.Fixed).Select(task => new Service(taskmap[task.Type], (int)task.Duration)).ToArray(),
                        instance.ServiceTypes);
                    trainunits.Add(trainunit);
                    currenttrainunits.Add(trainunit);
                    trainunitmap[unit.TrainUnit.Id] = trainunit;
                    instance.TrainUnitConversion[trainunit] = unit.TrainUnit;
                    freeservicelists.Add(unit.Tasks.Where(task => taskmap[task.Type].LocationType == ServiceLocationType.Free).Select(task => new Service(taskmap[task.Type], (int)task.Duration)).ToArray());
                }

                Console.WriteLine($"Track part : {infrastructuremap[arrivaltrain.EntryTrackPart]}");

                GateWay gateway = infrastructuremap[arrivaltrain.EntryTrackPart] as GateWay;

                if (gateway != null)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);

                    Console.WriteLine($"connection :{connection}");

                    Console.WriteLine($"gateway :{gateway}");
                    Console.WriteLine($"side :{side}");



                    var train = new ArrivalTrain(currenttrainunits.ToArray(), connection.Track, side, (int)arrivaltrain.Departure);
                    Console.WriteLine($"connection.Track :{connection.Track}");

                    arrivals.Add(train);
                }
                // foreach (var arrival in arrivals)
                //     Console.WriteLine($"Arrival train : {arrival}");


            }

            // Consider the instanding trains as arrival (incoming) trains
            // the time of arrival of these trains is set to the start time of the scenario
            // TODO: check scenario of two instanding trains maybe conflict will happen 
            // because of the same arrival times ?
            foreach (var arrivaltrain in scenario.InStanding.Trains)
            {
                var currenttrainunits = new List<TrainUnit>();
                foreach (var unit in arrivaltrain.Members)
                {
                    if (!traintypemap.ContainsKey(unit.TrainUnit.Type))
                    {
                        var name = $"{unit.TrainUnit.Type.DisplayName}-{unit.TrainUnit.Type.Carriages}";
                        TrainType type = new TrainType(
                            traintypes.Count,
                            name,
                            (int)unit.TrainUnit.Type.Length,
                            instance.Tracks.Where(t => t.CanPark).ToArray(),
                            (int)unit.TrainUnit.Type.BackNormTime,
                            (int)unit.TrainUnit.Type.BackAdditionTime * (int)unit.TrainUnit.Type.Carriages,
                            (int)unit.TrainUnit.Type.CombineDuration,
                            (int)unit.TrainUnit.Type.SplitDuration);
                        traintypes.Add(type);
                        traintypemap[unit.TrainUnit.Type] = type;
                    }
                    TrainUnit trainunit = new TrainUnit(
                        unit.TrainUnit.Id,
                        trainunits.Count,
                        traintypemap[unit.TrainUnit.Type],
                        unit.Tasks.Where(task => taskmap[task.Type].LocationType == ServiceLocationType.Fixed).Select(task => new Service(taskmap[task.Type], (int)task.Duration)).ToArray(),
                        instance.ServiceTypes);
                    trainunits.Add(trainunit);
                    currenttrainunits.Add(trainunit);
                    trainunitmap[unit.TrainUnit.Id] = trainunit;
                    instance.TrainUnitConversion[trainunit] = unit.TrainUnit;
                    freeservicelists.Add(unit.Tasks.Where(task => taskmap[task.Type].LocationType == ServiceLocationType.Free).Select(task => new Service(taskmap[task.Type], (int)task.Duration)).ToArray());
                }

                Console.WriteLine($"Track part : {infrastructuremap[arrivaltrain.EntryTrackPart]}");

                GateWay gateway = infrastructuremap[arrivaltrain.EntryTrackPart] as GateWay;

                if (gateway != null)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);

                    Console.WriteLine($"connection :{connection}");

                    Console.WriteLine($"gateway :{gateway}");
                    Console.WriteLine($"side :{side}");


                    var train = new ArrivalTrain(currenttrainunits.ToArray(), connection.Track, side, (int)scenario.StartTime, true);
                    Console.WriteLine($"connection.Track :{connection.Track}");

                    arrivals.Add(train);
                }
                foreach (var arrival in arrivals)
                    Console.WriteLine($"Arrival train : {arrival}");


            }
            // only for harder instances
            TrainUnit tu9413 = null, tu9414 = null;
            if (include94139414)
            {
                tu9413 = new TrainUnit("9413", trainunits.Count, traintypes[2], new Service[1] { new Service(instance.ServiceTypes[2], 37 * Time.Minute) }, instance.ServiceTypes);
                tu9414 = new TrainUnit("9414", trainunits.Count + 1, traintypes[2], new Service[1] { new Service(instance.ServiceTypes[2], 37 * Time.Minute) }, instance.ServiceTypes);
                var at94139414 = new ArrivalTrain(new TrainUnit[2] { tu9413, tu9414 }, instance.Tracks[15], Side.A, 26 * Time.Hour + 17 * Time.Minute);
                trainunits.Add(tu9413);
                trainunits.Add(tu9414);

                arrivals.Add(at94139414);

                freeservicelists.Add(new Service[0]);
                freeservicelists.Add(new Service[0]);
            }
            TrainUnit tu2408 = null, tu2409 = null;
            if (include24082409)
            {
                tu2408 = new TrainUnit("2408", trainunits.Count, traintypes[0], new Service[1] { new Service(instance.ServiceTypes[2], 15 * Time.Minute) }, instance.ServiceTypes);
                tu2409 = new TrainUnit("2409", trainunits.Count + 1, traintypes[0], new Service[1] { new Service(instance.ServiceTypes[2], 15 * Time.Minute) }, instance.ServiceTypes);
                var at24082409 = new ArrivalTrain(new TrainUnit[2] { tu2408, tu2409 }, instance.Tracks[15], Side.A, 24 * Time.Hour);
                trainunits.Add(tu2408);
                trainunits.Add(tu2409);

                arrivals.Add(at24082409);

                freeservicelists.Add(new Service[0]);
                freeservicelists.Add(new Service[0]);
            }
            TrainUnit tu2610 = null;
            if (include2610)
            {
                tu2610 = new TrainUnit("2610", trainunits.Count, traintypes[0], new Service[1] { new Service(instance.ServiceTypes[2], 20 * Time.Minute) }, instance.ServiceTypes);
                var at2610 = new ArrivalTrain(new TrainUnit[1] { tu2610 }, instance.Tracks[15], Side.A, 24 * Time.Hour + 30 * Time.Minute);
                trainunits.Add(tu2610);

                arrivals.Add(at2610);

                freeservicelists.Add(new Service[0]);
            }
            TrainUnit tu2611 = null;
            if (include2611)
            {
                tu2611 = new TrainUnit("2611", trainunits.Count, traintypes[0], new Service[1] { new Service(instance.ServiceTypes[2], 20 * Time.Minute) }, instance.ServiceTypes);
                var at2611 = new ArrivalTrain(new TrainUnit[1] { tu2611 }, instance.Tracks[15], Side.A, 24 * Time.Hour + 45 * Time.Minute);
                trainunits.Add(tu2611);

                arrivals.Add(at2611);

                freeservicelists.Add(new Service[0]);
            }

            // No services
            if (noservices)
            {
                foreach (var unit in trainunits)
                {
                    unit.RequiredServices = new Service[0];
                    for (int i = 0; i < unit.ServiceDurations.Length; i++)
                        unit.ServiceDurations[i] = 0;
                }

                for (int i = 0; i < freeservicelists.Count; i++)
                    freeservicelists[i] = new Service[0];
            }

            instance.TrainTypes = traintypes.ToArray();
            instance.TrainUnits = trainunits.ToArray();
            instance.FillTrains();

            instance.ArrivalsOrdered = arrivals.OrderBy(arrival => arrival.Time).ToArray();

            instance.FillArrivals();

            instance.FreeServices = freeservicelists.ToArray();

            var departures = new List<DepartureTrain>();
            foreach (var departuretrain in scenario.Out.TrainRequests)
            {
                var units = departuretrain.TrainUnits.Select(
                    unit => unit.Id == string.Empty ? new DepartureTrainUnit(traintypemap[unit.Type]) : new DepartureTrainUnit(trainunitmap[unit.Id]));

                GateWay gateway = infrastructuremap[departuretrain.LeaveTrackPart] as GateWay;

                if (gateway != null)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);
                    var train = new DepartureTrain((int)departuretrain.Arrival, units.ToArray(), connection.Track, side);
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
            foreach (var departuretrain in scenario.OutStanding.TrainRequests)
            {
                var units = departuretrain.TrainUnits.Select(
                    unit => unit.Id == string.Empty ? new DepartureTrainUnit(traintypemap[unit.Type]) : new DepartureTrainUnit(trainunitmap[unit.Id]));

                GateWay gateway = infrastructuremap[departuretrain.LeaveTrackPart] as GateWay;

                if (gateway != null)
                {
                    var connection = gatewayconnections[gateway];
                    instance.GatewayConversion[connection.Track.ID] = connection;
                    var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);
                    var train = new DepartureTrain((int)scenario.EndTime, units.ToArray(), connection.Track, side, true);
                    departures.Add(train);

                    foreach (var unit in units)
                        unit.Train = train;

                }

                foreach (var departure in departures)
                    Console.WriteLine($"Departure train : {departure}");

            }

            // only for harder instance
            if (include94139414)
            {
                var dt94139414 = new DepartureTrain(31 * Time.Hour + 17 * Time.Minute, new DepartureTrainUnit[2] { new DepartureTrainUnit(tu9413), new DepartureTrainUnit(tu9414) }, instance.Tracks[15], Side.A);
                departures.Add(dt94139414);
            }
            if (include24082409)
            {
                var dt24082409 = new DepartureTrain(30 * Time.Hour + 30 * Time.Minute, new DepartureTrainUnit[2] { new DepartureTrainUnit(tu2408), new DepartureTrainUnit(tu2409) }, instance.Tracks[15], Side.A);
                departures.Add(dt24082409);
            }
            if (include2610)
            {
                var dt2610 = new DepartureTrain(32 * Time.Hour + 10 * Time.Minute, new DepartureTrainUnit[1] { new DepartureTrainUnit(tu2610) }, instance.Tracks[15], Side.A);
                departures.Add(dt2610);
            }
            if (include2611)
            {
                var dt2611 = new DepartureTrain(32 * Time.Hour + 30 * Time.Minute, new DepartureTrainUnit[1] { new DepartureTrainUnit(tu2611) }, instance.Tracks[15], Side.A);
                departures.Add(dt2611);
            }

            instance.DeparturesOrdered = departures.OrderBy(departure => departure.Time).ToArray();
            instance.FillDepartures();

            foreach (var t in instance.Tracks.Where(track => arrivals.Any(train => train.Track == track) || departures.Any(train => train.Track == track)).ToArray())
                t.IsActive = true;

            int id = 0;
            foreach (var train in instance.DeparturesOrdered)
                foreach (var unit in train.Units)
                    unit.ID = id++;


            return instance;
        }
    }
}
