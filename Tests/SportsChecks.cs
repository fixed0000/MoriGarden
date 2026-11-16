using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Mori;

internal static class SportsChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var beginner=SportsCatalog.Find("race-beginner-1")!;var pet=new PetState();
        check(SportsCatalog.Events.Count(e=>e.Mode==SportMode.Race)==18 && SportsCatalog.Events.Count(e=>e.Mode==SportMode.Karate)==4,"Stadium has eighteen race entries and four independent Karate ranks");
        check(SportsCatalog.Events.Count(e=>SportsCatalog.Open(e,pet))==2,"Only Beginner Race and Beginner Karate initially open");
        pet.Athletics.Results["race-beginner-1"]=new(){Wins=1,Entries=1};
        check(SportsCatalog.Open(SportsCatalog.Find("race-beginner-2")!,pet) && !SportsCatalog.Open(SportsCatalog.Find("karate-standard")!,pet),"Race progression never requires or unlocks Karate progression");
        foreach(var surface in new[]{RaceSurface.Run,RaceSurface.Swim,RaceSurface.Fly,RaceSurface.Power})
        {
            var low=new PetState{Run=50,Swim=50,Fly=50,Power=50};var high=SaveStore.Snapshot(low);
            if(surface==RaceSurface.Run)high.Run=700;else if(surface==RaceSurface.Swim)high.Swim=700;else if(surface==RaceSurface.Fly)high.Fly=700;else high.Power=700;
            check(ChaoRace.Pace(high,surface,1)>ChaoRace.Pace(low,surface,1)*2,$"{surface} directly controls its own racing section");
        }
        check(ChaoRace.Pace(new(){Run=500,Energy=10},RaceSurface.Run,1)<ChaoRace.Pace(new(){Run=500,Energy=100},RaceSurface.Run,1) && ChaoRace.Pace(new(){Run=500},RaceSurface.Run,0)<ChaoRace.Pace(new(){Run=500},RaceSurface.Run,1),"Race energy and stamina have bounded, real pace effects");
        var entrants=new[]{new RaceEntrant("main","Blue",new(){Run=700,Stamina=500,Bond=100},2),new RaceEntrant("slow","Yellow",new(){Run=80},3),new RaceEntrant("steady","Green",new(){Run=180},4),new RaceEntrant("fast","Red",new(){Run=300},5)};
        var race=new ChaoRace(beginner,entrants);race.Step(2);check(entrants.All(e=>e.Distance==0) && !race.Cheer("main"),"Racers wait for the common countdown");
        race.Step(2);check(race.Cheer("main") && !race.Cheer("main"),"Existing cheer action has a strict cooldown");
        for(int i=0;i<4000 && !race.Finished;i++)race.Step(.05f);
        check(race.Finished && entrants[0].Place==1 && race.Standings.Select(e=>e.Place).Distinct().Count()==4 && race.Time<=186,"Four racers finish with ordered actual results and a bounded duration");
        check(entrants.All(e=>e.Fatigue>=0 && e.Fatigue<=e.Capacity),"Race fatigue remains bounded");
        float corner=0;bool continuous=true;for(int section=0;section<race.Course.Lengths.Length-1;section++){corner+=race.Course.Lengths[section];foreach(float lane in new[]{-1.725f,-.575f,.575f,1.725f})continuous&=Vector2.Distance(race.Course.At(corner-.001f,lane),race.Course.At(corner+.001f,lane))<.01f;}
        check(continuous,"Every lane joins continuously at track corners without a sideways teleport");
        check(new RaceCourse(SportsCatalog.Find("race-swim-1")!).Length!=race.Course.Length,"Specialist tracks have different geometry as well as surfaces");
        var recoveryRace=new ChaoRace(beginner,new[]{new RaceEntrant("a","Blue",new(){Stamina=500},1),new RaceEntrant("b","Red",new(){},2)});recoveryRace.Step(3.01f);recoveryRace.Entrants[0].Distance=recoveryRace.Course.Lengths.Take(2).Sum()+1;recoveryRace.Entrants[0].Fatigue=0;recoveryRace.Step(.5f);
        check(recoveryRace.Entrants[0].Fatigue>2,"Recovery sections replenish racing stamina");
        bool allTracksFinish=true;
        foreach(var entry in SportsCatalog.Events.Where(e=>e.Mode==SportMode.Race))
        {
            var field=new[]{new RaceEntrant("chosen","Green",new(){Run=500,Swim=500,Fly=500,Power=500,Stamina=500},12)}.Concat(SportsRivals.All.Take(3).Select((r,i)=>new RaceEntrant(r.Id,r.Color,r.Create(entry.Rating),i+24)));
            var trial=new ChaoRace(entry,field);while(!trial.Finished)trial.Step(.2f);
            allTracksFinish&=trial.Entrants.All(p=>p.FinishedAt>0 && float.IsFinite(p.Distance)) && trial.Standings.Select(p=>p.Place).SequenceEqual(new[]{1,2,3,4});
        }
        check(allTracksFinish,"All eighteen race entries finish with finite positions and correctly ordered four-pet tables");
        int wins=0;for(int seed=0;seed<20;seed++)
        {
            var trial=new ChaoRace(beginner,new[]{new RaceEntrant("strong","Blue",new(){Run=700,Stamina=500},seed),new RaceEntrant("weak","Yellow",new(){Run=50,Stamina=100,Luck=990,Intelligence=990},seed+100)});
            while(!trial.Finished)trial.Step(.1f);if(trial.Entrants[0].Place==1)wins++;
        }
        check(wins>=18,"Strong sports stats usually outperform maximum luck/intelligence with weak primary skills");
        var owner=new PetState();var athlete=new PetState{Name="Fern"};owner.GardenEggs.Add(new(){Id="fern",Hatched=true,Chao=athlete});
        string ticket=owner.Stadium.Begin("fern","race-beginner-3");var outcome=new SportsOutcome(ticket,"fern","race-beginner-3",1,31);
        var prize=SportsRewards.Apply(owner,outcome)!;
        check(prize.Stars>0 && prize.Cup.Length>0 && prize.Toy.Length>0 && athlete.Athletics.Cups.Count==1 && owner.Athletics.Cups.Count==0 && owner.Stars==prize.Stars,"Race cups belong to the real selected Chao while stars enter the shared wallet");
        int stars=owner.Stars;check(SportsRewards.Apply(owner,outcome)==null && owner.Stars==stars,"A result receipt cannot grant rewards twice");
        ticket=owner.Stadium.Begin("fern","race-beginner-3");prize=SportsRewards.Apply(owner,new(ticket,"fern","race-beginner-3",1,33))!;
        check(prize.Cup=="" && prize.Toy=="" && athlete.Athletics.Results["race-beginner-3"].BestSeconds==31,"Repeated competitions keep the best record and cannot duplicate unique cups/toys");
        check(SportsCatalog.Open(SportsCatalog.Find("race-swim-1")!,athlete) && !SportsCatalog.Open(SportsCatalog.Find("karate-standard")!,athlete),"Individual category unlocks are independent between sports");
        ticket=owner.Stadium.Begin("fern","karate-beginner");SportsRewards.Apply(owner,new(ticket,"fern","karate-beginner",1,50,3));
        check(athlete.Athletics.Results["karate-beginner"].BoutWins==3 && owner.Stadium.Toys.Contains("karate-ball") && SportsCatalog.Open(SportsCatalog.Find("karate-standard")!,athlete),"Karate tournament victories award and unlock once for the selected Chao");
        ticket=owner.Stadium.Begin("fern","karate-standard");owner.Stadium.Cancel();check(SportsRewards.Apply(owner,new(ticket,"fern","karate-standard",1,50,3))==null,"Abandoned entries cannot later claim a trophy");
        var a=new KarateFighter("a","Blue",new(){Power=700,Run=400,Stamina=400},Vector2.Zero,2);var b=new KarateFighter("b","Red",new(){},new(.34f,0),3);
        a.Facing=Vector2.UnitX;a.Action=KarateAction.Attack;a.ActionTime=.41f;a.Duration=2;b.Action=KarateAction.Wait;b.Duration=2;
        var fight=new ChaoKarate(a,b);fight.Step(3.02f); // Position is still nearby when GO releases the attack.
        check(a.Hits>0 && b.Health<b.MaxHealth && a.Impact>0 && Vector2.Distance(a.Position,b.Position)<=ChaoKarate.ContactRange+.04f,"Karate damage occurs at a visible contact event");
        a=new("a","Blue",new(){Power=800},Vector2.Zero,2);b=new("b","Red",new(){},new(2,0),3);a.Facing=Vector2.UnitX;a.Action=KarateAction.Attack;a.ActionTime=.41f;a.Duration=20;b.Duration=20;fight=new(a,b);fight.Step(3.1f);
        check(b.Health==b.MaxHealth && a.Hits==0,"Out-of-range attacks cannot cause invisible damage");
        ChaoKarate Contact(KarateAction defence)
        {
            var striker=new KarateFighter("a","Blue",new(){Power=700},Vector2.Zero,1);var guard=new KarateFighter("b","Red",new(){Run=500},new(.34f,0),2);
            striker.Facing=Vector2.UnitX;striker.Action=KarateAction.Attack;striker.ActionTime=.41f;striker.Duration=3;guard.Action=defence;guard.ActionTime=.1f;guard.Duration=3;
            return new(striker,guard);
        }
        var blocked=Contact(KarateAction.Guard);var dodged=Contact(KarateAction.Dodge);blocked.Step(3.02f);dodged.Step(3.02f);
        check(blocked.B.Blocks==1 && dodged.B.Dodges==1 && dodged.B.Health==dodged.B.MaxHealth,"Guard reduces contact damage and active visible dodge avoids it");
        var down=Contact(KarateAction.Wait);down.B.Stamina=0;down.Step(3.02f);bool knocked=down.B.Action==KarateAction.Knockdown;down.A.Position=new(-2,0);down.A.Action=KarateAction.Wait;down.A.Duration=100;down.Step(2.3f);bool rose=down.B.Action==KarateAction.Rise;down.Step(1.4f);
        check(knocked && rose && down.B.Action!=KarateAction.Knockdown && down.B.Action!=KarateAction.Rise,"Knockdown progresses through a timed recovery before returning to decisions");
        var peaceful=Contact(KarateAction.Wait);peaceful.A.Action=KarateAction.Wait;peaceful.A.Duration=1000;peaceful.B.Duration=1000;peaceful.B.Health*=.5f;peaceful.Step(94);
        check(peaceful.Finished && peaceful.Method=="Decision" && peaceful.Winner=="a","An inactive round still finishes by a bounded health decision");
        var cheering=Contact(KarateAction.Wait);cheering.A.Action=KarateAction.Wait;cheering.A.Duration=1000;cheering.B.Duration=1000;cheering.Step(3.1f);cheering.A.Stamina=10;
        check(cheering.Cheer("a") && !cheering.Cheer("a") && cheering.A.Stamina<=16,"Karate support is small and cannot be spammed");
        var neutral=new KarateFighter("n","White",new(){Initiative=100,Aggressiveness=-90,Intelligence=800},new(-1,0),1);var assertive=new KarateFighter("x","Red",new(){Initiative=900,Aggressiveness=90,Intelligence=100},new(1,0),2);
        check(ChaoKarate.Weight(KarateAction.Attack,assertive,neutral)>ChaoKarate.Weight(KarateAction.Attack,neutral,assertive) && ChaoKarate.Weight(KarateAction.Guard,neutral,assertive)>ChaoKarate.Weight(KarateAction.Guard,assertive,neutral),"Continuous personality values change tactical priorities without mandatory aggression");
        check(blocked.A.Pet.Power==700 && blocked.B.Pet.Energy==new PetState().Energy,"Combat health and fatigue never damage permanent pet state");
        var knockout=Contact(KarateAction.Wait);knockout.B.Health=1;knockout.Step(3.02f);check(knockout.Finished && knockout.Winner=="a" && knockout.Method=="Knockout","Karate can end by health exhaustion");
        var ringout=Contact(KarateAction.Wait);ringout.B.Position=new(3,0);ringout.Step(3.02f);check(ringout.Finished && ringout.Winner=="a" && ringout.Method=="Ring out","Karate can end by leaving the arena");
        int calmWins=0;for(int seed=0;seed<20;seed++)
        {
            var strong=new PetState{Power=800,Run=600,Stamina=650,Intelligence=800,Initiative=650,Aggressiveness=-80};var weak=new PetState{Power=100,Run=100,Stamina=100,Aggressiveness=100,Initiative=900};
            var trial=new ChaoKarate(new("strong","Green",strong,new(-1,0),seed),new("weak","Red",weak,new(1,0),seed+123));
            while(!trial.Finished)trial.Step(.1f);if(trial.Winner=="strong")calmWins++;
            if(trial.Time>94)throw new Exception("Unbounded fight");
        }
        check(calmWins>=15,"Calm trained Chao can defeat aggressive weak opponents; aggression is not a victory key");
        bool allBoutsFinish=true;
        foreach(var entry in SportsCatalog.Events.Where(e=>e.Mode==SportMode.Karate))for(int bout=0;bout<entry.Bouts;bout++)
        {
            var rival=SportsRivals.All[(bout+entry.Level+2)%SportsRivals.All.Length];var trial=new ChaoKarate(new("chosen","Green",new(){Power=500,Run=500,Stamina=500},new(-1,0),10+bout),new(rival.Id,rival.Color,rival.Create(entry.Rating+bout*18),new(1,0),30+bout));
            while(!trial.Finished)trial.Step(.2f);
            allBoutsFinish&=trial.Time<=94 && trial.A.Health>=0 && trial.B.Health>=0 && float.IsFinite(trial.A.Position.X) && float.IsFinite(trial.B.Position.X);
        }
        check(allBoutsFinish,"Every opponent in all four Karate ranks resolves within the round limit");
        string folder=Path.Combine(Path.GetTempPath(),"MoriSports-"+Guid.NewGuid());
        try{string file=Path.Combine(folder,"pet.json");SaveStore.Save(file,owner);var read=SaveStore.Load(file,out var warning);check(warning=="" && read.GardenEggs.Single(e=>e.Id=="fern").Chao!.Athletics.Cups.Count==athlete.Athletics.Cups.Count && read.Stadium.Toys.Count==2,"Individual achievements, rewards and unlocks survive disk saving");File.WriteAllText(file,"{\"Name\":\"Legacy\"}");read=SaveStore.Load(file,out _);check(read.Athletics.Results.Count==0 && read.Stadium.Toys.Count==0,"Legacy saves load without fabricated sports achievements");}
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
