using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public sealed class AnimalDiscovery
{
    public bool Met { get; set; }
    public int Obtained { get; set; }
}
public sealed class AnimalHabitatState
{
    public int Generation { get; set; }
    public bool Collected { get; set; }
    public float Cooldown { get; set; }
}
// Inventory and spawn ledger live in the same save transaction; a captured actor cannot be counted twice.
public sealed class WildlifeProgress
{
    public const int Capacity=12;
    public Dictionary<string,int> Bag { get; set; }=new();
    public Dictionary<string,AnimalDiscovery> Discoveries { get; set; }=new();
    public Dictionary<string,AnimalHabitatState> Habitats { get; set; }=new();
    public int Total=>Bag.Values.Sum();
    public int Count(string id)=>Bag.GetValueOrDefault(id);
    public AnimalHabitatState Slot(string id)
    { if(!Habitats.TryGetValue(id,out var state))Habitats[id]=state=new();return state; }
    public void Meet(string species)
    {
        if(AnimalCatalog.Find(species)==null)return;
        if(!Discoveries.TryGetValue(species,out var record))Discoveries[species]=record=new();record.Met=true;
    }
    public bool Obtain(string habitat,string species,int generation)
    {
        var kind=AnimalCatalog.Find(species);var slot=Slot(habitat);
        if(kind?.Obtainable!=true || slot.Collected || slot.Generation!=generation || Total>=Capacity)return false;
        Bag[species]=Count(species)+1;Meet(species);Discoveries[species].Obtained=Math.Min(1000000,Discoveries[species].Obtained+1);
        slot.Collected=true;slot.Cooldown=kind.RespawnSeconds;return true;
    }
    public bool Take(string id)
    { if(AnimalCatalog.Find(id)==null || Count(id)<=0)return false;Bag[id]--;return true; }
    public bool Store(string id)
    { if(AnimalCatalog.Find(id)==null || Total>=Capacity)return false;Bag[id]=Count(id)+1;return true; }
    public void Advance(float dt)
    { if(!float.IsFinite(dt) || dt<=0)return;foreach(var slot in Habitats.Values)slot.Cooldown=Math.Max(0,slot.Cooldown-dt); }
    public bool Respawn(string habitat,bool hidden,bool safe)
    {
        var slot=Slot(habitat);if(!slot.Collected || slot.Cooldown>0 || !hidden || !safe)return false;
        slot.Collected=false;slot.Generation=Math.Min(1000000,slot.Generation+1);return true;
    }
    public void Validate()
    {
        Bag ??=new();Discoveries ??=new();Habitats ??=new();int left=Capacity;
        Bag=AnimalCatalog.All.ToDictionary(a=>a.Id,a=>{int n=Math.Clamp(Count(a.Id),0,left);left-=n;return n;});
        Discoveries=Discoveries.Where(p=>AnimalCatalog.Find(p.Key)!=null && p.Value!=null).ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var record in Discoveries.Values){record.Obtained=Math.Clamp(record.Obtained,0,1000000);record.Met|=record.Obtained>0;}
        Habitats=Habitats.Where(p=>DiscoveryMemory.ValidId(p.Key) && p.Value!=null).Take(64).ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var slot in Habitats.Values){slot.Generation=Math.Clamp(slot.Generation,0,1000000);slot.Cooldown=float.IsFinite(slot.Cooldown) ? Math.Clamp(slot.Cooldown,0,600) : 180;}
    }
}
