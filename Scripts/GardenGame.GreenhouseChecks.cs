using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private int greenhouseCheckFrames;
    private void RunGreenhouseCheckMode()
    {
        if(++greenhouseCheckFrames<3)return;
        try{CheckGreenhouse();GetTree().Quit();}
        catch(Exception ex){GD.PushError("GREENHOUSE_CHECK_FAILED: "+ex);GetTree().Quit(1);}
    }
    private void CheckGreenhouse()
    {
        var original=pet;var originalPetZone=petZone;var originalPetAt=petZonePosition;
        void Fixture(int count,string area=WorldCatalog.Greenhouse)
        {
            pet=new PetState { Name="Mori",Run=180,Curiosity=95,Initiative=950,Intelligence=950,Sociability=100,Bond=90 };
            pet.GardenEggs.Clear();
            for(int i=1;i<count;i++)pet.GardenEggs.Add(new() { Id="greenhouse-test-"+i,Hatched=true,Color=i==1 ? "Black" : "Yellow",X=i+3,Z=5,
                Chao=new() { Run=160,Curiosity=95,Initiative=900,Intelligence=900,Sociability=100,Bond=85 } });
            pet.Expedition.LastTeamIds=ExpeditionRoster.Available(pet).Select(p=>p.Id).ToList();
            foreach(var id in pet.Expedition.LastTeamIds.Skip(1)){var link=pet.SocialLink("main",id,true)!;link.Met=true;link.Score=70;}
            carrying=false;paused=false;slotMode=SlotMode.Closed;travel.ExpeditionArea=area;SwitchZone(GardenZone.Expedition,restoring:true);
            expeditionTeam!.Discoveries.AutoScan=false;
        }
        void Step(int ticks,bool companions=false)
        {for(int i=0;i<ticks;i++){if(companions){expeditionTeam!.Step(1f/60,player.Position,camera);expeditionTeam.PushPlayer(player);}AdvanceSiteMission(1f/60);}}
        void Stand(string id)
        {
            var element=SiteMission!.Elements.First(e=>e.Id==id);
            player.Position=element.Position+Vector3.Back*1.65f;player.Rotation=new(0,Mathf.Pi,0);verticalSpeed=0;
        }
        void Operate(string id)
        {
            Stand(id);Interact();if(SiteMission!.PlayerAction.Target?.Id!=id)throw new Exception("Player could not begin "+id);
            Step(100);
        }
        void Travel(Vector3 point)
        {
            portalCooldown=0;player.Position=new(0,0,10);CheckPortals();player.Position=point;CheckPortals();
            if(transition<0)throw new Exception("Site portal failed");AdvanceTransition(.36f);AdvanceTransition(.36f);
        }
        try
        {
            for(int count=1;count<=3;count++)
            {
                Fixture(count,WorldCatalog.Meadow);Travel(new(3,0,-21));
                if(travel.ExpeditionArea!=WorldCatalog.Greenhouse || expeditionTeam!.Members.Count!=count || !ReferenceEquals(expeditionTeam.Members[0].Pet.State,pet))
                    throw new Exception("Site arrival changed companion identities or count");
                expeditionTeam.Discoveries.AutoScan=false;
                if(player.Position.DistanceTo(new(0,0,17))>.03f)throw new Exception("Greenhouse arrival is unsafe");
                foreach(var point in new[]{new Vector3(0,0,17),new Vector3(3,0,5),new Vector3(6,0,-7)})
                    if(GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point+Vector3.Up*2,point-Vector3.Up,1)).Count==0)
                        throw new Exception("Greenhouse floor has a hole");
                Stand(GreenhouseRules.Intake);Interact();player.Position+=Vector3.Right;Step(1);
                if(SiteMission!.PlayerAction.Target!=null || SiteMission.Progress.Element(GreenhouseRules.Intake).Value!=0)throw new Exception("Moving did not cancel the player operation");
                Operate(GreenhouseRules.Intake);Operate(GreenhouseRules.Diverter);
                if(SiteMission.Progress.Element(GreenhouseRules.Diverter).Value!=1 || GreenhouseRules.WaterRunning(SiteMission.Progress,true))throw new Exception("Wrong distributor routing solved the puzzle");
                Operate(GreenhouseRules.Diverter);
                player.Position=new(6,.02f,-7);Step(300);
                if(SiteMission.Progress.Restoration<=.1f || SiteMission.Progress.Completed)throw new Exception("Water did not gradually restore the central bed");
                paused=true;slotMode=SlotMode.Save;RefreshSaveSlots();saveSlots.Select(17);SaveToSlot();paused=false;slotMode=SlotMode.Closed;
                float partial=SiteMission.Progress.Restoration;
                Travel(new(0,0,21));
                if(travel.ExpeditionArea!=WorldCatalog.Meadow || expeditionTeam!.Members.Count!=count)throw new Exception("Site return lost the selected team");
                paused=true;slotMode=SlotMode.Load;RefreshSaveSlots();saveSlots.Select(17);LoadFromSlot();
                if(travel.ExpeditionArea!=WorldCatalog.Greenhouse || Math.Abs(SiteMission!.Progress.Restoration-partial)>.01f ||
                    !ReferenceEquals(expeditionTeam!.Members[0].Pet.State,pet))throw new Exception("Partial site save did not restore the same pet states");
                expeditionTeam.Discoveries.AutoScan=false;Step(1000);
                if(!SiteMission.Progress.Completed || pet.SeedCount(FruitKinds.Moonblossom)!=1 || world.GetNode<Node3D>("CentralBed/RecoveringPlants").Scale.Y<1)
                    throw new Exception("Player-only completion, plants or rare reward failed");
                Step(200);Travel(new(0,0,21));Travel(new(3,0,-21));Step(20);
                if(!SiteMission!.Progress.Completed || pet.SeedCount(FruitKinds.Moonblossom)!=1 || SiteMission.Elements.Last().Activate())throw new Exception("Revisiting reset the solved mission or repeated its reward");
                // New isolated attempt: a low-stat pet can help by request using the same discovery intent.
                Fixture(count);var helper=expeditionTeam!.Members[0];helper.Pet.State.Initiative=helper.Pet.State.Intelligence=0;helper.Pet.State.Curiosity=-60;helper.Pet.State.Bond=0;
                var intake=SiteMission!.Elements.First(e=>e.Id==GreenhouseRules.Intake);
                helper.Body.Position=intake.Position+Vector3.Back*2.6f;helper.SetIntent(ExpeditionIntent.Observe,helper.Body.Position,100);
                Stand(GreenhouseRules.Intake);_UnhandledKeyInput(new InputEventKey { PhysicalKeycode=Key.F,Pressed=true });
                if(!expeditionTeam.Discoveries.Sessions.Any(s=>s.Requested))throw new Exception("An inexperienced Chao could not accept a useful request");
                Step(1800,true);
                if(SiteMission.Progress.Element(GreenhouseRules.Intake).Value!=1 || SiteMission.ChaoActions<1 || helper.Recoveries!=0)
                    throw new Exception($"Requested Chao action failed for {count}: {helper.RouteDebug}, {string.Join(';',expeditionTeam.Discoveries.Events)}");
                // Natural initiative uses ordinary perception and weighted decisions, not a forced-use command.
                Fixture(count);var actor=expeditionTeam!.Members[0];intake=SiteMission!.Elements.First(e=>e.Id==GreenhouseRules.Intake);
                actor.Body.Position=intake.Position+Vector3.Back*2;actor.Body.Rotation=new(0,Mathf.Pi,0);actor.SetIntent(ExpeditionIntent.Observe,actor.Body.Position,100);
                Stand(GreenhouseRules.Intake);
                bool tried=false;
                for(int attempt=0;attempt<8 && !tried;attempt++)
                {
                    pet.Expedition.ExplorationSeconds+=130;
                    expeditionTeam!.Discoveries.Scan(actor);
                    Step(2100,true);tried=SiteMission!.ChaoActions>0;
                }
                if(!tried || SiteMission!.Progress.Element(GreenhouseRules.Intake).Value!=1)throw new Exception("Independent Chao never attempted a useful mechanism");
                if(count>1)
                {
                    Fixture(count);var dial=SiteMission!.Elements.First(e=>e.Id==GreenhouseRules.Diverter);
                    var source=expeditionTeam!.Members[0];var buddy=expeditionTeam.Members[1];
                    source.Body.Position=dial.Position+new Vector3(-.3f,0,2.3f);buddy.Body.Position=dial.Position+new Vector3(1.2f,0,1.5f);
                    foreach(var petActor in expeditionTeam.Members)petActor.SetIntent(ExpeditionIntent.Observe,petActor.Body.Position,100);
                    Stand(GreenhouseRules.Diverter);
                    bool shared=false;
                    for(int trial=0;trial<4 && !shared;trial++)
                    {
                        expeditionTeam.Discoveries.CancelAll("cooperation fixture");
                        foreach(var member in expeditionTeam.Members)
                        {
                            member.SetIntent(ExpeditionIntent.Observe,member.Body.Position,100);
                            var memory=pet.Expedition.Memory(member.Pet.Id);memory.Objects.Clear();memory.LastInvitation=-1000;
                        }
                        Step(250,true);
                        expeditionTeam.Discoveries.Start(source,dial.Interest,new(DiscoveryChoice.Approach,4,false,true),true);
                        Step(2400,true);
                        shared=expeditionTeam.Discoveries.Events.Any(e=>e.Action=="invited peer");
                    }
                    if(!shared || pet.Expedition.Memory(buddy.Pet.Id).Find(dial.Interest.Definition.Id)==null || SiteMission.ChaoActions<1)
                        throw new Exception("Friendly Chao did not jointly inspect and operate a mission mechanism");
                    var record=pet.Expedition.Memory(buddy.Pet.Id);var prior=dial.Interest.Definition;
                    record.Notice(prior.Id,pet.Expedition.ExplorationSeconds,prior.Revision);
                    Operate(GreenhouseRules.Intake);
                    if(dial.Interest.Definition.Revision<=prior.Revision || !record.CanConsider(dial.Interest.Definition,pet.Expedition.ExplorationSeconds))
                        throw new Exception("Chao could not notice a changed irrigation system");
                }
                // The movable body must move through collision tests, not snap to a destination.
                var crate=SiteMission.Counterweight;var plate=SiteMission.Elements.First(e=>e.Id==GreenhouseRules.Plate);
                var start=crate.Position;var direction=plate.Position-crate.Position;direction.Y=0;direction=direction.Normalized();player.Position=crate.Position-direction*.95f+Vector3.Up*.02f;
                // Native broadphase transforms flush between engine physics frames. A long dynamic-body push
                // is exercised by --greenhouse-review; this synchronous check verifies a real sweep and persisted pose.
                crate.Push(player,direction*.2f);
                if(ExpeditionCompanion.Flat(start,crate.Position)<.1f || crate.Position.Y<-.2f || Math.Abs(crate.State().X-crate.Position.X)>.001f)
                    throw new Exception($"Counterweight failed its collision sweep / persistence: {start} → {crate.Position}; {crate.LastContact}");
                crate.Position=plate.Position+Vector3.Up*.01f;crate.Capture();AdvanceSiteMission(1f/60);
                if(!SiteMission.PlateDown)throw new Exception("The positioned counterweight did not hold its pressure plate");
                var m=expeditionTeam!.Members[0];m.Body.Position=new(14.2f,0,10);m.Body.Rotation=new(0,Mathf.Pi/2,0);
                var motion=new ChaoMotion { GardenPond=false };
                for(int i=0;i<240;i++)motion.Step(m.Body,Vector3.Right*5,m.Pet.State,false,false,1f/60,false,out _);
                if(m.Body.Position.X>14.9f || m.Body.Position.X<14.3f)throw new Exception("A Chao crossed the greenhouse wall or never approached it");
                player.Position=new(14,0,10);cameraRig.Yaw=Mathf.Pi/2;cameraRig.Distance=6;cameraRig.Follow(player.Position,0,true);
                if(camera.GlobalPosition.X>14.9f || !camera.GlobalPosition.IsFinite())throw new Exception("The greenhouse wall did not protect the camera");
            }
            GD.Print("GREENHOUSE_OK: bidirectional site, 1/2/3 live pets, player-only solution, low-stat requests, autonomous actions, cancellation, counterweight/plate, partial save/load, one reward, revisit, walls, floor");
        }
        finally
        {
            pet=original;petZone=originalPetZone;petZonePosition=originalPetAt;paused=false;slotMode=SlotMode.Closed;travel.ExpeditionArea=WorldCatalog.Meadow;
            SwitchZone(GardenZone.Garden,restoring:true);
        }
    }
}
