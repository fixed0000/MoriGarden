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
    private bool learnedEditorStarted;
    private int learnedEditorFrames;
    private void RunLearnedEditorChecks(){if(learnedEditorStarted || ++learnedEditorFrames<3)return;learnedEditorStarted=true;CheckLearnedEditorScene();}
    private async void CheckLearnedEditorScene()
    {
        try
        {
            SetPhysicsProcess(false);string directory=Path.GetDirectoryName(savePath)!;
            string input=OS.GetEnvironment("MORI_LEARNED_EDITOR_INPUT");if(!File.Exists(input))throw new Exception("An isolated audited input copy is required.");
            pet=SaveStore.Load(input,out _);pet.History.Seconds=Math.Max(1000,pet.History.Seconds);pet.FavoriteToy="Rattle";pet.FavoriteInstrument="Flute";pet.FavoriteFruit=FruitKinds.Blue;pet.Energy=80;pet.Fullness=35;pet.Fun=20;pet.Joy=60;pet.SocialNeed=30;
            petZone=GardenZone.Garden;mainActivity=null;carrying=false;SwitchZone(GardenZone.Garden,restoring:true);friend.Position=new(20,0,14);player.Position=new(40,0,30);friend.Visible=true;
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var residents=world.GetChildren().OfType<GardenEgg>().Where(e=>e.Chao!=null).ToArray();var models=new[]{(ImportedChao)friendArt}.Concat(residents.Select(e=>(ImportedChao)e.Art)).ToArray();
            foreach(var visual in models)visual.SetPhysicsProcess(false);AdvanceChaoMemories(0);
            var roster=new Dictionary<string,PetState>{{"main",pet}};foreach(var r in residents)roster[r.Data.Id]=r.Chao!;
            var bridge=new ChaoEditorBridge(directory);bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);int serial=0,checks=0;
            void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;GD.Print("LEARNED_EDITOR: "+text);}
            void Commit(string id,PetState state){if(id=="main")pet=state;else pet.GardenEggs.Single(e=>e.Id==id).Chao=state;roster[id]=state;RebindGardenExecutors(id,state);AdvanceChaoMemories(0);}
            PetState? Edit(string target,string group,string id,decimal bias)
            {
                var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
                var request=new JsonObject{["id"]="native-learned-"+(++serial),["session"]=snapshot["session"]!.DeepClone(),["target"]=target,["patch"]=new JsonObject(),
                    ["learnedRevision"]=ChaoLearnedEditor.Revision(roster[target]),["learnedAdjustments"]=new JsonObject{[group]=new JsonObject{[id]=bias}}};
                File.WriteAllText(Path.Combine(directory,"chao-editor-request.json"),request.ToJsonString());var next=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);if(next!=null)Commit(target,next);return next;
            }
            string innate=JsonSerializer.Serialize(roster.ToDictionary(r=>r.Key,r=>r.Value.Personality));
            string favorites=JsonSerializer.Serialize(new{pet.FavoriteToy,pet.FavoriteFruit,pet.FavoriteInstrument,pet.LikesToyCars});
            var rattle=world.GetChildren().OfType<ToyCar>().First(t=>t.IsRattle);var flute=world.GetChildren().OfType<ToyCar>().First(t=>t.IsFlute);
            rattle.Position=friend.Position+Vector3.Right*.6f;flute.Position=friend.Position+Vector3.Left*.6f;
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            bool Target(ToyCar expected,Func<Node3D,bool>? gate=null)
            {
                var interest=new ChaoInterests();bool started=interest.BeginSelected(ChaoActivity.Play,friend,pet,Array.Empty<FruitTree>(),new[]{flute,rattle},gate ?? (_=>true));bool match=started && interest.Target==expected;interest.Cancel(friend,friendArt);return match;
            }
            Check(Edit("main","toys","rattle",.3m)!=null && Edit("main","toys","flute",-.3m)!=null && Target(rattle),"real target selector uses edited positive and negative toy Bias");
            Check(Target(flute,n=>n!=rattle),"high Bias never bypasses target availability");
            Check(Edit("main","toys","rattle",-.3m)!=null && Edit("main","toys","flute",.3m)!=null && Target(flute),"target selection reverses after a real IPC edit");
            var coordinator=MainActivity.Coordinator;var available=new[]{new ActivityAvailability(ChaoActivity.Play,true,"native available toy",5,ActivityInterruptibility.LongOptional,1)};
            Check(Edit("main","habits","Play",0)!=null,"neutral Play habit");var score=coordinator.Evaluate(pet,available).Single();
            coordinator.Adopt(ChaoActivity.Play,"retained active intention",ActivityInterruptibility.LongOptional);var selection=coordinator.SelectedActivity;
            Check(Edit("main","habits","Play",.1m)!=null && Math.Abs(coordinator.Evaluate(pet,available).Single().HabitAdjustment-score.HabitAdjustment-.1f)<.00001 && coordinator.SelectedActivity==selection,"same coordinator uses new habit without resetting intention");
            var model=(ImportedChao)friendArt;model.StepVocal(pet,false,0);var artId=model.GetInstanceId();var voiceId=model.GetNode<AudioStreamPlayer3D>("ChaoVoice").GetInstanceId();
            pet.Fun=5;Check(flute.BeginPlay(friend,pet),"real flute session starts");flute.PlayPose(friend,friendArt);
            int itemCount=pet.LearnedPreferences.ToyPreferences["flute"].ExperienceCount;int habitCount=pet.ActivityHabits.Entries["Play"].ExperienceCount;
            var stream=flute.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream;Check(Edit("main","toys","flute",.12m)!=null,"edit during actual musical play");flute.PlayPose(friend,friendArt);model.StepVocal(pet,false,0);
            Check(flute.Performing && model.GetInstanceId()==artId && voiceId==model.GetNode<AudioStreamPlayer3D>("ChaoVoice").GetInstanceId() && ReferenceEquals(stream,flute.GetNode<AudioStreamPlayer3D>("ChaoCue").Stream),"play, audio channels and model survive runtime rebind");
            pet.History.Seconds+=150;
            for(float t=0;t<flute.PlayDuration+.6f;t+=.05f){pet.Tick(.05f,false);flute.Advance(.05f);if(flute.PlayedBy(friend))flute.PlayPose(friend,friendArt);}
            Check(pet.LearnedPreferences.ToyPreferences["flute"].ExperienceCount==itemCount+1 && pet.ActivityHabits.Entries["Play"].ExperienceCount==habitCount+1,"real completion after rebind credits item and habit exactly once");
            float learnedBias=pet.LearnedPreferences.ToyPreferences["flute"].Bias;flute.Advance(20);Check(pet.LearnedPreferences.ToyPreferences["flute"].Bias==learnedBias,"completed toy cannot replay learning");
            pet.Fun=5;flute.BeginPlay(friend,pet);flute.CancelPlay(friend);flute.Advance(70);Check(pet.LearnedPreferences.ToyPreferences["flute"].ExperienceCount==itemCount+1,"cancelled play still gives no experience");
            string compatible=pet.FavoriteFruit;string rejected=new[]{FruitKinds.Blue,FruitKinds.Red,FruitKinds.Green,FruitKinds.Yellow}.First(k=>k!=compatible);
            FruitTree Food(string kind,Vector3 at){var food=new FruitTree{IsFeeder=true,SingleUse=true,FruitKind=kind};world.AddChild(food);food.Place(at);return food;}
            var garden=Food(FruitKinds.Garden,friend.Position+Vector3.Right*.5f);var favorite=Food(compatible,friend.Position+Vector3.Left*3.5f);
            Edit("main","fruits",FruitKinds.Garden,.3m);Edit("main","fruits",compatible,-.3m);pet.Fullness=20;
            var eat=new ChaoInterests();bool chose=eat.BeginSelected(ChaoActivity.Eat,friend,pet,new[]{favorite,garden},Array.Empty<ToyCar>(),_=>true);Check(chose && eat.Target==garden,"real Eat target selection includes edited fruit Bias");eat.Cancel(friend,friendArt);
            var bad=Food(rejected,friend.Position);Edit("main","fruits",rejected,.3m);var meal=new FruitMeal();meal.Begin(bad,pet,bad.FruitPosition);
            for(int i=0;i<500 && meal.Remaining>0;i++)meal.Advance(.05f,bad,pet,friendArt,friend.Position+Vector3.Up*.25f);
            Check(meal.RejectedForTaste && bad.Bites==0,"maximum manual Bias does not bypass fixed fruit taste");
            int meals=pet.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount;pet.Fullness=20;pet.History.Seconds+=60;meal=new();meal.Begin(garden,pet,garden.FruitPosition);
            for(int i=0;i<1200 && meal.Remaining>0;i++){pet.Tick(.05f,false);meal.Advance(.05f,garden,pet,friendArt,friend.Position+Vector3.Up*.25f);}
            Check(garden.Bites==4 && pet.LearnedPreferences.FruitPreferences[FruitKinds.Garden].ExperienceCount==meals+1,"real consumed fruit teaches after manual adjustment");
            var peers=residents.Take(2).ToArray();Check(peers.Length==2,"at least three genuine saved Chao are present");
            foreach(var p in peers)Check(Edit(p.Data.Id,"habits","Leisure",.06m)!=null,"resident adjustment "+p.Chao!.Name);
            Check(innate==JsonSerializer.Serialize(roster.ToDictionary(r=>r.Key,r=>r.Value.Personality)) && favorites==JsonSerializer.Serialize(new{pet.FavoriteToy,pet.FavoriteFruit,pet.FavoriteInstrument,pet.LikesToyCars}),"all saved personalities and fixed favorites unchanged");
            Save();var loaded=SaveStore.Load(savePath,out _);Check(ChaoLearnedEditor.Revision(loaded)==ChaoLearnedEditor.Revision(pet) && loaded.GardenEggs.Count==pet.GardenEggs.Count && loaded.GardenEggs.All(e=>e.Id==pet.GardenEggs.Single(r=>r.Id==e.Id).Id),"real save/load retains roster, edited biases and credited history");
            string before=JsonSerializer.Serialize(pet);Check(Edit("main","habits","Play",.101m)==null && JsonSerializer.Serialize(pet)==before,"invalid IPC leaves all real game data unchanged");
            File.WriteAllText(Path.Combine(directory,"learned-editor-check.json"),JsonSerializer.Serialize(new{passed=true,checks,ids=roster.Keys}));GD.Print("LEARNED_EDITOR_SCENE_OK");
            if(OS.GetCmdlineUserArgs().Contains("--learned-editor-live"))
            {
                File.WriteAllText(Path.Combine(directory,"learned-editor-live-ready.json"),"{}");
                for(int f=0;f<1200 && !File.Exists(Path.Combine(directory,"learned-editor-live-done.json"));f++)
                {await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);var next=bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);if(next!=null){Commit(bridge.EditedTarget,next);Save();}}
                Check(File.Exists(Path.Combine(directory,"learned-editor-live-done.json")) && JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"learned-editor-live-done.json")))!["passed"]!.GetValue<bool>(),"actual frontend live checks passed");
            }
            GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("LEARNED_EDITOR_SCENE_FAILED: "+ex);GetTree().Quit(1);}
    }
}

