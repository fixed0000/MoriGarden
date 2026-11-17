using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public sealed class PuzzleElementState
{
    public int Value { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public bool Positioned { get; set; }
}
public sealed class CompactMissionState
{
    public Dictionary<string,PuzzleElementState> Elements { get; set; }=new(StringComparer.Ordinal);
    public float Restoration { get; set; }
    public bool Completed { get; set; }
    public bool RewardClaimed { get; set; }
    public int Revision { get; set; }
    public PuzzleElementState Element(string id)
    { if(!Elements.TryGetValue(id,out var element))Elements[id]=element=new();return element; }
    public bool ResetUnfinished()
    { if(Completed || RewardClaimed)return false;Elements.Clear();Restoration=0;Revision=Math.Min(1000000,Revision+1);return true; }
    public void Validate()
    {
        Elements=(Elements ?? new()).Where(p=>DiscoveryMemory.ValidId(p.Key) && p.Value!=null).Take(32).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        foreach(var element in Elements.Values)
        {
            element.Value=Math.Clamp(element.Value,0,8);
            if(!float.IsFinite(element.X) || !float.IsFinite(element.Z) || Math.Abs(element.X)>100 || Math.Abs(element.Z)>100)
            { element.Positioned=false;element.X=element.Z=0; }
        }
        Restoration=float.IsFinite(Restoration) ? Math.Clamp(Restoration,0,1) : 0;
        Revision=Math.Clamp(Revision,0,1000000);
        Completed|=RewardClaimed;
        if(Completed)Restoration=1;
    }
}

public static class GreenhouseRules
{
    public const string Intake="intake",Diverter="diverter",Plate="filter-plate",Crate="counterweight";
    public static CompactMissionState State(PetState owner)=>owner.Expedition.Mission(WorldCatalog.Greenhouse);
    public static bool WaterRunning(CompactMissionState state,bool plate)=>state.Completed ||
        state.Element(Intake).Value==1 && state.Element(Diverter).Value==2 && plate;
    public static bool Advance(PetState owner,bool plate,float seconds)
    {
        var state=State(owner);
        if(state.Completed || !WaterRunning(state,plate) || seconds<=0)return false;
        state.Restoration=Math.Min(1,state.Restoration+seconds/18);
        if(state.Restoration<1)return false;
        state.Completed=true;
        if(!owner.Expedition.AreaFlags.TryGetValue(WorldCatalog.Meadow,out var flags))owner.Expedition.AreaFlags[WorldCatalog.Meadow]=flags=new();
        flags.Add("greenhouse-restored");return true;
    }
    public static bool ClaimReward(PetState owner)
    {
        var state=State(owner);
        if(!state.Completed || state.RewardClaimed || owner.SeedCount(FruitKinds.Moonblossom)>=12)return false;
        owner.SeedBag[FruitKinds.Moonblossom]=owner.SeedCount(FruitKinds.Moonblossom)+1;
        state.RewardClaimed=true;return true;
    }
}
public static class PuzzleReasoning
{
    public static float ThoughtSeconds(PetState state)=>4+(1-state.IntelligenceAbility/990f)*3+(1-state.Initiative/990f);
    public static float ActionChance(PetState state)=>Math.Clamp((.12f+.35f*state.Initiative/990f+.32f*state.IntelligenceAbility/990f+
        .13f*DiscoveryDecisions.Curiosity(state))*(.5f+.5f*state.Energy/100),.05f,.94f);
}
