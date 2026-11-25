using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

public enum LearnedItemGroup { Toy, Fruit }

// Exact canonical type IDs, never instance names or localized display strings.
public static class LearnedPreferenceIds
{
    public static IReadOnlyList<string> Toys { get; }=Array.AsReadOnly(new[]{"toy-car","rattle","beach-ball","flute","drums","electric-guitar","synthesizer"});
    public static IReadOnlyList<string> Fruits { get; }=Array.AsReadOnly(new[]{FruitKinds.Garden,FruitKinds.Heart,FruitKinds.Blue,FruitKinds.Yellow,FruitKinds.Red,FruitKinds.Green,FruitKinds.Moonblossom});
    public static bool Known(LearnedItemGroup group,string id)=>group==LearnedItemGroup.Toy ? Toys.Contains(id) : group==LearnedItemGroup.Fruit && Fruits.Contains(id);
    public static string Toy(bool rattle,bool flute,bool guitar,bool synth,bool ball,bool drums)=>
        drums ? "drums" : ball ? "beach-ball" : guitar ? "electric-guitar" : synth ? "synthesizer" : flute ? "flute" : rattle ? "rattle" : "toy-car";
    public static string? Fruit(string kind)=>Fruits.Contains(kind) ? kind : null;
    public static string? FixedToy(string name)=>name switch
    {"Toy Car"=>"toy-car","Rattle"=>"rattle","Beach Ball"=>"beach-ball","Flute"=>"flute","Drums"=>"drums","Electric Guitar"=>"electric-guitar","Synthesizer"=>"synthesizer",_=>null};
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LearnedPreferenceEntry
{
    [JsonRequired] public float Bias { get; set; }
    [JsonRequired] public int ExperienceCount { get; set; }
    [JsonRequired] public double LastUpdateSeconds { get; set; }
    [JsonRequired] public double LastCreditSeconds { get; set; }
    internal void Validate()
    {
        if(!float.IsFinite(Bias) || Bias is < -.30f or > .30f || ExperienceCount is <0 or >1000 ||
            !double.IsFinite(LastUpdateSeconds) || LastUpdateSeconds<0 || !double.IsFinite(LastCreditSeconds) || LastCreditSeconds<0 || LastCreditSeconds>LastUpdateSeconds)
            throw new ArgumentException("Invalid learned preference entry. The save was kept.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChaoLearnedPreferences
{
    public const double HalfLife=12000, CreditInterval=45;
    public const int ReplayCapacity=128;
    [JsonRequired] public int SchemaVersion { get; set; }=1;
    [JsonRequired] public Dictionary<string,LearnedPreferenceEntry> ToyPreferences { get; set; }=new(StringComparer.Ordinal);
    [JsonRequired] public Dictionary<string,LearnedPreferenceEntry> FruitPreferences { get; set; }=new(StringComparer.Ordinal);
    private readonly Queue<string> processedOrder=new();
    private readonly HashSet<string> processed=new(StringComparer.Ordinal);
    [JsonIgnore] public int ProcessedCompletionCount=>processed.Count;
    private Dictionary<string,LearnedPreferenceEntry> Entries(LearnedItemGroup group)=>group switch
    {LearnedItemGroup.Toy=>ToyPreferences,LearnedItemGroup.Fruit=>FruitPreferences,_=>throw new ArgumentException("Unknown preference group")};
    public void Validate()
    {
        if(SchemaVersion!=1)throw new UnsupportedLearnedPreferencesSchemaException();
        foreach(var group in Enum.GetValues<LearnedItemGroup>())
        {
            var entries=Entries(group);
            if(entries==null || entries.Count>7)throw new ArgumentException("Invalid learned preference collection");
            foreach(var (id,entry) in entries)
            {if(!LearnedPreferenceIds.Known(group,id) || entry==null)throw new ArgumentException("Unknown learned item type");entry.Validate();}
        }
    }
    private static void CheckTime(double now)
    {if(!double.IsFinite(now) || now<0)throw new ArgumentException("Invalid active Chao time");}
    public float EffectiveBias(LearnedItemGroup group,string id,double now)
    {
        CheckTime(now);
        if(!LearnedPreferenceIds.Known(group,id) || !Entries(group).TryGetValue(id,out var entry))return 0;
        return (float)(entry.Bias*Math.Pow(.5,Math.Max(0,now-entry.LastUpdateSeconds)/HalfLife));
    }
    public float Multiplier(LearnedItemGroup group,string id,double now)=>1+EffectiveBias(group,id,now);
    // Only executor-verified completions call this. The negative handler is
    // deliberately not wired to path failure, taste gates or player interruption.
    public bool Credit(LearnedItemGroup group,string id,double now,string completionId,int outcomeSign=1)
    {
        CheckTime(now);
        if(outcomeSign is not (1 or -1) || string.IsNullOrWhiteSpace(completionId) || completionId.Length>160)throw new ArgumentException("Invalid completed preference experience");
        if(!LearnedPreferenceIds.Known(group,id))return false;
        if(!processed.Add(completionId))return false;
        processedOrder.Enqueue(completionId);
        while(processedOrder.Count>ReplayCapacity)processed.Remove(processedOrder.Dequeue());
        var entries=Entries(group);entries.TryGetValue(id,out var prior);
        if(prior is {ExperienceCount:>0} && now-prior.LastCreditSeconds<CreditInterval)return false;
        float effective=EffectiveBias(group,id,now);
        float bias=Math.Clamp(effective+outcomeSign*.07f*Math.Max(0,1-Math.Abs(effective)/.30f),-.30f,.30f);
        entries[id]=new(){Bias=bias,ExperienceCount=Math.Min(1000,(prior?.ExperienceCount ?? 0)+1),LastUpdateSeconds=now,LastCreditSeconds=now};
        return true;
    }
    internal void KeepRuntimeFrom(ChaoLearnedPreferences source)
    {
        if(ReferenceEquals(this,source))return;
        processedOrder.Clear();processed.Clear();
        foreach(var id in source.processedOrder){processedOrder.Enqueue(id);processed.Add(id);}
    }
}

public sealed class UnsupportedLearnedPreferencesSchemaException : ArgumentException
{
    public UnsupportedLearnedPreferencesSchemaException():base("Unsupported learned preferences schema. Use a compatible game version; the save was kept."){}
}
