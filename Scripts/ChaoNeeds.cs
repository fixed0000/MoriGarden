using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChaoNeed { Hunger, Fatigue, Boredom, SocialNeed }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NeedUrgency { Low, Moderate, High, Critical }
public enum NeedActionContext { None, ShortSafe, LongOptional }
public sealed record NeedPriority(ChaoNeed Need,float Value,NeedUrgency Level,float Weight,
    bool CanInterruptCurrentAction,double LevelSeconds,string Reason);
public sealed record NeedEvaluation(IReadOnlyList<NeedPriority> Priorities);

// A view of the existing meters, plus runtime-only classification/history.
// It never owns another fullness, energy or fun value and never selects actions.
public sealed class ChaoNeeds
{
    private readonly PetState pet;
    private readonly Dictionary<ChaoNeed,(NeedUrgency Level,double Seconds)> levels=new();
    private double aloneSeconds;
    public const float AwakeSocialRate=.02f, SleepingSocialRate=.001f, LonelySocialRate=.015f;
    public const double LonelyAfterSeconds=1200;
    internal ChaoNeeds(PetState pet) => this.pet=pet;
    internal ChaoNeeds CopyFor(PetState owner)
    {
        var copy=new ChaoNeeds(owner){aloneSeconds=aloneSeconds};
        foreach(var entry in levels)copy.levels[entry.Key]=entry.Value;
        return copy;
    }
    public float Hunger => 100-Meter(pet.Fullness);
    public float Fatigue => 100-Meter(pet.Energy);
    public float Boredom => 100-Meter(pet.Fun);
    public float SocialNeed => Meter(pet.SocialNeed);
    private static float Meter(float value) => float.IsFinite(value) ? Math.Clamp(value,0,100) : throw new ArgumentException("A need meter is not finite.");
    private float Value(ChaoNeed need) => need switch
    { ChaoNeed.Hunger=>Hunger,ChaoNeed.Fatigue=>Fatigue,ChaoNeed.Boredom=>Boredom,_=>SocialNeed };
    private void Refresh()
    {
        foreach(var need in Enum.GetValues<ChaoNeed>())
        {
            float value=Value(need);
            var raw=(NeedUrgency)(value>=75 ? 3 : value>=50 ? 2 : value>=25 ? 1 : 0);
            if(!levels.TryGetValue(need,out var previous)){levels[need]=(raw,0);continue;}
            int level=(int)previous.Level;
            if(raw>previous.Level)level=(int)raw;
            else while(level>0 && value<level*25-5)level--;
            levels[need]=((NeedUrgency)level,level==(int)previous.Level ? previous.Seconds : 0);
        }
    }
    internal void Advance(float seconds,bool sleeping)
    {
        double prior=aloneSeconds;
        if(!sleeping)aloneSeconds=Math.Min(1e9,aloneSeconds+seconds);
        // Integrate only the portion of this tick actually past the loneliness threshold.
        double lonelyTime=sleeping ? 0 : Math.Max(0,aloneSeconds-LonelyAfterSeconds)-Math.Max(0,prior-LonelyAfterSeconds);
        pet.SocialNeed=Math.Clamp(pet.SocialNeed+seconds*(sleeping ? SleepingSocialRate : AwakeSocialRate)+(float)lonelyTime*LonelySocialRate,0,100);
        Refresh();
        foreach(var key in levels.Keys.ToArray()){var state=levels[key];levels[key]=(state.Level,Math.Min(1e9,state.Seconds+seconds));}
    }
    internal void PositiveContact(float relief)
    { pet.SocialNeed=Math.Clamp(pet.SocialNeed-relief,0,100);aloneSeconds=0;Refresh(); }
    public NeedEvaluation Evaluate(NeedActionContext context=NeedActionContext.None)
    {
        Refresh();
        return new(Enum.GetValues<ChaoNeed>().Select(need=>
        {
            var state=levels[need];float value=Value(need);bool essential=need is ChaoNeed.Hunger or ChaoNeed.Fatigue;
            float weight=Math.Clamp(value/100*(essential ? .92f : .76f)+(state.Level==NeedUrgency.Critical ? .06f : 0)+(float)Math.Min(.06,state.Seconds/6000),0,1);
            bool interrupt=essential && state.Level>=NeedUrgency.High && context==NeedActionContext.LongOptional;
            return new NeedPriority(need,value,state.Level,weight,interrupt,state.Seconds,
                $"{need}: {state.Level}, {value:0.0}/100; same level for {state.Seconds:0}s. "+
                (interrupt ? "May interrupt a long optional action." : "No immediate interruption; finish a short safe action."));
        }).ToArray());
    }
    public static NeedEvaluation EvaluateNeeds(PetState pet,NeedActionContext context=NeedActionContext.None) => pet.Needs.Evaluate(context);
}
