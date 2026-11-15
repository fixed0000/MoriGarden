using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckNaturalWaterAndCoast()
    {
        void CheckSurface(NaturalWaterBody body)
        {
            var water=world.GetNode<MeshInstance3D>(body.Id);
            if(water.GetChildren().OfType<StaticBody3D>().Any())throw new Exception("Water has an invisible solid surface");
            var points=water.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            foreach(var p in points)
                if(Mathf.Abs(p.Y-body.Level)>.001f || body.Bed(p.X,p.Z)>body.Level+.002f)
                    throw new Exception($"Water geometry lies above dry ground: {body.Id}/{p}");
            if(water.MaterialOverride is not ShaderMaterial material || !material.GetShaderParameter("natural_surface").AsBool())
                throw new Exception("Natural water has no depth profile");
        }
        carrying=false;SwitchZone(GardenZone.Expedition);CheckSurface(WindfallMeadow.Water);
        var pond=WindfallMeadow.Water;
        if(pond.Depth(new(5.1f,9.8f))>0)throw new Exception("Pond creates disconnected water outside its basin");
        bool wetOutsideOldEllipse=false;
        for(int i=0;i<64;i++)
        {
            float angle=i*Mathf.Tau/64;
            var p=WindfallMeadow.Pond+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*WindfallMeadow.PondRadii*1.12f;
            if(pond.Depth(p)>.01f) {wetOutsideOldEllipse=true;if(!WaterArt.PlayerWet(zone,new(p.X,pond.Bed(p.X,p.Y),p.Y)))throw new Exception("Visible pond and wet zone disagree");}
        }
        if(!wetOutsideOldEllipse)throw new Exception("The pond is still clipped at its old decorative ellipse");
        foreach(var p in new[]{new Vector2(12,2),new Vector2(14,4),new Vector2(13,3)})
        {
            var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(new(p.X,.20f,p.Y),new(p.X,-1,p.Y),1));
            if(hit.Count==0 || Mathf.Abs(((Vector3)hit["position"]).Y-pond.Bed(p.X,p.Y))>.007f)throw new Exception("Pond depth differs from its physical bed");
        }
        player.Position=new(7,WindfallMeadow.Height(7,5.8f)+.025f,5.8f);verticalSpeed=0;cameraRig.Yaw=0;
        DriveShoreCheck(380,Key.D);
        if(player.Position.X<19 || player.Position.Y<WindfallMeadow.Height(player.Position.X,player.Position.Z)-.08f)
            throw new Exception($"Player cannot enter, cross and leave the forest pond safely: {player.Position}, floor={WindfallMeadow.Height(player.Position.X,player.Position.Z)}");
        carrying=true;SwitchZone(GardenZone.CoastalCove);carrying=false;CheckSurface(CoastalCove.Water);
        if(WaterArt.PlayerWet(zone,new(0,0,-18)))throw new Exception("Sea floods the isolated plaza gate depression");
        if(WorldCatalog.Default.Areas["coastal-cove"].Kind!=WorldAreaKind.Garden || chaoMotion.GardenPond || moriWander.GardenLandmarks || chaoMotion.IsSwimmingAt(GardenArt.Pond))
            throw new Exception("The coastal garden uses old garden pond coordinates or became an expedition");
        float shore=CoastalCove.Shore(0);
        player.Position=new(shore+3,CoastalCove.Height(shore+3,0)+.03f,0);verticalSpeed=0;cameraRig.Yaw=0;
        DriveShoreCheck(240,Key.A);
        if(CoastalCove.Water.Depth(new(player.Position.X,player.Position.Z))>CoastalCove.MaxWadingDepth+.006f || player.Position.X>shore-2)
            throw new Exception("Coastal shallows are blocked or the player enters unsafe deep water");
        DriveShoreCheck(260,Key.D);
        if(player.Position.X<shore+2 || player.Position.Y<CoastalCove.Water.Bed(player.Position.X,player.Position.Z)-.06f)
            throw new Exception("Player cannot leave the lagoon");
        pet=new PetState { Run=80,Fullness=80,Energy=90,Fun=70 };friend.Visible=true;
        friend.Position=CoastalCove.LandPoint(new(2,0,6));friend.Rotation=new(0,Mathf.Pi/2,0);
        wanderTarget=CoastalCove.LandPoint(new(8,0,6));wanderTime=25;
        var start=friend.Position;int stalls=0;
        for(int i=0;i<320;i++) {var before=friend.Position;MoveFriend(1f/60);if(i>20 && FlatDistance(before,friend.Position)<.001f)stalls++;}
        if(FlatDistance(start,friend.Position)<2 || stalls>75)throw new Exception($"Mori cannot wander in Coastal Cove: {start}/{friend.Position}, stalls={stalls}");
        float edge=CoastalCove.Shore(5);
        friend.Position=new(edge-2,CoastalCove.Water.Bed(edge-2,5)-.025f,5);friend.Rotation=new(0,Mathf.Pi/2,0);
        chaoMotion.Reset();wanderTarget=CoastalCove.LandPoint(new(edge+1,0,5));wanderTime=20;
        for(int i=0;i<300;i++)MoveFriend(1f/60);
        if(friend.Position.X<edge+.25f || friend.Position.Y<-.08f)throw new Exception($"Mori is stuck at the lagoon shore: {friend.Position}");
        friend.Position=CoastalCove.LandPoint(new(2,0,6));var bumpStart=friend.Position;
        player.Position=friend.Position+Vector3.Back*.72f;verticalSpeed=0;cameraRig.Yaw=0;
        DriveShoreCheck(24,Key.W);
        if(FlatDistance(friend.Position,bumpStart)<.18f || FlatDistance(player.Position,friend.Position)<.43f)
            throw new Exception("Coastal player passes through Mori instead of pushing him");
        player.Position=new(27,CoastalCove.Height(27,6)+.03f,6);cameraRig.Yaw=Mathf.Pi/2;cameraRig.Distance=6.5f;cameraRig.Follow(player.Position,0,true);
        if(camera.GlobalPosition.X>29.8f)throw new Exception("Coastal camera passes through the inland rocks or boundary");
        if(world.GetChildren().OfType<MultiMeshInstance3D>().Count()>45 || world.GetChildCount()>180)throw new Exception("Coastal scenery exceeds its object budget");
        carrying=true;SwitchZone(GardenZone.Garden);carrying=false;
        GD.Print("COASTAL_WATER_OK: clipped shores, depth/physics agreement, pond crossing, lagoon limits, Chao wandering and shore exit, camera, budget");
    }
}
