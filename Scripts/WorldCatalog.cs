using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum WorldAreaKind { Garden, Plaza, Stadium, ExpeditionRegion, ExpeditionSite }
public readonly record struct WorldPoint(float X,float Y,float Z);
public sealed record WorldArea(string Id,string Title,WorldAreaKind Kind,string? Region=null,bool InitiallyOpen=false);
public sealed record WorldRoute(string Id,string From,string To,WorldPoint Entrance,WorldPoint Arrival,float ArrivalYaw=0,float Radius=1.3f);

// Stable IDs are save keys. Regions and sites share a travel graph, never a garden population.
public sealed class WorldCatalog
{
    public const string Meadow="windfall-meadow";
    public IReadOnlyDictionary<string,WorldArea> Areas { get; }
    public IReadOnlyList<WorldRoute> Routes { get; }
    public WorldCatalog(IEnumerable<WorldArea> areas,IEnumerable<WorldRoute> routes)
    {
        Areas=areas.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        Routes=routes.ToArray();
        if (Routes.Select(r=>r.Id).Distinct().Count()!=Routes.Count || Routes.Any(r=>!Areas.ContainsKey(r.From) || !Areas.ContainsKey(r.To)))
            throw new ArgumentException("Travel routes require unique IDs and existing endpoints.");
        if (Areas.Values.Any(a=>a.Kind==WorldAreaKind.ExpeditionSite && (a.Region==null || !Areas.TryGetValue(a.Region,out var region) || region.Kind!=WorldAreaKind.ExpeditionRegion)))
            throw new ArgumentException("An expedition site must belong to an expedition region.");
    }
    public bool IsExpedition(string id) => Areas.TryGetValue(id,out var area) && area.Kind is WorldAreaKind.ExpeditionRegion or WorldAreaKind.ExpeditionSite;
    public bool IsOpen(string id,ExpeditionProgress progress) => Areas.TryGetValue(id,out var area) && (area.InitiallyOpen || progress.UnlockedAreas.Contains(id));
    public IEnumerable<WorldRoute> Exits(string id,ExpeditionProgress progress) => Routes.Where(r=>r.From==id && IsOpen(r.To,progress));
    public static WorldCatalog Default { get; }=new(new[]{
        new WorldArea("garden","MORI GARDEN",WorldAreaKind.Garden,InitiallyOpen:true),
        new WorldArea("plaza","GARDEN PLAZA",WorldAreaKind.Plaza,InitiallyOpen:true),
        new WorldArea("stadium","STAR STADIUM",WorldAreaKind.Stadium,InitiallyOpen:true),
        new WorldArea("coastal-cove","COASTAL COVE",WorldAreaKind.Garden,InitiallyOpen:true),
        new WorldArea(Meadow,"WINDFALL MEADOW",WorldAreaKind.ExpeditionRegion,InitiallyOpen:true)
    },new[]{
        new WorldRoute("garden-plaza","garden","plaza",new(-18.45f,.43f,-16.85f),new(0,0,-7)),
        new WorldRoute("plaza-garden","plaza","garden",new(0,0,-10),new(-18.45f,.43f,-13.85f)),
        new WorldRoute("plaza-coast","plaza","coastal-cove",new(8,0,6),new(0,0,-14)),
        new WorldRoute("coast-plaza","coastal-cove","plaza",new(0,0,-18),new(8,0,9)),
        new WorldRoute("plaza-stadium","plaza","stadium",new(6,0,-4),new(0,0,-7),Radius:1.35f),
        new WorldRoute("stadium-plaza","stadium","plaza",new(0,0,-10),new(6,0,-1)),
        new WorldRoute("plaza-meadow","plaza",Meadow,new(-8,0,6),new(0,0,23),MathF.PI),
        new WorldRoute("meadow-plaza",Meadow,"plaza",new(0,0,27),new(-8,0,9))
    });
}

public sealed class ExpeditionProgress
{
    public HashSet<string> UnlockedAreas { get; set; }=new(StringComparer.Ordinal);
    public HashSet<string> VisitedAreas { get; set; }=new(StringComparer.Ordinal);
    // Future missions can record unlock facts here without introducing a mission runner now.
    public Dictionary<string,HashSet<string>> AreaFlags { get; set; }=new(StringComparer.Ordinal);
    public void Validate()
    {
        UnlockedAreas??=new(); VisitedAreas??=new(); AreaFlags??=new();
        UnlockedAreas.RemoveWhere(string.IsNullOrWhiteSpace); VisitedAreas.RemoveWhere(string.IsNullOrWhiteSpace);
        foreach(var id in AreaFlags.Keys.ToArray())
            if (string.IsNullOrWhiteSpace(id)) AreaFlags.Remove(id);
            else { AreaFlags[id]??=new(); AreaFlags[id].RemoveWhere(string.IsNullOrWhiteSpace); }
    }
}
