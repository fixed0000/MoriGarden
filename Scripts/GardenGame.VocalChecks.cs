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
    private bool vocalCheckStarted;
    private int vocalCheckFrames;
    private void RunVocalChecks(){if(vocalCheckStarted || ++vocalCheckFrames<3)return;vocalCheckStarted=true;CheckVocalScene();}
    private async void CheckVocalScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1603);GetWindow().Title="MoriGarden — GENERATED VOCAL CHECK";
            string directory=Path.GetDirectoryName(savePath)!;Directory.CreateDirectory(directory);
            void Check(bool value,string label){if(!value)throw new Exception(label);GD.Print("VOCAL_CHECK: "+label);}
            PetState State(string name,int chatty)=>new(){Name=name,Fullness=90,Energy=90,Joy=50,Fun=80,Run=180,PersonalitySchemaVersion=1,Personality=new(){Chatty=chatty}};
            pet=State("Voice Neutral",0);pet.GardenEggs.Clear();
            for(int i=0;i<3;i++)pet.GardenEggs.Add(new(){Id="voice-"+i,Hatched=true,Color=new[]{"White","Black","Yellow"}[i],X=22+i*2,Z=14,Chao=State("Voice "+i,(i-1)*100)});
            petZone=GardenZone.Garden;carrying=false;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);player.Position=new(40,0,30);friend.Visible=true;chaoMotion.Reset();
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();var actors=new CharacterBody3D[]{friend}.Concat(residents).ToArray();
            var models=new[]{(ImportedChao)friendArt}.Concat(residents.Select(e=>(ImportedChao)e.Art)).ToArray();var states=new[]{pet}.Concat(residents.Select(e=>e.Chao!)).ToArray();var ids=new[]{"main"}.Concat(residents.Select(e=>e.Data.Id)).ToArray();
            for(int i=0;i<4;i++){models[i].SetPhysicsProcess(false);ImportedChao.ApplyDevelopment(models[i],states[i]);ImportedChao.BindPresentation(models[i],states[i],false,ids[i]);models[i].GetNode<EmotionOrb>("MoodOrb").WaitForPetting();}
            Check(models.All(m=>m.GetChildren().OfType<AudioStreamPlayer3D>().Count(a=>a.Name=="ChaoVoice")==1),"each bound model owns exactly one channel even before its first voice");
            ChaoVocalDiagnostic D(int i)=>models[i].VocalDiagnostics ?? throw new Exception("Missing actual voice");
            var suppressionSamples=new Dictionary<string,int>();
            void Sample(){for(int i=0;i<4;i++){string reason=D(i).SuppressionReason;if(reason.Length>0)suppressionSamples[reason]=suppressionSamples.GetValueOrDefault(reason)+1;}}
            void Tick(float seconds,bool sleep=false,bool social=false)
            {for(int f=0;f<(int)Math.Ceiling(seconds*60);f++){for(int i=0;i<4;i++){states[i].AdvanceCurrentState(1f/60,sleep);models[i].StepVocal(states[i],sleep,1f/60,social);models[i]._PhysicsProcess(1f/60);}Sample();}}
            void Idle(){foreach(var m in models){ImportedChao.ClearActionPause(m);ImportedChao.MovePose(m,0,false,false,false,false,false,false);}}
            Idle();Tick(180);
            GD.Print("VOCAL_INITIAL: "+JsonSerializer.Serialize(models.Select((_,i)=>D(i))));
            Check(models.All(m=>m.GetChildren().OfType<AudioStreamPlayer3D>().Count(a=>a.Name=="ChaoVoice")==1),"four independent actual voice channels after 180s");
            Check(models.Select(m=>m.GetNode<AudioStreamPlayer3D>("ChaoVoice").GetInstanceId()).Distinct().Count()==4 && models.Select(m=>m.VocalDiagnostics!.BasePitchMultiplier).Distinct().Count()==4,"unique channels and stable individual pitches");
            var ambient=models.Select((_,i)=>D(i)).ToArray();Check(ambient.All(d=>d.AmbientCount>=3 && d.MaximumPitch is >=.8f and <=1.35f),"four voices produce bounded separated ambient reactions");
            var toy=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);pet.Fun=10;Check(toy.BeginPlay(friend,pet),"real rattle interaction starts");toy.PlayPose(friend,friendArt);
            int emotion=D(0).EmotionalCount;toy.Advance(toy.PlayDuration+.5f);Idle();Tick(1);
            Check(D(0).EmotionalCount==emotion+1 && D(0).LastVocalKind=="Excited","real successful PlayFinished creates exactly one emotional voice");
            await ToSignal(GetTree().CreateTimer(.03),SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ReferenceEquals(models[0].GetNode<AudioStreamPlayer3D>("ChaoVoice").Stream,ChaoSounds.VocalStream(ChaoVocalKind.Excited)),"actual audio player receives Excited WAV after bounded release");
            Tick(10);int gameEvents=pet.Mood.ActiveReactions.Count;Check(pet.ObserveEvent(ChaoStateEvent.DiscoveryNoticed,"vocal-found"),"existing discovery event accepted");Tick(.1f);
            Check(D(0).LastVocalKind=="Curious" && pet.Mood.ActiveReactions.Count<=gameEvents+1,"discovery voice does not create additional Mood events");
            await ToSignal(GetTree().CreateTimer(.03),SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ReferenceEquals(models[0].GetNode<AudioStreamPlayer3D>("ChaoVoice").Stream,ChaoSounds.VocalStream(ChaoVocalKind.Curious)),"actual discovery audio uses Curious WAV");
            string json=JsonSerializer.Serialize(pet);models[0].StepVocal(pet,false,1);models[0]._PhysicsProcess(1);Check(json==JsonSerializer.Serialize(pet),"voice and presentation only do not mutate saved data");
            // Existing dialogue sequence: exact special calls remain on each real actor's own channel.
            friend.Position=new(20,0,14);residents[0].Position=new(20.38f,0,14);var a=new ChaoIntroductions.Candidate("main",friend,friendArt,pet);var b=new ChaoIntroductions.Candidate(ids[1],residents[0],models[1],states[1]);var both=new[]{a,b};
            var intro=new ChaoIntroductions();int before=D(0).SpecialCount+D(1).SpecialCount;int introAmbient=D(0).AmbientCount+D(1).AmbientCount;
            Check(intro.TryStart(pet,a,b),"real introduction starts");for(int f=0;f<1200 && intro.Active;f++){intro.Advance(1f/60,pet,()=>both,()=>{},false);Tick(1f/60,false,true);}
            Check(!intro.Active && pet.SocialLink("main",ids[1])?.Introduced==true && D(0).SpecialCount+D(1).SpecialCount==before+3,"original three alternating introduction replies and handshake preserved");
            Check(D(0).AmbientCount+D(1).AmbientCount==introAmbient,"actual introduction has no additional ambient replies");
            intro.Cancel();Idle();
            CheckBallAndInstruments(); // Existing full ball invitation, passes, misses, refusals and exact instrument stream checks.
            Check(true,"existing real shared ball and instrument scenarios pass unchanged");
            GD.Seed(914);friend.Position=new(20,0,14);residents[0].Position=new(20.6f,0,14);pet.Fun=states[1].Fun=0;pet.Energy=states[1].Energy=90;pet.Joy=states[1].Joy=80;pet.Sociability=80;
            var beach=world.GetNode<ToyCar>("BeachBall");beach.FinishBall();Check(beach.BeginPlay(friend,pet),"actual bound pair takes beach ball");
            var sharedBall=new ChaoBallGame();bool sawToss=false;int pairVoices=D(0).SpecialCount+D(1).SpecialCount;
            for(int f=0;f<2400;f++)
            {sharedBall.Advance(1f/30,pet,both,both,new[]{beach});for(int i=0;i<2;i++){states[i].AdvanceCurrentState(1f/30,false);models[i].StepVocal(states[i],false,1f/30,true);models[i]._PhysicsProcess(1f/30);}sawToss|=sharedBall.Stage=="Toss";if(f%200==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(f>2 && !sharedBall.Active)break;}
            Check(sawToss && D(0).SpecialCount+D(1).SpecialCount>pairVoices && beach.GetNodeOrNull<AudioStreamPlayer3D>("ChaoCue")?.Stream is AudioStreamWav,"actual ball pair uses personal voices while ball retains its own WAV channel");sharedBall.Cancel();Idle();
            ImportedChao.BindPresentation(friendArt,pet,false,"main");Idle();var instrument=world.GetChildren().OfType<ToyCar>().First(t=>t.IsFlute);pet.Fun=0;
            Check(instrument.BeginPlay(friend,pet),"real flute starts");instrument.PlayPose(friend,friendArt);var stream=instrument.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream;int voices=D(0).AmbientCount;
            for(int f=0;f<30*60;f++){instrument.Advance(1f/60);instrument.PlayPose(friend,friendArt);pet.AdvanceCurrentState(1f/60,false);models[0].StepVocal(pet,false,1f/60);models[0]._PhysicsProcess(1f/60);}
            Check(ReferenceEquals(stream,instrument.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream) && stream is AudioStreamWav && D(0).AmbientCount==voices,"real instrument WAV/channel preserved, background voice suppressed");instrument.CancelPlay(friend);Idle();
            Tick(2);for(int i=0;i<4;i++)ImportedChao.SleepPose(models[i],5,states[i].Manners);Tick(12,true);
            Check(D(0).LastVocalKind=="SleepBreath" && D(0).SleepCount>0,"normal non-snoring sleep uses original Breath");
            states[1].Personality!.BigEater=75;Tick(10,true);Check(D(1).LastVocalKind=="Snore","original Glutton snore rule preserved");
            Idle();Tick(.1f);int waking=D(0).EmotionalCount;Tick(2);Check(D(0).EmotionalCount==waking && D(0).TimeUntilNextAmbientVoice>0,"wake drops sleeping backlog and uses fresh voice delay");
            pet.Energy=5;pet.Fullness=90;pet.Fun=100;pet.SocialNeed=0;friend.Position=ChaoRest.Bed+Vector3.Back*2.2f;MainActivity.Reset();moriRest.Cancel();chaoMotion.Reset();wanderTime=0;bool slept=false,rose=false,woke=false;int breaths=D(0).SleepCount;
            for(int f=0;f<11000;f++)
            {const float dt=1f/60;pet.Tick(dt,sleeping);MoveFriend(dt);models[0]._PhysicsProcess(dt);slept|=sleeping;rose|=moriRising.Active;if(sleeping && D(0).CurrentVocalKind is not ("None" or "SleepBreath" or "Snore"))throw new Exception("Awake voice in natural sleep");if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(slept && !sleeping && !moriRising.Active && !chaoMotion.Airborne && f>2200){woke=true;break;}}
            Check(slept && rose && woke && D(0).SleepCount>breaths,"natural bed approach, sleep Breath, Rising and wake without a voice backlog");Idle();Tick(1);
            pet.ObserveEvent(ChaoStateEvent.Frightened,"vocal-startle");ImportedChao.BallReaction(friendArt,1.3f,true,false,false);Tick(.1f);
            Check(D(0).LastVocalKind=="Startled" && ChaoVoicePolicy.Duration(ChaoVocalKind.Startled)<.4f,"real distress-compatible short fright reaction");ImportedChao.ClearBallReaction(friendArt);Idle();Tick(1);
            await ToSignal(GetTree().CreateTimer(.03),SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(ReferenceEquals(models[0].GetNode<AudioStreamPlayer3D>("ChaoVoice").Stream,ChaoSounds.VocalStream(ChaoVocalKind.Startled)),"actual fright audio uses cached short Startled WAV");
            var bridge=new ChaoEditorBridge(Path.Combine(directory,"voice-editor"));var roster=new Dictionary<string,PetState>{{"main",pet}};for(int i=1;i<4;i++)roster[ids[i]]=states[i];
            bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);var snap=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"voice-editor/chao-editor-state.json")))!;
            var prior=D(0);File.WriteAllText(Path.Combine(directory,"voice-editor/chao-editor-request.json"),JsonSerializer.Serialize(new{id="voice-rebind",session=snap["session"]!.GetValue<string>(),target="main",patch=new{Fullness=88}}));
            pet=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics)!;states[0]=pet;RebindGardenExecutors("main",pet);AdvanceChaoMemories(0);
            Check(D(0)==prior && ReferenceEquals(models[0].PresentationState,pet),"real editor rebind preserves vocal executor/timers/counters");
            var audioReport=new List<object>();var montage=new List<byte>();
            foreach(var kind in Enum.GetValues<ChaoVocalKind>())
            {
                var wav=ChaoSounds.VocalStream(kind);byte[] data=wav.Data;int n=data.Length/2;double sum=0,sum2=0,peak=0;
                for(int i=0;i<n;i++){double v=BitConverter.ToInt16(data,i*2)/32768d;sum+=v;sum2+=v*v;peak=Math.Max(peak,Math.Abs(v));}
                double duration=n/(double)wav.MixRate,rms=Math.Sqrt(sum2/n),dc=sum/n;
                Check(n>0 && wav.MixRate==22050 && peak<=.95 && rms>.001 && rms<.5 && Math.Abs(dc)<.015 && Math.Abs(BitConverter.ToInt16(data,0))<100 && Math.Abs(BitConverter.ToInt16(data,data.Length-2))<100,"PCM duration/rate/peak/RMS/DC/edges "+kind);
                Check(Math.Abs(duration-ChaoVoicePolicy.Duration(kind))<.001,"voice duration "+kind);
                Check(ReferenceEquals(wav,ChaoSounds.VocalStream(kind)),"cached stream "+kind);wav.SaveToWav(Path.Combine(directory,kind+".wav"));montage.AddRange(data);montage.AddRange(new byte[22050]);audioReport.Add(new{kind=kind.ToString(),duration,rate=wav.MixRate,peak,rms,dc});
            }
            new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=22050,Data=montage.ToArray()}.SaveToWav(Path.Combine(directory,"voice-preview.wav"));
            for(int f=0;f<180*30;f++){pet.Tick(1f/30,sleeping);_PhysicsProcess(1f/30);foreach(var m in models)m._PhysicsProcess(1f/30);Sample();if(f%150==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(models.All(m=>m.GetChildren().OfType<AudioStreamPlayer3D>().Count(x=>x.Name=="ChaoVoice")==1),"180s actual autonomous garden has bounded voice nodes");
            Save();var loaded=SaveStore.Load(savePath,out _);Check(loaded.GardenEggs.Select(e=>e.Id).SequenceEqual(pet.GardenEggs.Select(e=>e.Id)) && !File.ReadAllText(savePath).Contains("VocalSignals"),"save/load preserves roster without voice fields");
            File.WriteAllText(Path.Combine(directory,"chao-vocal-check.json"),JsonSerializer.Serialize(new{passed=true,controlledSeconds=180,autonomousSeconds=180,voiceChannels=models.Sum(m=>m.GetChildren().OfType<AudioStreamPlayer3D>().Count(x=>x.Name=="ChaoVoice")),ambient,suppressionSamples,audioReport,diagnostics=ExpressionDiagnostics}));
            GD.Print("CHAO_VOCAL_SCENE_OK");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("CHAO_VOCAL_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
