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
    private bool memoryDiagnosticStarted;
    private int memoryDiagnosticFrames;
    private void RunMemoryDiagnosticChecks(){if(memoryDiagnosticStarted || ++memoryDiagnosticFrames<3)return;memoryDiagnosticStarted=true;CheckMemoryDiagnosticScene();}
    private async void CheckMemoryDiagnosticScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1703);string directory=Path.GetDirectoryName(savePath)!;
            string input=OS.GetEnvironment("MORI_MEMORY_INPUT");if(!File.Exists(input))throw new Exception("An isolated audited input copy is required.");
            pet=SaveStore.Load(input,out _);var all=new[]{pet}.Concat(pet.GardenEggs.Where(e=>e.Chao!=null).Select(e=>e.Chao!)).ToArray();
            string innate=JsonSerializer.Serialize(all.Select(s=>s.Personality));
            // This route fixture must not inherit an unrelated active courtship from its safe input copy.
            foreach(var state in all){state.CourtshipReadiness=0;state.BloomRemaining=0;state.Run=180;state.Fullness=80;state.Energy=90;state.Fun=100;state.Joy=70;}
            string peerId=pet.GardenEggs.First(e=>e.Chao!=null && e.Hatched).Id;
            pet.Expedition.LastTeamIds=new(){"main",peerId};travel.ExpeditionArea=WorldCatalog.Meadow;
            SwitchZone(GardenZone.Expedition,restoring:true);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var team=expeditionTeam ?? throw new Exception("Real expedition team was not created");team.Discoveries.AutoScan=false;
            var flower=world.GetChildren().OfType<DiscoveryInterest>().First(i=>i.Definition.Kind==DiscoveryKind.Plant);
            var member=team.Members.First();var nav=new ExpeditionNavigation(world);member.Body.Position=nav.Nearest(flower.Position+Vector3.Back*2.6f);
            member.SetIntent(ExpeditionIntent.Observe,member.Body.Position,100);
            // Isolate repeat eligibility in this test copy, without inventing a retained event.
            pet.Expedition.Memory(member.Pet.Id).Objects.RemoveAll(r=>r.Id==flower.Definition.Id);
            member.Pet.State.History.Advance(121);
            double beforeStudy=member.Pet.State.History.Events.Where(e=>e.Source==flower.Definition.Id && e.Kind==ChaoMemoryKind.IndependentStudy).Select(e=>e.LastAt).DefaultIfEmpty(-1).Max();
            int checks=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;GD.Print("MEMORY_DIAGNOSTICS: "+text);}
            Check(team.Discoveries.Start(member,flower,new(DiscoveryChoice.Approach,4,false,false),true),"actual discovery approach starts");
            for(int f=0;f<6000 && !member.Pet.State.History.Events.Any(e=>e.Source==flower.Definition.Id && e.Kind==ChaoMemoryKind.IndependentStudy && e.LastAt>beforeStudy);f++)
            {team.Step(1f/60,flower.Position+Vector3.Back*4,camera);if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(member.Pet.State.History.Events.Any(e=>e.Source==flower.Definition.Id && e.Kind==ChaoMemoryKind.IndependentStudy && e.LastAt>beforeStudy),"physical Discovery completion enters canonical history");
            SwitchZone(GardenZone.Garden,restoring:true);mainActivity=null;friend.Position=ChaoLeisure.Routes[1][0];friend.Visible=true;player.Position=new(40,0,30);
            chaoMotion.Reset();wanderTarget=friend.Position;wanderTime=0;await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);AdvanceChaoMemories(0);
            MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);ImportedChao.ClearActionPause(friendArt);moriLeisure.BeginRoute(1);
            const string routeId="garden/leisure/route-1";
            double beforeRoute=pet.History.Events.Where(e=>e.Source==routeId).Select(e=>e.LastAt).DefaultIfEmpty(-1).Max();pet.History.Advance(121);
            for(int f=0;f<18000 && !pet.History.Events.Any(e=>e.Source==routeId && e.LastAt>beforeRoute);f++)
            {pet.Tick(1f/60,false);MoveFriend(1f/60);if(f%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(!moriLeisure.Active && moriLeisure.LastFailureReason.Length>0)throw new Exception($"{moriLeisure.LastFailureReason}; position={friend.Position}, speed={chaoMotion.GroundSpeed}, level={pet.Stat("Run").Level}, social={GardenSocialUses(friend)}, coordinator={MainActivity.Coordinator.CurrentExecutor}");}
            Check(pet.History.Events.Any(e=>e.Source==routeId && e.Place==LeisureDestinationCatalog.Find(routeId)!.End),"real Leisure route and final pause record source/area/position");
            var resident=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id==peerId);var peer=resident.Chao!;
            friend.Position=new(20,0,14);resident.Position=new(21.2f,0,14);
            var flute=world.GetChildren().OfType<ToyCar>().First(t=>t.IsFlute);var drums=world.GetChildren().OfType<ToyCar>().First(t=>t.IsDrums);
            void PlayTogether()
            {
                pet.Fun=peer.Fun=5;flute.Advance(20);drums.Advance(20);
                if(!flute.BeginPlay(friend,pet) || !drums.BeginPlay(resident,peer))throw new Exception("Actual instrument performance did not begin");
                drums.StartWith(flute,peerId,"main");flute.PlayPose(friend,friendArt);drums.PlayPose(resident,resident.Art);flute.Advance(61);drums.Advance(1);
            }
            pet.History.Advance(121);peer.History.Advance(121);PlayTogether();
            var shared=pet.History.Events.Single(e=>e.Source=="music-ensemble" && e.Participants.Contains(peerId));int count=shared.Occurrences;double last=shared.LastAt;
            Check(peer.History.Events.Any(e=>e.Source=="music-ensemble" && e.Participants.Contains("main")),"actual ensemble completion records each real participant ID");
            pet.History.Advance(121);peer.History.Advance(121);PlayTogether();
            Check(shared.Occurrences==Math.Min(20,count+1) && shared.LastAt>last && pet.History.Events.Count(e=>e.Key==shared.Key)==1,"actual repeated ensemble coalesces after original cooldown");
            var roster=new Dictionary<string,PetState>{{"main",pet}};foreach(var e in pet.GardenEggs.Where(e=>e.Chao!=null))roster[e.Id]=e.Chao!;
            var snapshot=ChaoMemoryDiagnostics.Capture(pet,"main",roster);float influence=snapshot.events.Single(e=>e.source==routeId).influence;
            double lastRoute=pet.History.Events.Single(e=>e.Source==routeId).LastAt;pet.History.Advance(12000);snapshot=ChaoMemoryDiagnostics.Capture(pet,"main",roster);
            Check(snapshot.events.Single(e=>e.source==routeId).influence<influence && pet.History.Events.Single(e=>e.Source==routeId).LastAt==lastRoute,"decay uses active time without altering last occurrence");
            Check(snapshot.leisure.Single(r=>r.referenceScore.Id==routeId).referenceScore==ChaoLeisurePreferenceScorer.Evaluate(pet.History,"garden",routeId),"native diagnostic leisure score matches actual scorer without double counting");
            string before=JsonSerializer.Serialize(pet);var bridge=new ChaoEditorBridge(directory);
            for(int i=0;i<25;i++){foreach(var entry in roster)ChaoMemoryDiagnostics.Capture(entry.Value,entry.Key,roster);bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);}
            Check(JsonSerializer.Serialize(pet)==before,"repeated capture and bridge publication do not mutate the garden");
            var published=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"chao-editor-state.json")))!;
            Check(published["chao"]!.AsArray().All(r=>r!["memoryDiagnostics"]!["available"]!.GetValue<bool>() && r["memoryDiagnostics"]!["count"]!.GetValue<int>()==roster[r["id"]!.GetValue<string>()].History.Events.Count),"all real retained rows are published separately for each stable ID");
            OpenMemories();Check(memoriesPanel.Visible && memoryText.Text.Length>0,"original CHAO MEMORIES panel still opens and reads canonical history");
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"memory-panel.png"));
            memoriesPanel.Hide();memoriesOpen=false;
            string Signature(PetState s)=>JsonSerializer.Serialize(s.History.Events.OrderBy(e=>e.Key,StringComparer.Ordinal));
            var expected=roster.ToDictionary(r=>r.Key,r=>Signature(r.Value));Save();var loaded=SaveStore.Load(savePath,out _);
            Check(Signature(loaded)==expected["main"] && loaded.GardenEggs.Where(e=>e.Chao!=null).All(e=>Signature(e.Chao!)==expected[e.Id]),"save/load preserves exact event values, participants and occurrences");
            Check(innate==JsonSerializer.Serialize(all.Select(s=>s.Personality)),"real scenario leaves all thirteen innate traits intact");
            File.WriteAllText(Path.Combine(directory,"memory-diagnostics-check.json"),JsonSerializer.Serialize(new{passed=true,checks,ids=roster.Keys,retained=snapshot.count}));GD.Print("MEMORY_DIAGNOSTICS_SCENE_OK");
            if(OS.GetCmdlineUserArgs().Contains("--memory-diagnostics-live"))
            {
                File.WriteAllText(Path.Combine(directory,"memory-diagnostics-live-ready.json"),"{}");
                for(int f=0;f<1500 && !File.Exists(Path.Combine(directory,"memory-diagnostics-live-done.json"));f++)
                {await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);bridge.Poll(roster,true,EditorRhythmContext,ExpressionDiagnostics);}
                Check(File.Exists(Path.Combine(directory,"memory-diagnostics-live-done.json")) && JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"memory-diagnostics-live-done.json")))!["passed"]!.GetValue<bool>(),"real editor read-only live checks passed");
                Check(roster.All(r=>Signature(r.Value)==expected[r.Key]),"opening/filtering/closing editor history changed no events");
            }
            GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("MEMORY_DIAGNOSTICS_SCENE_FAILED: "+ex);GetTree().Quit(1);}
    }
}
