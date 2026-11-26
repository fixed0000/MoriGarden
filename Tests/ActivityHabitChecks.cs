using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class ActivityHabitChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var model=new ChaoActivityHabits();string empty=JsonSerializer.Serialize(model);
        check(ChaoActivityHabits.Supported.All(a=>model.EffectiveBias(a,0)==0) && model.Entries.Count==0,"Habit A: independent empty neutral profile");
        check(model.Credit(ChaoActivity.Play,0,"first") && model.Entries["Play"].Bias==.02f,"Habit B: first real completion gives exactly .02");
        model.Credit(ChaoActivity.Play,120,"second");float second=model.Entries["Play"].Bias;
        check(second>.02f && second<.04f,"Habit C: diminishing gradual growth");
        for(int i=2;i<1100;i++)model.Credit(ChaoActivity.Play,i*120,"play-"+i);
        check(model.Entries["Play"].Bias is >.09f and <=.10f && model.Entries["Play"].ExperienceCount==1000 && model.ProcessedCompletionCount==128,"Habit D: growth/count and transient replay storage are bounded");
        var e=model.Entries["Play"];string stable=JsonSerializer.Serialize(model);
        check(Math.Abs(model.EffectiveBias(ChaoActivity.Play,e.LastUpdateSeconds+24000)-e.Bias/2)<.000001f && stable==JsonSerializer.Serialize(model),"Habit E/L: existing clock half-life halves bias without rewriting records");
        model=new();model.Credit(ChaoActivity.Play,0,"one");
        check(!model.Credit(ChaoActivity.Play,119,"two") && model.Credit(ChaoActivity.Play,120,"three") && model.Entries["Play"].ExperienceCount==2,"Habit F: first immediate, then exact 120-second interval");
        stable=JsonSerializer.Serialize(model);
        check(!model.Credit(ChaoActivity.Play,240,"three") && !model.Credit(ChaoActivity.Socialize,240,"three") && stable==JsonSerializer.Serialize(model),"Habit G: one retained completion identity cannot teach twice or two categories");
        check(!model.Credit(ChaoActivity.Play,240,"two"),"A previously rejected physical completion cannot be replayed later");
        check(model.EffectiveBias(ChaoActivity.Socialize,240)==0 && model.EffectiveBias(ChaoActivity.Leisure,240)==0,"Habit H: category histories remain separate");
        var profiles=Enumerable.Range(0,3).Select(_=>new PetState{Personality=new(),PersonalitySchemaVersion=1}).ToArray();
        for(int i=0;i<3;i++)profiles[i].ActivityHabits.Credit(ChaoActivityHabits.Supported[i],0,"experience");
        check(profiles.Select(p=>p.ActivityHabits).Distinct().Count()==3 && profiles.Select(p=>p.ActivityHabits.Entries.Single().Key).Distinct().Count()==3,"Habit I: three pets learn independently");
        var pet=profiles[0];var innate=pet.Personality!.Copy();
        for(int i=1;i<=1000;i++)pet.ActivityHabits.Credit(ChaoActivity.Play,i*120,"physical-"+i);
        check(pet.Personality.Equals(innate),"Habit J: 1000 accepted outcomes never alter thirteen innate traits");
        bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        foreach(float bias in new[]{-.01f,.11f,float.NaN,float.PositiveInfinity})
            check(Reject(()=>new ChaoActivityHabits{Entries=new(){{"Play",new(){Bias=bias}}}}.Validate()),"Habit K: reject invalid bias "+bias);
        check(Reject(()=>new ChaoActivityHabits{SchemaVersion=2}.Validate()) &&
            Reject(()=>new ChaoActivityHabits{Entries=new(){{"Eat",new()}}}.Validate()) &&
            Reject(()=>new ChaoActivityHabits{Entries=new(){{"play",new()}}}.Validate()) &&
            Reject(()=>new ChaoActivityHabits{Entries=new(){{"Socialize",new(){LastUpdateSeconds=-1}}}}.Validate()) &&
            Reject(()=>new ChaoActivityHabits{Entries=new(){{"Socialize",new(){LastCreditSeconds=double.NaN}}}}.Validate()),"Habit K: unknown keys, future schema and invalid times fail safely");
        model=new();foreach(var a in new[]{ChaoActivity.Eat,ChaoActivity.Sleep,ChaoActivity.Wander})model.EffectiveBias(a,100);
        check(empty==JsonSerializer.Serialize(model) && !model.Credit(ChaoActivity.Eat,100,"food") && model.Entries.Count==0,"Habit L/S: reading never creates records; physiological/fallback categories cannot learn");

        var all=PersonalityDecisionChecks.All();
        PetState Neutral()=>new(){Personality=new(),PersonalitySchemaVersion=1,Fullness=80,Energy=80,Fun=65,SocialNeed=35};
        pet=Neutral();var original=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all).ToArray();
        var weights=new ChaoPersonalityActivityWeights(pet.Personality);var needs=pet.Needs.Evaluate().Priorities.ToDictionary(n=>n.Need);
        check(original.All(s=>s.HabitAdjustment==0 && s.FinalScore==weights.Evaluate(s.Availability.Activity,s.BaseScore,
            s.Availability.Activity switch{ChaoActivity.Eat=>needs[ChaoNeed.Hunger].Level,ChaoActivity.Sleep=>needs[ChaoNeed.Fatigue].Level,ChaoActivity.Play=>needs[ChaoNeed.Boredom].Level,ChaoActivity.Socialize=>needs[ChaoNeed.SocialNeed].Level,_=>NeedUrgency.Low}).FinalScore),"Habit P: zero-habit scores equal the pre-stage formulas exactly");
        void Set(PetState p,ChaoActivity a,float bias)=>p.ActivityHabits.Entries[a.ToString()]=new(){Bias=bias,ExperienceCount=1};
        Set(pet,ChaoActivity.Play,.08f);var after=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all);
        check(Math.Abs(after.Single(s=>s.Availability.Activity==ChaoActivity.Play).FinalScore-original.Single(s=>s.Availability.Activity==ChaoActivity.Play).FinalScore-.08f)<.000001f,"Habit Q: Play adds exactly .08 before repeat and clamp");
        pet=Neutral();Set(pet,ChaoActivity.Socialize,.10f);after=new ChaoActivityCoordinator(new Random(9)).Evaluate(pet,all);
        check(after.All(s=>s.Availability.Activity==ChaoActivity.Socialize ? s.HabitAdjustment==.10f : s.Score==original.Single(o=>o.Availability.Activity==s.Availability.Activity).Score),"Habit R/S: social habit affects only its enabled category");
        var hungry=Neutral();hungry.Fullness=5;Set(hungry,ChaoActivity.Play,.10f);Set(hungry,ChaoActivity.Leisure,.10f);
        var tired=Neutral();tired.Energy=5;Set(tired,ChaoActivity.Socialize,.10f);
        check(Enumerable.Range(0,100).All(seed=>new ChaoActivityCoordinator(new Random(seed)).Choose(hungry,all)==ChaoActivity.Eat && new ChaoActivityCoordinator(new Random(seed)).Choose(tired,all)==ChaoActivity.Sleep),"Habit T: critical hunger/fatigue remain deterministic across seeds");
        var threshold=new PetState{Fullness=80,Energy=80,Fun=0,SocialNeed=20,Personality=new(){Curious=100,Energetic=100,Carefree=100,Vitality=100},PersonalitySchemaVersion=1};
        threshold.Needs.Advance(6000,false);threshold.History.Advance(6000);threshold.Energy=25;Set(threshold,ChaoActivity.Leisure,.10f);
        threshold.ActivityHabits.Entries["Leisure"].LastUpdateSeconds=threshold.ActivityHabits.Entries["Leisure"].LastCreditSeconds=threshold.History.Seconds;
        var optional=new ChaoActivityCoordinator(new Random(1));optional.Adopt(ChaoActivity.Leisure,"real optional outing",ActivityInterruptibility.LongOptional);
        check(optional.ShouldInterrupt(threshold,all),"Habit T: maximum Leisure habit cannot delay the old interruption at the Critical fatigue threshold");
        var bored=Neutral();bored.Fun=10;Set(bored,ChaoActivity.Leisure,.10f);Set(bored,ChaoActivity.Socialize,.10f);
        check(Enumerable.Range(0,100).All(seed=>new ChaoActivityCoordinator(new Random(seed)).Choose(bored,all)==ChaoActivity.Play),"Strong boredom still favors need-based Play over weak competing habits");
        var unavailable=all.Select(a=>a.Activity==ChaoActivity.Socialize ? a with{Available=false,AvailableTargetCount=0} : a).ToArray();
        check(new ChaoActivityCoordinator(new Random(1)).Evaluate(pet,unavailable).Single(s=>s.Availability.Activity==ChaoActivity.Socialize).Score==0,"Habit U: unavailable favorite remains excluded");
        var coordinator=new ChaoActivityCoordinator(new Random(1));coordinator.Adopt(ChaoActivity.Play,"actual bite/grip",ActivityInterruptibility.Atomic);
        check(!coordinator.ShouldInterrupt(tired,all),"Habit V: protected atomic execution remains authoritative");
        coordinator=new(new Random(1));coordinator.Adopt(ChaoActivity.Socialize,"completed ensemble",ActivityInterruptibility.Atomic);coordinator.Complete();
        check(coordinator.Evaluate(pet,all).Single(s=>s.Availability.Activity==ChaoActivity.Socialize).RepeatPenalty==.15f,"Habit W: existing immediate-repeat penalty retained");
        check(new ChaoActivityCoordinator(new Random(74)).Choose(pet,all)==new ChaoActivityCoordinator(new Random(74)).Choose(pet,all),"Habit X: equal seed and state reproduce the category choice");
        var fixture=Enumerable.Range(0,4).Select(_=>new PetState{Personality=new(),PersonalitySchemaVersion=1,Fullness=90,Energy=90,Fun=100-100*.03f/.38f,SocialNeed=100*.065f/.57f}).ToArray();
        for(int i=0;i<3;i++)Set(fixture[i+1],ChaoActivityHabits.Supported[i],.10f);
        var options=all.Where(a=>ChaoActivityHabits.Supported.Contains(a.Activity)).ToArray();int[][] counts=fixture.Select(_=>new int[3]).ToArray();const int trials=20000;
        for(int seed=0;seed<trials;seed++)for(int i=0;i<4;i++)
        {var selected=new ChaoActivityCoordinator(new Random(seed+7311)).Choose(fixture[i],options)!.Value;counts[i][Array.IndexOf(ChaoActivityHabits.Supported.ToArray(),selected)]++;}
        Console.WriteLine("HABIT_DISTRIBUTION: trials=20000; rows=neutral/play/social/leisure; columns=play/social/leisure; "+JsonSerializer.Serialize(counts));
        check(Enumerable.Range(0,3).All(i=>counts[i+1][i]>counts[0][i]+1000 && counts[i+1].All(n=>n>1000)),"Habit Y: 20000 independent matched decisions per profile show increased trained choices without monopoly");

        string folder=Path.Combine(Path.GetTempPath(),"MoriHabits-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"pet.json");pet=Neutral();pet.GardenEggs.Clear();pet.GardenEggs.Add(new(){Id="original-peer",Hatched=true,Chao=Neutral()});
            pet.ActivityHabits.Credit(ChaoActivity.Play,0,"real-one");pet.GardenEggs[0].Chao!.ActivityHabits.Credit(ChaoActivity.Leisure,0,"real-two");SaveIntegrity.Validate(pet);pet.EnsureStatisticsTree();
            stable=JsonSerializer.Serialize(pet);SaveStore.Save(file,pet);var read=SaveStore.Load(file,out _);SaveStore.Save(file,read);
            check(stable==JsonSerializer.Serialize(read) && read.ActivityHabits.Entries["Play"].Bias==.02f && read.GardenEggs[0].Id=="original-peer","Habit M: complete canonical garden round-trip retains all data and independent habits");
            check(!read.ActivityHabits.Credit(ChaoActivity.Play,119,"new-after-load") && read.ActivityHabits.Entries["Play"].ExperienceCount==1,"Saved credit interval prevents immediate reload farming");
            var roster=ExpeditionRoster.Available(read).ToDictionary(p=>p.Id,p=>p.State);var bridge=new ChaoEditorBridge(folder);bridge.Poll(roster,true);
            var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"chao-editor-state.json")))!;string habits=JsonSerializer.Serialize(read.ActivityHabits);
            File.WriteAllText(Path.Combine(folder,"chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot["session"]!.GetValue<string>(),id="edit",target="main",patch=new{Fullness=70},statistics=new{Run=new{Progress=1}}}));
            var edited=bridge.Poll(roster,true)!;
            check(edited!=null && JsonSerializer.Serialize(edited.ActivityHabits)==habits && !edited.ActivityHabits.Credit(ChaoActivity.Play,240,"new-after-load"),"Habit O: editor preserves exact records and transient rejected-ID ledger");
            var legacy=JsonNode.Parse(stable)!;legacy.AsObject().Remove("ActivityHabits");legacy["GardenEggs"]![0]!["Chao"]!.AsObject().Remove("ActivityHabits");File.WriteAllText(file,legacy.ToJsonString());
            read=SaveStore.Load(file,out _);check(read.ActivityHabits.Entries.Count==0 && read.GardenEggs[0].Chao!.ActivityHabits.Entries.Count==0,"Habit N: old root and resident gain neutral independent records");
            var future=JsonNode.Parse(stable)!;future["ActivityHabits"]!["SchemaVersion"]=999;future["ActivityHabits"]!["Entries"]!["unknown-future"]=new JsonObject();string bad=future.ToJsonString();File.WriteAllText(file,bad);
            bool failed=false;try{SaveStore.Load(file,out _);}catch(SaveLoadException){failed=true;}
            check(failed && SaveStore.LoadSlot(file)==null && File.ReadAllText(file)==bad,"Future version refuses fallback and preserves primary even with a readable old backup");
            future=JsonNode.Parse(stable)!;future["ActivityHabits"]!["Entries"]!["Play"]!["Bias"]=.20;bad=future.ToJsonString();File.WriteAllText(file,bad);
            bool overwrite=false;try{SaveStore.Save(file,pet);}catch(SaveLoadException){overwrite=true;}
            check(overwrite && File.ReadAllText(file)==bad,"Corrupt habit values cannot be overwritten by another garden");
        }
        finally{Directory.Delete(folder,true);}
    }
}
