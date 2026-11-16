using System;
using System.Collections.Generic;
using System.Linq;

namespace Mori;

public enum DiscoveryKind { Plant, Stone, Shelter, Unknown, Vista, Water, Sound, Hidden }
[Flags] public enum DiscoveryReaction { Watch=1, Inspect=2, Touch=4, Listen=8, Share=16 }
public enum DiscoveryKnowledge { Unknown, Noticed, Studied, Familiar, Recent }
public sealed record DiscoveryDefinition(string Id,DiscoveryKind Kind,WorldPoint Position,float Radius,float Appeal,
    DiscoveryReaction Reactions,bool Repeatable=true,double RepeatSeconds=1200,string? RequiredFlag=null,float LookHeight=.3f,string AreaId=WorldCatalog.Meadow,int Revision=0);

public sealed class DiscoveryRecord
{
    public string Id { get; set; }="";
    public double NoticedAt { get; set; }
    public double StudiedAt { get; set; }=-1;
    public int Visits { get; set; }
    public int ObservedRevision { get; set; }
    public int StudiedRevision { get; set; }=-1;
}
public sealed class DiscoveryMemory
{
    public const int Capacity=48;
    public List<DiscoveryRecord> Objects { get; set; }=new();
    public HashSet<string> Rewarded { get; set; }=new(StringComparer.Ordinal);
    public HashSet<string> SharedRewards { get; set; }=new(StringComparer.Ordinal);
    public Dictionary<DiscoveryKind,float> Affinity { get; set; }=new();
    public float InitiativeCredit { get; set; }
    public int InitiativeGained { get; set; }
    public float SocialCredit { get; set; }
    public int SocialGained { get; set; }
    public double LastAttention { get; set; }=-1000;
    public double LastInvitation { get; set; }=-1000;
    public double LastFinished { get; set; }=-1000;
    public DiscoveryRecord? Find(string id)=>Objects.Find(r=>r.Id==id);
    public DiscoveryKnowledge Knowledge(string id,double now)
    {
        var record=Find(id);
        return record==null ? DiscoveryKnowledge.Unknown : record.Visits==0 ? DiscoveryKnowledge.Noticed :
            now-record.StudiedAt<180 ? DiscoveryKnowledge.Recent : record.Visits>=3 ? DiscoveryKnowledge.Familiar : DiscoveryKnowledge.Studied;
    }
    public bool CanConsider(DiscoveryDefinition item,double now)
    {
        var record=Find(item.Id);
        return record==null || record.ObservedRevision<item.Revision ||
            (record.Visits==0 || record.StudiedRevision<item.Revision ? now-record.NoticedAt>=120 : item.Repeatable && now-record.StudiedAt>=Math.Max(600,item.RepeatSeconds));
    }
    public DiscoveryRecord Notice(string id,double now,int revision=0)
    {
        var record=Find(id);
        if(record==null)
        {
            if(Objects.Count>=Capacity)Objects.Remove(Objects.OrderBy(r=>Math.Max(r.NoticedAt,r.StudiedAt)).First());
            record=new() { Id=id };Objects.Add(record);
        }
        record.NoticedAt=now;record.ObservedRevision=Math.Max(record.ObservedRevision,revision);return record;
    }
    public bool Complete(DiscoveryDefinition item,PetState state,double now)
    {
        var old=Find(item.Id);
        if(old?.Visits>0 && old.StudiedRevision>=item.Revision && (!item.Repeatable || now-old.StudiedAt<Math.Max(600,item.RepeatSeconds)))return false;
        var record=Notice(item.Id,now,item.Revision);record.Visits=Math.Min(15,record.Visits+1);record.StudiedAt=now;record.StudiedRevision=item.Revision;LastFinished=now;
        if(Rewarded.Count<64 && Rewarded.Add(item.Id))
        {
            Affinity[item.Kind]=Math.Min(1,Affinity.GetValueOrDefault(item.Kind)+.035f);
            InitiativeCredit+=.2f;
            if(InitiativeCredit>=.999f && InitiativeGained<8)
            { state.Initiative=Math.Min(990,state.Initiative+1);InitiativeGained++;InitiativeCredit=0; }
        }
        else Affinity[item.Kind]=Math.Min(1,Affinity.GetValueOrDefault(item.Kind)+.003f);
        return true;
    }
    public int ShareSuccess(string item,string peer)
    {
        string key=item+"/"+peer;
        if(SocialGained>=6 || SharedRewards.Count>=96 || !SharedRewards.Add(key))return 0;
        SocialCredit+=.2f;
        if(SocialCredit<.999f)return 0;
        SocialCredit=0;SocialGained++;return 1;
    }
    public void Validate()
    {
        Objects=(Objects ?? new()).Where(r=>r!=null && ValidId(r.Id) && double.IsFinite(r.NoticedAt) && double.IsFinite(r.StudiedAt))
            .DistinctBy(r=>r.Id).OrderByDescending(r=>Math.Max(r.NoticedAt,r.StudiedAt)).Take(Capacity).ToList();
        foreach(var record in Objects) { record.Visits=Math.Clamp(record.Visits,0,15);record.NoticedAt=Math.Max(0,record.NoticedAt);record.StudiedAt=Math.Max(-1,record.StudiedAt);
            record.ObservedRevision=Math.Clamp(record.ObservedRevision,0,1000000);record.StudiedRevision=record.Visits>0 ? Math.Clamp(record.StudiedRevision<0 ? record.ObservedRevision : record.StudiedRevision,0,1000000) : -1; }
        Rewarded=new((Rewarded ?? new()).Where(ValidId).Take(64),StringComparer.Ordinal);
        SharedRewards=new((SharedRewards ?? new()).Where(id=>id!=null && id.Length<=193).Take(96),StringComparer.Ordinal);
        Affinity=(Affinity ?? new()).Where(p=>Enum.IsDefined(p.Key) && float.IsFinite(p.Value)).ToDictionary(p=>p.Key,p=>Math.Clamp(p.Value,0,1));
        InitiativeCredit=float.IsFinite(InitiativeCredit) ? Math.Clamp(InitiativeCredit,0,.999f) : 0;InitiativeGained=Math.Clamp(InitiativeGained,0,8);
        SocialCredit=float.IsFinite(SocialCredit) ? Math.Clamp(SocialCredit,0,.999f) : 0;SocialGained=Math.Clamp(SocialGained,0,6);
        if(!double.IsFinite(LastAttention))LastAttention=-1000;if(!double.IsFinite(LastInvitation))LastInvitation=-1000;if(!double.IsFinite(LastFinished))LastFinished=-1000;
    }
    public static bool ValidId(string? id)=>!string.IsNullOrWhiteSpace(id) && id.Length<=96;
}
