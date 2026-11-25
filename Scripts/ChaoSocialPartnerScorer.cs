using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

public sealed record SocialPartnerCandidate(string Id,float Distance,bool Available=true);
public sealed record SocialPartnerScore(string Id,int Relationship,string Status,float PartnerBias,
    float RelationshipContribution,float MemoryContribution,float PreferenceModifier,float PartnerWeight,
    float DistanceWeight,float RecentMultiplier,bool Available,float FinalWeight);

// Read-only views of the existing directional relationship and personal memory.
public static class ChaoSocialPartnerScorer
{
    public static SocialPartnerScore Evaluate(PetState root,string seekerId,PetState seeker,string peerId,
        float distance=0,bool available=true,bool recent=false)
    {
        var link=root.SocialLink(seekerId,peerId,create:false);
        int score=link?.ScoreFrom(seekerId) ?? 0;
        float memory=seeker.History.PartnerBias(peerId),relationship=.20f*Math.Clamp(score/100f,-1,1),experience=.25f*memory;
        float modifier=Math.Clamp(relationship+experience,-.30f,.30f),weight=Math.Clamp(1+modifier,.70f,1.30f);
        available=available && peerId!=seekerId && !string.IsNullOrWhiteSpace(peerId) && float.IsFinite(distance) && distance>=0;
        float proximity=float.IsFinite(distance) && distance>=0 ? 1/(1+.12f*distance) : 0;
        return new(peerId,score,link?.StatusFrom(seekerId) ?? "Stranger",memory,relationship,experience,modifier,weight,
            proximity,recent ? .85f : 1,available,available ? weight*proximity*(recent ? .85f : 1) : 0);
    }
    public static IReadOnlyList<SocialPartnerScore> Candidates(PetState root,string seekerId,PetState seeker,
        IEnumerable<SocialPartnerCandidate> candidates,bool repeatable=true)
    {
        var peers=candidates.Where(c=>c.Id!=seekerId).DistinctBy(c=>c.Id).ToArray();
        int count=peers.Count(c=>c.Available && float.IsFinite(c.Distance) && c.Distance>=0);
        return peers.Select(c=>Evaluate(root,seekerId,seeker,c.Id,c.Distance,c.Available,
            repeatable && count>1 && seeker.SocialSelection.LastCompletedPartner==c.Id)).ToArray();
    }
    public static SocialPartnerScore? Choose(IEnumerable<SocialPartnerScore> scores,Random random)
    {
        var choices=scores.Where(s=>s.Available && float.IsFinite(s.FinalWeight) && s.FinalWeight>0).ToArray();
        if(choices.Length==0)return null;
        double draw=random.NextDouble()*choices.Sum(s=>(double)s.FinalWeight);
        foreach(var item in choices){draw-=item.FinalWeight;if(draw<0)return item;}return choices[^1];
    }
    // Completion only: no additional need relief, relationship growth or personality mutation.
    public static void CompleteShared(string source,IReadOnlyList<(string Id,PetState State)> participants)
    {
        if(source is not ("shared-ball" or "music-ensemble") || participants.Count<2 ||
            participants.Select(p=>p.Id).Distinct().Count()!=participants.Count || participants.Any(p=>!DiscoveryMemory.ValidId(p.Id)))return;
        foreach(var member in participants)
        {
            var peers=participants.Where(p=>p.Id!=member.Id).Select(p=>p.Id).ToArray();
            member.State.History.Record(ChaoMemoryKind.Pleasant,source,"garden",
                source=="shared-ball" ? "Enjoyed playing ball with another Chao." : "Enjoyed performing music with other Chao.",.65f,.55f,peers);
            // Record may refuse cooldown/capacity; the physical interaction still completed.
            member.State.SocialSelection.LastCompletedPartner=peers[0];
        }
    }
}

public sealed class SocialPartnerSelection
{
    private Random? random;
    public string? LastCompletedPartner { get; internal set; }
    public string LastInteraction { get; private set; }="None";
    public string? LastChosenPartner { get; private set; }
    public IReadOnlyList<SocialPartnerScore> LastCandidates { get; private set; }=Array.Empty<SocialPartnerScore>();
    public SocialPartnerSelection(Random? injected=null)=>random=injected;
    public Random RandomFor(string id)
    {if(random==null){int seed=7219;foreach(char c in id)seed=unchecked(seed*31+c);random=new(seed);}return random;}
    public string? Select(PetState root,string id,PetState seeker,IEnumerable<SocialPartnerCandidate> candidates,string interaction,bool repeatable=true)
    {
        LastCandidates=ChaoSocialPartnerScorer.Candidates(root,id,seeker,candidates,repeatable);LastInteraction=interaction;
        LastChosenPartner=ChaoSocialPartnerScorer.Choose(LastCandidates,RandomFor(id))?.Id;return LastChosenPartner;
    }
}

public sealed partial class PetState
{
    [JsonIgnore] public SocialPartnerSelection SocialSelection { get; internal set; }=new();
    public object SocialPreferenceDiagnostics(PetState root,string id,IReadOnlyDictionary<string,PetState> roster)=>new
    {
        lastCompletedPartner=SocialSelection.LastCompletedPartner,lastInteraction=SocialSelection.LastInteraction,
        lastChosenPartner=SocialSelection.LastChosenPartner,
        partners=roster.Where(p=>p.Key!=id && !p.Value.IsEgg).Select(p=>new{ id=p.Key,name=p.Value.Name,
            score=ChaoSocialPartnerScorer.Evaluate(root,id,this,p.Key),
            lastSelection=SocialSelection.LastCandidates.FirstOrDefault(c=>c.Id==p.Key) }).ToArray()
    };
}
