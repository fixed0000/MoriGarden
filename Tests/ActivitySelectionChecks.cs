using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mori;

internal static class ActivitySelectionChecks
{
    private static ActivityAvailability[] All()=>Enum.GetValues<ChaoActivity>().Select(a=>new ActivityAvailability(a,true,"Available",35,a is ChaoActivity.Leisure or ChaoActivity.Wander ? ActivityInterruptibility.LongOptional : ActivityInterruptibility.Atomic,1)).ToArray();
    private static PetState Normal()=>new(){Fullness=80,Energy=80,Fun=80,SocialNeed=20};
    internal static void Run(Action<bool,string> check)
    {
        foreach(var (label,expected,p) in new[]{("A",ChaoActivity.Eat,new PetState{Fullness=10,Energy=80,Fun=80,SocialNeed=20}),
            ("B",ChaoActivity.Sleep,new PetState{Fullness=80,Energy=5,Fun=80,SocialNeed=20}),
            ("D",ChaoActivity.Play,new PetState{Fullness=80,Energy=80,Fun=5,SocialNeed=20}),
            ("E",ChaoActivity.Socialize,new PetState{Fullness=80,Energy=80,Fun=80,SocialNeed=90})})
            check(new ChaoActivityCoordinator(new Random(3)).Choose(p,All())==expected,$"Activity {label}: the dominant current need selects {expected}");
        var p1=Normal();p1.Fullness=5;var choices=All().Select(a=>a.Activity==ChaoActivity.Eat ? a with{Available=false,AvailableTargetCount=0,Reason="No food"}:a).ToArray();
        var chooser=new ChaoActivityCoordinator(new Random(7));
        check(chooser.Choose(p1,choices)!=ChaoActivity.Eat && chooser.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Eat).Score==0,"Activity C: missing food is excluded before selection");
        p1=Normal();p1.SocialNeed=95;choices=All().Select(a=>a.Activity==ChaoActivity.Socialize?a with{Available=false,AvailableTargetCount=0}:a).ToArray();
        check(new ChaoActivityCoordinator(new Random(7)).Choose(p1,choices)!=ChaoActivity.Socialize,"Activity F: absent partners cannot produce a social intention");
        chooser=new(new Random(5));chooser.Adopt(ChaoActivity.Eat,"FruitMeal",ActivityInterruptibility.Atomic);p1=Normal();p1.Energy=1;
        check(!chooser.ShouldInterrupt(p1,All()) && chooser.SelectedActivity==ChaoActivity.Eat,"Activity G: even critical fatigue cannot cancel an atomic bite");
        chooser=new(new Random(5));chooser.Adopt(ChaoActivity.Wander,"ChaoWander",ActivityInterruptibility.LongOptional);
        check(chooser.ShouldInterrupt(p1,All()),"Activity H: critical fatigue can interrupt a long optional walk before five seconds");
        chooser.Interrupt("Critical fatigue");check(chooser.Choose(p1,All())==ChaoActivity.Sleep,"Interrupted walking selects available sleep");
        chooser=new(new Random(5));chooser.Adopt(ChaoActivity.Leisure,"ChaoLeisure",ActivityInterruptibility.LongOptional);p1.Energy=40;
        check(!chooser.ShouldInterrupt(p1,All()),"High fatigue respects the leisure commitment");chooser.Advance(8);
        check(chooser.ShouldInterrupt(p1,All()),"High fatigue can interrupt optional leisure after eight seconds");
        chooser=new(new Random(5));chooser.Adopt(ChaoActivity.Leisure,"brief observation",ActivityInterruptibility.ShortSafe);p1.Energy=1;
        check(!chooser.ShouldInterrupt(p1,All()),"Short safe observation finishes even when a need becomes critical");
        chooser=new(new Random(5));p1=Normal();p1.Fullness=10;p1.Energy=40;chooser.Adopt(ChaoActivity.Eat,"food approach",ActivityInterruptibility.LongOptional);
        check(!chooser.ShouldInterrupt(p1,All()),"A lower-scoring urgency cannot restart the same food approach every frame");
        choices=All();chooser=new(new Random(5));chooser.Adopt(ChaoActivity.Sleep,"bed approach",ActivityInterruptibility.LongOptional);chooser.Fail("Bed occupied");
        check(chooser.RetryAt(ChaoActivity.Sleep)==4 && chooser.Choose(p1,choices)!=ChaoActivity.Sleep,"Activity I: an occupied bed cools only Sleep and leaves alternatives enabled");
        chooser.Suspend("test");chooser.Advance(4);chooser.Adopt(ChaoActivity.Sleep,"bed",ActivityInterruptibility.LongOptional);chooser.Fail("Bed occupied");chooser.Advance(4);chooser.Adopt(ChaoActivity.Sleep,"bed",ActivityInterruptibility.LongOptional);chooser.Fail("Bed occupied");
        check(chooser.RetryAt(ChaoActivity.Sleep)==chooser.Now+12,"Three identical failed starts lengthen that category's retry to twelve seconds");
        chooser.Advance(12);chooser.Adopt(ChaoActivity.Sleep,"sleep",ActivityInterruptibility.Atomic);chooser.Complete();
        check(chooser.RetryAt(ChaoActivity.Sleep)==0,"A successfully completed action clears its failure counter");
        var coordinators=Enumerable.Range(0,3).Select(i=>new ChaoActivityCoordinator(new Random(i))).ToArray();
        var pets=new[]{new PetState{Fullness=5,Energy=80,Fun=80},new PetState{Fullness=80,Energy=80,Fun=5},new PetState{Fullness=80,Energy=5,Fun=80}};
        check(coordinators.Select((c,i)=>c.Choose(pets[i],All())).SequenceEqual(new ChaoActivity?[]{ChaoActivity.Eat,ChaoActivity.Play,ChaoActivity.Sleep}),"Activity J: three pets choose independent Eat, Play and Sleep intentions");
        p1=Normal();p1.SocialNeed=80;p1.ObserveEvent(ChaoStateEvent.PositiveSocial,"same-source","contact-one");
        bool second=p1.ObserveEvent(ChaoStateEvent.PositiveSocial,"same-source","contact-two");
        for(int i=0;i<1000;i++)p1.ObserveEvent(ChaoStateEvent.PositiveSocial,"same-source","contact-two");
        check(second && p1.SocialNeed==64 && p1.Mood.ActiveReactions.Count==1,"Activity K: two actual contact IDs relieve twice, one repeated ID once, despite emotion cooldown");
        var traitsA=Normal();traitsA.Curiosity=-100;traitsA.Kindness=-100;traitsA.Initiative=0;traitsA.Sociability=0;
        var traitsB=Normal();traitsB.Curiosity=100;traitsB.Kindness=100;traitsB.Initiative=990;traitsB.Sociability=100;
        check(new ChaoActivityCoordinator(new Random(1)).Evaluate(traitsA,All()).Select(s=>s.Score).SequenceEqual(new ChaoActivityCoordinator(new Random(1)).Evaluate(traitsB,All()).Select(s=>s.Score)),"Stage 1.4A category scores do not introduce personality, initiative or sociability weighting");
        p1.SocialNeed=80;p1.ObserveEvent(ChaoStateEvent.PlayerContact,"player","player-one");p1.ObserveEvent(ChaoStateEvent.PlayerContact,"player","player-two");
        check(p1.SocialNeed==68,"Player contact keeps six-point relief per distinct completion");
        chooser=new(new Random(5));p1=Normal();var snapshot=chooser.Evaluate(p1,All()).Single(s=>s.Availability.Activity==ChaoActivity.Play).Score;
        chooser.Adopt(ChaoActivity.Play,"toy",ActivityInterruptibility.Atomic);chooser.Complete();
        check(Math.Abs(chooser.Evaluate(p1,All()).Single(s=>s.Availability.Activity==ChaoActivity.Play).Score-(snapshot-.15f))<.00001f,"The immediate repeated category receives exactly the configured penalty");
        chooser.Adopt(ChaoActivity.Wander,"walk",ActivityInterruptibility.LongOptional);chooser.Complete();chooser.Adopt(ChaoActivity.Leisure,"route",ActivityInterruptibility.LongOptional);chooser.Complete();
        check(Math.Abs(chooser.Evaluate(p1,All()).Single(s=>s.Availability.Activity==ChaoActivity.Play).Score-snapshot)<.00001f,"Two intervening categories remove the repeat penalty");
        chooser.Adopt(ChaoActivity.Eat,"meal",ActivityInterruptibility.Atomic);chooser.Complete();p1.Fullness=5;
        check(Math.Abs(chooser.Evaluate(p1,All()).Single(s=>s.Availability.Activity==ChaoActivity.Eat).Score-(.1f+p1.Needs.Evaluate().Priorities[0].Weight*.75f))<.00001f,"Critical physiological need overrides the repeat penalty");
        for(int i=0;i<12;i++){chooser.Adopt(ChaoActivity.Wander,"walk",ActivityInterruptibility.LongOptional);chooser.Complete();}
        check(chooser.History.Count==5,"Activity history stores only five completed intentions");
        var first=new ChaoActivityCoordinator(new Random(314));var secondSeed=new ChaoActivityCoordinator(new Random(314));
        var tie=new[]{new ActivityAvailability(ChaoActivity.Leisure,true,"",8,ActivityInterruptibility.LongOptional,1),new ActivityAvailability(ChaoActivity.Wander,true,"",5,ActivityInterruptibility.LongOptional,1)};p1.Fun=100;
        check(first.Choose(p1,tie)==secondSeed.Choose(p1,tie),"Equal-score selection is reproducible with a controlled random seed");
        p1.EnsurePersonality();var innate=p1.Personality!.Copy();p1.GardenActivity=chooser;
        string json=JsonSerializer.Serialize(p1);var round=JsonSerializer.Deserialize<PetState>(json)!;
        check(round.Personality!.Equals(innate) && round.GardenActivity==null && !json.Contains("SelectedActivity") && !json.Contains("contactOrder"),"Activity L: personality survives while intention history and contact ledger stay out of JSON");
        chooser=new(new Random(4));var calm=Normal();choices=All().Select(a=>a with{Available=a.Activity is ChaoActivity.Leisure or ChaoActivity.Wander,AvailableTargetCount=1}).ToArray();
        for(int tick=0;tick<18000;tick++)
        {
            chooser.Advance(1f/60);chooser.Choose(calm,choices);
            if(chooser.SelectedActivity==ChaoActivity.Wander && chooser.MinimumRemaining<=0 || chooser.SelectedActivity==ChaoActivity.Leisure && chooser.Now-chooser.StartedAt>=30)chooser.Complete();
        }
        check(chooser.Selections is >=8 and <30 && chooser.Interruptions==0,"Activity N: five minutes of unchanged safe conditions produce completed activities, not per-second switching");
        Console.WriteLine($"ACTIVITY_STABILITY: simulated=300s selections={chooser.Selections} interruptions={chooser.Interruptions}");
    }
}
