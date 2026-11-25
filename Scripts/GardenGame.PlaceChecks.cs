using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private bool placeCheckStarted;
    private int placeCheckFrames;
    private void RunPlaceChecks()
    {if(placeCheckStarted || ++placeCheckFrames<3)return;placeCheckStarted=true;CheckPlaceScene();}
    private async void CheckPlaceScene()
    {
        try
        {
            SetPhysicsProcess(false);GD.Seed(1511);Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            GetWindow().Title="MoriGarden — GENERATED PLACE PREFERENCE TEST";
            PetState State(string name)=>new(){Name=name,Fullness=80,Energy=80,Fun=100,Joy=65,SocialNeed=20,Run=180,Personality=new(),PersonalitySchemaVersion=1};
            void Fixture()
            {
                pet=State("Walker");pet.GardenEggs.Clear();pet.GardenEggs.Add(new(){Id="sky-resident",Hatched=true,Color="White",X=0,Z=12,Chao=State("Sky watcher")});
                pet.GardenEggs.Add(new(){Id="neutral-resident",Hatched=true,Color="Yellow",X=24,Z=14,Chao=State("Neutral")});
                petZone=GardenZone.Garden;carrying=false;callTime=0;mainActivity=null;SwitchZone(GardenZone.Garden,restoring:true);
                friend.Position=ChaoLeisure.Routes[1][0];friend.Visible=true;player.Position=new(30,0,30);wanderTarget=friend.Position;wanderTime=0;
                chaoMotion.Reset();ImportedChao.ClearActionPause(friendArt);MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);
            }
            Fixture();await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var sky=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id=="sky-resident");
            sky.PrepareActivityFrame(0);ImportedChao.ClearActionPause(sky.Art);sky.LeisureExecutor.BeginSky(ChaoLeisure.SkySpots[0]);
            moriLeisure.BeginRoute(1);
            const string routeId="garden/leisure/route-1",skyId="garden/leisure/sky-0";
            for(int frame=0;frame<18000;frame++)
            {
                const float dt=1f/60;pet.Tick(dt,false);UpdateEggs(dt);MoveFriend(dt);
                if(frame%120==0)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(pet.History.Events.Any(e=>e.Source==routeId) && sky.Chao!.History.Events.Any(e=>e.Source==skyId))break;
                if(!moriLeisure.Active && moriLeisure.LastFailureReason.Length>0)throw new Exception("Existing route failed: "+moriLeisure.LastFailureReason);
            }
            var route=pet.History.Events.SingleOrDefault(e=>e.Source==routeId);var skyEvent=sky.Chao!.History.Events.SingleOrDefault(e=>e.Source==skyId);
            if(route==null || skyEvent==null || route.Occurrences!=1 || skyEvent.Occurrences!=1 || route.Place!=LeisureDestinationCatalog.Find(routeId)!.End || skyEvent.Place!=LeisureDestinationCatalog.Find(skyId)!.End)
                throw new Exception($"Actual route/sky completion missing: route={route!=null}, sky={skyEvent!=null}");
            for(int n=0;n<60;n++){moriLeisure.Step(friend,friendArt,pet,chaoMotion,.01f,false);sky.LeisureExecutor.Step(sky,sky.Art,sky.Chao,new ChaoMotion(),.01f,false);}
            if(route.Occurrences!=1 || skyEvent.Occurrences!=1)throw new Exception("Idle processing replayed a completed outing");
            string learnedItems=JsonSerializer.Serialize(pet.LearnedPreferences);var innate=pet.Personality!.Copy();
            Save();var loaded=SaveStore.Load(savePath,out _);
            if(loaded.GardenEggs.Count!=2 || loaded.GardenEggs[0].Id!="sky-resident" || !loaded.History.Events.Any(e=>e.Source==routeId) ||
                !loaded.GardenEggs[0].Chao!.History.Events.Any(e=>e.Source==skyId) || !loaded.Personality!.Equals(innate) || JsonSerializer.Serialize(loaded.LearnedPreferences)!=learnedItems)
                throw new Exception("Place memory save/load lost data");
            GD.Print("PLACE_COMPLETION_OK: main MoveFriend completed route-1 and final pause; resident GardenEgg completed sky-0 and rising; save/load and duplicate guard");
            Fixture();moriLeisure.BeginRoute(1);MoveFriend(.02f);moriLeisure.Cancel(friendArt);
            if(pet.History.Events.Any(e=>e.Source.StartsWith(LeisureDestinationCatalog.SourcePrefix)))throw new Exception("Cancelled route recorded success");
            moriLeisure.BeginRoute(1);moriLeisure.Step(friend,friendArt,pet,chaoMotion,301,true);
            if(moriLeisure.Active || moriLeisure.LastFailureReason!="Leisure route timed out" || pet.History.Events.Count!=0)throw new Exception("Route timeout credited experience");
            moriLeisure.BeginSky(friend.Position);moriLeisure.Cancel(friendArt);
            if(pet.History.Events.Count!=0)throw new Exception("Interrupted sky outing recorded success");
            friend.Position=ChaoLeisure.Routes[1][0]+Vector3.Back;ImportedChao.ClearActionPause(friendArt);moriLeisure.BeginRoute(1);MoveFriend(.02f);pet.Energy=5;MoveFriend(.02f);
            if(moriLeisure.Active || pet.History.Events.Count!=0)throw new Exception("Critical need did not cancel cleanly");
            Fixture();friend.Position=ChaoLeisure.Routes[1][0]+Vector3.Back;moriLeisure.BeginRoute(1);
            var frozen=friend.Position;
            for(int n=0;n<35 && moriLeisure.Active;n++){friend.Position=frozen;moriLeisure.Step(friend,friendArt,pet,chaoMotion,.1f,true);}
            if(moriLeisure.Active || moriLeisure.LastFailureReason!="Leisure route blocked" || pet.History.Events.Count!=0)throw new Exception("Blocked route credited experience");
            Fixture();friend.Position=ChaoLeisure.SkySpots[0];moriLeisure.BeginSky(friend.Position);
            moriLeisure.Step(friend,friendArt,pet,chaoMotion,.01f,true);moriLeisure.Step(friend,friendArt,pet,chaoMotion,24,true);
            if(pet.History.Events.Count!=0)throw new Exception("Sky experience credited before rising finished");
            moriLeisure.Cancel(friendArt);moriLeisure.Step(friend,friendArt,pet,chaoMotion,2,false);
            if(pet.History.Events.Count!=0)throw new Exception("Interrupted rising credited experience");
            Fixture();moriLeisure.BeginRoute(1);chaoMotion.GlideFromPerch(friend,Vector3.Back,pet);
            moriLeisure.Step(friend,friendArt,pet,chaoMotion,.02f,true);
            if(moriLeisure.Active || pet.History.Events.Count!=0)throw new Exception("Airborne interruption credited experience");
            Fixture();sky=world.GetChildren().OfType<GardenEgg>().Single(e=>e.Data.Id=="sky-resident");sky.PrepareActivityFrame(0);sky.LeisureExecutor.BeginSky(ChaoLeisure.SkySpots[0]);
            UpdateEggs(.02f);sky.Hold(sky.Position,false,.02f);
            if(sky.LeisureExecutor.Active || sky.Chao!.History.Events.Count!=0)throw new Exception("Pickup credited resident sky watching");
            carrying=true;SwitchZone(GardenZone.CoastalCove);carrying=false;MainActivity.Begin(0,pet,ActivityContext,false,false,false,false);
            moriLeisure.BeginRoute(1);moriLeisure.Step(friend,friendArt,pet,chaoMotion,.02f,true);
            if(pet.History.Events.Count!=0 || moriLeisure.Active)throw new Exception("Coastal Cove applied garden destination preference");
            Fixture();var residents=world.GetChildren().OfType<GardenEgg>().ToArray();
            PetState[] states={pet,residents.Single(e=>e.Data.Id=="sky-resident").Chao!,residents.Single(e=>e.Data.Id=="neutral-resident").Chao!};
            var ids=LeisureDestinationCatalog.All.Select(d=>d.Id).ToArray();
            for(int n=0;n<5;n++)for(int p=0;p<2;p++){states[p].History.Advance(120);ChaoLeisurePreferenceScorer.RecordSuccess(states[p].History,"garden",p==0 ? skyId : routeId);}
            int[][] counts=states.Select(_=>new int[8]).ToArray();
            for(int seed=0;seed<1000;seed++)for(int p=0;p<3;p++)
            {
                // Controlled candidate/position reset, no simulated successes.
                friend.Position=new(12,0,12);ImportedChao.ClearActionPause(friendArt);var executor=new ChaoLeisure(new Random(seed+9181));
                executor.BindContext(states[p],travel.AreaId(zone),"controlled-"+p);executor.Reconsider(friend);
                executor.Step(friend,friendArt,states[p],new ChaoMotion(),.001f,true);
                int chosen=Array.IndexOf(ids,executor.CurrentDestinationId);if(chosen<0)throw new Exception("Available destination selection failed");counts[p][chosen]++;
                executor.Cancel(friendArt);
            }
            if(counts.Any(c=>c.Count(n=>n>0)<2))throw new Exception("Destination selection locked to one place");
            GD.Print("PLACE_ENGINE_COUNTS: "+JsonSerializer.Serialize(counts));
            if(!states[0].Personality!.Equals(states[1].Personality) || JsonSerializer.Serialize(states[0].LearnedPreferences)!=learnedItems)throw new Exception("Place sampling changed unrelated traits");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(savePath)!,"place-check.json"),JsonSerializer.Serialize(new{passed=true,ids,counts,decisionsPerChao=1000,mainRoute=true,residentSky=true,cancellations=true,saveLoad=true}));
            GD.Print("PLACE_SCENE_OK: actual main/resident completions, cancelled/picked-up/timeout/critical outings, area isolation, controlled actor-specific selection, compatible persistence");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError("PLACE_SCENE_FAILED: "+ex);GetTree().Quit(1);}
    }
}
