using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActivityHabitEntry
{
    [JsonRequired] public float Bias { get; set; }
    [JsonRequired] public int ExperienceCount { get; set; }
    [JsonRequired] public double LastUpdateSeconds { get; set; }
    [JsonRequired] public double LastCreditSeconds { get; set; }
    internal void Validate()
    {
        if(!float.IsFinite(Bias) || Bias is <0 or >.10f || ExperienceCount is <0 or >1000 ||
            !double.IsFinite(LastUpdateSeconds) || LastUpdateSeconds<0 || !double.IsFinite(LastCreditSeconds) ||
            LastCreditSeconds<0 || LastCreditSeconds>LastUpdateSeconds)throw new ArgumentException("Invalid activity habit entry. The save was kept.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChaoActivityHabits
{
    public const double HalfLife=24000,CreditInterval=120;
    public const int ReplayCapacity=128;
    public static IReadOnlyList<ChaoActivity> Supported { get; }=Array.AsReadOnly(new[]{ChaoActivity.Play,ChaoActivity.Socialize,ChaoActivity.Leisure});
    [JsonRequired] public int SchemaVersion { get; set; }=1;
    [JsonRequired] public Dictionary<string,ActivityHabitEntry> Entries { get; set; }=new(StringComparer.Ordinal);
    private readonly Queue<string> order=new();
    private readonly HashSet<string> processed=new(StringComparer.Ordinal);
    private ChaoActivity? lastCategory;
    private double? lastTime;
    [JsonIgnore] public ChaoActivity? LastCreditedCategory => lastCategory ??
        (Enum.TryParse<ChaoActivity>(Entries.Where(p=>p.Value.ExperienceCount>0).OrderByDescending(p=>p.Value.LastCreditSeconds).ThenBy(p=>p.Key,StringComparer.Ordinal).FirstOrDefault().Key,out var a) ? a : null);
    [JsonIgnore] public double? LastCreditTime => lastTime ?? Entries.Values.Where(e=>e.ExperienceCount>0).Select(e=>(double?)e.LastCreditSeconds).DefaultIfEmpty(null).Max();
    [JsonIgnore] public string LastRejectedReason { get; private set; }="None";
    [JsonIgnore] public int ProcessedCompletionCount=>processed.Count;
    public void Validate()
    {
        if(SchemaVersion!=1)throw new UnsupportedActivityHabitsSchemaException();
        if(Entries==null || Entries.Count>3)throw new ArgumentException("Invalid activity habit collection");
        foreach(var (id,entry) in Entries)
        {if(!Supported.Any(a=>a.ToString()==id) || entry==null)throw new ArgumentException("Unknown activity habit category");entry.Validate();}
    }
    private static void CheckTime(double now)
    {if(!double.IsFinite(now) || now<0)throw new ArgumentException("Invalid active Chao time");}
    public float EffectiveBias(ChaoActivity activity,double now)
    {
        CheckTime(now);
        if(!Supported.Contains(activity) || !Entries.TryGetValue(activity.ToString(),out var entry))return 0;
        return (float)(entry.Bias*Math.Pow(.5,Math.Max(0,now-entry.LastUpdateSeconds)/HalfLife));
    }
    public bool Credit(ChaoActivity activity,double now,string completionId)
    {
        CheckTime(now);
        if(string.IsNullOrWhiteSpace(completionId) || completionId.Length>160)throw new ArgumentException("Invalid completed habit identity");
        if(!Supported.Contains(activity)){LastRejectedReason="Unsupported category";return false;}
        if(!processed.Add(completionId)){LastRejectedReason="Duplicate completion";return false;}
        order.Enqueue(completionId);while(order.Count>ReplayCapacity)processed.Remove(order.Dequeue());
        Entries.TryGetValue(activity.ToString(),out var previous);
        if(previous is {ExperienceCount:>0} && now-previous.LastCreditSeconds<CreditInterval)
        {LastRejectedReason="Category credit interval (120 active seconds)";return false;}
        float effective=EffectiveBias(activity,now);
        Entries[activity.ToString()]=new(){Bias=Math.Clamp(effective+.02f*Math.Max(0,1-effective/.10f),0,.10f),
            ExperienceCount=Math.Min(1000,(previous?.ExperienceCount ?? 0)+1),LastUpdateSeconds=now,LastCreditSeconds=now};
        lastCategory=activity;lastTime=now;return true;
    }
    internal void KeepRuntimeFrom(ChaoActivityHabits source)
    {
        if(ReferenceEquals(this,source))return;
        order.Clear();processed.Clear();foreach(var id in source.order){order.Enqueue(id);processed.Add(id);}
        lastCategory=source.lastCategory;lastTime=source.lastTime;LastRejectedReason=source.LastRejectedReason;
    }
}
public sealed class UnsupportedActivityHabitsSchemaException : ArgumentException
{public UnsupportedActivityHabitsSchemaException():base("Unsupported activity habits schema. Use a compatible game version; the save was kept."){} }

public sealed partial class PetState
{
    public ChaoActivityHabits ActivityHabits { get; set; }=new();
    internal void ValidateActivityHabits()
    {if(ActivityHabits==null)throw new ArgumentException("Missing activity habits model");ActivityHabits.Validate();}
    public object ActivityHabitDiagnostics()=>new
    {
        activeSeconds=History.Seconds,ActivityHabits.LastCreditedCategory,ActivityHabits.LastCreditTime,ActivityHabits.LastRejectedReason,
        entries=ChaoActivityHabits.Supported.Select(a=>
        {
            ActivityHabits.Entries.TryGetValue(a.ToString(),out var e);
            return new{activity=a.ToString(),effectiveBias=ActivityHabits.EffectiveBias(a,History.Seconds),experienceCount=e?.ExperienceCount ?? 0,
                lastCreditSeconds=(double?)e?.LastCreditSeconds,lastUpdateSeconds=(double?)e?.LastUpdateSeconds};
        }).ToArray()
    };
}
