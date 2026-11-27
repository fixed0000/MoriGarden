using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class DailyRhythmChecks
{
    internal static void Run(Action<bool,string> check)
    {
        bool Near(double a,double b,double e=.000002)=>Math.Abs(a-b)<e;
        GardenRhythmContext Context(double t)=>new(t,"garden","main",true);
        var all=PersonalityDecisionChecks.All();
        var pet=new PetState{Fullness=80,Energy=70,Fun=40,SocialNeed=50,Personality=new(),PersonalitySchemaVersion=1};
        pet.EnsureStatistics();string before=JsonSerializer.Serialize(pet);
        check(Enumerable.Range(0,600).All(t=>GardenDayCycle.Evaluate(t)==GardenDayCycle.Evaluate(t+600)),"Rhythm A: 600-second cycle");
        check(GardenDayCycle.GetPhase(60)==GardenDayPhase.Day && GardenDayCycle.GetPhase(265)==GardenDayPhase.Dusk && GardenDayCycle.GetPhase(360)==GardenDayPhase.Night && GardenDayCycle.GetPhase(455)==GardenDayPhase.Dawn,"Rhythm B: existing solar clock alignment +145, 410/190");
        float PreviousLight(double t){float c=(float)GardenDayCycle.GetCyclePosition(t),f=c<410 ? .5f*c/410 : .5f+.5f*(c-410)/190;float x=Math.Clamp((MathF.Sin(MathF.Tau*f)+.07f)/.16f,0,1);return x*x*(3-2*x);}
        check(Enumerable.Range(0,60000).All(i=>Near(GardenDayCycle.Evaluate(i*.01).DaylightIntensity,PreviousLight(i*.01))),"Rhythm B: dense numerical comparison with original sky formula");
        check(Near(GardenDayCycle.Evaluate(454.99999).DaylightIntensity,GardenDayCycle.Evaluate(455).DaylightIntensity),"Rhythm C: seamless wrap lighting");
        check(new[]{20,390,430,580,0}.All(c=>Enum.GetValues<ChaoActivity>().All(a=>Near(ChaoDailyRhythmScorer.Evaluate(a,Context(c-145-.0001)).Adjustment,ChaoDailyRhythmScorer.Evaluate(a,Context(c-145+.0001)).Adjustment,.00001))),"Rhythm C/D: phase boundaries and wrap have no score jumps");
        check(new ChaoActivityCoordinator(new Random(1)).Evaluate(pet,all).All(s=>s.RhythmAdjustment==0) && Enum.GetValues<ChaoActivity>().All(a=>ChaoDailyRhythmScorer.Evaluate(a,null).Adjustment==0),"Rhythm E: neutral absent context");
        check(new[]{"stadium","plaza",WorldCatalog.Meadow,WorldCatalog.Greenhouse}.All(area=>Enum.GetValues<ChaoActivity>().All(a=>ChaoDailyRhythmScorer.Evaluate(a,Context(360) with{AreaId=area}).Adjustment==0)),"Rhythm: no expedition/plaza/stadium changes");
        check(Enumerable.Range(0,600).All(t=>new[]{ChaoActivity.Eat,ChaoActivity.Wander}.All(a=>ChaoDailyRhythmScorer.Evaluate(a,Context(t)).Adjustment==0)),"Rhythm F: Eat and Wander isolated");
        float Score(ChaoActivity a,double t)=>ChaoDailyRhythmScorer.Evaluate(a,Context(t)).Adjustment;
        check(Near(Score(ChaoActivity.Play,60),.05) && Near(Score(ChaoActivity.Socialize,60),.03) && Near(Score(ChaoActivity.Leisure,60),.035),"Rhythm G: exact day adjustments");
        check(Near(Score(ChaoActivity.Sleep,360),.08) && Near(Score(ChaoActivity.Play,360),-.025) && Near(Score(ChaoActivity.Leisure,360),-.025),"Rhythm H: exact night adjustments");
        check(Enumerable.Range(0,6000).All(t=>Enum.GetValues<ChaoActivity>().All(a=>float.IsFinite(Score(a,t*.1)) && Score(a,t*.1) is >=-.05f and <=.08f)),"Rhythm I: finite bounded adjustments");
        check(Near(GardenRhythmContext.OffsetFor("main"),2.4372563377109486,1e-12) && GardenRhythmContext.OffsetFor("main")!=GardenRhythmContext.OffsetFor("white") && Enumerable.Range(0,1000).All(i=>GardenRhythmContext.OffsetFor("pet-"+i) is >=-20 and <=20),"Rhythm J/K: stable independent bounded FNV identity offsets");
        check(before==JsonSerializer.Serialize(pet),"Rhythm L: pure evaluation never mutates PetState");
        var eligibility=ChaoDailyRhythmScorer.RestEligibility;
        check(eligibility(70,Context(360)).Allowed && !eligibility(70,Context(60)).Allowed && !eligibility(100,Context(360)).Allowed && eligibility(5,Context(60)).Allowed,"Rhythm T/U/V: mild night fatigue allowed, daytime/full energy blocked, critical fatigue always available");
        check(Enumerable.Range(-20,41).All(offset=>eligibility(70,Context(60) with{PersonalPhaseOffset=offset}).Allowed==false),"Rhythm: personal preference cannot invent night rest in actual daylight");
        var old=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all).ToArray();
        var current=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all,Context(360));
        check(current.All(s=>Near(s.RhythmAdjustment,Score(s.Availability.Activity,360)) && Near(s.FinalScore,Math.Clamp(s.BaseScore+s.PersonalityAdjustment+s.HabitAdjustment+s.RhythmAdjustment,0,1))),"Rhythm O: exact additional score contribution");
        pet.ActivityHabits.Entries["Play"]=new(){Bias=.1f,ExperienceCount=1};
        old=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all).ToArray();current=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all,Context(360));
        check(current.All(s=>s.HabitAdjustment==old.Single(o=>o.Availability.Activity==s.Availability.Activity).HabitAdjustment && s.PersonalityAdjustment==old.Single(o=>o.Availability.Activity==s.Availability.Activity).PersonalityAdjustment),"Rhythm P/Q: habit and personality formulas preserved");
        var hungry=SaveStore.Snapshot(pet);hungry.Fullness=5;var tired=SaveStore.Snapshot(pet);tired.Energy=5;
        check(Enumerable.Range(0,100).All(seed=>new ChaoActivityCoordinator(new Random(seed)).Choose(hungry,all,Context(360))==ChaoActivity.Eat && new ChaoActivityCoordinator(new Random(seed)).Choose(tired,all,Context(60))==ChaoActivity.Sleep),"Rhythm R/S: critical physiology overrides optional preferences");
        var occupied=all.Select(a=>a.Activity==ChaoActivity.Sleep ? a with{Available=false,AvailableTargetCount=0}:a).ToArray();
        check(Enumerable.Range(0,100).All(seed=>new ChaoActivityCoordinator(new Random(seed)).Choose(pet,occupied,Context(360))!=ChaoActivity.Sleep),"Rhythm W: unavailable reserved bed remains unavailable");
        var co=new ChaoActivityCoordinator(new Random(2));co.Adopt(ChaoActivity.Play,"actual atomic",ActivityInterruptibility.Atomic);
        check(!co.ShouldInterrupt(tired,all,Context(360)),"Rhythm X: atomic action not interrupted by phase change");
        co=new(new Random(2));co.Adopt(ChaoActivity.Wander,"outing",ActivityInterruptibility.LongOptional);co.Advance(10);
        check(!co.ShouldInterrupt(pet,all,Context(360)) && co.ShouldInterrupt(tired,all,Context(60)),"Rhythm Y/Z: physiological interruption retained, night alone never interrupts");
        before=JsonSerializer.Serialize(pet);for(int i=0;i<600;i++)new ChaoActivityCoordinator(new Random(i)).Evaluate(pet,all,Context(i));
        check(before==JsonSerializer.Serialize(pet),"Rhythm: time passage scoring cannot teach habits or mutate personality/favorites/relationships");
        var dir=Path.Combine(Path.GetTempPath(),"MoriDailyRhythm-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            pet.GardenSeconds=360;SaveStore.Save(Path.Combine(dir,"pet.json"),pet);var loaded=SaveStore.Load(Path.Combine(dir,"pet.json"),out _);
            check(loaded.GardenSeconds==360 && GardenDayCycle.Evaluate(loaded.GardenSeconds)==GardenDayCycle.Evaluate(pet.GardenSeconds),"Rhythm N: save/load preserves solar phase without new schema");
            var bridge=new ChaoEditorBridge(dir);var roster=new System.Collections.Generic.Dictionary<string,PetState>{{"main",loaded}};bridge.Poll(roster,true,Context(360));
            var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"chao-editor-state.json")))!;
            File.WriteAllText(Path.Combine(dir,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="rhythm-editor",target="main",patch=new{Fullness=77}}));
            before=JsonSerializer.Serialize(loaded.ActivityHabits);var edited=bridge.Poll(roster,true,Context(360))!;
            check(edited.GardenSeconds==loaded.GardenSeconds && before==JsonSerializer.Serialize(edited.ActivityHabits) && edited.Personality!.Equals(loaded.Personality),"Rhythm: actual editor patch preserves clock, habits, innate personality");
        }
        finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
        Statistics(check);
    }
    private static void Statistics(Action<bool,string> check)
    {
        var results=new System.Collections.Generic.List<object>();
        var counts=new System.Collections.Generic.Dictionary<string,int[]>();
        foreach(string profile in new[]{"Neutral","Play","Socialize","Leisure"})
        foreach(var phase in new[]{("Day",60d),("Dusk",265d),("Night",360d)})
        {
            var p=new PetState{Fullness=80,Energy=50,Fun=42.105263f,SocialNeed=61.40351f,Joy=70,PersonalitySchemaVersion=1,
                Personality=new(){Curious=100,Energetic=-100,Carefree=100,Vitality=100}};
            if(profile!="Neutral")p.ActivityHabits.Entries[profile]=new(){Bias=.1f,ExperienceCount=1};
            var available=PersonalityDecisionChecks.All().Select(a=>a.Activity is ChaoActivity.Eat or ChaoActivity.Wander ? a with{Available=false,AvailableTargetCount=0}:a).ToArray();
            var c=new int[6];for(int seed=0;seed<20000;seed++){var chosen=new ChaoActivityCoordinator(new Random(seed)).Choose(p,available,new(phase.Item2,"garden","main",true));if(chosen is {} a)c[(int)a]++;}
            counts[profile+"/"+phase.Item1]=c;results.Add(new{profile,phase=phase.Item1,trials=20000,counts=c});
            check(c.Sum()==20000 && c[0]==0 && c[5]==0 && c.Count(n=>n>0)>1,"Rhythm statistics: independent 20000 / "+profile+" / "+phase.Item1+"; availability and non-monopoly");
        }
        check(counts["Neutral/Day"][2]>counts["Neutral/Night"][2] && counts["Neutral/Night"][1]>counts["Neutral/Day"][1],"Rhythm statistics: day Play and night Sleep gain relative preference");
        foreach(var (name,index) in new[]{("Play",2),("Socialize",3),("Leisure",4)})
            check(new[]{"Day","Dusk","Night"}.All(phase=>counts[name+"/"+phase][index]>counts["Neutral/"+phase][index]),"Rhythm statistics: "+name+" habit remains influential across all phases");
        string? output=Environment.GetEnvironmentVariable("MORI_RHYTHM_RESULTS");if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
    }
}
