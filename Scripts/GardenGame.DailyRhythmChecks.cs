using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool dailyCheckStarted;
    private int dailyCheckFrames;
    private void RunDailyRhythmChecks()
    {if(dailyCheckStarted || ++dailyCheckFrames<3)return;dailyCheckStarted=true;CheckDailyRhythmScene();}
    private async void CheckDailyRhythmScene()
    {
        try
        {
            SetPhysicsProcess(false);
            string directory=Path.GetDirectoryName(savePath)!;Directory.CreateDirectory(directory);
            async Task Shot(string name)
            {
                UpdateUi();cameraRig.Distance=4;cameraRig.Follow(friend.Position,0,true);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"daily-"+name+".png"));
            }
            GetWindow().Title="MoriGarden вЂ” GENERATED DAILY RHYTHM TEST";
            void Require(bool ok,string message){if(!ok)throw new Exception(message);GD.Print("RHYTHM_SCENE: "+message);}
            PetState State(string name)=>new(){Name=name,Fullness=85,Energy=70,Fun=100,Joy=70,SocialNeed=0,Run=180,Personality=new(),PersonalitySchemaVersion=1};
            pet=State("Rhythm Mori");pet.GardenSeconds=360;pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="rhythm-white",Hatched=true,Color="White",X=24,Z=14,Chao=State("Rhythm White")});
            pet.GardenEggs.Add(new(){Id="rhythm-yellow",Hatched=true,Color="Yellow",X=28,Z=14,Chao=State("Rhythm Yellow")});
            pet.ActivityHabits.Entries["Play"]=new(){Bias=.1f,ExperienceCount=1};
            pet.GardenEggs[0].Chao!.ActivityHabits.Entries["Socialize"]=new(){Bias=.1f,ExperienceCount=1};
            pet.GardenEggs[1].Chao!.ActivityHabits.Entries["Leisure"]=new(){Bias=.1f,ExperienceCount=1};
            petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
            friend.Position=ChaoRest.Bed+Vector3.Back*2.2f;friend.Visible=true;player.Position=new(20,0,20);wanderTime=0;chaoMotion.Reset();
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            for(int i=0;i<60000;i++)
            {
                float t=i*.01f,c=Mathf.PosMod(t+145,600),f=c<410 ? .5f*c/410 : .5f+.5f*(c-410)/190;
                var d=GardenDayCycle.Evaluate(t);
                RequireSky(Mathf.Abs(d.DaylightIntensity-Mathf.SmoothStep(-.07f,.09f,Mathf.Sin(Mathf.Tau*f)))<.00001f);
            }
            void RequireSky(bool ok){if(!ok)throw new Exception("Original Godot sky diverged");}
            Require(true,"60000 native Godot sky samples match shared math");
            foreach(double t in new[]{60d,265d,360d}){pet.GardenSeconds=t;gardenSky.Advance(t,true);Require(GardenDayCycle.GetPhase(t)==(t==60?GardenDayPhase.Day:t==265?GardenDayPhase.Dusk:GardenDayPhase.Night),"visual sky and behavior phase "+GardenDayCycle.GetPhase(t));await Shot(GardenDayCycle.GetPhase(t).ToString());}
            pet.GardenSeconds=60;MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Select(true);
            Require(!MainActivity.Coordinator.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Sleep).Availability.Available,"Energy 70 daylight preserves old sleep threshold");
            MainActivity.Reset();pet.GardenSeconds=360;
            var blockers=Enumerable.Range(0,4).Select(_=>new ChaoRest{Spaces=bedSpaces}).ToArray();
            foreach(var r in blockers)r.Request(ChaoRest.Bed+Vector3.Back*2);
            friend.Position=ChaoRest.Bed+Vector3.Up*.43f;MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Select(true);
            Require(!MainActivity.Coordinator.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Sleep).Availability.Available,"all four occupied bed slots cannot be bypassed by night preference");
            foreach(var r in blockers)r.Cancel();MainActivity.Reset();friend.Position=ChaoRest.Bed+Vector3.Back*2.2f;
            pet.GardenSeconds=360;ImportedChao.ClearActionPause(friendArt);MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Select(true);
            Require(MainActivity.Coordinator.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Sleep).Availability.Available,"Energy 70 actual night has a safe available bed");
            Require(MainActivity.Coordinator.SelectedActivity==ChaoActivity.Sleep && moriRest.Requested && !sleeping,"night Sleep selected through existing adapter; no teleport");
            Vector3 start=friend.Position;bool moved=false,reached=false,woke=false,sawRising=false,sleepShot=false,riseShot=false;
            for(int frame=0;frame<11000;frame++)
            {
                const float dt=1f/60;
                pet.GardenSeconds+=dt;pet.Tick(dt,sleeping);UpdateEggs(dt);MoveFriend(dt);
                if(sleeping && !sleepShot){sleepShot=true;await Shot("Sleep");}
                if(moriRising.Active && !riseShot){riseShot=true;await Shot("Rising");}
                moved|=friend.Position.DistanceTo(start)>.15f;reached|=sleeping;sawRising|=moriRising.Active;
                if(frame%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(reached && !sleeping && !moriRising.Active && !chaoMotion.Airborne && frame>2200){woke=true;break;}
            }
            Require(moved && reached && woke && sawRising,"natural approach, bed hop, minimum sleep, Energy 100, wake and existing Rising complete");
            MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Select(true);
            Require(!MainActivity.Coordinator.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Sleep).Availability.Available,"fully rested Chao cannot immediately sleep again");
            var actors=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();
            foreach(var e in actors)e.PrepareActivityFrame(0);
            Require(actors.Length==2 && actors.All(e=>e.Chao!.DailyRhythm?.GardenSeconds==pet.GardenSeconds) &&
                actors.Select(e=>e.Chao!.DailyRhythm!.PersonalPhaseOffset).Append(MainActivity.Rhythm.PersonalPhaseOffset).Distinct().Count()==3 &&
                pet.ActivityHabits.Entries.Keys.Single()=="Play" && actors.All(e=>e.Chao!.ActivityHabits.Entries.Count==1),"three Chao share root time with independent offsets and habits");
            double savedTime=pet.GardenSeconds;carrying=true;SwitchZone(GardenZone.CoastalCove);carrying=false;MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);
            Require(pet.GardenSeconds==savedTime && MainActivity.Rhythm.Enabled && MainActivity.Rhythm.AreaId=="coastal-cove","garden-to-Coastal Cove preserves root time");
            paused=true;double pauseTime=pet.GardenSeconds;_PhysicsProcess(.25);paused=false;
            Require(pet.GardenSeconds==pauseTime,"actual paused physics leaves garden clock frozen");
            var bridge=new ChaoEditorBridge(Path.Combine(directory,"rhythm-editor"));
            var roster=new System.Collections.Generic.Dictionary<string,PetState>{{"main",pet}};
            foreach(var e in pet.GardenEggs)roster[e.Id]=e.Chao!;
            bridge.Poll(roster,true,EditorRhythmContext);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"rhythm-editor/chao-editor-state.json")))!;
            string immutable=JsonSerializer.Serialize(new{pet.Personality,pet.ActivityHabits,pet.LearnedPreferences,pet.SocialLinks});
            File.WriteAllText(Path.Combine(directory,"rhythm-editor/chao-editor-request.json"),JsonSerializer.Serialize(new{session=snap["session"]!.GetValue<string>(),id="daily-rebind",target="main",patch=new{Fullness=84}}));
            pet=bridge.Poll(roster,true,EditorRhythmContext) ?? throw new Exception("Editor did not apply safe-copy patch");RebindGardenExecutors("main",pet);
            Require(pet.GardenSeconds==savedTime && immutable==JsonSerializer.Serialize(new{pet.Personality,pet.ActivityHabits,pet.LearnedPreferences,pet.SocialLinks}),"editor rebind preserves time, thirteen traits, habits, preferences and relationships");
            pet.GardenSeconds=360;Save();var restored=SaveStore.Load(savePath,out _);
            Require(restored.GardenSeconds==360 && GardenDayCycle.GetPhase(restored.GardenSeconds)==GardenDayPhase.Night && restored.GardenEggs.Count==2,"night save/load restores phase and same pet roster");
            File.WriteAllText(Path.Combine(directory,"daily-rhythm-check.json"),JsonSerializer.Serialize(new{passed=true,scene="actual executors",time=restored.GardenSeconds,threeChao=true,skyMatch=true,naturalSleep=true,wake=true,editor=true,pause=true,transitions=true}));
            GD.Print("DAILY_RHYTHM_SCENE_OK");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
}
