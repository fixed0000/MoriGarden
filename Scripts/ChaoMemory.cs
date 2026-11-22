using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum ChaoMemoryKind { FirstVisit,ReturnVisit,AnimalSeen,IndependentStudy,PlayerDiscovery,SharedStudy,HelpReceived,HelpGiven,FailedAttempt,Pleasant,Unpleasant,SharedRest,Introduction }
public sealed class ChaoMemoryEvent
{
    public ChaoMemoryKind Kind { get; set; }
    public string Source { get; set; }="";
    public string Area { get; set; }="";
    public string[] Participants { get; set; }=Array.Empty<string>();
    public string Description { get; set; }="";
    public double FirstAt { get; set; }
    public double LastAt { get; set; }
    public float Valence { get; set; }
    public float Importance { get; set; }
    public int Occurrences { get; set; }=1;
    public WorldPoint? Place { get; set; }
    public float Influence(double now)=>(float)(Importance*Math.Pow(.5,Math.Max(0,now-LastAt)/(1800+Importance*18000)));
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key=>$"{Kind}|{Source}|{Area}|{string.Join(',',Participants)}";
}
// Subjective active-play time, bounded salient events and a separate bounded anti-farming ledger.
// This stores experience, not a second personality or friendship model.
public sealed class ChaoMemory
{
    public const int Capacity=64;
    public double Seconds { get; set; }
    public bool PastUnknown { get; set; }
    public string LastArea { get; set; }="";
    public HashSet<string> Visited { get; set; }=new();
    public List<ChaoMemoryEvent> Events { get; set; }=new();
    public HashSet<string> GrowthSources { get; set; }=new();
    public Dictionary<string,float> Credits { get; set; }=new();
    public Dictionary<string,int> Gained { get; set; }=new();
    public Dictionary<string,int> Baseline { get; set; }=new();
    public void Advance(float dt){if(float.IsFinite(dt) && dt>0)Seconds=Math.Min(1e12,Seconds+dt);}
    public bool Record(ChaoMemoryKind kind,string source,string area,string description,float valence,float importance,
        string[]? participants=null,WorldPoint? place=null)
    {
        if(!DiscoveryMemory.ValidId(source) || !DiscoveryMemory.ValidId(area) || string.IsNullOrWhiteSpace(description) || !float.IsFinite(valence) || !float.IsFinite(importance))return false;
        var item=new ChaoMemoryEvent { Kind=kind,Source=source,Area=area,Description=description[..Math.Min(180,description.Length)],
            Valence=Math.Clamp(valence,-1,1),Importance=Math.Clamp(importance,.05f,1),Participants=(participants ?? Array.Empty<string>()).Where(DiscoveryMemory.ValidId).Distinct().OrderBy(s=>s,StringComparer.Ordinal).Take(3).ToArray(),
            FirstAt=Seconds,LastAt=Seconds,Place=place };
        var previous=Events.FirstOrDefault(e=>e.Key==item.Key);
        if(previous!=null)
        {
            if(Seconds-previous.LastAt<120)return false;
            previous.LastAt=Seconds;previous.Occurrences=Math.Min(20,previous.Occurrences+1);
            previous.Valence=previous.Valence*.7f+item.Valence*.3f;previous.Importance=Math.Max(previous.Importance,item.Importance);
            previous.Description=item.Description;previous.Place=place ?? previous.Place;return true;
        }
        if(Events.Count>=Capacity)
        {
            var weakest=Events.OrderBy(e=>e.Influence(Seconds)).ThenBy(e=>e.LastAt).First();
            if(weakest.Influence(Seconds)>importance)return false;Events.Remove(weakest);
        }
        Events.Add(item);return true;
    }
    public void Visit(string area,string title,string[]? party=null)
    {
        if(LastArea==area)return;
        bool known=Visited.Contains(area) || Events.Any(e=>e.Area==area && e.Kind==ChaoMemoryKind.FirstVisit);
        Record(known ? ChaoMemoryKind.ReturnVisit : ChaoMemoryKind.FirstVisit,"visit",area,
            known ? $"Returned to {title}." : PastUnknown ? $"Visited {title}." : $"Visited {title} for the first time.",.3f,known ? .35f : .85f,party);
        LastArea=area;
        if(Visited.Count<32)Visited.Add(area);
    }
    public bool KnowsAnimal(string id)=>Events.Any(e=>e.Kind==ChaoMemoryKind.AnimalSeen && e.Source=="animal-"+id);
    public float ObjectBias(string id,string area)
    {
        float success=Events.Where(e=>e.Source==id && e.Area==area && e.Valence>0).Sum(e=>e.Influence(Seconds)*(1+Math.Min(e.Occurrences,4)*.15f));
        float failure=Events.Where(e=>e.Source==id && e.Area==area && e.Valence<0).Sum(e=>e.Influence(Seconds));
        return Math.Clamp(success*.10f-failure*.24f/(1+success),-.25f,.25f);
    }
    public float PartnerBias(string peer)=>Math.Clamp(Events.Where(e=>e.Participants.Contains(peer)).Sum(e=>e.Valence*e.Influence(Seconds))*.07f,-.3f,.4f);
    [System.Text.Json.Serialization.JsonIgnore]
    public float Confidence=>Math.Clamp(Events.Where(e=>e.Kind==ChaoMemoryKind.IndependentStudy).Sum(e=>e.Influence(Seconds))*.025f,0,.18f);
    public float PlaceBias(string area,WorldPoint point)=>Math.Clamp(Events.Where(e=>e.Area==area && e.Place is {} p &&
        (p.X-point.X)*(p.X-point.X)+(p.Z-point.Z)*(p.Z-point.Z)<9).Sum(e=>e.Valence*e.Influence(Seconds))*.18f,-.3f,.6f);
    public ChaoMemoryEvent? FavoritePlace(string area)=>Events.Where(e=>e.Area==area && e.Place!=null && e.Valence>.2f)
        .OrderByDescending(e=>e.Influence(Seconds)*e.Valence).FirstOrDefault();
    public void Grow(PetState pet,string source,string trait,float credit)
    {
        // Base personality is innate in stage 1.2; retain memories and social growth.
        if(trait is "Curiosity" or "Kindness")return;
        if(GrowthSources.Count>=128 || !GrowthSources.Add(trait+"|"+source))return;
        int value=trait switch {"Curiosity"=>pet.Curiosity,"Sociability"=>pet.Sociability,"Kindness"=>pet.Kindness,"Bond"=>pet.Bond,_=>0};
        if(!Baseline.ContainsKey(trait))Baseline[trait]=value;
        // A cautious pet remains cautious: maximum eight points of experience growth per trait.
        Credits[trait]=Math.Min(1.99f,Credits.GetValueOrDefault(trait)+Math.Clamp(credit,0,.22f)*(.75f+(value+100)/800f));
        if(Credits[trait]<1 || Gained.GetValueOrDefault(trait)>=8 || value>=Baseline[trait]+8)return;
        Credits[trait]--;Gained[trait]=Gained.GetValueOrDefault(trait)+1;
        switch(trait){case "Curiosity":pet.Curiosity=Math.Min(100,value+1);break;case "Sociability":pet.Sociability=Math.Min(100,value+1);break;case "Kindness":pet.Kindness=Math.Min(100,value+1);break;case "Bond":pet.Bond=Math.Min(100,value+1);break;}
    }
    public void Validate()
    {
        Seconds=double.IsFinite(Seconds) ? Math.Clamp(Seconds,0,1e12) : 0;LastArea=DiscoveryMemory.ValidId(LastArea) ? LastArea : "";
        Visited=new((Visited ?? new()).Where(DiscoveryMemory.ValidId).Take(32));
        Events=(Events ?? new()).Where(e=>e!=null && Enum.IsDefined(e.Kind) && DiscoveryMemory.ValidId(e.Source) && DiscoveryMemory.ValidId(e.Area) &&
            double.IsFinite(e.FirstAt) && double.IsFinite(e.LastAt) && float.IsFinite(e.Valence) && float.IsFinite(e.Importance)).ToList();
        foreach(var e in Events)
        {
            e.Participants=(e.Participants ?? Array.Empty<string>()).Where(DiscoveryMemory.ValidId).Distinct().OrderBy(p=>p,StringComparer.Ordinal).Take(3).ToArray();
            e.Description=(e.Description ?? "")[..Math.Min(180,(e.Description ?? "").Length)];e.FirstAt=Math.Clamp(e.FirstAt,0,Seconds);e.LastAt=Math.Clamp(e.LastAt,e.FirstAt,Seconds);
            e.Valence=Math.Clamp(e.Valence,-1,1);e.Importance=Math.Clamp(e.Importance,.05f,1);e.Occurrences=Math.Clamp(e.Occurrences,1,20);
            if(e.Place is {} p && (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z) || Math.Abs(p.X)>10000 || Math.Abs(p.Z)>10000))e.Place=null;
        }
        Events=Events.GroupBy(e=>e.Key).Select(g=>g.OrderByDescending(e=>e.LastAt).First()).OrderByDescending(e=>e.Influence(Seconds)).Take(Capacity).ToList();
        GrowthSources=new((GrowthSources ?? new()).Where(s=>s!=null && s.Length<=220).Take(128));
        var traits=new[]{"Curiosity","Sociability","Kindness","Bond"};
        Credits=(Credits ?? new()).Where(p=>traits.Contains(p.Key) && float.IsFinite(p.Value)).ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,0,1.99f));
        Gained=(Gained ?? new()).Where(p=>traits.Contains(p.Key)).ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,0,8));
        Baseline=(Baseline ?? new()).Where(p=>traits.Contains(p.Key)).ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,-100,100));
    }
}
