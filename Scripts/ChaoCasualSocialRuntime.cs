using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Mori;

public enum CasualSocialPhase { Notice,Approach,Face,Exchange,React,Finish }
public sealed record CasualSocialDiagnostic(bool Active,string? InitiatorId,string? PartnerId,string Phase,float Elapsed,float Distance,
    string CurrentAvailability,string? LastCompletedPair,double PairCooldownRemaining,double ActorCooldownRemaining,string LastCancellationReason,int SuccessfulEncounters,int CancelledEncounters);

// Runtime eligibility/timing only. No new need meter, relationship or persisted state.
internal sealed class ChaoCasualSocialRuntime
{
    internal const double PairInterval=180,ActorInterval=60,CancelInterval=15;
    internal const int PairCapacity=256,ActorCapacity=64;
    private readonly Dictionary<string,double> pairs=new(StringComparer.Ordinal),actors=new(StringComparer.Ordinal);
    private PetState? a,b;
    internal bool Active {get;private set;}
    internal string A {get;private set;}="";
    internal string B {get;private set;}="";
    internal CasualSocialPhase Phase {get;private set;}
    internal float Elapsed {get;private set;}
    internal float PhaseSeconds {get;private set;}
    internal double Now {get;private set;}
    internal string LastCancellationReason {get;private set;}="None";
    internal string? LastCompletedPair {get;private set;}
    internal int Successful {get;private set;}
    internal int Cancelled {get;private set;}
    internal static string PairKey(string a,string b)=>JsonSerializer.Serialize(new[]{a,b}.OrderBy(s=>s,StringComparer.Ordinal));
    internal void Clock(double now)
    {
        if(!double.IsFinite(now) || now<0)throw new ArgumentException("Invalid active garden time");
        if(now<Now){Cancel("Garden clock restored");pairs.Clear();actors.Clear();}Now=now;
        foreach(var key in pairs.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())pairs.Remove(key);
        foreach(var key in actors.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())actors.Remove(key);
    }
    internal double PairRemaining(string first,string second)=>Math.Max(0,pairs.GetValueOrDefault(PairKey(first,second))-Now);
    internal double ActorRemaining(string id)=>Math.Max(0,actors.GetValueOrDefault(id)-Now);
    internal string Eligibility(PetState root,string first,PetState x,bool readyA,string second,PetState y,bool readyB,float distance,float height)
    {
        if(Active)return "Another casual encounter owns this executor";
        if(first==second || !DiscoveryMemory.ValidId(first) || !DiscoveryMemory.ValidId(second))return "Invalid pair";
        if(!readyA || !readyB || !x.CanAct || !y.CanAct)return "Protected or unavailable participant";
        var relation=root.SocialLink(first,second,create:false);
        if(relation is not {Met:true,Introduced:true})return "First introduction required";
        if(relation.ScoreFrom(first)<0 || relation.ScoreFrom(second)<0)return "Negative directional relationship";
        if(x.Energy<35 || y.Energy<35)return "Energy below 35";
        if(x.SocialNeed<25 || y.SocialNeed<15)return "SocialNeed below initiator/recipient threshold";
        if(new[]{x,y}.Any(p=>p.Needs.Evaluate().Priorities.Any(n=>n.Need is ChaoNeed.Hunger or ChaoNeed.Fatigue && n.Level==NeedUrgency.Critical)))return "Critical food or rest need";
        if(!float.IsFinite(distance) || distance>2.2f || distance<.45f)return "Distance outside safe nearby range";
        if(!float.IsFinite(height) || Math.Abs(height)>.16f)return "Height difference";
        if(PairRemaining(first,second)>0 || ActorRemaining(first)>0 || ActorRemaining(second)>0)return "Casual greeting cooldown";
        return "Available";
    }
    internal bool Begin(PetState root,string first,PetState x,bool readyA,string second,PetState y,bool readyB,float distance,float height)
    {
        if(Eligibility(root,first,x,readyA,second,y,readyB,distance,height)!="Available")return false;
        A=first;B=second;a=x;b=y;Active=true;Phase=CasualSocialPhase.Notice;Elapsed=PhaseSeconds=0;return true;
    }
    internal void Rebind(string id,PetState state){if(Active){if(id==A)a=state;if(id==B)b=state;}}
    internal void Advance(float dt,float distance,bool prepared=true)
    {
        if(!Active || !float.IsFinite(dt) || dt<=0)return;Elapsed+=dt;
        if(Elapsed>10){Cancel("Hard encounter timeout");return;}
        if(!prepared)return;PhaseSeconds+=dt;
        switch(Phase)
        {
            case CasualSocialPhase.Notice:if(PhaseSeconds>=.6f)Next(distance>.85f ? CasualSocialPhase.Approach : CasualSocialPhase.Face);break;
            case CasualSocialPhase.Approach:if(distance<=.85f)Next(CasualSocialPhase.Face);else if(PhaseSeconds>=3)Cancel("Approach timeout");break;
            case CasualSocialPhase.Face:if(PhaseSeconds>=.45f)Next(CasualSocialPhase.Exchange);break;
            case CasualSocialPhase.Exchange:if(PhaseSeconds>=2)Next(CasualSocialPhase.React);break;
            case CasualSocialPhase.React:if(PhaseSeconds>=1)Complete();break;
        }
    }
    private void Next(CasualSocialPhase phase){Phase=phase;PhaseSeconds=0;}
    private void Complete()
    {
        if(!Active || a==null || b==null)return;Next(CasualSocialPhase.Finish);Successful++;
        a.Needs.PositiveContact(3);b.Needs.PositiveContact(3);
        string source="casual-greeting-"+Successful;
        a.Mood.React(ChaoEmotion.Happy,.25f,3,source,25);b.Mood.React(ChaoEmotion.Happy,.25f,3,source,25);
        a.SocialSelection.LastCompletedPartner=B;b.SocialSelection.LastCompletedPartner=A;
        LastCompletedPair=PairKey(A,B);Cooldown(PairInterval,ActorInterval);Active=false;a=b=null;
    }
    internal void Cancel(string reason)
    {if(!Active)return;Cancelled++;LastCancellationReason=reason;Cooldown(CancelInterval,CancelInterval);Active=false;a=b=null;}
    private void Cooldown(double pair,double actor)
    {
        pairs[PairKey(A,B)]=Now+pair;actors[A]=actors[B]=Now+actor;
        while(pairs.Count>PairCapacity)pairs.Remove(pairs.MinBy(p=>p.Value).Key);
        while(actors.Count>ActorCapacity)actors.Remove(actors.MinBy(p=>p.Value).Key);
    }
    internal CasualSocialDiagnostic Diagnostic(string id,float distance,string availability)=>new(Active && (id==A || id==B),
        Active && (id==A || id==B) ? A : null,Active && (id==A || id==B) ? B : null,Active && (id==A || id==B) ? Phase.ToString() : "Inactive",Active && (id==A || id==B) ? Elapsed : 0,distance,availability,LastCompletedPair,
        A.Length>0 && (id==A || id==B) ? PairRemaining(A,B) : 0,ActorRemaining(id),LastCancellationReason,Successful,Cancelled);
}
