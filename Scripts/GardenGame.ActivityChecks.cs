using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int activityCheckFrame;
    private bool activityCheckStarted;
    private void RunActivityCheckMode()
    {if(activityCheckStarted || ++activityCheckFrame<3)return;activityCheckStarted=true;CheckActivityScene();}
    private async void CheckActivityScene()
    {
        try
        {
            GD.Seed(1440);GetWindow().Title="MoriGarden — GENERATED ACTIVITY TEST";
            PetState State(string name,float food,float energy,float fun,float social=20)=>new(){Name=name,Run=180,Fullness=food,Energy=energy,Fun=fun,SocialNeed=social};
            void Fixture(PetState main,params GardenEggState[] residents)
            {
                pet=main;pet.GardenEggs.Clear();pet.GardenEggs.AddRange(residents);petZone=GardenZone.Garden;
                carrying=false;callTime=0;mainActivity=null;
                SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);friend.Visible=true;
                ImportedChao.ClearActionPause(friendArt);ImportedChao.CloseEyes(friendArt,false);ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);
                player.Position=new(35,0,25);wanderTarget=friend.Position;wanderTime=0;
            }
            void Simulate(float seconds,bool social=false)
            {
                for(int i=0;i<(int)(seconds*60);i++)
                {
                    const float dt=1f/60;
                    pet.Tick(dt,sleeping || moriIslandNap.IsNapping);
                    foreach(var t in world.GetChildren().OfType<ToyCar>())t.Advance(dt);
                    if(social){musicGroup.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial,allowStart:false);ballGame.Advance(dt,pet,BallParticipants(),SocialCandidates(true),world.GetChildren().OfType<ToyCar>(),PauseForSocial);}
                    UpdateEggs(dt);MoveFriend(dt);
                    if(social)introductions.Advance(dt,pet,SocialCandidates,()=>{},allowStart:false);
                    ResolveChaoContacts();
                }
            }
            Fixture(State("Hungry",10,80,80),new GardenEggState{Id="play-pet",Hatched=true,Color="Yellow",X=23,Z=14,Chao=State("Playful",80,80,5)},
                new GardenEggState{Id="sleep-pet",Hatched=true,Color="Black",X=5,Z=6.1f,Chao=State("Sleepy",80,5,80)});
            var food=new FruitTree{IsFeeder=true,SingleUse=true};world.AddChild(food);food.Place(new(20.7f,.18f,14));
            var rattle=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);rattle.Position=new(23.6f,.02f,14);
            Simulate(.1f);
            var play=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id=="play-pet");
            var sleep=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id=="sleep-pet");
            if(pet.GardenActivity?.SelectedActivity!=ChaoActivity.Eat || play.Chao!.GardenActivity?.SelectedActivity!=ChaoActivity.Play || sleep.Chao!.GardenActivity?.SelectedActivity!=ChaoActivity.Sleep)
                throw new Exception($"Three real actors chose wrong categories: {pet.GardenActivity?.SelectedActivity}/{play.Chao!.GardenActivity?.SelectedActivity}/{sleep.Chao!.GardenActivity?.SelectedActivity}\n{ChaoStateDiagnostics.Describe(pet)}");
            Simulate(10);
            if(!moriInterests.HasMeal)throw new Exception("Atomic-bite fixture did not reach an actual meal");
            pet.Energy=5;MoveFriend(.02f);
            if(!moriInterests.HasMeal || pet.GardenActivity?.SelectedActivity!=ChaoActivity.Eat)throw new Exception("Critical fatigue interrupted actual feeding");
            pet.Energy=80;Simulate(8);
            if(pet.Fullness<=10 || play.Chao.Fun<=5 || !sleep.Asleep)throw new Exception($"Existing executors did not feed/play/sleep independently: {pet.Fullness}/{play.Chao.Fun}/{sleep.Asleep}");
            var innate=new[]{pet.Personality!.Copy(),play.Chao.Personality!.Copy(),sleep.Chao.Personality!.Copy()};
            Simulate(282);
            var states=new[]{pet,play.Chao,sleep.Chao};
            if(states.Where((s,i)=>!s.Personality!.Equals(innate[i])).Any())throw new Exception("Garden selection altered innate personality");
            if(states.Any(s=>s.GardenActivity!.Selections>120))throw new Exception("Garden intentions are thrashing");
            GD.Print("ACTIVITY_REAL_STABILITY: "+string.Join("; ",states.Select(s=>$"{s.Name}: selections={s.GardenActivity!.Selections}, interruptions={s.GardenActivity.Interruptions}, failure={s.GardenActivity.LastFailureReason}")));
            string directory=Path.GetDirectoryName(savePath)!;
            File.WriteAllText(Path.Combine(directory,"stability.json"),JsonSerializer.Serialize(states.Select(s=>new{s.Name,s.GardenActivity!.Selections,s.GardenActivity.Interruptions,s.GardenActivity.LastFailureReason,history=s.GardenActivity.History}).ToArray()));
            Fixture(State("Blocked food",5,80,80));
            var inaccessible=new FruitTree{IsFeeder=true,SingleUse=true};world.AddChild(inaccessible);inaccessible.Place(new(20.7f,.18f,14));
            var barrier=new StaticBody3D{Position=new(20.35f,1,14),CollisionLayer=1};world.AddChild(barrier);barrier.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.18f,2,2)}});
            MoveFriend(.02f);
            if(pet.GardenActivity!.SelectedActivity==ChaoActivity.Eat || pet.GardenActivity.Scores.Single(s=>s.Availability.Activity==ChaoActivity.Eat).Score!=0)throw new Exception("Food behind a physical wall was counted as reachable");
            Fixture(State("Full bed",80,5,80));friend.Position=new(5,0,6.1f);
            for(int i=0;i<4;i++)bedSpaces[i]=new ChaoRest();Simulate(5);
            if(!sleeping || !moriRest.GroundRest || pet.GardenActivity!.Selections!=1)throw new Exception("Occupied bed caused repeated claims instead of safe free rest");
            Fixture(State("Toy A",80,80,5),new GardenEggState{Id="toy-peer",Hatched=true,X=21.2f,Z=14,Chao=State("Toy B",80,80,5)});
            world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle).Position=new(20.6f,.02f,14);Simulate(.1f);
            var toyPeer=world.GetChildren().OfType<GardenEgg>().Single();
            if(new[]{pet.GardenActivity!.SelectedActivity,toyPeer.Chao!.GardenActivity!.SelectedActivity}.Count(a=>a==ChaoActivity.Play)!=1)throw new Exception("One free toy was reserved by two actors");
            Fixture(State("Edited player",80,80,5));world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle).Position=new(20.6f,.02f,14);Simulate(8);
            var performing=world.GetChildren().OfType<ToyCar>().FirstOrDefault(t=>t.PlayedBy(friend));
            if(performing==null)throw new Exception("Editor rebind fixture did not start a real toy interaction");
            var changed=SaveStore.Snapshot(pet);changed.Fun=10;changed.KeepCurrentRuntimeFrom(pet);pet=changed;RebindGardenExecutors("main",pet);Simulate(1);
            if(pet.Fun<=10 || !ReferenceEquals(performing.PlayState,pet))throw new Exception("Playing executor kept the obsolete PetState after an editor replacement");
            // Actual urgent interruption of an existing route, not a synthetic mover.
            Fixture(State("Walker",80,80,80));moriLeisure.BeginRoute(0);MoveFriend(.02f);pet.Energy=5;MoveFriend(.02f);
            if(moriLeisure.Active || pet.GardenActivity?.SelectedActivity!=ChaoActivity.Sleep)throw new Exception("Critical fatigue did not safely cancel optional leisure into rest");
            // Shared introduction owns both actors, and both receive real contact relief.
            Fixture(State("Social",80,80,80,90),new GardenEggState{Id="social-peer",Hatched=true,Color="White",X=21.2f,Z=14,Chao=State("Peer",80,80,80,80)});
            Simulate(.1f,true);
            var peer=world.GetChildren().OfType<GardenEgg>().Single();
            if(!introductions.Uses(friend) || !introductions.Uses(peer) || peer.Chao!.GardenActivity?.SelectedActivity!=ChaoActivity.Socialize)throw new Exception("Social start did not reserve both actors");
            Simulate(23,true);
            if(pet.SocialLink("main","social-peer")?.Introduced!=true || pet.SocialNeed>=85 || peer.Chao!.SocialNeed>=75)throw new Exception("Existing handshake did not finish with independent physical relief");
            // Cove has no bed/toys/food: use the same rest executor on supported dry ground.
            carrying=true;SwitchZone(GardenZone.CoastalCove);carrying=false;pet.Energy=5;friend.Position=CoastalCove.LandPoint(new(-8,0,-12));
            Simulate(5);
            if(!sleeping || !moriRest.GroundRest)throw new Exception("Coastal Cove did not reuse safe ground rest");
            var json=JsonSerializer.Serialize(pet);if(json.Contains("SelectedActivity"))throw new Exception("Activity history leaked into a save");
            Save();var loaded=SaveStore.Load(savePath,out _);if(loaded.Name!=pet.Name || !loaded.Personality!.Equals(pet.Personality))throw new Exception("Garden activity save changed identity or personality");
            carrying=true;SwitchZone(GardenZone.Garden);carrying=false;friend.Position=new(20,0,14);Simulate(.1f);RefreshNeedsDebug();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"activity-debug.png"));
            File.WriteAllText(Path.Combine(directory,"activity-scene-check.json"),JsonSerializer.Serialize(new{passed=true,actors=3,simulatedSeconds=300,foodPlaySleep=true,socialOwnership=true,criticalInterruption=true,coastalRest=true,personalityUnchanged=true,safeSave=true}));
            GD.Print("ACTIVITY_SCENE_OK: three live actors, actual food/play/sleep, 300s stability, atomic social pair, urgent route cancellation, Coastal ground rest, unchanged personality and save");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("ACTIVITY_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
