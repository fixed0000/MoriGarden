using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Mori;

internal static class PersonalityDecisionChecks
{
    internal static ActivityAvailability[] All()=>Enum.GetValues<ChaoActivity>().Select(a=>new ActivityAvailability(a,true,"Available",35,
        a is ChaoActivity.Leisure or ChaoActivity.Wander ? ActivityInterruptibility.LongOptional : ActivityInterruptibility.Atomic,1)).ToArray();
    internal static ChaoPersonality Profile(string name)=>name switch
    {
        "Explorer"=>new(){Curious=90,Energetic=65,Carefree=30,Chatty=-30,Solitude=40},
        "Social"=>new(){Chatty=90,Solitude=-70,Curious=20,Energetic=40},
        "Playful"=>new(){Energetic=90,Carefree=85,Curious=20},
        "Quiet"=>new(){Curious=-70,Energetic=-65,Chatty=-70,Solitude=80,Carefree=-30},_=>new()
    };
    private static PetState Pet(ChaoPersonality p)
    {var pet=new PetState{Personality=p,PersonalitySchemaVersion=1,Fullness=80,Energy=80,Fun=65,SocialNeed=35};pet.EnsureStatistics();return pet;}
    internal static void Run(Action<bool,string> check)
    {
        var properties=typeof(ChaoPersonality).GetProperties();
        var normalized=typeof(NormalizedChaoPersonality).GetProperties().Where(p=>p.PropertyType==typeof(float)).ToArray();
        check(properties.Length==13 && normalized.Length==13 && properties.All(p=>
        {var input=new ChaoPersonality();p.SetValue(input,-100);var n=new ChaoPersonalityActivityWeights(input).Traits;
            return (float)normalized.Single(x=>x.Name==p.Name).GetValue(n)! == -1;}),"Personality decisions: all thirteen scales normalize to signed units");
        check(properties.All(p=>{var input=new ChaoPersonality();p.SetValue(input,100);return (float)normalized.Single(x=>x.Name==p.Name).GetValue(new ChaoPersonalityActivityWeights(input).Traits)! == 1;}) &&
            normalized.All(p=>(float)p.GetValue(new ChaoPersonalityActivityWeights(null).Traits)! == 0),"Personality decisions: positive extremes and neutral missing snapshots normalize without migration");
        var max=new ChaoPersonality{Curious=100,Energetic=100,Carefree=100,Vitality=100};
        var min=new ChaoPersonality{Curious=-100,Energetic=-100,Carefree=-100,Vitality=-100};
        check(new ChaoPersonalityActivityWeights(max).Evaluate(ChaoActivity.Leisure,.5f).Adjustment==.2f && new ChaoPersonalityActivityWeights(min).Evaluate(ChaoActivity.Leisure,.5f).Adjustment==-.2f,"Personality decisions: stacked preferences clamp at plus/minus .20");
        var unused=new ChaoPersonality{Kindness=100,Naive=-100,Regain=100,Skillful=-100,Charm=100};
        check(Enum.GetValues<ChaoActivity>().All(a=>new ChaoPersonalityActivityWeights(unused).Evaluate(a,.3f).Adjustment==0),"Personality decisions: five deferred traits have no category effect");
        var appetite=new ChaoPersonalityActivityWeights(new(){BigEater=-100});
        check(appetite.Evaluate(ChaoActivity.Eat,.6f,NeedUrgency.Critical).Adjustment==0 && appetite.Evaluate(ChaoActivity.Eat,.6f,NeedUrgency.High).Adjustment==-.05f && Math.Abs(appetite.Evaluate(ChaoActivity.Eat,.3f).Adjustment+.06f)<.00001f,"Personality decisions: critical/high physiological guards limit negative preferences");
        var state=Pet(Profile("Explorer"));state.EnsurePersonality();string before=JsonSerializer.Serialize(state);
        var coordinator=new ChaoActivityCoordinator(new Random(19));var scores=coordinator.Evaluate(state,All()).Select(s=>s.FinalScore).ToArray();
        for(int i=0;i<1000;i++)coordinator.Evaluate(state,All());
        check(before==JsonSerializer.Serialize(state) && scores.SequenceEqual(coordinator.Evaluate(state,All()).Select(s=>s.FinalScore)) && coordinator.PersonalityRefreshes==1,"Personality decisions: repeated evaluation neither mutates data nor recomputes unchanged traits");
        state.Personality!.Curious=80;coordinator.Evaluate(state,All());
        check(coordinator.PersonalityRefreshes==2,"Personality decisions: editor trait replacement invalidates the normalized snapshot");
        var starving=Pet(new(){Curious=100,Carefree=100,BigEater=-100});starving.Fullness=5;
        var tired=Pet(new(){Energetic=100,Curious=100,Carefree=100});tired.Energy=5;
        check(Enumerable.Range(0,1000).All(i=>new ChaoActivityCoordinator(new Random(i)).Choose(starving,All())==ChaoActivity.Eat),"Personality decisions: critical hunger always wins despite exploratory preferences");
        check(Enumerable.Range(0,1000).All(i=>new ChaoActivityCoordinator(new Random(i)).Choose(tired,All())==ChaoActivity.Sleep),"Personality decisions: critical fatigue always selects available rest");
        starving.Energy=1;
        check(new ChaoActivityCoordinator(new Random(1)).Choose(starving,All())==ChaoActivity.Sleep,"Personality decisions: simultaneous critical physiology follows the more urgent need");
        var full=Pet(new(){BigEater=100});full.Fullness=100;var noFood=All().Select(a=>a.Activity==ChaoActivity.Eat ? a with{Available=false,AvailableTargetCount=0}:a).ToArray();
        check(Enumerable.Range(0,100).All(i=>new ChaoActivityCoordinator(new Random(i)).Choose(full,noFood)!=ChaoActivity.Eat),"Personality decisions: appetite never overrides executor availability");
        var lonely=Pet(new(){Solitude=100});lonely.SocialNeed=95;
        check(Enumerable.Range(0,100).All(i=>new ChaoActivityCoordinator(new Random(i)).Choose(lonely,All())==ChaoActivity.Socialize),"Personality decisions: solitude does not veto critical social need");
        var fickle=Pet(new(){Fickle=100});var options=All();
        var a1=new ChaoActivityCoordinator(new Random(152));var a2=new ChaoActivityCoordinator(new Random(152));bool deterministic=true,bounded=true;
        for(int i=0;i<1000;i++)
        {deterministic &= a1.Choose(fickle,options)==a2.Choose(fickle,options);float bestBase=a1.Scores.Where(s=>s.Availability.Available).Max(s=>s.BaseScore);
            bounded &= a1.Scores.All(s=>Math.Abs(s.RandomVariation)<=.030001f && (s.RandomVariation==0 || bestBase-s.BaseScore<=.100001f));a1.Complete();a2.Complete();}
        check(deterministic && bounded,"Personality decisions: per-coordinator random sequence is reproducible and Fickle stays within its close-base window");
        var negativeFickle=new ChaoPersonalityActivityWeights(new(){Fickle=-100});
        check(negativeFickle.MaximumVariation==0,"Personality decisions: negative Fickle adds no jitter but preserves weighted close choices");
        var neutral=Pet(new(){Fickle=-100});var neutralChoices=All().Where(a=>a.Activity is ChaoActivity.Leisure or ChaoActivity.Wander).ToArray();neutral.Fun=100;
        check(Enumerable.Range(0,100).Select(i=>new ChaoActivityCoordinator(new Random(i)).Choose(neutral,neutralChoices)).Distinct().Count()==2,"Personality decisions: a negative Fickle value does not force a single predictable intention");
        var counts=new Dictionary<string,int[]>();
        // 2000 independent decisions per profile; identical meters, candidates and
        // seed set. No previous completed history can bias this comparison.
        foreach(string name in new[]{"Explorer","Social","Playful","Quiet"})
        {
            state=Pet(Profile(name));before=JsonSerializer.Serialize(state);int[] distribution=new int[6];
            var common=All().Select(a=>a.Activity==ChaoActivity.Sleep ? a with{Available=false,AvailableTargetCount=0}:a).ToArray();
            for(int i=0;i<2000;i++)distribution[(int)new ChaoActivityCoordinator(new Random(i+4100)).Choose(state,common)!]++;
            counts[name]=distribution;
            check(before==JsonSerializer.Serialize(state) && distribution.Max()<1800 && distribution.Count(c=>c>0)>=2,$"Personality decisions: {name} keeps innate data and does not monopolize one action");
            Console.WriteLine("PERSONALITY_DISTRIBUTION: "+name+" "+string.Join(", ",Enum.GetValues<ChaoActivity>().Select(a=>$"{a}={distribution[(int)a]}")));
        }
        check(counts["Explorer"][4]>counts["Quiet"][4] && counts["Social"][3]>counts["Explorer"][3] && counts["Playful"][2]>counts["Social"][2],"Personality decisions: Explorer/Social/Playful/Quiet have distinguishable distributions under identical conditions");
        state=Pet(Profile("Playful"));var atomic=new ChaoActivityCoordinator(new Random(1));atomic.Adopt(ChaoActivity.Play,"held toy",ActivityInterruptibility.Atomic);state.Energy=1;
        check(!atomic.ShouldInterrupt(state,All()) && atomic.Choose(state,All())==ChaoActivity.Play,"Personality decisions: weighted selection cannot replace an atomic executor");
        atomic=new(new Random(1));atomic.Adopt(ChaoActivity.Leisure,"route",ActivityInterruptibility.LongOptional);
        check(atomic.ShouldInterrupt(state,All()),"Personality decisions: extreme active traits do not block critical optional interruption");
        var independent=Enumerable.Range(0,3).Select(i=>new ChaoActivityCoordinator(new Random(i))).ToArray();
        independent[0].Adopt(ChaoActivity.Play,"toy",ActivityInterruptibility.Atomic);independent[0].Fail("occupied");independent[1].Adopt(ChaoActivity.Wander,"walk",ActivityInterruptibility.LongOptional);independent[1].Complete();
        check(independent[0].RetryAt(ChaoActivity.Play)==4 && independent[2].RetryAt(ChaoActivity.Play)==0 && independent[1].History.Count==1 && independent[2].History.Count==0,"Personality decisions: three pets retain separate cooldowns/history/random state");
        state=Pet(Profile("Explorer"));state.GardenActivity=independent[1];before=JsonSerializer.Serialize(state);var loaded=JsonSerializer.Deserialize<PetState>(before)!;var twice=JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(loaded))!;
        check(before==JsonSerializer.Serialize(twice) && twice.GardenActivity==null,"Personality decisions: load/save/load preserves permanent data without temporary weights");
    }
}
