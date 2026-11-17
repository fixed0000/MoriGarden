using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int wildlifeCheckFrame;
    private void RunWildlifeCheckMode()
    {
        if(++wildlifeCheckFrame<3)return;
        try{CheckWildlife();GetTree().Quit();}
        catch(Exception ex){GD.PushError("WILDLIFE_CHECK_FAILED: "+ex);GetTree().Quit(1);}
    }
    private void CheckWildlife()
    {
        var original=pet;var originalZone=petZone;var originalAt=petZonePosition;
        void Step(int ticks,bool team=false)
        {
            for(int i=0;i<ticks;i++)
            {
                if(team)expeditionTeam!.Step(1f/60,player.Position,camera);
                AdvanceWildlife(1f/60);
            }
        }
        void Stand(WildAnimal animal,float distance=1.35f)
        {
            bool found=false;
            for(int side=0;side<16;side++)
            {
                var raw=animal.Position+(Vector3.Back*distance).Rotated(Vector3.Up,side*Mathf.Tau/16);raw.Y=expeditionTeam!.Navigation.Height(raw.X,raw.Z)+.02f;
                if(!expeditionTeam.Navigation.Safe(raw) || !Wildlife!.VisibleTo(raw+Vector3.Up*.9f,animal))continue;
                player.Position=raw;found=true;break;
            }
            if(!found)throw new Exception("No visible safe approach to "+animal.Species.Id);
            player.Position=new(player.Position.X,expeditionTeam.Navigation.Height(player.Position.X,player.Position.Z),player.Position.Z);
            player.Rotation=new(0,Mathf.Atan2(animal.Position.X-player.Position.X,animal.Position.Z-player.Position.Z),0);player.Velocity=Vector3.Zero;
        }
        try
        {
            for(int count=1;count<=3;count++)
            {
                pet=new PetState { Run=200,Curiosity=100,Initiative=950,Intelligence=900,Kindness=95,Sociability=100,Bond=90 };
                for(int i=1;i<count;i++)pet.GardenEggs.Add(new(){Id="wildlife-test-"+i,Hatched=true,Chao=new(){Run=150,Curiosity=80,Initiative=850,Sociability=90,Bond=85}});
                pet.Expedition.LastTeamIds=ExpeditionRoster.Available(pet).Select(p=>p.Id).ToList();
                petZone=GardenZone.Garden;carrying=false;paused=false;travel.ExpeditionArea=WorldCatalog.Meadow;SwitchZone(GardenZone.Expedition,restoring:true);
                var wildlife=Wildlife!;expeditionTeam!.Discoveries.AutoScan=false;Step(1);
                if(wildlife.Animals.Count!=4 || wildlife.Animals.Any(a=>!a.Present || !expeditionTeam.Navigation.Safe(a.Position)))throw new Exception("Habitat spawn is missing or unsafe");
                if(wildlife.Animals.Single(a=>a.Species.Id=="penguin").Position.DistanceTo(new Vector3(14,WindfallMeadow.Height(14,9),9))>4)throw new Exception("Shore species left its habitat");
                var rabbit=wildlife.Animals.Single(a=>a.Species.Id=="rabbit");Stand(rabbit);Step(600);
                if(!rabbit.ReadyToJoin || rabbit.Helped)throw new Exception("Calm solo approach failed");
                Face(rabbit.Position);Interact();
                if(rabbit.Present || pet.Wildlife.Count("rabbit")!=1 || wildlife.Interact(player))throw new Exception("Solo capture duplicated or did not enter bag");
                var bird=wildlife.Animals.Single(a=>a.Species.Id=="songbird");Stand(bird);expeditionTeam.Reposition(player.Position);
                if(!wildlife.Ask(player,expeditionTeam))throw new Exception("Could not request an animal greeting");
                player.Position=expeditionTeam.Navigation.Nearest(bird.Position+Vector3.Back*5);Step(2400,true);
                if(!bird.Helped || bird.Trust<.2f)throw new Exception($"Existing discovery interaction did not help the animal: count={count}, bird={bird.Position}, player={player.Position}, pets={string.Join(';',expeditionTeam.Members.Select(m=>$"{m.Pet.Id}:{m.Body.Position}/{m.Intent}/{m.NavigationFailure}"))}, events={string.Join(';',expeditionTeam.Discoveries.Events.TakeLast(7).Select(e=>$"{e.Pet}:{e.Action}"))}");
                Stand(bird);Step(600,true);Face(bird.Position);Interact();
                if(pet.Wildlife.Count("songbird")!=1)throw new Exception($"Assisted capture failed: count={count} trust={bird.Trust} present={bird.Present} pos={bird.Position} player={player.Position} intent={bird.Intent} hint={wildlife.Hint(player)} toast={toast.Text}");
                Save();var loaded=SaveStore.Load(savePath,out _);
                if(loaded.Wildlife.Total!=2 || !loaded.Wildlife.Slot(rabbit.Habitat.Id).Collected)throw new Exception("Capture persistence failed");
                var bear=wildlife.Animals.Single(a=>a.Species.Id=="bear-cub");Stand(bear,1.3f);player.Velocity=Vector3.Back*5.5f;Step(1);
                if(!bear.Fleeing)throw new Exception($"Sprinting did not trigger retreat: count={count}, distance={ExpeditionCompanion.Flat(bear.Position,player.Position)}, visible={wildlife.VisibleTo(player.Position+Vector3.Up*.9f,bear)}, state={bear.Intent}");player.Velocity=Vector3.Zero;
                pet.Wildlife.Advance(400);player.Position=new(0,0,24);camera.GlobalPosition=new(0,3,24);camera.LookAt(new Vector3(0,2,30));Step(180);
                if(!rabbit.Present || pet.Wildlife.Slot(rabbit.Habitat.Id).Generation!=1 || pet.Wildlife.Count("rabbit")!=1)throw new Exception("Offscreen renewable spawn changed ownership or failed");
                if(pet.Wildlife.Obtain(rabbit.Habitat.Id,"rabbit",0))throw new Exception("Old capture generation was accepted");
                // Perception and ordinary weighted decisions can greet an animal without a request.
                foreach(var interest in expeditionTeam.Discoveries.Perception.Interests)interest.Available=interest==rabbit.Interest;
                var explorer=expeditionTeam.Members[0];explorer.Pet.State.Curiosity=100;explorer.Pet.State.Initiative=990;explorer.Pet.State.Intelligence=990;explorer.Pet.State.Energy=95;
                expeditionTeam.Discoveries.CancelAll("autonomy fixture");
                player.Position=expeditionTeam.Navigation.Nearest(rabbit.Position+Vector3.Back*5);
                explorer.Body.Position=expeditionTeam.Navigation.Nearest(rabbit.Position+Vector3.Left*2);
                explorer.Body.Rotation=new(0,Mathf.Atan2(rabbit.Position.X-explorer.Body.Position.X,rabbit.Position.Z-explorer.Body.Position.Z),0);
                for(int attempt=0;attempt<5 && !rabbit.Helped;attempt++)
                {
                    pet.Expedition.ExplorationSeconds+=130;explorer.SetIntent(ExpeditionIntent.Observe,explorer.Body.Position,100);
                    expeditionTeam.Discoveries.Scan(explorer);Step(2200,true);
                }
                if(!rabbit.Helped || !expeditionTeam.Discoveries.Events.Any(e=>e.Object==rabbit.Interest.Definition.Id && e.Action=="mechanism action"))throw new Exception("Independent perception and greeting failed");
                SwitchZone(GardenZone.Garden);AdvanceChaoMemories(.02f);bagAnimals.Select(Array.FindIndex(AnimalCatalog.All,a=>a.Id=="rabbit"));TakeSelectedAnimal();
                if(heldAnimal?.InventorySpecies!="rabbit" || AvailableAnimal("rabbit")!=0)throw new Exception("Garden inventory did not reserve animal ownership");
                UpdatePickupPose(PickupDuration);friend.Position=new(1,0,3);player.Position=new(1,0,4);petZone=GardenZone.Garden;friend.Visible=true;sleeping=false;
                Face(friend.Position);int run=pet.Stat("Run").Level*100+pet.Stat("Run").Progress;RunOfferCheck();
                if(pet.Stat("Run").Level*100+pet.Stat("Run").Progress!=run+40 || pet.Wildlife.Count("rabbit")!=0 || heldAnimal!=null || pet.RecentAnimalSkill!="Run")throw new Exception("Captured animal did not train through the original gift animation");
                if(!pet.History.KnowsAnimal("rabbit"))throw new Exception("Meeting an animal during garden training was not remembered");
                bagAnimals.Select(Array.FindIndex(AnimalCatalog.All,a=>a.Id=="songbird"));TakeSelectedAnimal();
                SwitchZone(GardenZone.Lobby);
                if(pet.Wildlife.Count("songbird")!=1)throw new Exception("Unused animal was lost during travel");
                Save();loaded=SaveStore.Load(savePath,out _);
                if(loaded.Wildlife.Count("rabbit")!=0 || loaded.Wildlife.Count("songbird")!=1 || loaded.Run!=pet.Run)throw new Exception("Training was not saved atomically with inventory");
            }
            // Optional post-restoration encounter never changes the mission's first-clear reward.
            pet=new PetState();travel.ExpeditionArea=WorldCatalog.Greenhouse;SwitchZone(GardenZone.Expedition,restoring:true);
            if(Wildlife!.Animals.Any(a=>a.Present))throw new Exception("Greenhouse wildlife unlocked before restoration");
            pet.Expedition.AreaFlags[WorldCatalog.Meadow]=new(){"greenhouse-restored"};player.Position=new(0,0,17);camera.GlobalPosition=player.Position+Vector3.Up*2;camera.LookAt(new Vector3(0,2,23));Step(150);
            if(!Wildlife.Animals.Single().Present || pet.SeedCount(FruitKinds.Moonblossom)!=0)throw new Exception("Optional encounter changed mission reward or failed to unlock");
            GD.Print("WILDLIFE_OK: 1/2/3 pets, safe habitats, solo and Chao-assisted capture, sprint retreat, bag, live training, save/load, travel ownership, respawn generations, optional greenhouse encounter");
        }
        finally {pet=original;petZone=originalZone;petZonePosition=originalAt;SwitchZone(GardenZone.Garden,restoring:true);}
    }
}
