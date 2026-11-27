using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool expressionCheckStarted;
    private int expressionCheckFrames;
    private void RunExpressionIntegrationCheck()
    {if(expressionCheckStarted || ++expressionCheckFrames<3)return;expressionCheckStarted=true;CheckExpressionIntegration();}
    private async void CheckExpressionIntegration()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1601);GetWindow().Title="MoriGarden — GENERATED EXPRESSION INTEGRATION TEST";
            string directory=Path.GetDirectoryName(savePath)!;Directory.CreateDirectory(directory);
            void Check(bool ok,string why){if(!ok)throw new Exception(why);GD.Print("EXPRESSION_CHECK: "+why);}
            PetState State(string name,int energetic=0)=>new(){Name=name,Fullness=90,Energy=90,Fun=90,Joy=90,SocialNeed=20,Run=180,
                PersonalitySchemaVersion=1,Personality=new(){Energetic=energetic}};
            pet=State("Expression A",100);pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="expression-b",Hatched=true,Color="White",X=23,Z=14,Chao=State("Expression B",-100)});
            pet.GardenEggs.Add(new(){Id="expression-c",Hatched=true,Color="Yellow",X=26,Z=14,Chao=State("Expression C")});
            petZone=GardenZone.Garden;carrying=false;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
            friend.Position=new(20,0,14);player.Position=new(40,0,30);friend.Visible=true;chaoMotion.Reset();
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();
            var actors=new CharacterBody3D[]{friend}.Concat(residents).ToArray();var models=actors.Select(a=>a==friend ? (ImportedChao)friendArt : (ImportedChao)((GardenEgg)a).Art).ToArray();
            var states=new[]{pet}.Concat(residents.Select(e=>e.Chao!)).ToArray();var ids=new[]{"main"}.Concat(residents.Select(e=>e.Data.Id)).ToArray();
            for(int i=0;i<3;i++){models[i].SetPhysicsProcess(false);ImportedChao.ApplyDevelopment(models[i],states[i]);ImportedChao.BindPresentation(models[i],states[i],false,ids[i]);models[i].GetNode<EmotionOrb>("MoodOrb").WaitForPetting();}
            void Visual(int frames,bool walking=false)
            {
                for(int frame=0;frame<frames;frame++)for(int i=0;i<3;i++)
                {ImportedChao.MovePose(models[i],frame*.12f,walking,false,false,false,false,false);models[i]._PhysicsProcess(1f/60);}
            }
            async System.Threading.Tasks.Task Shot(string name)
            {
                UpdateUi();cameraRig.Distance=1.3f;cameraRig.Pitch=name=="Protected-Sleep" ? 1.1f : .02f;cameraRig.Yaw=friend.Rotation.Y;
                cameraRig.Follow(friend.Position-Vector3.Up*(name=="Protected-Sleep" ? .95f : .65f),0,true);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,name+".png"));
            }
            // No artificial presentation events: use the canonical mood reactions.
            states[0].Mood.React(ChaoEmotion.Happy,.7f,120,"same-happy",60);states[1].Mood.React(ChaoEmotion.Happy,.7f,120,"same-happy",60);states[2].Joy=20;
            string[] before=states.Select(s=>{s.EnsureStatisticsTree();s.EnsurePersonalityTree();SaveIntegrity.Validate(s);return JsonSerializer.Serialize(s);}).ToArray();
            var sameMood=states[0].Mood.Snapshot;Visual(120);
            Check(models[0].Expression==ChaoExpression.Happy && models[1].Expression==ChaoExpression.Happy && models[2].Expression==ChaoExpression.Sad,"three independent emotions visible in Idle");
            Check(sameMood==states[1].Mood.Snapshot && models[0].HappyWeight>models[1].HappyWeight+.1f,"same internal .7 Happy, different actual rendered smile weights from Energetic");
            for(int i=0;i<3;i++)if(before[i]!=JsonSerializer.Serialize(states[i])){var b=JsonNode.Parse(before[i])!.AsObject();var a=JsonSerializer.SerializeToNode(states[i])!.AsObject();GD.Print("EXPRESSION_STATE_DIFF "+i+": "+string.Join(',',a.Where(k=>!JsonNode.DeepEquals(k.Value,b[k.Key])).Select(k=>k.Key)));}
            Check(before.SequenceEqual(states.Select(s=>JsonSerializer.Serialize(s))),"visual frames preserve needs, traits, memories and all save data");
            float previous=models[0].HappyWeight;ImportedChao.MovePose(models[0],0,false,false,false,false,false,false);ImportedChao.Express(models[0],ChaoExpression.Neutral,.1f);models[0]._PhysicsProcess(1f/60);
            Check(models[0].HappyWeight>0 && models[0].HappyWeight<previous,"Neutral transition eases rather than snapping");
            Visual(90);Check(models[0].Expression==ChaoExpression.Happy && models[0].HappyWeight>previous-.01f,"expired explicit expression returns to current mood");
            var blinkFrames=new List<int>[] {new(),new(),new()};var blinking=new bool[3];
            for(int frame=0;frame<1200;frame++)
            {
                Visual(1);for(int i=0;i<3;i++){bool current=models[i].BlinkAmount>.1f;if(current && !blinking[i])blinkFrames[i].Add(frame);blinking[i]=current;}
            }
            // Strong happy lines intentionally suppress blinking; force neutral leases
            // for this independent-clock probe, not new personality blink formulas.
            for(int frame=0;frame<1200;frame++)for(int i=0;i<3;i++)
            {
                ImportedChao.MovePose(models[i],0,false,false,false,false,false,false);ImportedChao.Express(models[i],ChaoExpression.Neutral);models[i]._PhysicsProcess(1f/60);
                bool current=models[i].BlinkAmount>.1f;if(current && !blinking[i])blinkFrames[i].Add(1200+frame);blinking[i]=current;
            }
            Check(blinkFrames.All(f=>f.Count>=2) && blinkFrames.Select(f=>string.Join(',',f)).Distinct().Count()==3,"three stable-ID models retain independent blink clocks");
            var pictures=new Dictionary<string,ChaoExpressionDiagnostic>();
            foreach(var (emotion,name) in new[]{(ChaoEmotion.Neutral,"Neutral"),(ChaoEmotion.Happy,"Happy"),(ChaoEmotion.Sad,"Sad"),(ChaoEmotion.Curious,"Curious"),(ChaoEmotion.Sleepy,"Sleepy")})
            {
                var state=State(name);state.Joy=50;if(emotion!=ChaoEmotion.Neutral)state.Mood.React(emotion,.7f,120,"photo",60);
                ImportedChao.BindPresentation(models[0],state,false,"main");for(int f=0;f<120;f++){ImportedChao.MovePose(models[0],0,false,false,false,false,false,false);models[0]._PhysicsProcess(1f/60);}
                Check(models[0].Expression==ChaoExpressiveness.FromMood(state.Mood.Snapshot),"actual face mapping "+name);pictures[name]=models[0].ExpressionDiagnostics;await Shot(name);
            }
            ImportedChao.BindPresentation(models[0],pet,false,"main");MainActivity.Reset();MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);
            MainActivity.Coordinator.Adopt(ChaoActivity.Wander,"existing ChaoWander check",ActivityInterruptibility.LongOptional);wanderTime=100;wanderTarget=friend.Position+Vector3.Right*6;
            Vector3 start=friend.Position;int walking=0;
            for(int frame=0;frame<120;frame++)
            {
                MoveFriend(1f/60);models[0]._PhysicsProcess(1f/60);
                if(models[0].PoseKind==ChaoPoseKind.Walk){walking++;CheckWalk(models[0].Expression==ChaoExpression.Happy);}
            }
            void CheckWalk(bool ok){if(!ok)throw new Exception("Walk flickered away from current Happy");}
            Check(walking>60 && friend.Position.DistanceTo(start)>.5f,"actual existing Wander moves with Happy on every walking frame");await Shot("Walking-Happy");
            pet.ObserveEvent(ChaoStateEvent.UnpleasantContact,"expression-walk");float smile=models[0].HappyWeight;MoveFriend(1f/60);models[0]._PhysicsProcess(1f/60);
            Check(models[0].Expression==ChaoExpression.Annoyed && models[0].HappyWeight>0 && models[0].HappyWeight<smile && models[0].PoseKind==ChaoPoseKind.Walk,"ObserveEvent changes walking face smoothly without interrupting movement");
            var sad=State("Walking Sad");sad.Joy=20;ImportedChao.BindPresentation(models[0],sad,false,"main");
            for(int f=0;f<120;f++){ImportedChao.MovePose(models[0],f*.12f,true,false,false,false,false,false);models[0]._PhysicsProcess(1f/60);}
            Check(models[0].Expression==ChaoExpression.Sad,"walking sadness remains visible");await Shot("Walking-Sad");
            ImportedChao.BindPresentation(models[0],pet,false,"main");ImportedChao.BallReaction(models[0],1.3f,true,false,false);models[0]._PhysicsProcess(1f/60);
            Check(models[0].ExpressionDiagnostics.ProtectedAnimation && models[0].Expression!=ChaoExpression.Happy,"real scolding distress protects squint from high Joy");ImportedChao.ClearBallReaction(models[0]);
            // Real toy and handshake, including their pose/hand owners.
            pet.Fun=10;var toy=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);Check(toy.BeginPlay(friend,pet),"real rattle starts");
            for(int f=0;f<400;f++){toy.Advance(1f/60);toy.PlayPose(friend,friendArt);models[0]._PhysicsProcess(1f/60);}
            Check(!toy.PlayedBy(friend),"real rattle completes without face conflict");toy.CancelPlay(friend);ImportedChao.ClearActionPause(friendArt);
            friend.Position=new(20,0,14);residents[0].Position=new(21.2f,0,14);pet.SocialLinks.Clear();
            var pair=new[]{new ChaoIntroductions.Candidate("main",friend,friendArt,pet),new("expression-b",residents[0],residents[0].Art,states[1])};
            var greeting=new ChaoIntroductions();Check(greeting.TryStart(pet,pair[0],pair[1]),"actual introduction starts");bool protectedHands=false;
            for(int f=0;f<2000 && greeting.Active;f++){greeting.Advance(1f/60,pet,()=>pair,()=>{},false);foreach(var model in models)model._PhysicsProcess(1f/60);protectedHands|=models[0].PoseKind==ChaoPoseKind.Grip;}
            Check(protectedHands && pet.SocialLink("main","expression-b")?.Introduced==true,"actual handshake completes with protected grips");
            // Natural sleep through the old adapter/rest/wake path.
            pet.Energy=5;pet.Fullness=90;pet.Fun=100;pet.SocialNeed=0;friend.Position=ChaoRest.Bed+Vector3.Back*2.2f;MainActivity.Reset();moriRest.Cancel();chaoMotion.Reset();wanderTime=0;
            bool slept=false,rising=false,woke=false;int noBlink=0;
            for(int f=0;f<11000;f++)
            {
                const float dt=1f/60;pet.Tick(dt,sleeping);MoveFriend(dt);models[0]._PhysicsProcess(dt);
                if(sleeping){if(!slept){slept=true;await Shot("Protected-Sleep");}if(models[0].BlinkAmount==0 && models[0].Expression==ChaoExpression.Sleepy)noBlink++;}
                rising|=moriRising.Active;
                if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(slept && !sleeping && !moriRising.Active && !chaoMotion.Airborne && f>2200){woke=true;break;}
            }
            Check(slept && rising && woke && noBlink>1800,"real rest approach, protected closed eyes/no blink, minimum sleep and Rising preserved");
            var bridge=new ChaoEditorBridge(Path.Combine(directory,"expression-editor"));var roster=new Dictionary<string,PetState>{{"main",pet},{"expression-b",states[1]},{"expression-c",states[2]}};
            ImportedChao.MovePose(models[0],0,false,false,false,false,false,false);models[0]._PhysicsProcess(1f/60);float clock=models[0].PresentationClock,deadline=models[0].BlinkDeadline;
            bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"expression-editor/chao-editor-state.json")))!;
            File.WriteAllText(Path.Combine(directory,"expression-editor/chao-editor-request.json"),JsonSerializer.Serialize(new{session=snap["session"]!.GetValue<string>(),id="expression-rebind",target="main",patch=new{Fullness=89}}));
            var prior=pet;pet=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics)!;RebindGardenExecutors("main",pet);AdvanceChaoMemories(0);
            Check(!ReferenceEquals(prior,pet) && ReferenceEquals(models[0].PresentationState,pet) && models[0].PresentationClock==clock && models[0].BlinkDeadline==deadline,"actual editor rebind preserves presentation/blink clocks and binds current PetState");
            Check(pet.Mood.Snapshot==prior.Mood.Snapshot,"editor rebind retains current runtime mood");
            states[0]=pet;
            var innate=states.Select(s=>s.Personality!.Copy()).ToArray();for(int f=0;f<180*30;f++)
            {
                const float dt=1f/30;pet.Tick(dt,sleeping);foreach(var t in world.GetChildren().OfType<ToyCar>())t.Advance(dt);UpdateEggs(dt);MoveFriend(dt);AdvanceChaoMemories(dt);foreach(var model in models)model._PhysicsProcess(dt);
                if(f%300==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(models.Any(m=>!float.IsFinite(m.ExpressionDiagnostics.FinalVisualIntensity) || m.BlinkAmount is <0 or >1))throw new Exception("Nonfinite/unbounded face during three-Chao stability");
            }
            Check(states.Select((s,i)=>s.Personality!.Equals(innate[i])).All(v=>v) && states.All(s=>s.GardenActivity?.Selections>0),"three actual Chao remain active and preserve innate traits for 180 seconds");
            Save();var restored=SaveStore.Load(savePath,out _);Check(restored.GardenEggs.Count==2 && restored.Personality!.Equals(pet.Personality),"save/load needs no expression schema and retains pet identities");
            File.WriteAllText(Path.Combine(directory,"expression-integration.json"),JsonSerializer.Serialize(new{passed=true,pictures,blinkFrames,actualWalkFrames=walking,actualSleep=true,realToy=true,realHandshake=true,editorRebind=true,threeChaoSeconds=180,diagnostics=ExpressionDiagnostics}));
            GD.Print("EXPRESSION_INTEGRATION_OK");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("EXPRESSION_INTEGRATION_FAILED: "+error);GetTree().Quit(1);}
    }
}
