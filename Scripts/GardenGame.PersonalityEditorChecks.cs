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
    private bool personalityEditorStarted;
    private int personalityEditorFrames;
    private void RunPersonalityEditorChecks(){if(personalityEditorStarted || ++personalityEditorFrames<3)return;personalityEditorStarted=true;CheckPersonalityEditorScene();}
    private async void CheckPersonalityEditorScene()
    {
        try
        {
            SetPhysicsProcess(false);string directory=Path.GetDirectoryName(savePath)!;
            string input=OS.GetEnvironment("MORI_PERSONALITY_INPUT");if(!File.Exists(input))throw new Exception("An isolated input copy is required.");
            pet=SaveStore.Load(input,out _);pet.Personality!.Curious=-50;pet.Personality.Chatty=-80;pet.Personality.Energetic=-40;
            pet.Energy=80;pet.Fullness=80;pet.Fun=40;pet.Joy=50;pet.SocialNeed=20;
            petZone=GardenZone.Garden;mainActivity=null;carrying=false;SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);player.Position=new(40,0,30);friend.Visible=true;
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();var models=new[]{(ImportedChao)friendArt}.Concat(residents.Select(e=>(ImportedChao)e.Art)).ToArray();
            foreach(var m in models)m.SetPhysicsProcess(false);AdvanceChaoMemories(0);
            var roster=new Dictionary<string,PetState>{{"main",pet}};foreach(var r in residents)roster[r.Data.Id]=r.Chao!;
            var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);int serial=0;
            void Check(bool ok,string text){if(!ok)throw new Exception(text);GD.Print("PERSONALITY_EDITOR: "+text);}
            void Commit(string id,PetState state)
            {
                if(id=="main")pet=state;else pet.GardenEggs.Single(e=>e.Id==id).Chao=state;
                roster[id]=state;RebindGardenExecutors(id,state);AdvanceChaoMemories(0);
            }
            PetState? Edit(string target,JsonObject personality,JsonObject? stats=null,JsonObject? ordinary=null,JsonObject? relationship=null)
            {
                var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
                var request=new JsonObject{["id"]="native-"+(++serial),["session"]=snapshot["session"]!.DeepClone(),["target"]=target,["patch"]=ordinary ?? new(),["personality"]=personality,["personalityRevision"]=ChaoPersonalityEditor.Revision(roster[target])};
                if(stats!=null)request["statistics"]=stats;if(relationship!=null)request["relationship"]=relationship;
                File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),request.ToJsonString());var next=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);if(next!=null)Commit(target,next);return next;
            }
            void Visual()
            {foreach(var m in models)m._PhysicsProcess(1f/60);}
            ImportedChao.StandForAction(friendArt);ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);ImportedChao.HalfEyes(friendArt,false);
            pet.ObserveEvent(ChaoStateEvent.DiscoveryNoticed,"editor-test");Visual();
            var availability=new[]{new ActivityAvailability(ChaoActivity.Leisure,true,"native existing category",20,ActivityInterruptibility.LongOptional,1)};
            var coordinator=MainActivity.Coordinator;var score=coordinator.Evaluate(pet,availability).Single();coordinator.Adopt(ChaoActivity.Leisure,"existing native activity",ActivityInterruptibility.LongOptional);var selection=coordinator.SelectedActivity;int refreshed=coordinator.PersonalityRefreshes;
            var artId=friendArt.GetInstanceId();var voiceId=models[0].GetNode<AudioStreamPlayer3D>("ChaoVoice").GetInstanceId();
            Check(Edit("main",new(){["Curious"]=100})!=null,"Curious edited through actual IPC");Visual();
            Check(coordinator.Evaluate(pet,availability).Single().PersonalityAdjustment>score.PersonalityAdjustment && coordinator.PersonalityRefreshes==refreshed+1 && coordinator.SelectedActivity==selection,"existing coordinator refreshes weights without resetting intention");
            Check(models[0].PresentationState==pet && models[0].IdleGestureDiagnostics!.CuriousContribution==.25f && Math.Abs(ChaoExpressiveness.PersonalityMultiplier(ChaoExpression.Curious,pet.Personality)-1.1f)<.00001,"visual and gesture readers use updated canonical state");
            float energy=pet.Energy;Check(Edit("main",new(){["Chatty"]=100,["Energetic"]=100})!=null,"Chatty and Energetic edited together");models[0].StepVocal(pet,false,0);Visual();
            Check(models[0].VocalDiagnostics!.ChattyMultiplier==.8f && models[0].VocalDiagnostics!.EnergeticVocalAdjustment==1.08f && pet.Energy==energy && voiceId==models[0].GetNode<AudioStreamPlayer3D>("ChaoVoice").GetInstanceId(),"actual voice profile updates without a new channel or Energy change");
            pet.ObserveEvent(ChaoStateEvent.PlayerContact,"editor-positive-expression");ImportedChao.StandForAction(friendArt);ImportedChao.MovePose(friendArt,0,false,false,false,false,false,false);Visual();
            Check(models[0].Expression==ChaoExpression.Happy && Math.Abs(models[0].ExpressionDiagnostics.AppliedPersonalityMultiplier-1.12f)<.00001,"actual Happy expression uses edited Energetic multiplier");
            var instrument=world.GetChildren().OfType<ToyCar>().First(t=>t.IsFlute);Check(instrument.BeginPlay(friend,pet),"real flute performance begins before editor update");instrument.PlayPose(friend,friendArt);Visual();
            var stream=instrument.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream;Check(Edit("main",new(){["Energetic"]=20})!=null,"edit during safe active instrument performance");instrument.PlayPose(friend,friendArt);Visual();
            Check(instrument.Performing && friendArt.GetInstanceId()==artId && models[0].PoseKind==ChaoPoseKind.Grip && ReferenceEquals(stream,instrument.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream),"music, grip and visual object survive editor rebind");instrument.CancelPlay(friend);
            string resident=residents[0].Data.Id;string mainPersonality=JsonSerializer.Serialize(pet.Personality);Check(Edit(resident,new(){["Curious"]=-88,["Naive"]=-100})!=null && JsonSerializer.Serialize(pet.Personality)==mainPersonality,"resident edited independently of Mori");
            string all=JsonSerializer.Serialize(pet);Check(Edit(resident,new(){["Curious"]=101},relationship:new(){["other"]="main",["score"]=80})==null && JsonSerializer.Serialize(pet)==all,"invalid personality/relationship request leaves live roster unchanged");
            foreach(var (grade,points) in new[]{(0,1287),(2,1881),(5,2772)})
                Check(Edit("main",new(),new(){["Run"]=new JsonObject{["Grade"]=grade,["Level"]=99,["Points"]=points,["Progress"]=0}})!=null && pet.Stat("Run").Points==points,"existing sports editor Grade/Level/Points "+grade);
            Save();var loaded=SaveStore.Load(savePath,out _);Check(loaded.Personality!.Equals(pet.Personality) && loaded.GardenEggs.Where(e=>e.Hatched).All(e=>e.Chao!.Personality!.Equals(roster[e.Id].Personality)),"all thirteen edited values and stable IDs survive save/load");
            File.WriteAllText(Path.Combine(directory,"personality-editor-check.json"),JsonSerializer.Serialize(new{passed=true,ids=roster.Keys,personality=roster.ToDictionary(p=>p.Key,p=>p.Value.Personality),visualObjectPreserved=true,voiceChannelPreserved=true}));
            GD.Print("PERSONALITY_EDITOR_SCENE_OK");
            if(OS.GetCmdlineUserArgs().Contains("--personality-editor-live"))
            {
                File.WriteAllText(Path.Combine(directory,"personality-editor-live-ready.json"),"{}");
                for(int f=0;f<600 && !File.Exists(Path.Combine(directory,"personality-editor-live-done.json"));f++)
                {await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);var next=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);if(next!=null){Commit(bridge.EditedTarget,next);Save();}Visual();}
                Check(File.Exists(Path.Combine(directory,"personality-editor-live-done.json")),"live frontend completed within timeout");
                Check(JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"personality-editor-live-done.json")))!["passed"]!.GetValue<bool>(),"real frontend reports successful checks");
            }
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError("PERSONALITY_EDITOR_SCENE_FAILED: "+error);GetTree().Quit(1);}
    }
}
