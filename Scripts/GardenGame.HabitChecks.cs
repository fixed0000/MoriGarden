using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Mori;

public partial class GardenGame
{
    // Reuses the actual shared music/ball scenarios before this final independent
    // play/leisure/rebind fixture; there is no second simulation or planner.
    private async Task CheckHabitExecutors(PetState actualSocialLearner)
    {
        int Credits(PetState p,ChaoActivity a)=>p.ActivityHabits.Entries.TryGetValue(a.ToString(),out var e) ? e.ExperienceCount : 0;
        PetState State(string name)=>new(){Name=name,Fullness=80,Energy=80,Fun=100,Joy=70,SocialNeed=20,Run=180,Personality=new(),PersonalitySchemaVersion=1};
        pet=State("Play learner");pet.Fun=5;pet.GardenEggs.Clear();
        var social=SaveStore.Snapshot(actualSocialLearner);social.Fun=100;social.Fullness=social.Energy=80;social.SocialNeed=20;
        pet.GardenEggs.Add(new(){Id="partner-b",Hatched=true,Color="White",X=30,Z=18,Chao=social});
        pet.GardenEggs.Add(new(){Id="habit-sky",Hatched=true,Color="Yellow",X=0,Z=12,Chao=State("Leisure learner")});
        petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
        friend.Position=new(20,0,14);friend.Visible=true;player.Position=new(40,0,30);wanderTarget=friend.Position;wanderTime=0;chaoMotion.Reset();
        await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        var sky=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id=="habit-sky");
        var bodies=BallParticipants();for(int i=0;i<bodies.Count;i++)for(int j=i+1;j<bodies.Count;j++){var l=pet.SocialLink(bodies[i].Id,bodies[j].Id,true)!;l.Met=l.Introduced=true;}
        var rattle=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);rattle.Position=friend.Position+Vector3.Right*.5f;
        ImportedChao.ClearActionPause(friendArt);MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Select(true);
        sky.PrepareActivityFrame(0);ImportedChao.ClearActionPause(sky.Art);sky.LeisureExecutor.BeginSky(ChaoLeisure.SkySpots[0]);
        if(MainActivity.Coordinator.SelectedActivity!=ChaoActivity.Play)throw new Exception("Habit fixture did not select real need-based Play");
        for(int frame=0;frame<4000;frame++)
        {
            const float dt=1f/60;pet.Tick(dt,false);foreach(var t in world.GetChildren().OfType<ToyCar>())t.Advance(dt);UpdateEggs(dt);MoveFriend(dt);
            if(frame%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(Credits(pet,ChaoActivity.Play)==1 && Credits(sky.Chao!,ChaoActivity.Leisure)==1)break;
        }
        if(Credits(pet,ChaoActivity.Play)!=1 || Credits(pet,ChaoActivity.Socialize)!=0 || Credits(pet,ChaoActivity.Leisure)!=0 ||
            Credits(social,ChaoActivity.Socialize)!=1 || Credits(social,ChaoActivity.Play)!=0 ||
            Credits(sky.Chao!,ChaoActivity.Leisure)!=1 || Credits(sky.Chao!,ChaoActivity.Play)!=0 ||
            pet.LearnedPreferences.ToyPreferences["rattle"].ExperienceCount!=1 || sky.Chao!.History.Events.Single(e=>e.Source=="garden/leisure/sky-0").Occurrences!=1)
            throw new Exception("Three actual Chao did not retain independent play/social/leisure histories");
        var independent=new[]{new{id="main",habits=SaveStore.Snapshot(pet).ActivityHabits},new{id="partner-b",habits=SaveStore.Snapshot(social).ActivityHabits},new{id="habit-sky",habits=SaveStore.Snapshot(sky.Chao!).ActivityHabits}};
        GD.Print("HABIT_INDEPENDENCE_OK: actual rattle Play, prior actual ten-pass Socialize, resident full sky/rise Leisure; no crossover");

        moriInterests.Cancel(friend,friendArt);moriLeisure.Cancel(friendArt);foreach(var t in world.GetChildren().OfType<ToyCar>())t.CancelPlay(friend);
        friend.Position=ChaoLeisure.Routes[1][0];ImportedChao.ClearActionPause(friendArt);chaoMotion.Reset();
        MainActivity.Coordinator.Suspend("");MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);moriLeisure.BeginRoute(1);
        MainActivity.Coordinator.Adopt(ChaoActivity.Leisure,"real route test",ActivityInterruptibility.LongOptional);
        PetState? obsolete=null;string directory=Path.GetDirectoryName(savePath)!;
        for(int frame=0;frame<14000 && Credits(pet,ChaoActivity.Leisure)==0;frame++)
        {
            const float dt=1f/60;pet.Tick(dt,false);UpdateEggs(dt);MoveFriend(dt);
            if(frame==120)
            {
                var roster=BallParticipants().ToDictionary(c=>c.Id,c=>c.Pet);var bridge=new ChaoEditorBridge(Path.Combine(directory,"habit-editor"));bridge.Poll(roster,true);
                var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"habit-editor/chao-editor-state.json")));
                File.WriteAllText(Path.Combine(directory,"habit-editor/chao-editor-request.json"),JsonSerializer.Serialize(new{session=snapshot.RootElement.GetProperty("session").GetString(),id="route-rebind",target="main",patch=new{Fullness=79}}));
                obsolete=pet;pet=bridge.Poll(roster,true) ?? throw new Exception("Route editor rebind failed");RebindGardenExecutors("main",pet);
            }
            if(frame%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        if(Credits(pet,ChaoActivity.Leisure)!=1 || obsolete==null || Credits(obsolete,ChaoActivity.Leisure)!=0 || Credits(pet,ChaoActivity.Play)!=1 ||
            !pet.History.Events.Any(e=>e.Source=="garden/leisure/route-1"))throw new Exception("Full route did not credit the current editor-rebound actor and its place memory");
        string before=JsonSerializer.Serialize(pet.ActivityHabits);moriLeisure.BeginRoute(1);moriLeisure.Cancel(friendArt);
        pet.Fun=5;rattle.BeginPlay(friend,pet);rattle.Advance(1);rattle.CancelPlay(friend);rattle.Advance(30);
        if(before!=JsonSerializer.Serialize(pet.ActivityHabits))throw new Exception("Interrupted route/toy credited a habit");
        Save();var loaded=SaveStore.Load(savePath,out _);
        if(JsonSerializer.Serialize(loaded.ActivityHabits)!=before || loaded.GardenEggs[0].Id!="partner-b" || loaded.GardenEggs[1].Id!="habit-sky" ||
            Credits(loaded.GardenEggs[0].Chao!,ChaoActivity.Socialize)!=1 || Credits(loaded.GardenEggs[1].Chao!,ChaoActivity.Leisure)!=1)
            throw new Exception("Habit save/reload lost individual data");
        var restoredScores=new ChaoActivityCoordinator(new Random(4)).Evaluate(loaded,Enum.GetValues<ChaoActivity>().Select(a=>new ActivityAvailability(a,true,"Available",35,ActivityInterruptibility.LongOptional,1)));
        if(restoredScores.Single(s=>s.Availability.Activity==ChaoActivity.Play).HabitAdjustment<=0 || restoredScores.Single(s=>s.Availability.Activity==ChaoActivity.Leisure).HabitAdjustment<=0)
            throw new Exception("Restored habit did not affect the actual coordinator");
        // Explicit maximum-habit fixture: player ownership still cancels leisure.
        sky.Chao!.ActivityHabits.Entries["Leisure"].Bias=.10f;
        string beforePickup=JsonSerializer.Serialize(sky.Chao.ActivityHabits);
        sky.PrepareActivityFrame(0);sky.LeisureExecutor.BeginSky(ChaoLeisure.SkySpots[0]);sky.Hold(sky.Position,false,.02f);
        if(sky.LeisureExecutor.Active || beforePickup!=JsonSerializer.Serialize(sky.Chao.ActivityHabits))throw new Exception("Habit overrode player pickup or pickup taught an activity");
        File.WriteAllText(Path.Combine(directory,"habit-check.json"),JsonSerializer.Serialize(new{passed=true,independent,root=loaded.ActivityHabitDiagnostics(),
            sharedMusicAndBall=true,realSolo=true,realRoute=true,realSky=true,editorRebind=true,cancellations=true,saveLoad=true,playerPickup=true,restoredScores}));
        GD.Print("HABITS_SCENE_OK: actual solo rattle, shared music/ball Socialize only, route and sky, three independent Chao, cancellation, editor rebind, save/load and scores");
    }
}
