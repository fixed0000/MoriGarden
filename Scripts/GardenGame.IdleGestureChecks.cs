using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool idleCheckStarted;
    private int idleCheckFrames;
    private void RunIdleGestureChecks(){if(idleCheckStarted || ++idleCheckFrames<3)return;idleCheckStarted=true;CheckIdleGestureScene();}
    private async void CheckIdleGestureScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1602);GetWindow().Title="MoriGarden — GENERATED IDLE GESTURE TEST";
            string directory=Path.GetDirectoryName(savePath)!;Directory.CreateDirectory(directory);
            void Check(bool ok,string why){if(!ok)throw new Exception(why);GD.Print("IDLE_CHECK: "+why);}
            PetState State(string name,ChaoPersonality p)=>new(){Name=name,Fullness=90,Energy=90,Fun=90,Joy=50,SocialNeed=20,Run=180,Personality=p,PersonalitySchemaVersion=1};
            pet=State("Curious",new(){Curious=100});pet.GardenEggs.Clear();
            pet.GardenEggs.Add(new(){Id="idle-energetic",Hatched=true,Color="White",X=22,Z=14,Chao=State("Energetic",new(){Energetic=100})});
            pet.GardenEggs.Add(new(){Id="idle-carefree",Hatched=true,Color="Yellow",X=24,Z=14,Chao=State("Carefree",new(){Carefree=100})});
            pet.GardenEggs.Add(new(){Id="idle-solitary",Hatched=true,Color="Purple",X=26,Z=14,Chao=State("Solitary",new(){Solitude=100})});
            petZone=GardenZone.Garden;carrying=false;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);friend.Visible=true;player.Position=new(40,0,30);chaoMotion.Reset();
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();var actors=new CharacterBody3D[]{friend}.Concat(residents).ToArray();
            var models=new[]{(ImportedChao)friendArt}.Concat(residents.Select(e=>(ImportedChao)e.Art)).ToArray();var states=new[]{pet}.Concat(residents.Select(e=>e.Chao!)).ToArray();
            var ids=new[]{"main"}.Concat(residents.Select(e=>e.Data.Id)).ToArray();
            for(int i=0;i<4;i++){models[i].SetPhysicsProcess(false);ImportedChao.ApplyDevelopment(models[i],states[i]);ImportedChao.BindPresentation(models[i],states[i],false,ids[i]);models[i].GetNode<EmotionOrb>("MoodOrb").WaitForPetting();}
            IdleGestureDiagnostic D(int i)=>models[i].IdleGestureDiagnostics ?? throw new Exception("No bound idle controller: "+ids[i]);
            void Visual(int frames=1)
            {for(int f=0;f<frames;f++)for(int i=0;i<4;i++){ImportedChao.MovePose(models[i],0,false,false,false,false,false,false);models[i]._PhysicsProcess(1f/60);}}
            async Task Shot(string name,int index=0)
            {
                UpdateUi();cameraRig.Distance=1.3f;cameraRig.Pitch=.02f;cameraRig.Yaw=actors[index].Rotation.Y;
                cameraRig.Follow(actors[index].Position-Vector3.Up*.65f,0,true);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,name+".png"));
            }
            Visual();pet.EnsureStatisticsTree();pet.EnsurePersonalityTree();SaveIntegrity.Validate(pet);string visualBefore=JsonSerializer.Serialize(pet);
            await Shot("Neutral-Idle");var positions=actors.Select(a=>a.Position).ToArray();var footStart=models.Select(m=>new[]{"FootL","FootR"}.Select(n=>((Node3D)m.FindChild(n,true,false)).GlobalPosition).ToArray()).ToArray();
            var wanted=new[]{"LookAround","PawFidget","GentleSway","None"};var photographed=new bool[4];var sequences=Enumerable.Range(0,4).Select(_=>new List<string>()).ToArray();var lastStarted=new int[4];
            float maxFootShift=0;float maxHeadStep=0;var lastHead=models.Select(m=>((Node3D)m.FindChild("HeadPivot",true,false)).Quaternion).ToArray();
            for(int f=0;f<180*60;f++)
            {
                Visual();
                for(int i=0;i<4;i++)
                {
                    var d=D(i);
                    if(d.Started!=lastStarted[i]){sequences[i].Add(d.CurrentIdleGesture+"@"+models[i].PresentationClock.ToString("0.000"));lastStarted[i]=d.Started;}
                    if(!photographed[i] && (i==3 ? d.SolitudePoseAdjustment>.039f : d.CurrentIdleGesture==wanted[i] && d.GestureElapsed/d.GestureDuration is >.30 and <.65))
                    {photographed[i]=true;await Shot(new[]{"Curious-LookAround","Energetic-PawFidget","Carefree-GentleSway","Solitude-Posture"}[i],i);}
                    int foot=0;foreach(string name in new[]{"FootL","FootR"})maxFootShift=Math.Max(maxFootShift,((Node3D)models[i].FindChild(name,true,false)).GlobalPosition.DistanceTo(footStart[i][foot++]));
                    var head=((Node3D)models[i].FindChild("HeadPivot",true,false)).Quaternion;maxHeadStep=Math.Max(maxHeadStep,lastHead[i].AngleTo(head));lastHead[i]=head;
                    if(actors[i].Position!=positions[i])throw new Exception("Decorative layer moved CharacterBody3D");
                }
                if(f%300==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            Check(photographed.All(v=>v) && sequences.Select(s=>string.Join(',',s)).Distinct().Count()==4,"180s four actual models show independent profile-specific gestures");
            Check(maxFootShift<.0001f && maxHeadStep<.1f,"sway preserves planted feet and head changes stay smooth");
            Check(visualBefore==JsonSerializer.Serialize(pet),"180s visual-only frames do not change needs, mood, learning, memories or save data");
            // Two identical models: same baseline blink/head stream, one zero layer.
            var driftActors=new[]{new CharacterBody3D{Position=new(30,0,24)},new CharacterBody3D{Position=new(30,0,24)}};
            var driftModels=new ImportedChao[2];
            for(int i=0;i<2;i++){world.AddChild(driftActors[i]);var s=State("Drift",new(){Curious=100,Energetic=100,Carefree=100,Solitude=100});driftModels[i]=(ImportedChao)GardenEgg.BuildChaoArt(driftActors[i],s,"Blue");driftModels[i].SetPhysicsProcess(false);ImportedChao.ApplyDevelopment(driftModels[i],s);ImportedChao.BindPresentation(driftModels[i],s,false,"same-drift-id");ImportedChao.MovePose(driftModels[i],0,false,false,false,false,false,false);}
            driftModels[1].IdleGestureWeight=0;float drift=0;bool identicalBlink=true;
            for(int f=0;f<10000;f++)
            {
                foreach(var m in driftModels)m._PhysicsProcess(1f/60);
                identicalBlink&=driftModels[0].BlinkAmount==driftModels[1].BlinkAmount;driftModels[0].RestoreIdleGestureOffsets();
                foreach(string name in new[]{"PaddleL","PaddleR","FootL","FootR","HeadPivot"})
                {var a=(Node3D)driftModels[0].FindChild(name,true,false);var b=(Node3D)driftModels[1].FindChild(name,true,false);drift=Math.Max(drift,a.Position.DistanceTo(b.Position)+a.Basis.X.DistanceTo(b.Basis.X)+a.Basis.Y.DistanceTo(b.Basis.Y)+a.Basis.Z.DistanceTo(b.Basis.Z));}
                var bodyA=driftModels[0].GetChild<Node3D>(0).Transform;var bodyB=driftModels[1].GetChild<Node3D>(0).Transform;
                drift=Math.Max(drift,bodyA.Origin.DistanceTo(bodyB.Origin)+bodyA.Basis.X.DistanceTo(bodyB.Basis.X)+bodyA.Basis.Y.DistanceTo(bodyB.Basis.Y)+bodyA.Basis.Z.DistanceTo(bodyB.Basis.Z));
                if(f%500==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            }
            GD.Print($"IDLE_DRIFT: maximum matrix error={drift:R}, unchanged blink={identicalBlink}");
            Check(drift<.00001f && identicalBlink,"10000 frames without new Pose writes: zero transform drift and unchanged old blinking stream");
            for(int f=0;f<12000 && (driftModels[0].IdleGestureDiagnostics!.CurrentIdleGesture=="None" || driftModels[0].IdleGestureDiagnostics!.CurrentGestureAmplitude<.01f);f++)foreach(var m in driftModels)m._PhysicsProcess(1f/60);
            var clip=SportMotions.Find("karate_idle")!;foreach(var m in driftModels){ImportedChao.PrepareSportRig(m);ImportedChao.ApplySportMotion(m,clip,.2f);m._PhysicsProcess(1f/60);}
            foreach(string n in new[]{"PaddleL","PaddleR","FootL","FootR","HeadPivot"}){var a=(Node3D)driftModels[0].FindChild(n,true,false);var b=(Node3D)driftModels[1].FindChild(n,true,false);GD.Print($"IDLE_SPORT_COMPARE {n}: equal={a.Transform.IsEqualApprox(b.Transform)}, scale {a.Scale} / {b.Scale}, pos {a.Position.DistanceTo(b.Position):R}, basis {a.Basis.X.DistanceTo(b.Basis.X)+a.Basis.Y.DistanceTo(b.Basis.Y)+a.Basis.Z.DistanceTo(b.Basis.Z):R}");}
            Check(new[]{"PaddleL","PaddleR","FootL","FootR","HeadPivot"}.All(n=>((Node3D)driftModels[0].FindChild(n,true,false)).Transform.IsEqualApprox(((Node3D)driftModels[1].FindChild(n,true,false)).Transform)) && driftModels[0].IdleGestureDiagnostics!.CurrentIdleGesture=="None","sports rig initialization and clip application exclude active decorative offsets");
            foreach(var a in driftActors)a.QueueFree();
            // Same actual visual layer at three update rates, aligned at the same initial visual time.
            var fpsSamples=new List<IdleGestureDiagnostic[]>();
            foreach(int fps in new[]{30,60,120})
            {
                var body=new CharacterBody3D{Position=new(30,0,24)};world.AddChild(body);var state=State("FPS",new(){Curious=100,Energetic=100,Carefree=100,Solitude=100});
                var model=(ImportedChao)GardenEgg.BuildChaoArt(body,state,"Blue");model.SetPhysicsProcess(false);ImportedChao.ApplyDevelopment(model,state);ImportedChao.BindPresentation(model,state,false,"same-fps-id");ImportedChao.MovePose(model,0,false,false,false,false,false,false);model._PhysicsProcess(.001);
                var samples=new List<IdleGestureDiagnostic>();
                for(int f=1;f<=fps*60;f++){ImportedChao.MovePose(model,0,false,false,false,false,false,false);model._PhysicsProcess(1d/fps);if(f%(fps/2)==0)samples.Add(model.IdleGestureDiagnostics!);if(f%(fps*5)==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
                fpsSamples.Add(samples.ToArray());body.QueueFree();
            }
            float fpsError=0;for(int i=1;i<3;i++)for(int sample=0;sample<120;sample++)
            {
                var a=fpsSamples[0][sample];var b=fpsSamples[i][sample];if(a.CurrentIdleGesture!=b.CurrentIdleGesture)throw new Exception("FPS changed gesture sequence");
                fpsError=Math.Max(fpsError,Math.Max(Math.Abs(a.CurrentGestureAmplitude-b.CurrentGestureAmplitude),Math.Abs(a.SolitudePoseAdjustment-b.SolitudePoseAdjustment)));
            }
            Check(fpsError<.001f,"actual 30/60/120 FPS layer stays phase/amplitude equivalent: "+fpsError.ToString("R"));
            // A real interaction removes the nonzero Solitude posture as well.
            var solitaryToy=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);
            Check(solitaryToy.BeginPlay(actors[3],states[3]),"real Solitude actor interaction starts");solitaryToy.Advance(1f/60);solitaryToy.PlayPose(actors[3],models[3]);models[3]._PhysicsProcess(1f/60);
            Check(D(3).SolitudePoseAdjustment==0 && D(3).Suppressed,"nonzero Solitude posture yields to actual item grip");solitaryToy.CancelPlay(actors[3]);ImportedChao.ClearActionPause(models[3]);
            for(int f=0;f<12000 && D(0).CurrentIdleGesture!="LookAround";f++)Visual();
            Check(D(0).CurrentIdleGesture=="LookAround","actual LookAround available for attention priority check");
            ImportedChao.Attend(models[0],player.Position+Vector3.Up,100,.5f);models[0]._PhysicsProcess(1f/60);
            Check(D(0).CurrentIdleGesture!="LookAround" && Mathf.Abs(((Node3D)models[0].FindChild("HeadPivot",true,false)).Rotation.Y)<.551f,"real player Attend cancels decorative head scan and keeps original neck limits");
            // Pause and editor replacement while a real visual gesture is active.
            for(int f=0;f<12000 && D(0).CurrentIdleGesture=="None";f++)Visual();
            var active=D(0);Check(active.CurrentIdleGesture!="None","actual gesture active before pause/rebind");
            paused=true;_PhysicsProcess(1f/60);for(int f=0;f<120;f++)models[0]._PhysicsProcess(1f/60);
            Check(D(0).GestureElapsed==active.GestureElapsed,"pause freezes actual gesture elapsed time");paused=false;PauseIdleGesturePresentations(false);
            var bridge=new ChaoEditorBridge(Path.Combine(directory,"idle-editor"));var roster=new Dictionary<string,PetState>{{"main",pet}};foreach(var e in pet.GardenEggs)roster[e.Id]=e.Chao!;
            bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"idle-editor/chao-editor-state.json")))!;
            File.WriteAllText(Path.Combine(directory,"idle-editor/chao-editor-request.json"),JsonSerializer.Serialize(new{session=snap["session"]!.GetValue<string>(),id="idle-rebind",target="main",patch=new{Fullness=89}}));
            pet=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics)!;states[0]=pet;RebindGardenExecutors("main",pet);AdvanceChaoMemories(0);
            Check(ReferenceEquals(models[0].PresentationState,pet) && D(0).CurrentIdleGesture==active.CurrentIdleGesture && D(0).GestureElapsed==active.GestureElapsed,"actual editor rebind retains current gesture and reads replacement state");
            var toy=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);pet.Fun=10;Check(toy.BeginPlay(friend,pet),"real Play interrupts active microgesture");toy.Advance(1f/60);toy.PlayPose(friend,friendArt);models[0]._PhysicsProcess(1f/60);
            Check(D(0).CurrentIdleGesture=="None" && D(0).SolitudePoseAdjustment==0,"real toy grip has no residual microgesture or posture");await Shot("Gesture-Cancellation");await Shot("Protected-Interaction");
            for(int f=0;f<400;f++){toy.Advance(1f/60);toy.PlayPose(friend,friendArt);models[0]._PhysicsProcess(1f/60);}toy.CancelPlay(friend);ImportedChao.ClearActionPause(friendArt);
            friend.Position=new(20,0,14);residents[0].Position=new(21.2f,0,14);pet.SocialLinks.Clear();var pair=new[]{new ChaoIntroductions.Candidate("main",friend,friendArt,pet),new("idle-energetic",residents[0],residents[0].Art,states[1])};var intro=new ChaoIntroductions();Check(intro.TryStart(pet,pair[0],pair[1]),"real introduction starts");bool sawGrip=false;
            for(int f=0;f<2000 && intro.Active;f++){intro.Advance(1f/60,pet,()=>pair,()=>{},false);foreach(var m in models)m._PhysicsProcess(1f/60);if(models[0].PoseKind==ChaoPoseKind.Grip){sawGrip=true;if(D(0).SolitudePoseAdjustment!=0)throw new Exception("Gesture leaked into handshake");}}
            Check(sawGrip && pet.SocialLink("main","idle-energetic")?.Introduced==true,"original handshake completes with sole hand ownership");
            pet.Energy=5;pet.Fullness=90;pet.Fun=100;pet.SocialNeed=0;friend.Position=ChaoRest.Bed+Vector3.Back*2.2f;MainActivity.Reset();moriRest.Cancel();chaoMotion.Reset();wanderTime=0;bool slept=false,rose=false,woke=false;
            for(int f=0;f<11000;f++)
            {const float dt=1f/60;pet.Tick(dt,sleeping);MoveFriend(dt);models[0]._PhysicsProcess(dt);slept|=sleeping;rose|=moriRising.Active;if(sleeping && (D(0).CurrentIdleGesture!="None" || D(0).SolitudePoseAdjustment!=0))throw new Exception("Gesture affected actual sleep");if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(slept && !sleeping && !moriRising.Active && !chaoMotion.Airborne && f>2200){woke=true;break;}}
            Check(slept && rose && woke,"natural ChaoRest approach, sleep and Rising fully own visual pose");
            for(int f=0;f<180*30;f++)
            {const float dt=1f/30;pet.Tick(dt,sleeping);_PhysicsProcess(dt);foreach(var m in models)m._PhysicsProcess(dt);if(f%300==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(states.All(s=>s.GardenActivity?.Selections>0) && states.Count(s=>s.GardenActivity?.History.Count>0)>=3,"four autonomous garden actors continue ordinary selection for another 180s");
            string json=JsonSerializer.Serialize(pet);Check(!json.Contains("CurrentGesture") && !json.Contains("GestureSeed") && !json.Contains("IdleGestures"),"visual controller is absent from saved JSON");Save();var loaded=SaveStore.Load(savePath,out _);Check(loaded.GardenEggs.Select(e=>e.Id).SequenceEqual(pet.GardenEggs.Select(e=>e.Id)) && loaded.Personality!.Equals(pet.Personality),"save/load retains stable actors without gesture migration");
            File.WriteAllText(Path.Combine(directory,"idle-gesture-check.json"),JsonSerializer.Serialize(new{passed=true,sequences,maxFootShift,maxHeadStep,drift,identicalBlink,fpsError,controlledSeconds=180,autonomousSeconds=180,diagnostics=ExpressionDiagnostics}));
            GD.Print("IDLE_GESTURE_SCENE_OK");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("IDLE_GESTURE_SCENE_FAILED: "+ex);GetTree().Quit(1);}
    }
}
