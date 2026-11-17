using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Mori;

internal static class SportsChecks
{
    internal static void Run(Action<bool,string> check)
    {
        SportMotionChecks.Run(check);KarateChecks.Run(check);StadiumMotionChecks.Run(check);
        var pool=SportsCatalog.Find("race-crab-pool-1")!;var forest=SportsCatalog.Find("race-mushroom-forest-1")!;
        check(SportsCatalog.Events.Count(e=>e.Mode==SportMode.Race)==12,"Four distinct Beginner courses each have three levels");
        check(SportsCatalog.Events.Count(e=>e.Mode==SportMode.Race && SportsCatalog.Open(e,new()))==4,"Each Beginner course starts at level one");
        check(SportsCatalog.Find("race-beginner-3")!=null && !SportsCatalog.Open(SportsCatalog.Find("race-beginner-3")!,new()),"Legacy races remain historical entries rather than unearned new course victories");
        foreach(var surface in new[]{RaceSurface.Run,RaceSurface.Swim,RaceSurface.Fly,RaceSurface.Power})
        {
            var low=new PetState{Run=50,Swim=50,Fly=50,Power=50};var high=SaveStore.Snapshot(low);
            if(surface==RaceSurface.Run)high.Run=(int)Math.Round(700*3266d/990);else if(surface==RaceSurface.Swim)high.Swim=(int)Math.Round(700*3266d/990);else if(surface==RaceSurface.Fly)high.Fly=(int)Math.Round(700*3266d/990);else high.Power=(int)Math.Round(700*3266d/990);
            check(ChaoRace.Pace(high,surface,1)>ChaoRace.Pace(low,surface,1)*2,surface+" controls actual course traversal");
        }
        var strong=new PetState{Run=550,Swim=550,Fly=550,Power=550,Stamina=550,Energy=100};
        ChaoRace Make(SportsEvent e,int seed=1)=>new(e,new[]{new RaceEntrant("chosen","Green",SaveStore.Snapshot(strong),seed)}.Concat(SportsRivals.All.Take(7).Select((r,i)=>new RaceEntrant(r.Id,r.Color,r.Create(e.Rating),seed+i+20))));
        var race=Make(pool);race.Step(2);check(race.Entrants.All(e=>e.Distance==0),"Eight racers share a stationary start countdown");race.Step(1.2f);
        float reserve=race.Entrants[0].Fatigue;check(race.Cheer("chosen") && race.Entrants[0].Fatigue<reserve,"Race cheering consumes rather than restores Stamina");
        check(!race.Cheer("chosen"),"Repeated boost input is debounced");race.Step(.2f);check(race.Cheer("chosen"),"Race support is repeatable without a long cooldown");
        var selected=race.Entrants[0];race.Trip(selected);float at=selected.Distance;check(!race.Cheer("chosen"),"A fallen Chao cannot be boosted");race.Step(.8f);check(selected.Action==RaceAction.Prone && selected.Distance==at,"A trip has a visible prone interval and stops progress");race.Step(1.2f);check(selected.Action==RaceAction.Move || selected.Action==RaceAction.Rise,"Fallen racers stand up before continuing");
        check(ChaoRace.Pace(strong,RaceSurface.Run,0)<=ChaoRace.Pace(strong,RaceSurface.Run,1)*.2f,"Complete exhaustion reduces normal pace by at least eighty percent");
        reserve=selected.Fatigue;race.Step(1);check(selected.Fatigue<reserve,"Stamina only depletes during a race");
        selected.Fatigue=0;check(!race.Cheer("chosen"),"An empty energy bar cannot fund a boost");
        bool valid=true,geometry=true;foreach(var entry in SportsCatalog.Events.Where(e=>e.Mode==SportMode.Race))
        {
            var trial=Make(entry);while(!trial.Finished)trial.Step(.25f);
            valid&=trial.Entrants.Count==8 && trial.Entrants.All(e=>e.FinishedAt>0) && trial.Standings.Select(e=>e.Place).SequenceEqual(Enumerable.Range(1,8));
            geometry&=Vector3.Distance(trial.Course.Anchors[0],trial.Course.Anchors[^1])>45;
            for(float d=.05f;d<trial.Course.Length;d+=.25f)for(int lane=0;lane<8;lane++)geometry&=Vector3.Distance(trial.Course.Position(d-.05f,(lane-3.5f)*.55f,strong),trial.Course.Position(d,(lane-3.5f)*.55f,strong))<.16f;
        }
        check(valid,"All twelve open-ended courses finish with eight ordered independent results");check(geometry,"Courses have distinct start/finish and continuous lane geometry");
        var valley=new RaceCourse(SportsCatalog.Find("race-stump-valley-1")!);float gapStart=valley.Lengths.Take(2).Sum();float gapMid=gapStart+valley.Lengths[2]*.65f;
        check(valley.Position(gapMid,0,new(){Fly=800}).Y>valley.Position(gapMid,0,new(){Fly=0}).Y+1,"Strong fliers remain aloft while weak fliers descend into the valley");
        check(valley.TravelSurface(gapStart+valley.Lengths[2]*.85f,new(){Fly=0})==RaceSurface.Power,"Weak fliers physically climb out of the valley");
        var canyon=new RaceCourse(SportsCatalog.Find("race-block-canyon-1")!);check(canyon.Anchors[^1].Y>5 && canyon.Surfaces.Count(s=>s==RaceSurface.Power)==2,"Block Canyon contains real vertical climbing sections");
        var startPositions=Enumerable.Range(0,8).Select(i=>race.Course.Position(0,(i-3.5f)*.55f,strong)).ToArray();check(startPositions.Distinct().Count()==8 && Vector3.Distance(startPositions[0],startPositions[1])>.5f,"Eight start positions cannot overlap");
        var owner=new PetState();owner.GardenEggs.Clear();var athlete=new PetState{Name="Fern"};owner.GardenEggs.Add(new(){Id="fern",Hatched=true,Chao=athlete});
        string ticket=owner.Stadium.Begin("fern","race-mushroom-forest-3");var result=new SportsOutcome(ticket,"fern","race-mushroom-forest-3",1,31);var prize=SportsRewards.Apply(owner,result)!;
        check(prize.Cup.Length>0 && athlete.Athletics.Cups.Count==1 && owner.Athletics.Cups.Count==0,"The real selected Chao owns its cup");int stars=owner.Stars;check(SportsRewards.Apply(owner,result)==null && stars==owner.Stars,"A result receipt cannot award twice");
        ticket=owner.Stadium.Begin("fern","race-mushroom-forest-3");var repeated=SportsRewards.Apply(owner,new(ticket,"fern","race-mushroom-forest-3",1,33))!;check(repeated.Cup=="" && repeated.Toy=="" && athlete.Athletics.Results[result.EventId].BestSeconds==31,"Unique awards cannot be duplicated by repeat entries");
        var legacy=new PetState();legacy.Athletics.Results["race-beginner-3"]=new(){Entries=1,Wins=1};legacy.Athletics.Cups.Add("race-beginner-3");legacy.Validate();check(legacy.Athletics.Cups.Contains("race-beginner-3") && !SportsCatalog.Open(SportsCatalog.Find("race-crab-pool-2")!,legacy),"Previous achievements survive without fabricating wins on new courses");
        var directory=Path.Combine(Path.GetTempPath(),"MoriSports-"+Guid.NewGuid());try{var file=Path.Combine(directory,"pet.json");SaveStore.Save(file,owner);var read=SaveStore.Load(file,out string warning);check(warning=="" && read.GardenEggs.Single().Chao!.Athletics.Cups.Count==1,"Individual sports results survive saving and loading");File.WriteAllText(file,"{\"Name\":\"Legacy\"}");check(SaveStore.Load(file,out _).Athletics.Results.Count==0,"Older saves require no competition data");}finally{if(Directory.Exists(directory) && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(directory,true);}
    }
}
