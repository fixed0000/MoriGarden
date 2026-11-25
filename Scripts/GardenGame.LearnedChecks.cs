using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int learnedCheckFrame;
    private bool learnedCheckStarted;
    private void RunLearnedCheckMode()
    {if(learnedCheckStarted || ++learnedCheckFrame<3)return;learnedCheckStarted=true;CheckLearnedScene();}
    private async void CheckLearnedScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1500);Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            GetWindow().Title="MoriGarden — GENERATED LEARNED PREFERENCES TEST";
            PetState State(string name)=>new(){Name=name,Run=180,Fullness=35,Energy=80,Fun=30,Joy=65,SocialNeed=35,
                PersonalitySchemaVersion=1,Personality=new(),FavoriteToy="Rattle",FavoriteInstrument="Flute",FavoriteFruit=FruitKinds.Blue,LikesToyCars=false};
            pet=State("Learner A");pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="learned-flute",Hatched=true,Color="White",X=23,Z=14,Chao=State("Learner B")});
            pet.GardenEggs.Add(new(){Id="learned-neutral",Hatched=true,Color="Yellow",X=26,Z=14,Chao=State("Learner C")});
            petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
            friend.Position=new(20,0,14);friend.Visible=true;player.Position=new(20,0,19);
            var peers=world.GetChildren().OfType<GardenEgg>().OrderBy(e=>e.Data.Id).ToArray();
            var flutePeer=peers.Single(e=>e.Data.Id=="learned-flute");var neutralPeer=peers.Single(e=>e.Data.Id=="learned-neutral");
            CharacterBody3D[] bodies={friend,flutePeer,neutralPeer};Node3D[] arts={friendArt,flutePeer.Art,neutralPeer.Art};
            PetState[] states={pet,flutePeer.Chao!,neutralPeer.Chao!};var innate=states.Select(s=>s.Personality!.Copy()).ToArray();
            ToyCar Toy(bool flute,Vector3 at){var t=new ToyCar{IsFlute=flute,IsRattle=!flute};world.AddChild(t);t.Position=at;return t;}
            var rattle=Toy(false,friend.Position+Vector3.Right*.6f);var flute=Toy(true,flutePeer.Position+Vector3.Right*.6f);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            void Play(ToyCar toy,int i)
            {
                states[i].Fun=5;toy.Advance(20);toy.Position=bodies[i].Position+Vector3.Right*.6f;
                if(!toy.BeginPlay(bodies[i],states[i]))throw new Exception("Existing play executor did not start");
                for(float t=0;t<toy.PlayDuration+.6f;t+=.05f){states[i].Tick(.05f,false);toy.Advance(.05f);if(toy.PlayedBy(bodies[i]))toy.PlayPose(bodies[i],arts[i]);}
            }
            for(int n=0;n<6;n++)
            {states[0].History.Advance(45);Play(rattle,0);states[1].History.Advance(45);Play(flute,1);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            if(states[0].LearnedPreferences.ToyPreferences["rattle"].ExperienceCount!=6 || states[1].LearnedPreferences.ToyPreferences["flute"].ExperienceCount!=6 || states[2].LearnedPreferences.ToyPreferences.Count!=0)
                throw new Exception("Actual completed sessions did not teach independent preferences");
            double commonTime=states.Max(s=>s.History.Seconds);
            foreach(var state in states){state.History.Advance((float)(commonTime-state.History.Seconds));state.Fullness=35;state.Energy=80;state.Fun=30;state.Joy=65;state.SocialNeed=35;}
            rattle.Advance(20);flute.Advance(20);
            var adapters=new[]{MainActivity,flutePeer.ActivityAdapter,neutralPeer.ActivityAdapter};
            for(int i=0;i<3;i++)adapters[i].Begin(0,states[i],ActivityContext,false,false,false,false);
            int[,] counts=new int[3,2];
            string beforeRanking=JsonSerializer.Serialize(states.Select(s=>s.LearnedPreferences).ToArray());
            void ChooseTrial(int actor,int seed)
            {
                var random=new Random(seed);float left=.5f+(float)random.NextDouble()*.12f,right=.5f+(float)random.NextDouble()*.12f;
                rattle.Position=bodies[actor].Position+Vector3.Right*left;flute.Position=bodies[actor].Position+Vector3.Left*right;
                var interest=new ChaoInterests();var props=seed%2==0 ? new[]{rattle,flute} : new[]{flute,rattle};
                if(!interest.BeginSelected(ChaoActivity.Play,bodies[actor],states[actor],Array.Empty<FruitTree>(),props,adapters[actor].CanUseTarget))throw new Exception("Controlled targets were unavailable");
                counts[actor,interest.Target==rattle ? 0 : 1]++;interest.Cancel(bodies[actor],arts[actor]);
            }
            for(int seed=0;seed<200;seed++)for(int i=0;i<3;i++)ChooseTrial(i,seed+5100);
            if(counts[0,0]<180 || counts[1,1]<180 || counts[2,0]<50 || counts[2,1]<50 || beforeRanking!=JsonSerializer.Serialize(states.Select(s=>s.LearnedPreferences).ToArray()))
                throw new Exception($"Controlled target comparison failed: {counts[0,0]}/{counts[1,1]}/{counts[2,0]}/{counts[2,1]}");
            // A real wall/occupied toy must still win over acquired preference.
            rattle.Position=friend.Position+Vector3.Right*.7f;flute.Position=friend.Position+Vector3.Left*.7f;
            var wall=new StaticBody3D{Position=friend.Position+new Vector3(.35f,.7f,0),CollisionLayer=1};world.AddChild(wall);
            wall.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.12f,1.4f,1.4f)}});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var blocked=new ChaoInterests();blocked.BeginSelected(ChaoActivity.Play,friend,pet,Array.Empty<FruitTree>(),new[]{rattle,flute},MainActivity.CanUseTarget);
            if(blocked.Target!=flute)throw new Exception("Preference bypassed a real collision wall");blocked.Cancel(friend,friendArt);wall.Free();
            int credited=pet.LearnedPreferences.ToyPreferences["rattle"].ExperienceCount;pet.History.Advance(60);pet.Fun=5;rattle.Position=friend.Position+Vector3.Right*.6f;
            rattle.BeginPlay(friend,pet);pet.Tick(1,false);rattle.Advance(1);rattle.CancelPlay(friend);rattle.Advance(30);
            if(pet.LearnedPreferences.ToyPreferences["rattle"].ExperienceCount!=credited)throw new Exception("Cancelled play was credited");
            // Natural group finish credits each performer once; forced group
            // cleanup must not manufacture success for the other instrument.
            var lead=Toy(true,friend.Position);var second=new ToyCar{IsDrums=true};world.AddChild(second);second.Position=flutePeer.Position;
            pet.History.Advance(100);states[1].History.Advance(100);pet.Fun=states[1].Fun=5;
            lead.BeginPlay(friend,pet);second.BeginPlay(flutePeer,states[1]);second.StartWith(lead);lead.Advance(61);second.Advance(1);
            if(pet.LearnedPreferences.ToyPreferences["flute"].ExperienceCount!=1 || states[1].LearnedPreferences.ToyPreferences["drums"].ExperienceCount!=1)throw new Exception("Normal ensemble completion did not credit both performers");
            pet.History.Advance(100);states[1].History.Advance(100);pet.Fun=states[1].Fun=5;
            lead.BeginPlay(friend,pet);second.BeginPlay(flutePeer,states[1]);second.StartWith(lead);lead.Advance(1);lead.CancelPlay(friend);second.Advance(1);
            if(pet.LearnedPreferences.ToyPreferences["flute"].ExperienceCount!=1 || states[1].LearnedPreferences.ToyPreferences["drums"].ExperienceCount!=1)throw new Exception("Forced ensemble cleanup taught preferences");
            var disappearing=new CharacterBody3D{Position=friend.Position};world.AddChild(disappearing);var lostState=State("Gone");var lostToy=Toy(true,friend.Position);
            lostToy.BeginPlay(disappearing,lostState);disappearing.Free();lostToy.Advance(61);
            if(lostState.LearnedPreferences.ToyPreferences.Count!=0)throw new Exception("A disappeared actor earned play credit");
            var beach=new ToyCar{IsBeachBall=true};world.AddChild(beach);pet.Fun=5;beach.Position=friend.Position;
            beach.BeginPlay(friend,pet);var solo=new ChaoBallGame();var candidate=new ChaoIntroductions.Candidate("main",friend,friendArt,pet);
            for(int n=0;n<180;n++){pet.Tick(.05f,false);solo.Advance(.05f,pet,new[]{candidate},Array.Empty<ChaoIntroductions.Candidate>(),new[]{beach});}
            if(pet.LearnedPreferences.ToyPreferences["beach-ball"].ExperienceCount!=1)throw new Exception("Natural solo ball completion did not teach the ball type");
            FruitTree Food(string kind){var f=new FruitTree{IsFeeder=true,SingleUse=true,FruitKind=kind};world.AddChild(f);f.Place(friend.Position+Vector3.Right*.4f);return f;}
            void Eat(FruitTree food,FruitMeal meal)
            {for(int i=0;i<1200 && meal.Remaining>0;i++){pet.Tick(.05f,false);meal.Advance(.05f,food,pet,friendArt,friend.Position+Vector3.Up*.25f);}}
            var food=Food(FruitKinds.Garden);pet.Fullness=20;var meal=new FruitMeal();meal.Begin(food,pet,food.FruitPosition);Eat(food,meal);
            if(food.Bites!=4 || pet.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount!=1)throw new Exception("A natural meal did not credit exactly once");
            for(int n=0;n<100;n++)meal.Advance(.01f,food,pet,friendArt,friend.Position+Vector3.Up*.25f);
            if(pet.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount!=1)throw new Exception("A completed meal replayed credit per frame");
            pet.History.Advance(60);food=Food(FruitKinds.Garden);pet.Fullness=96;meal=new();meal.Begin(food,pet,food.FruitPosition);Eat(food,meal);
            if(!meal.Rejected || food.Bites!=1 || pet.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount!=2)throw new Exception("Partial consumption stopped by fullness was not recognized");
            pet.LearnedPreferences.FruitPreferences[FruitKinds.Red]=new(){Bias=.30f,ExperienceCount=10};
            food=Food(FruitKinds.Red);pet.Fullness=20;meal=new();meal.Begin(food,pet,food.FruitPosition);Eat(food,meal);
            if(!meal.RejectedForTaste || food.Bites!=0 || pet.LearnedPreferences.FruitPreferences[FruitKinds.Red].ExperienceCount!=10)throw new Exception("Acquired preference bypassed fixed special-fruit taste");
            // The same target gate excludes incompatible fruit before an approach.
            var fruitChoice=new ChaoInterests();if(fruitChoice.BeginSelected(ChaoActivity.Eat,friend,pet,new[]{food},Array.Empty<ToyCar>(),_=>true))throw new Exception("Taste-incompatible fruit entered Eat targets");
            var missedFood=Food(FruitKinds.Moonblossom);var aborted=new ChaoInterests();aborted.BeginSelected(ChaoActivity.Eat,friend,pet,new[]{missedFood},Array.Empty<ToyCar>(),_=>true);aborted.Cancel(friend,friendArt);
            if(pet.LearnedPreferences.FruitPreferences.ContainsKey(FruitKinds.Moonblossom))throw new Exception("An unconsumed approach taught a fruit preference");
            if(states.Where((s,i)=>!s.Personality!.Equals(innate[i])).Any() || states.Any(s=>s.FavoriteToy!="Rattle" || s.FavoriteInstrument!="Flute" || s.FavoriteFruit!=FruitKinds.Blue || s.LikesToyCars))throw new Exception("Experience changed fixed/innate traits");
            Save();var loaded=SaveStore.Load(savePath,out _);
            if(loaded.GardenEggs.Count!=2 || loaded.GardenEggs[0].Id!="learned-flute" || loaded.LearnedPreferences.ToyPreferences["rattle"].ExperienceCount!=credited || loaded.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount!=2)throw new Exception("Learned save round-trip lost real Chao data");
            var afterLoad=new ChaoInterests();loaded.Fun=30;rattle.Advance(20);rattle.Position=friend.Position+Vector3.Right*.6f;flute.Position=friend.Position+Vector3.Left*.6f;
            if(!afterLoad.BeginSelected(ChaoActivity.Play,friend,loaded,Array.Empty<FruitTree>(),new[]{flute,rattle},MainActivity.CanUseTarget) || afterLoad.Target!=rattle)throw new Exception("Loaded experience did not affect the real target chooser");afterLoad.Cancel(friend,friendArt);
            string directory=Path.GetDirectoryName(savePath)!;
            var results=states.Select((s,i)=>new{s.Name,Rattle=counts[i,0],Flute=counts[i,1],Preferences=s.LearnedPreferenceDiagnostics()}).ToArray();
            File.WriteAllText(Path.Combine(directory,"learned-check.json"),JsonSerializer.Serialize(new{passed=true,decisionsPerChao=200,results},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("LEARNED_TARGET_COUNTS: "+string.Join("; ",results.Select(r=>$"{r.Name} rattle={r.Rattle}, flute={r.Flute}")));
            cameraRig.Distance=8;cameraRig.Follow(friend.Position,0,true);RefreshNeedsDebug();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"learned-check.png"));
            GD.Print("LEARNED_SCENE_OK: actual completed play and eating, cancellations/refusal, partial meal, 3 independent identical-personality Chao, controlled target trials, wall, save/load and target selection after load");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("LEARNED_SCENE_FAILED: "+ex);GetTree().Quit(1);}
    }
}
