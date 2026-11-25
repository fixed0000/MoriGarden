using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum LeisureDestinationType { Route, Sky }
public sealed record LeisureDestination(string Id,LeisureDestinationType Type,int Index,IReadOnlyList<WorldPoint> Points)
{public WorldPoint End=>Points[^1];}

// Saved identity and geometry share one explicit mapping. Changing this order
// or any anchor requires a deliberate compatibility decision, not a new index.
public static class LeisureDestinationCatalog
{
    public const string Area="garden", SourcePrefix="garden/leisure/";
    private static LeisureDestination D(string id,LeisureDestinationType type,int index,params WorldPoint[] points)=>new(id,type,index,Array.AsReadOnly(points));
    public static IReadOnlyList<LeisureDestination> All { get; }=Array.AsReadOnly(new[]{
        D("garden/leisure/route-0",LeisureDestinationType.Route,0,new WorldPoint(12,0,12),new WorldPoint(29,0,12),new WorldPoint(46,0,12),new WorldPoint(64,0,12)),
        D("garden/leisure/route-1",LeisureDestinationType.Route,1,new WorldPoint(-15,0,14),new WorldPoint(-25,0,14),new WorldPoint(-35,0,14),new WorldPoint(-45,0,14)),
        D("garden/leisure/route-2",LeisureDestinationType.Route,2,new WorldPoint(36,0,5),new WorldPoint(36,0,17),new WorldPoint(36,0,29),new WorldPoint(36,0,41)),
        D("garden/leisure/route-3",LeisureDestinationType.Route,3,new WorldPoint(-10,0,25),new WorldPoint(-22,0,30),new WorldPoint(-34,0,35),new WorldPoint(-46,0,40)),
        D("garden/leisure/sky-0",LeisureDestinationType.Sky,0,new WorldPoint(0,0,12)),
        D("garden/leisure/sky-1",LeisureDestinationType.Sky,1,new WorldPoint(11,0,10)),
        D("garden/leisure/sky-2",LeisureDestinationType.Sky,2,new WorldPoint(-2,0,4)),
        D("garden/leisure/sky-3",LeisureDestinationType.Sky,3,new WorldPoint(14,0,-1))});
    static LeisureDestinationCatalog()
    {
        if(All.Count!=8 || All.Select(d=>d.Id).Distinct().Count()!=8 || Enum.GetValues<LeisureDestinationType>().Any(t=>
            !All.Where(d=>d.Type==t).Select(d=>d.Index).SequenceEqual(new[]{0,1,2,3}) || All.Where(d=>d.Type==t).Any(d=>d.Points.Count!=(t==LeisureDestinationType.Route ? 4 : 1))))
            throw new InvalidOperationException("Incomplete leisure destination registry");
    }
    public static LeisureDestination? Find(string? id)=>All.FirstOrDefault(d=>d.Id==id);
}
