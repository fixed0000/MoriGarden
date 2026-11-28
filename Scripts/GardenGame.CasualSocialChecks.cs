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
    private bool casualCheckStarted;
    private int casualCheckFrames;
    private void RunCasualSocialChecks(){if(casualCheckStarted || ++casualCheckFrames<3)return;casualCheckStarted=true;CheckCasualSocialScene();}
    private async void CheckCasualSocialScene()
    {
        try
        {
            SetPhysicsProcess(false);string directory=Path.GetDirectoryName(savePath)!;Directory.CreateDirectory(directory);string input=OS.GetEnvironment("MORI_CASUAL_INPUT");
            if(!File.Exists(input))throw new Exception("An isolated verified input copy is required.");
            ChaoIntroductions.Candidate[] actors=Array.Empty<ChaoIntroductions.Candidate>();double fixtureTime=20000;int checks=0;
            void Check(bool value,string text){if(!value)throw new Exception(text);checks++;GD.Print("CASUAL_SOCIAL: "+text);}
            async Task Fixture(bool known=true)
            {
                casualSocial.Cancel();introductions.Cancel();ballGame.Cancel();musicGroup.Cancel();
                pet=SaveStore.Load(input,out _);fixtureTime+=400;pet.GardenSeconds=fixtureTime;
                foreach(var s in new[]{pet}.Concat(pet.GardenEggs.Where(e=>e.Chao!=null).Select(e=>e.Chao!)))
                {s.Energy=80;s.Fullness=80;s.Fun=80;s.SocialNeed=90;s.Joy=65;s.Run=180;}
                mainActivity=null;carrying=false;callTime=0;petZone=GardenZone.Garden;SwitchZone(GardenZone.Garden,restoring:true);
                player.Position=new(40,0,30);friend.Position=new(20,0,14);friend.Visible=true;chaoMotion.Reset();
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                actors=BallParticipants().OrderBy(c=>c.Id=="main" ? "" : c.Id,StringComparer.Ordinal).ToArray();
                Vector3[] at={new(20,0,14),new(21.3f,0,14),new(25,0,14),new(26.3f,0,14)};
                for(int i=0;i<actors.Length;i++)
                {
                    actors[i].Actor.Position=at[i];ImportedChao.StandForAction(actors[i].Art);ImportedChao.MovePose(actors[i].Art,0,false,false,false,false,false,false);
                    AdapterFor(actors[i].Actor).Begin(0,actors[i].Pet,ActivityContext,false,false,false,false);AdapterFor(actors[i].Actor).Coordinator.Suspend("");
                }
                for(int i=0;i<actors.Length;i++)for(int j=i+1;j<actors.Length;j++)
                {var link=pet.SocialLink(actors[i].Id,actors[j].Id,true)!;link.Met=link.Introduced=known;link.SetScoreFrom(actors[i].Id,0);link.SetScoreFrom(actors[j].Id,0);}
                AdvanceChaoMemories(0);casualSocial.Runtime.Clock(pet.GardenSeconds);
            }
            string Persistent()=>JsonSerializer.Serialize(new{pet.SocialLinks,states=actors.Select(c=>new{c.Pet.Personality,c.Pet.History,c.Pet.ActivityHabits,c.Pet.LearnedPreferences,c.Pet.Joy})});
            void Step(float dt=.02f)
            {pet.GardenSeconds+=dt;casualSocial.Advance(dt,pet,c=>CasualReady(c,continuing:true));}
            void Start()
            {
                MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);MainActivity.Coordinator.Suspend("");MainActivity.Select(true);
                if(!casualSocial.Active || MainActivity.Coordinator.SelectedActivity!=ChaoActivity.Socialize)throw new Exception("Normal coordinator did not start CasualGreeting; "+MainActivity.Coordinator.Describe()+" / "+casualSocial.Diagnostic("main"));
            }
            await Fixture(false);
            MainActivity.Select(true);Check(introductions.Active && !casualSocial.Active,"unintroduced pair still starts original introduction through Socialize");
            for(int f=0;f<6000 && introductions.Active;f++)introductions.Advance(.02f,pet,SocialCandidates,()=>{},allowStart:false);
            Check(pet.SocialLinks.Any(l=>l.Introduced) && pet.History.Events.Any(e=>e.Kind==ChaoMemoryKind.Introduction),"old handshake/introduction completion still records its original effects");
            await Fixture();
            string before=Persistent();Vector3 partnerAt=actors[1].Actor.Position;float beforeNeed=pet.SocialNeed;int completed=casualSocial.Runtime.Successful;
            Start();var phases=new HashSet<CasualSocialPhase>();float maxStep=0;bool captured=false;
            for(int f=0;f<600 && casualSocial.Active;f++)
            {
                phases.Add(casualSocial.Runtime.Phase);var old=friend.Position;Step();maxStep=Math.Max(maxStep,old.DistanceTo(friend.Position));
                if(!captured && casualSocial.Runtime.Phase==CasualSocialPhase.Exchange)
                {
                    captured=true;camera.GlobalPosition=new(20.6f,1.05f,16.5f);camera.LookAt(new Vector3(20.6f,.25f,14));
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    if(GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"casual-exchange.png"))!=Error.Ok)
                        throw new Exception("Could not capture the real exchange frame");
                }
            }
            phases.Add(casualSocial.Runtime.Phase);
            Check(casualSocial.Runtime.Successful==completed+1 && !casualSocial.Active && pet.SocialNeed==beforeNeed-3,"real Notice/Approach/Face/Exchange/React/Finish gives one three-point relief");
            Check(phases.Count==6 && maxStep<=.0161f && actors[1].Actor.Position==partnerAt,"all phases, bounded collision movement and no forced recipient movement");
            Check(before==Persistent(),"greeting does not change memory, relationships, habits, Joy or personality");
            Check(casualSocial.AuthoredSignals==2 && actors.Take(2).All(c=>c.Art.GetChildren().Count(n=>n.Name=="ChaoVoice")==1) && casualSocial.Runtime.PairRemaining(actors[0].Id,actors[1].Id)>0,"exactly two authored signals, one voice channel per model, and pair cooldown");
            for(int i=1;i<10;i++)
            {
                pet.GardenSeconds+=181;friend.Position=new(20,0,14);actors[1].Actor.Position=new(21.3f,0,14);
                foreach(var c in actors){AdapterFor(c.Actor).Coordinator.Suspend("");c.Pet.SocialNeed=90;}
                Start();for(int f=0;f<600 && casualSocial.Active;f++)Step();
            }
            Check(casualSocial.Runtime.Successful==completed+10 && before==Persistent(),"ten real completed encounters teach no habits or memories and create no relationship growth");
            await Fixture();
            var peer=actors[1];var rattle=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);peer.Pet.Fun=5;rattle.BeginPlay(peer.Actor,peer.Pet);
            Check(!casualSocial.CanStart(pet,actors[0],peer,CasualReady(actors[0],initiator:true),CasualReady(peer)) && rattle.PlayedBy(peer.Actor),"active toy play cannot be stolen");rattle.CancelPlay(peer.Actor);
            if(peer.Actor is GardenEgg egg){egg.ReserveForOffer();Check(!casualSocial.CanStart(pet,actors[0],peer,true,CasualReady(peer)),"protected gift receiver excluded");egg.FinishOffer();}
            var wall=new StaticBody3D{Position=new(20.65f,.6f,14),CollisionLayer=1};world.AddChild(wall);wall.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.12f,1.2f,1)}});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(!casualSocial.CanStart(pet,actors[0],peer,true,true),"real wall blocks encounter before any motion");wall.Free();await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await Fixture();Start();float untouchedNeed=pet.SocialNeed;
            wall=new StaticBody3D{Position=new(20.65f,.6f,14),CollisionLayer=1};world.AddChild(wall);
            wall.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.12f,1.2f,1)}});
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);Step();
            Check(!casualSocial.Active && pet.SocialNeed==untouchedNeed,"new obstacle cancels an active approach without completion reward");wall.Free();
            await Fixture();Start();actors[1].Art.QueueFree();Step();
            Check(!casualSocial.Active && !GardenSocialUses(friend),"disappearing participant model releases the remaining actor safely");
            await Fixture();Start();Step();int cancelled=casualSocial.Runtime.Cancelled;
            casualSocial.Cancel(friend,"Player pickup test");Check(!casualSocial.Active && casualSocial.Runtime.Cancelled==cancelled+1 && !GardenSocialUses(actors[1].Actor),"pickup cancellation releases the whole pair");
            await Fixture();sleeping=true;
            Check(!casualSocial.CanStart(pet,actors[1],actors[0],CasualReady(actors[1],initiator:true),CasualReady(actors[0])),"real sleeping Mori is excluded without waking");sleeping=false;
            await Fixture();Start();Step();SwitchZone(GardenZone.CoastalCove,restoring:true);Check(!casualSocial.Active,"garden transition cancels before removing models");
            await Fixture();Start();Step();var phase=casualSocial.Runtime.Phase;
            var roster=new Dictionary<string,PetState>{{"main",pet}};foreach(var e in pet.GardenEggs.Where(e=>e.Chao!=null))roster[e.Id]=e.Chao!;
            var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics,CasualSocialDiagnostics);
            var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
            File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),new JsonObject{["session"]=snap["session"]!.DeepClone(),["id"]="casual-rebind",["target"]="main",["patch"]=new JsonObject{["Fun"]=79}}.ToJsonString());
            var edited=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics,CasualSocialDiagnostics);Check(edited!=null,"real IPC editor update during encounter");
            var oldPet=pet;pet=edited!;RebindGardenExecutors("main",pet);float oldMeter=oldPet.SocialNeed,newMeter=pet.SocialNeed;
            Check(casualSocial.Active && casualSocial.Runtime.Phase==phase,"rebind keeps phase and selected pair");for(int f=0;f<600 && casualSocial.Active;f++)Step();
            Check(pet.SocialNeed==newMeter-3 && oldPet.SocialNeed==oldMeter,"completion uses rebound state exactly once");
            await Fixture();pet.Run=0;ImportedChao.ApplyDevelopment(friendArt,pet);ImportedChao.MovePose(friendArt,0,true,false,false,false,false,false);
            Start();for(int f=0;f<600 && casualSocial.Active;f++)Step();Check(casualSocial.Runtime.Phase==CasualSocialPhase.Finish && casualSocial.Runtime.Elapsed<10,"crawler uses existing rising animation and finishes below hard limit");
            await Fixture();int prior=casualSocial.Runtime.Successful,priorCancels=casualSocial.Runtime.Cancelled;int socialFrames=0,ordinaryFrames=0,musicFrames=0,ballFrames=0,introFrames=0;
            int priorChoices=socialStartAttempts,priorStarts=casualStarts,priorIntro=firstIntroductionStarts,priorMusic=musicJoinStarts,ballStarts=0;bool ballWasActive=false;
            actors[2].Actor.Position=new(20,0,15.4f);actors[3].Actor.Position=new(21.3f,0,15.4f);
            for(int f=0;f<6000;f++)
            {
                const float dt=.05f;pet.GardenSeconds+=dt;pet.Tick(dt,false);_PhysicsProcess(dt);
                if(casualSocial.Active)socialFrames++;else ordinaryFrames++;
                if(musicGroup.Active)musicFrames++;if(ballGame.Active){ballFrames++;if(!ballWasActive)ballStarts++;}ballWasActive=ballGame.Active;if(introductions.Active)introFrames++;
                if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(f%600==0)GD.Print($"CASUAL_AUTONOMY_PROGRESS: {f*.05f:F0}s, greetings {casualSocial.Runtime.Successful-prior}, active {casualSocial.Active}");
            }
            int autoDone=casualSocial.Runtime.Successful-prior;
            Check(autoDone>0 && ordinaryFrames>socialFrames && actors.All(c=>c.Pet.CanAct),"300-second normal four-Chao autonomy produces real greetings and remains mostly ordinary activity");
            var report=new{passed=true,checks,autonomySeconds=300,autoDone,autoCancelled=casualSocial.Runtime.Cancelled-priorCancels,socialFrames,ordinaryFrames,musicFrames,ballFrames,introFrames,
                socialChoices=socialStartAttempts-priorChoices,greetingStarts=casualStarts-priorStarts,introductionStarts=firstIntroductionStarts-priorIntro,musicJoinStarts=musicJoinStarts-priorMusic,ballStarts,
                socialSelections=actors.Select(c=>new{c.Id,selections=AdapterFor(c.Actor).Coordinator.Selections}),current=CasualSocialDiagnostics};
            File.WriteAllText(Path.Combine(directory,"casual-social-check.json"),JsonSerializer.Serialize(report));GD.Print("CASUAL_SOCIAL_SCENE_OK: "+JsonSerializer.Serialize(report));GetTree().Quit();
        }
        catch(Exception e){GD.PushError("CASUAL_SOCIAL_SCENE_FAILED: "+e);GetTree().Quit(1);}
    }
}
