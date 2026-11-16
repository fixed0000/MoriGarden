using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckExpeditionFoundation()
    {
        carrying=false; heldEgg=null; heldFruit=null; heldToy=null; heldAnimal=null;
        offerTime=eatTime=thinkTime=releasePose=0; pickingItem=false;
        SwitchZone(GardenZone.Lobby);
        void Enter(Vector3 portal)
        {
            portalCooldown=0; player.Position=Vector3.Zero; CheckPortals();
            player.Position=portal; CheckPortals();
            if(teamUi.Choosing) { pet.Expedition.LastTeamIds=new() { "main" };teamUi.Cancel();
                portalCooldown=0;player.Position=Vector3.Zero;CheckPortals();player.Position=portal;CheckPortals();
                // Select through the same roster used by the picker.
                teamUi.Confirm(); }
            if (transition<0) throw new Exception("Travel graph did not activate a portal");
            AdvanceTransition(.36f); AdvanceTransition(.36f);
        }
        carrying=true;
        portalCooldown=0; player.Position=Vector3.Zero; CheckPortals();
        player.Position=new(-8,0,6); CheckPortals(); carrying=false;
        if(transition>=0 || teamUi.Choosing) throw new Exception("Carried items bypassed the expedition entry guard");
        Enter(new(-8,0,6));
        if(zone!=GardenZone.Expedition || travel.ExpeditionArea!=WorldCatalog.Meadow ||
            player.Position.DistanceTo(new(0,0,23))>.01f || friend.Visible ||
            world.GetChildren().Any(n=>n is GardenEgg or ToyCar or TrainingAnimal) ||
            !pet.Expedition.VisitedAreas.Contains(WorldCatalog.Meadow))
            throw new Exception("Expedition arrival, population isolation, or discovery failed");
        CheckExpeditionTeam();
        portalCooldown=0; CheckPortals();
        if(transition>=0) throw new Exception("Expedition arrival immediately retriggered a portal");
        foreach(var point in new[]{new Vector3(0,0,18),new Vector3(-21,0,-10),new Vector3(15,0,12)})
        {
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point+Vector3.Up*8,point+Vector3.Down*3,1));
            if(hit.Count==0 || ((Vector3)hit["normal"]).Y<.7f) throw new Exception($"Meadow has an unsafe terrain sample at {point}: {hit}");
        }
        player.Position=new(0,.05f,19); verticalSpeed=0; cameraRig.Yaw=0;
        DriveShoreCheck(90,Key.W);
        if(player.Position.Z>15 || player.Position.Y<-.05f) throw new Exception("Player cannot traverse meadow terrain");
        player.Position=new(35,ExpeditionArt.Height(35,0)+.02f,0); verticalSpeed=0;
        DriveShoreCheck(100,Key.D);
        if(player.Position.X>=37.8f || player.Position.Y<-.1f) throw new Exception("Player escaped the expedition cliffs");
        player.Position=new(0,.02f,19); cameraRig.Distance=6.5f;
        for(int i=0;i<8;i++) { cameraRig.Yaw=i*Mathf.Tau/8; cameraRig.Follow(player.Position,.05f,true); }
        if(!camera.GlobalPosition.IsFinite() || camera.GlobalPosition.DistanceTo(player.Position)<1) throw new Exception("Expedition camera did not follow the player");
        player.Position=new(35,ExpeditionArt.Height(35,0)+.02f,0); cameraRig.Yaw=Mathf.Pi/2;
        cameraRig.Follow(player.Position,0,true);
        if(camera.GlobalPosition.X>37.5f || camera.GlobalPosition.DistanceTo(player.Position)>5)
            throw new Exception("Expedition camera clips through the boundary cliffs");
        player.Position=new(0,.02f,19);
        pet.Expedition.UnlockedAreas.Add("future-region");
        pet.Expedition.AreaFlags[WorldCatalog.Meadow]=new() { "test-visited" };
        paused=true; slotMode=SlotMode.Save; RefreshSaveSlots(); saveSlots.Select(18); SaveToSlot();
        var snapshot=SaveStore.LoadSlot(SlotPath(18));
        if(snapshot?.ManualLocation?.AreaId!=WorldCatalog.Meadow || !snapshot.Expedition.UnlockedAreas.Contains("future-region"))
            throw new Exception("Expedition save lost its stable area or progress");
        paused=false; slotMode=SlotMode.Closed;
        Enter(new(0,0,27));
        if(zone!=GardenZone.Lobby || player.Position.DistanceTo(new(-8,0,9))>.01f) throw new Exception("Expedition return point is wrong");
        paused=true; slotMode=SlotMode.Load; RefreshSaveSlots(); saveSlots.Select(18); LoadFromSlot();
        if(zone!=GardenZone.Expedition || player.Position.DistanceTo(new(0,.02f,19))>.04f ||
            !pet.Expedition.AreaFlags[WorldCatalog.Meadow].Contains("test-visited")) throw new Exception("Loading an expedition slot failed");
        Enter(new(0,0,27));
        Enter(new(0,0,-10));
        if(zone!=GardenZone.Garden) throw new Exception("Original garden portal no longer works");
        // Also test the data-driven stadium route and its return, not only direct scene switches.
        Enter(ZoneArt.GardenLobbyGate); Enter(ZoneArt.PlazaStadiumPad);
        if(zone!=GardenZone.Stadium) throw new Exception("Original stadium portal no longer works");
        Enter(new(0,0,-10)); Enter(new(0,0,-10));
        if(zone!=GardenZone.Garden) throw new Exception("Original route back to the garden failed");
        GD.Print("EXPEDITION_OK: round trip, legacy portals, save/load, isolation, terrain, boundaries, camera");
    }

    private void PreviewExpedition()
    {
        if(frame==290)
        {
            carrying=false; SwitchZone(GardenZone.Expedition);
            player.Position=new(0,0,18); player.Rotation=new(0,Mathf.Pi,0);
            cameraRig.Distance=9; cameraRig.Pitch=.25f; cameraRig.Yaw=.16f; cameraRig.Follow(player.Position,0,true);
        }
        if(frame==310) GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://artifacts/expedition-meadow.png"));
    }
}
