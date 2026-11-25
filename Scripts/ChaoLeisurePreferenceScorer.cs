using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

public sealed record LeisurePlaceScore(string Id,LeisureDestinationType Type,bool Available,float LearnedBias,float SpatialBias,float TotalBias,float SelectionWeight);
public sealed record LeisureSelectionSnapshot(string AreaId,string? CurrentId,string? RecentRoute,string? RecentSky,string Outcome,IReadOnlyList<LeisurePlaceScore> Destinations);

public static class ChaoLeisurePreferenceScorer
{
    public static LeisurePlaceScore Evaluate(ChaoMemory memory,string area,string id,bool available=true,bool recent=false)
    {
        var d=LeisureDestinationCatalog.Find(id) ?? throw new ArgumentException("Unknown leisure destination");
        if(area!=LeisureDestinationCatalog.Area)return new(id,d.Type,false,0,0,0,0);
        var experience=memory.Events.Where(e=>e.Kind==ChaoMemoryKind.Pleasant && e.Source==id && e.Area==area && e.Valence>0)
            .OrderByDescending(e=>e.LastAt).FirstOrDefault();
        float learned=experience is {Importance:>0} ? Math.Clamp(.05f*Math.Min(5,experience.Occurrences)*experience.Influence(memory.Seconds)/experience.Importance,0,.25f) : 0;
        float spatial=Math.Clamp(memory.Events.Where(e=>e.Area==area && !e.Source.StartsWith(LeisureDestinationCatalog.SourcePrefix,StringComparison.Ordinal) &&
            e.Place is {} p && d.Points.Any(point=>(p.X-point.X)*(p.X-point.X)+(p.Z-point.Z)*(p.Z-point.Z)<9))
            .Sum(e=>e.Valence*e.Influence(memory.Seconds)*.18f),-.15f,.15f);
        float total=Math.Clamp(learned+spatial,-.30f,.30f);
        return new(id,d.Type,available,learned,spatial,total,available ? Math.Clamp(1+total,.70f,1.30f)*(recent ? .85f : 1) : 0);
    }
    public static IReadOnlyList<LeisurePlaceScore> Candidates(ChaoMemory memory,string area,IEnumerable<string> available,string? recentRoute=null,string? recentSky=null)
    {
        var ids=available.ToHashSet(StringComparer.Ordinal);
        int routes=LeisureDestinationCatalog.All.Count(d=>d.Type==LeisureDestinationType.Route && ids.Contains(d.Id));
        int skies=LeisureDestinationCatalog.All.Count(d=>d.Type==LeisureDestinationType.Sky && ids.Contains(d.Id));
        return LeisureDestinationCatalog.All.Select(d=>Evaluate(memory,area,d.Id,ids.Contains(d.Id),
            d.Type==LeisureDestinationType.Route ? routes>1 && recentRoute==d.Id : skies>1 && recentSky==d.Id)).ToArray();
    }
    public static LeisurePlaceScore? WeightedChoice(IEnumerable<LeisurePlaceScore> candidates,Random random)
    {
        var items=candidates.Where(c=>c.Available && float.IsFinite(c.SelectionWeight) && c.SelectionWeight>0).ToArray();
        if(items.Length==0)return null;
        double pick=random.NextDouble()*items.Sum(c=>(double)c.SelectionWeight);
        foreach(var item in items){pick-=item.SelectionWeight;if(pick<0)return item;}return items[^1];
    }
    public static LeisurePlaceScore? Choose(IReadOnlyList<LeisurePlaceScore> candidates,Random random)
    {
        bool routes=candidates.Any(c=>c.Available && c.Type==LeisureDestinationType.Route),sky=candidates.Any(c=>c.Available && c.Type==LeisureDestinationType.Sky);
        if(!routes && !sky)return null;
        var type=sky && (!routes || random.NextDouble()<.32) ? LeisureDestinationType.Sky : LeisureDestinationType.Route;
        return WeightedChoice(candidates.Where(c=>c.Type==type),random);
    }
    public static bool RecordSuccess(ChaoMemory memory,string area,string id)
    {
        var d=LeisureDestinationCatalog.Find(id);
        return area==LeisureDestinationCatalog.Area && d!=null && memory.Record(ChaoMemoryKind.Pleasant,id,area,
            d.Type==LeisureDestinationType.Route ? "Enjoyed completing a garden walk." : "Enjoyed watching the sky in the garden.",.55f,.55f,place:d.End);
    }
}

public sealed partial class PetState
{
    [JsonIgnore] public LeisureSelectionSnapshot? LeisureSelection { get; internal set; }
    public object PlacePreferenceDiagnostics()=>new{area=LeisureDestinationCatalog.Area,current=LeisureSelection,
        destinations=ChaoLeisurePreferenceScorer.Candidates(History,LeisureDestinationCatalog.Area,
            LeisureSelection?.Destinations.Where(d=>d.Available).Select(d=>d.Id) ?? Array.Empty<string>(),LeisureSelection?.RecentRoute,LeisureSelection?.RecentSky)};
}
