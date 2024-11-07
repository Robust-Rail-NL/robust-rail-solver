using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public static ProblemInstance Parse(string locationpath, string scenariopath)
        {
            // Location
            AlgoIface.Location location;
            using (var input = File.OpenRead(locationpath))
                location = AlgoIface.Location.Parser.ParseFrom(input);

            // Scenario
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

            instance.InterfaceLocation = location;
            instance.InterfaceScenario = scenario;

            // Trackparts
            List<Track> tracks = new List<Track>();
            List<GateWay> gateways = new List<GateWay>();
            Dictionary<ulong, Infrastructure> infrastructuremap = new Dictionary<ulong, Infrastructure>();
            int index = 0;
            // Construct the parts
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
                        @switch.Connect(infrastructuremap[part.ASide.First()], new Infrastructure[2] { infrastructuremap[part.BSide.First()], infrastructuremap[part.BSide.Last()] });
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
                        gateway.Connect(infrastructuremap[part.ASide.First()]);
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
            }

            // Services
            Dictionary<AlgoIface.TaskType, ServiceType> taskmap = new Dictionary<AlgoIface.TaskType, ServiceType>();
            var tasktypes = scenario.In.Trains.Aggregate(
                new List<AlgoIface.TaskType>(),
                (list, train) => {
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
            }


            // Facilities
            // Connect the tracks to the services
            instance.FacilityConversion = new Dictionary<ServiceType, AlgoIface.Facility>();
            var servicetracks = new HashSet<Track>();
            var freetracks = new HashSet<Track>();
            var crews = new List<ServiceCrew>();
            foreach (var facility in location.Facilities)
            {
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
                    for (int i = 0; i < /*facility.SimultaneousUsageCount*/ 30; i++)
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


            /*
            // only for database/*.dat
            servicetracks.Add(instance.Tracks[10]);
            servicetracks.Add(instance.Tracks[11]);
            servicetracks.Add(instance.Tracks[12]);
            servicetracks.Add(instance.Tracks[13]);
            instance.ServiceTypes[1].Tracks.Add(instance.Tracks[12]);
            instance.ServiceTypes[2].Tracks.Add(instance.Tracks[10]);
            instance.ServiceTypes[2].Tracks.Add(instance.Tracks[11]);
            instance.ServiceTypes[0].LocationType = ServiceLocationType.Free;
            instance.ServiceTypes[3].LocationType = ServiceLocationType.Free;


            // Create the location resources
            instance.ServiceLocations = new ServiceLocation[instance.Tracks.Length];
            foreach (var track in servicetracks)
                instance.ServiceLocations[track.Index] = new ServiceLocation(track.ID.ToString(), instance.ServiceTypes.Where(type => type.Tracks.Contains(track)), track);
            // Connect the resources to the services
            foreach (var service in instance.ServiceTypes)
                foreach (var track in service.Tracks)
                    service.Resources.Add(instance.ServiceLocations[track.Index]);
            // Add tracks to free service types
            var freetracks = instance.Tracks.Where(track => track.Length > 0).Except(servicetracks);
            var freeservices = new List<ServiceType>();
            foreach (var service in instance.ServiceTypes)
            {
                if (service.Tracks.Count == 0)
                {
                    freeservices.Add(service);
                    foreach (var track in freetracks)
                        service.Tracks.Add(track);
                }
            }
            // Add crews to free service types
            // only for database/*.dat
            ServiceCrew[] crews = new ServiceCrew[30];
            for (int i = 0; i < crews.Length; i++)
                crews[i] = new ServiceCrew("crew" + i, freeservices);
            foreach (var service in freeservices)
                service.Resources.AddRange(crews);

            // only for database/*.dat
            AlgoIface.Facility fac = new AlgoIface.Facility();
            fac.Id = 0;
            fac.TaskTypes.Add(tasktypes[0]);
            fac.TaskTypes.Add(tasktypes[3]);
            fac.SimultaneousUsageCount = 30;
            fac.Type = "crew";
            foreach (var track in freetracks)
                fac.RelatedTrackParts.Add(track.ID);
            instance.FacilityConversion[instance.ServiceTypes[0]] = fac;
            instance.FacilityConversion[instance.ServiceTypes[3]] = fac;
            fac = new AlgoIface.Facility();
            fac.Id = 1;
            fac.TaskTypes.Add(tasktypes[1]);
            fac.SimultaneousUsageCount = 1;
            fac.RelatedTrackParts.Add(instance.Tracks[12].ID);
            fac.Type = instance.ServiceTypes[1].Name;
            instance.FacilityConversion[instance.ServiceTypes[1]] = fac;
            fac = new AlgoIface.Facility();
            fac.Id = 2;
            fac.TaskTypes.Add(tasktypes[2]);
            fac.SimultaneousUsageCount = 2;
            fac.RelatedTrackParts.Add(instance.Tracks[10].ID);
            fac.RelatedTrackParts.Add(instance.Tracks[11].ID);
            fac.Type = instance.ServiceTypes[2].Name;
            instance.FacilityConversion[instance.ServiceTypes[2]] = fac;

            foreach (var t in instance.Tracks.Where(track => track.Length > 0))
                t.IsActive = true;*/

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

                var gateway = (GateWay)infrastructuremap[arrivaltrain.EntryTrackPart];
                var connection = gatewayconnections[gateway];
                instance.GatewayConversion[connection.Track.ID] = connection;
                var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);

                var train = new ArrivalTrain(currenttrainunits.ToArray(), connection.Track, side, (int)arrivaltrain.Departure);
                arrivals.Add(train);
            }

            // only for harder instance
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

                var gateway = (GateWay)infrastructuremap[departuretrain.LeaveTrackPart];
                var connection = gatewayconnections[gateway];
                instance.GatewayConversion[connection.Track.ID] = connection;
                var side = connection.Track.GetSide(connection.Path[connection.Path.Length - 2]);

                var train = new DepartureTrain((int)departuretrain.Arrival, units.ToArray(), connection.Track, side);
                departures.Add(train);
                foreach (var unit in units)
                    unit.Train = train;
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

            //instance.Tracks[25].CanReverse = true;
            //instance.Tracks[25].IsActive = true;
            //instance.Tracks[25].Length = 300;

            int id = 0;
            foreach (var train in instance.DeparturesOrdered)
                foreach (var unit in train.Units)
                    unit.ID = id++;

            return instance;
        }
    }
}
