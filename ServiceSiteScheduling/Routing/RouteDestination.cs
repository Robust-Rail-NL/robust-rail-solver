namespace ServiceSiteScheduling.Routing
{
    // What a ComputeRoute destination side means for the search's target
    // vertex (RoutingGraph.ResolveEndpoints): Rest targets the train's
    // resting vertex (AA/BB, ArrivalSide==TrackSide) -- the only mode used
    // before #51's fix, and still the only mode an ordinary RoutingTask
    // needs, since any orientation it ends up in gets corrected by the next
    // leg's own route. ReadyToDepart targets the departure-ready vertex
    // instead (AB/BA) -- the side actually reachable without a further
    // reversal -- for the one leg that has no next leg to hand a correction
    // to: a DepartureRoutingTask's final hop onto a scenario-fixed
    // DepartureTask.
    enum RouteDestination
    {
        Rest,
        ReadyToDepart,
    }
}
