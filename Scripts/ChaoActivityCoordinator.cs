using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mori;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChaoActivity { Eat, Sleep, Play, Socialize, Leisure, Wander }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActivityInterruptibility { Atomic, ShortSafe, LongOptional }
public sealed record ActivityAvailability(ChaoActivity Activity,bool Available,string Reason,float EstimatedDuration,
    ActivityInterruptibility Interruptibility,int AvailableTargetCount);
public sealed record ActivityScore(ActivityAvailability Availability,float Score);
public sealed record ActivityCompletion(ChaoActivity Activity,double At);

// Selection and ownership only. No Godot dependency, targets, animation, movement,
// memories, relationships or personality weights. One instance per garden actor.
public sealed class ChaoActivityCoordinator
{
    private readonly Random random;
    private readonly Queue<ActivityCompletion> history=new();
    private readonly Dictionary<ChaoActivity,(string Reason,int Count,double Until)> failures=new();
    public ChaoActivity? SelectedActivity { get; private set; }
    public double Now { get; private set; }
    public double StartedAt { get; private set; }
    public double MinimumCommitment { get; private set; }
    public double MinimumRemaining=>Math.Max(0,StartedAt+MinimumCommitment-Now);
    public double CooldownUntil=>SelectedActivity is {} a && failures.TryGetValue(a,out var f) ? f.Until : 0;
    public string LastFailureReason { get; private set; }="";
    public string CurrentExecutor { get; private set; }="";
    public ActivityInterruptibility Interruptibility { get; private set; }=ActivityInterruptibility.LongOptional;
    public IReadOnlyList<ActivityScore> Scores { get; private set; }=Array.Empty<ActivityScore>();
    public IReadOnlyList<ActivityCompletion> History=>history.ToArray();
    public int Selections { get; private set; }
    public int Interruptions { get; private set; }
    public string LastCancellation { get; private set; }="";
    public ChaoActivityCoordinator(Random random) => this.random=random;
    public void Advance(float seconds)
    {if(!float.IsFinite(seconds) || seconds<0)throw new ArgumentException("Invalid activity interval");Now+=seconds;}
    private static ChaoNeed? NeedFor(ChaoActivity a)=>a switch
    {ChaoActivity.Eat=>ChaoNeed.Hunger,ChaoActivity.Sleep=>ChaoNeed.Fatigue,ChaoActivity.Play=>ChaoNeed.Boredom,ChaoActivity.Socialize=>ChaoNeed.SocialNeed,_=>null};
    public IReadOnlyList<ActivityScore> Evaluate(PetState pet,IEnumerable<ActivityAvailability> available)
    {
        var needs=pet.Needs.Evaluate().Priorities.ToDictionary(n=>n.Need);
        var options=available.ToArray();
        var previous=history.LastOrDefault();
        Scores=options.Select(a=>
        {
            if(!a.Available || a.AvailableTargetCount<=0)return new ActivityScore(a,0);
            if(failures.TryGetValue(a.Activity,out var failure) && Now<failure.Until)
                return new ActivityScore(a with{Available=false,Reason=$"Retry at {failure.Until:0.0}s: {failure.Reason}"},0);
            ChaoNeed? need=NeedFor(a.Activity);
            float score=a.Activity switch
            {ChaoActivity.Leisure=>.15f+needs[ChaoNeed.Boredom].Weight*.25f,ChaoActivity.Wander=>.15f,
                _=>(a.Activity==ChaoActivity.Play ? .12f : .10f)+needs[need!.Value].Weight*.75f};
            bool criticalPhysiology=a.Activity is ChaoActivity.Eat or ChaoActivity.Sleep && needs[need!.Value].Level==NeedUrgency.Critical;
            bool alternatives=options.Any(o=>o.Activity!=a.Activity && o.Available && o.AvailableTargetCount>0 && (!failures.TryGetValue(o.Activity,out var f) || f.Until<=Now));
            if(previous?.Activity==a.Activity && !criticalPhysiology && alternatives)score=Math.Max(0,score-.15f);
            return new ActivityScore(a,score);
        }).ToArray();
        return Scores;
    }
    public ChaoActivity? Choose(PetState pet,IEnumerable<ActivityAvailability> available)
    {
        Evaluate(pet,available);
        if(SelectedActivity!=null)return SelectedActivity;
        float best=Scores.Where(s=>s.Availability.Available).Select(s=>s.Score).DefaultIfEmpty(0).Max();
        var tied=Scores.Where(s=>s.Availability.Available && s.Score>0 && Math.Abs(s.Score-best)<.000001f).ToArray();
        if(tied.Length==0)return null;
        var selected=tied[random.Next(tied.Length)].Availability;
        Adopt(selected.Activity,"pending start",selected.Interruptibility);
        return SelectedActivity;
    }
    public void Adopt(ChaoActivity activity,string executor,ActivityInterruptibility interruptibility)
    {
        if(SelectedActivity!=activity)
        {
            SelectedActivity=activity;StartedAt=Now;Selections++;
            MinimumCommitment=activity switch{ChaoActivity.Sleep=>35,ChaoActivity.Leisure=>8,ChaoActivity.Wander=>5,_=>0};
        }
        CurrentExecutor=executor;Interruptibility=interruptibility;
    }
    public bool CanJoinSocial => Interruptibility!=ActivityInterruptibility.Atomic && (SelectedActivity==null || SelectedActivity is ChaoActivity.Wander or ChaoActivity.Leisure &&
        Interruptibility==ActivityInterruptibility.LongOptional && MinimumRemaining<=0);
    public void ProtectTransition(string executor){CurrentExecutor=executor;Interruptibility=ActivityInterruptibility.Atomic;}
    public bool ShouldInterrupt(PetState pet,IEnumerable<ActivityAvailability> available)
    {
        Evaluate(pet,available);
        if(SelectedActivity==null || Interruptibility!=ActivityInterruptibility.LongOptional)return false;
        foreach(var need in pet.Needs.Evaluate().Priorities.Where(n=>n.Need is ChaoNeed.Hunger or ChaoNeed.Fatigue))
        {
            var target=need.Need==ChaoNeed.Hunger ? ChaoActivity.Eat : ChaoActivity.Sleep;
            float current=Scores.FirstOrDefault(s=>s.Availability.Activity==SelectedActivity)?.Score ?? 0;
            if(target==SelectedActivity || !Scores.Any(s=>s.Availability.Activity==target && s.Availability.Available && s.Score>current+.000001f))continue;
            if(need.Level==NeedUrgency.Critical || need.Level==NeedUrgency.High && MinimumRemaining<=0)return true;
        }
        return false;
    }
    public void Interrupt(string reason)
    {LastCancellation=reason;Interruptions++;Clear();}
    public void Complete()
    {
        if(SelectedActivity is not {} a)return;
        history.Enqueue(new(a,Now));while(history.Count>5)history.Dequeue();failures.Remove(a);Clear();
    }
    public void Fail(string reason)
    {
        if(SelectedActivity is not {} a)return;
        var old=failures.GetValueOrDefault(a);int count=old.Reason==reason ? old.Count+1 : 1;
        failures[a]=(reason,count,Now+(count>=3 ? 12 : 4));LastFailureReason=$"{a}: {reason}";Clear();
    }
    public double RetryAt(ChaoActivity a)=>failures.TryGetValue(a,out var f) ? f.Until : 0;
    public void Suspend(string reason){LastCancellation=reason;Clear();}
    private void Clear(){SelectedActivity=null;CurrentExecutor="";MinimumCommitment=0;Interruptibility=ActivityInterruptibility.LongOptional;}
    public string Describe()=>
        $"\nActivity: {SelectedActivity?.ToString() ?? "Idle"}\nExecutor: {CurrentExecutor}\nCommitment left: {MinimumRemaining:0.0}s\n"+
        string.Join('\n',Scores.Select(s=>$"{s.Availability.Activity}: {s.Score:0.00} / {(s.Availability.Available ? "available" : s.Availability.Reason)}"))+
        $"\nLast completed: {history.LastOrDefault()?.Activity.ToString() ?? "None"}\nLast failure: {LastFailureReason}\nCancellation: {LastCancellation}\nSelections {Selections}, interruptions {Interruptions}";
}
