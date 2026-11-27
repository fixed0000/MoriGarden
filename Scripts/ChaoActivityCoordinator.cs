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
public sealed record ActivityScore(ActivityAvailability Availability,float Score)
{
    public float BaseScore { get; init; }
    public float PersonalityAdjustment { get; init; }
    public float HabitAdjustment { get; init; }
    public float RhythmAdjustment { get; init; }
    public float FinalScore { get; init; }
    public float RepeatPenalty { get; init; }
    public float RandomVariation { get; init; }
    public IReadOnlyList<PersonalityContribution> Contributions { get; init; }=Array.Empty<PersonalityContribution>();
}
public sealed record ActivityCompletion(ChaoActivity Activity,double At);

// Selection and ownership only. No Godot dependency, targets, animation, movement,
// memories or relationships. Personality scoring is a separate pure component.
public sealed class ChaoActivityCoordinator
{
    private readonly Random random;
    private ChaoPersonality? cachedPersonality;
    private ChaoPersonalityActivityWeights personalityWeights=new(null);
    public int PersonalityRefreshes { get; private set; }
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
    public int FailedAttempts { get; private set; }
    public int CompletedRepeats { get; private set; }
    public string LastCancellation { get; private set; }="";
    public ChaoActivityCoordinator(Random random) => this.random=random;
    public void Advance(float seconds)
    {if(!float.IsFinite(seconds) || seconds<0)throw new ArgumentException("Invalid activity interval");Now+=seconds;}
    private static ChaoNeed? NeedFor(ChaoActivity a)=>a switch
    {ChaoActivity.Eat=>ChaoNeed.Hunger,ChaoActivity.Sleep=>ChaoNeed.Fatigue,ChaoActivity.Play=>ChaoNeed.Boredom,ChaoActivity.Socialize=>ChaoNeed.SocialNeed,_=>null};
    public IReadOnlyList<ActivityScore> Evaluate(PetState pet,IEnumerable<ActivityAvailability> available,GardenRhythmContext? rhythm=null)
    {
        if(!(cachedPersonality?.Equals(pet.Personality) ?? pet.Personality==null))
        {cachedPersonality=pet.Personality?.Copy();personalityWeights=new(cachedPersonality);PersonalityRefreshes++;}
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
            var personality=personalityWeights.Evaluate(a.Activity,score,need is {} n ? needs[n].Level : NeedUrgency.Low);
            float habit=pet.ActivityHabits.EffectiveBias(a.Activity,pet.History.Seconds);
            float daily=ChaoDailyRhythmScorer.Evaluate(a.Activity,rhythm).Adjustment;
            float beforeRepeat=Math.Clamp(score+personality.Adjustment+habit+daily,0,1);
            float penalty=previous?.Activity==a.Activity && !criticalPhysiology && alternatives ? .15f : 0;
            return new ActivityScore(a,Math.Max(0,beforeRepeat-penalty))
            {BaseScore=score,PersonalityAdjustment=personality.Adjustment,HabitAdjustment=habit,RhythmAdjustment=daily,FinalScore=beforeRepeat,
                RepeatPenalty=penalty,Contributions=personality.Contributions};
        }).ToArray();
        return Scores;
    }
    public ChaoActivity? Choose(PetState pet,IEnumerable<ActivityAvailability> available,GardenRhythmContext? rhythm=null)
    {
        Evaluate(pet,available,rhythm);
        if(SelectedActivity!=null)return SelectedActivity;
        float best=Scores.Where(s=>s.Availability.Available).Select(s=>s.Score).DefaultIfEmpty(0).Max();
        // A critical available physiological need is deterministic; never gamble
        // away starvation/rest to a nearby optional score or Fickle jitter.
        var critical=pet.Needs.Evaluate().Priorities.Where(n=>n.Need is ChaoNeed.Hunger or ChaoNeed.Fatigue && n.Level==NeedUrgency.Critical)
            .OrderByDescending(n=>n.Weight).ThenBy(n=>n.Need).Select(n=>n.Need==ChaoNeed.Hunger ? ChaoActivity.Eat : ChaoActivity.Sleep)
            .Select(a=>Scores.FirstOrDefault(s=>s.Availability.Activity==a && s.Availability.Available && s.Score>0)).FirstOrDefault(s=>s!=null);
        ActivityAvailability selected;
        if(critical!=null)selected=critical.Availability;
        else
        {
            var close=Scores.Where(s=>s.Availability.Available && s.Score>0 && best-s.Score<=.12f+.000001f).ToArray();
            if(close.Length==0)return null;
            if(close.Length==1)selected=close[0].Availability;
            else
            {
                float baseBest=Scores.Where(s=>s.Availability.Available).Max(s=>s.BaseScore);
                close=close.Select(s=>s with{RandomVariation=baseBest-s.BaseScore<=.10f+.000001f && personalityWeights.MaximumVariation>0
                    ? ((float)random.NextDouble()*2-1)*personalityWeights.MaximumVariation : 0}).ToArray();
                Scores=Scores.Select(s=>close.FirstOrDefault(c=>c.Availability.Activity==s.Availability.Activity) ?? s).ToArray();
                float total=close.Sum(s=>Math.Max(.000001f,s.Score+s.RandomVariation)),pick=(float)random.NextDouble()*total;
                selected=close[^1].Availability;
                foreach(var score in close){pick-=Math.Max(.000001f,score.Score+score.RandomVariation);if(pick<=0){selected=score.Availability;break;}}
            }
        }
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
    private static float InterruptionScore(ActivityScore score)=>Math.Max(0,Math.Clamp(score.BaseScore+score.PersonalityAdjustment,0,1)-score.RepeatPenalty);
    public bool ShouldInterrupt(PetState pet,IEnumerable<ActivityAvailability> available,GardenRhythmContext? rhythm=null)
    {
        Evaluate(pet,available,rhythm);
        if(SelectedActivity==null || Interruptibility!=ActivityInterruptibility.LongOptional)return false;
        foreach(var need in pet.Needs.Evaluate().Priorities.Where(n=>n.Need is ChaoNeed.Hunger or ChaoNeed.Fatigue))
        {
            var target=need.Need==ChaoNeed.Hunger ? ChaoActivity.Eat : ChaoActivity.Sleep;
            // Preserve the pre-habit physiological interruption comparison.
            // Habits and rhythms rank the next choice; neither changes a live lease.
            float current=Scores.FirstOrDefault(s=>s.Availability.Activity==SelectedActivity) is {} active ? InterruptionScore(active) : 0;
            if(target==SelectedActivity || !Scores.Any(s=>s.Availability.Activity==target && s.Availability.Available && InterruptionScore(s)>current+.000001f))continue;
            if(need.Level==NeedUrgency.Critical || need.Level==NeedUrgency.High && MinimumRemaining<=0)return true;
        }
        return false;
    }
    public void Interrupt(string reason)
    {LastCancellation=reason;Interruptions++;Clear();}
    public void Complete()
    {
        if(SelectedActivity is not {} a)return;
        if(history.LastOrDefault()?.Activity==a)CompletedRepeats++;
        history.Enqueue(new(a,Now));while(history.Count>5)history.Dequeue();failures.Remove(a);Clear();
    }
    public void Fail(string reason)
    {
        if(SelectedActivity is not {} a)return;
        FailedAttempts++;
        var old=failures.GetValueOrDefault(a);int count=old.Reason==reason ? old.Count+1 : 1;
        failures[a]=(reason,count,Now+(count>=3 ? 12 : 4));LastFailureReason=$"{a}: {reason}";Clear();
    }
    public double RetryAt(ChaoActivity a)=>failures.TryGetValue(a,out var f) ? f.Until : 0;
    public void Suspend(string reason){LastCancellation=reason;Clear();}
    private void Clear(){SelectedActivity=null;CurrentExecutor="";MinimumCommitment=0;Interruptibility=ActivityInterruptibility.LongOptional;}
    public string Describe()=>
        $"\nActivity: {SelectedActivity?.ToString() ?? "Idle"}\nExecutor: {CurrentExecutor}\nCommitment left: {MinimumRemaining:0.0}s\n"+
        string.Join('\n',Scores.Select(s=>$"{s.Availability.Activity}: base {s.BaseScore:0.00}, personality {s.PersonalityAdjustment:+0.00;-0.00;0.00}, habit {s.HabitAdjustment:+0.00;-0.00;0.00}, rhythm {s.RhythmAdjustment:+0.000;-0.000;0.000}, final {s.FinalScore:0.00}, repeat -{s.RepeatPenalty:0.00}, choice {s.Score:0.00}, variation {s.RandomVariation:+0.000;-0.000;0.000}\n  "+
            (s.Availability.Available ? string.Join(", ",s.Contributions.Select(c=>$"{c.Trait}={c.Normalized:0.00}: {c.Contribution:+0.00;-0.00;0.00}")) : s.Availability.Reason)))+
        $"\nLast completed: {history.LastOrDefault()?.Activity.ToString() ?? "None"}\nLast failure: {LastFailureReason}\nCancellation: {LastCancellation}\nSelections {Selections}, interruptions {Interruptions}";
}
