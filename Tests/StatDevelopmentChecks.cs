using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mori;

internal static class StatDevelopmentChecks
{
    private sealed class MaximumRandom : Random { public override int Next(int minValue,int maxValue)=>maxValue-1; }
    internal static void Run(Action<bool,string> check)
    {
        for(int grade=0;grade<6;grade++)
        {
            var stat=new ChaoStat{Grade=grade};var random=new Random(100+grade);bool valid=true;
            for(int i=0;i<99;i++){int before=stat.Points;stat.AddProgress(100,false,random);valid&=stat.Points-before>=11+grade*3 && stat.Points-before<=15+grade*3;}
            check(valid && stat.Level==99,$"Grade {grade} has the documented five-value gain window through level 99");
            int last=stat.Points;stat.AddProgress(int.MaxValue,false,random);check(stat.Points==last && stat.Progress<=99,"Level 99 cannot farm points: grade "+grade);
        }
        var negative=new ChaoStat{Level=12,Points=200,Progress=5};negative.AddProgress(-200,false,new(1));
        check(negative.Level==12 && negative.Points==200 && negative.Progress==0,"Negative progress never removes levels or points");
        var remainder=new ChaoStat{Progress=95};remainder.AddProgress(48,false,new(2));
        check(remainder.Level==1 && remainder.Progress==43,"Level-up retains the unspent progress remainder");
        var hidden=new ChaoStat{Grade=255};hidden.AddProgress(600,true,new(3));
        check(hidden.Level==6 && hidden.Points==4000,"Hidden grade 255 uses its own domain and reaches its 4000 cap safely");
        var capped=new ChaoStat{Grade=5,Level=98,Points=3260};capped.AddProgress(100,false,new(4));
        check(capped.Level==99 && capped.Points==3266,"Main points stop at the requested console-only ceiling");
        var evolved=PetState.NewEgg();evolved.Hatch();evolved.ChildSeconds=PetState.FirstEvolutionSeconds;evolved.RecentAnimalSkill="Fly";evolved.Stat("Fly").Grade=4;evolved.Evolve();
        check(evolved.Stat("Fly").Grade==5 && !evolved.Evolve(),"The first Fly evolution promotes exactly once, at most to S");
        var normal=PetState.NewEgg();normal.Hatch();normal.Care=10;normal.Stat("Stamina").Grade=2;normal.Evolve();check(normal.Stat("Stamina").Grade==3,"Normal Bloom evolution promotes Stamina");
        var reborn=new ChaoStatistics();reborn.Run=new(){Grade=5,Level=99,Points=2979,Progress=48};var next=reborn.Reborn();
        check(next.Run.Level==1 && next.Run.Points==297 && next.Run.Grade==5 && next.Run.Progress==0 && reborn.Run.Points==2979,"Reincarnation is a separate floor-ten-percent transform, without mutating its input");
        var old=JsonSerializer.Deserialize<PetState>("{\"Name\":\"Legacy\",\"Run\":600,\"Luck\":400,\"GardenEggs\":[{\"Id\":\"stable\",\"Hatched\":true,\"Chao\":{\"Name\":\"Child\",\"Swim\":80}}]}")!;old.Validate();int migrated=old.Run;old.Validate();
        check(old.StatSchemaVersion==1 && old.Run==migrated && old.RunAbility==600 && old.LuckAbility==400 && old.GardenEggs[0].Chao!.SwimAbility==80 && old.GardenEggs[0].Id=="stable","Migration is deterministic, recursive, idempotent and preserves calibrated ability and IDs");
        var copy=SaveStore.Snapshot(old);check(copy.Run==old.Run && copy.Stat("Run").Grade==2 && copy.Stat("Run").Level==old.Stat("Run").Level,"New snapshots retain canonical stat fields without remigration");
        foreach(var effect in AnimalTraining.Effects)
        {
            var p=PetState.NewEgg();p.Hatch();foreach(string name in ChaoStatistics.Names)p.Stat(name).Progress=30;
            p.TrainWithSpecies(effect.Species,new(7));
            check(p.Stat("Swim").Progress==Math.Max(0,30+effect.Swim) && p.Stat("Power").Progress==Math.Max(0,30+effect.Power) && p.Stat("Luck").Progress==50 && p.Stat("Intelligence").Progress==50,$"{effect.Species} applies species-specific signed progress and hidden progress");
        }
        var meal=PetState.NewEgg();meal.Hatch();meal.Fullness=0;for(int i=0;i<4;i++)meal.FeedBite(i==3);
        check(meal.Stat("Stamina").Progress==40 && meal.Stat("Stamina").Points==0 && meal.Energy==90,"Four bites grant 40 stamina progress, without changing current energy");
        for(int i=0;i<6;i++)meal.FeedBite(i==5);check(meal.Stat("Stamina").Level==1 && meal.Stat("Stamina").Progress==0,"Food grants points only when the stamina bar levels up");
        var e=PetState.NewEgg();e.Hatch();var s=PetState.NewEgg();s.Hatch();e.Stat("Run").Grade=0;s.Stat("Run").Grade=5;
        e.TrainProgress("Run",9900,new(8));s.TrainProgress("Run",9900,new(8));
        check(e.Stat("Run").Level==s.Stat("Run").Level && s.Run>e.Run && ChaoRace.Pace(s,RaceSurface.Run,1)>ChaoRace.Pace(e,RaceSurface.Run,1),"Same level E/S pets have different actual points and race pace");
        var same=SaveStore.Snapshot(e);same.Stat("Run").Grade=5;check(ChaoRace.Pace(e,RaceSurface.Run,1)==ChaoRace.Pace(same,RaceSurface.Run,1),"Changing grade alone never changes immediate race strength");
        float Finish(PetState p)
        {
            var race=new ChaoRace(SportsCatalog.Find("race-mushroom-forest-1")!,new[]{new RaceEntrant("test","Blue",p,43),new RaceEntrant("other","Green",SaveStore.Snapshot(p),44)});
            while(!race.Finished)race.Step(.2f);return race.Entrants[0].FinishedAt;
        }
        float eTime=Finish(e),sTime=Finish(s);Console.WriteLine($"STAT_RACE_TIMES: E99={e.Run} pts/{eTime:0.00}s; S99={s.Run} pts/{sTime:0.00}s");
        check(sTime>0 && eTime>sTime,"Different actual points at level 99 change the full course finish time");
        var peak=new ChaoStat{Grade=5};int[] expected={2970,3237,3263,3266};bool retained=true;
        for(int life=0;life<4;life++){peak.AddProgress(9900,false,new MaximumRandom());retained&=peak.Points==expected[life];peak=peak.Reborn();}
        check(retained,"Four optimal console-only lives reach 3266, without confusing life-one and long-term limits");
        foreach(int points in new[]{0,100,1300,2500,3266})
        {
            var p=PetState.NewEgg();p.Hatch();foreach(string name in ChaoStatistics.Names){p.Stat(name).Level=99;p.Stat(name).Points=points;}
            check(ChaoKarate.AttackDuration(p)>0 && ChaoKarate.ZealDrain(p)>0 && ChaoKarate.Defence(p)>=1 && float.IsFinite(ChaoRace.Pace(p,RaceSurface.Run,1)),"Sports formulas remain bounded and positive at points "+points);
        }
        bool invalid=false;try{ChaoStatistics.ValidateEditor("Run",new(){Level=0,Points=3000});}catch(ArgumentException){invalid=true;}check(invalid,"The editor rejects a zero-level 3000-point stat");
        var directory=Path.Combine(Path.GetTempPath(),"MoriStats-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            string file=Path.Combine(directory,"slot.json");File.WriteAllText(file,"{\"Name\":\"Saved Legacy\",\"Run\":900}");string original=File.ReadAllText(file);var loaded=SaveStore.LoadSlot(file)!;
            check(loaded.RunAbility==900 && File.ReadAllText(file)==original,"Reading a legacy slot migrates in memory without rewriting it");SaveStore.Save(file,loaded);var again=SaveStore.LoadSlot(file)!;
            check(again.Run==loaded.Run && File.ReadAllText(file+".bak")==original,"Saving retains the old file as backup and reload does not migrate twice");
            var roster=new System.Collections.Generic.Dictionary<string,PetState>{{"main",loaded}};var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true);
            string session=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json"))).RootElement.GetProperty("session").GetString()!;
            var request=new{session,id="valid",target="main",patch=new{Joy=77.0f},statistics=new{Run=new{Grade=5,Level=99,Points=2772,Progress=0}}};
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(request));var edited=bridge.Poll(roster,true);
            check(edited!=null && edited.Stat("Run").Grade==5 && edited.Run==2772 && edited.Joy==77 && edited.Name==loaded.Name,"The real editor bridge applies structured stats and preserves other pet data");
            check(bridge.Poll(roster,true)==null,"The editor cannot reapply an acknowledged request");
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),JsonSerializer.Serialize(new{session,id="invalid",target="main",patch=new{},statistics=new{Run=new{Grade=8,Level=100,Points=999999,Progress=100}}}));
            check(bridge.Poll(roster,true)==null && roster["main"].Run==2772,"Invalid editor stats are rejected atomically without damaging the pet");
        }
        finally{foreach(var file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
}
