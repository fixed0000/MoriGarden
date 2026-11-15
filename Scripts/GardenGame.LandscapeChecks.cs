using System;
using System.Linq;
using Godot;

namespace Mori;

public partial class GardenGame
{
    private void CheckLandscapeRefinements()
    {
        if(MountainInterior.Room.Size.X<5.7f || MountainInterior.Room.Size.Z<3.7f) throw new Exception("The cave was not enlarged");
        var lawn=world.GetChildren().OfType<MeshInstance3D>().Select(m=>m.MaterialOverride).OfType<ShaderMaterial>().First(m=>m.Shader.Code.Contains("vec3 grass"));
        if(!lawn.Shader.Code.Contains(MountainInterior.VoidDiscard)) throw new Exception("Grass shader does not clear the enlarged cave floor");
        var furniture=world.GetNode<Node3D>("ChaoWoodenFurniture");
        if(furniture.Position.X>MountainInterior.Room.Position.X+.8f || furniture.Position.Z>MountainInterior.Room.Position.Z+.8f)
            throw new Exception("Cave furniture was not placed in a corner");
        foreach(float x in new[]{MountainInterior.Room.Position.X+.35f,MountainInterior.Room.End.X-.35f})
            foreach(float z in new[]{MountainInterior.Room.Position.Z+.35f,MountainInterior.Room.End.Z-.35f})
            {
                var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(new(x,2.85f,z),new(x,13,z),1));
                if(hit.Count==0 || hit["collider"].AsGodotObject() is not GodotObject rock || !rock.HasMeta("stone"))
                    throw new Exception($"Expanded cave corner is exposed outside the mountain: {x}/{z}");
            }
        friend.Position=new(21,0,15); friend.Rotation=Vector3.Zero;
        ImportedChao.ApplyDevelopment(friendArt,new PetState { Run=0 });
        ImportedChao.MovePose(friendArt,.8f,true,false,false,false,false,false);
        var rise=new ChaoRising(); rise.Begin(friendArt); rise.Step(friendArt,.05f);
        var paw=(MeshInstance3D)friendArt.FindChild("PaddleL",true,false); var bounds=paw.GetAabb(); float bottom=float.MaxValue;
        for(int i=0;i<8;i++) bottom=Mathf.Min(bottom,(paw.GlobalTransform*bounds.GetEndpoint(i)).Y);
        if(bottom<.012f) throw new Exception($"Crawling Chao drags its paw when rising: {bottom}");
        for(int i=0;i<100;i++)rise.Step(friendArt,1f/60);
        ImportedChao.ClearActionPause(friendArt); ImportedChao.ApplyDevelopment(friendArt,new PetState { Run=80 });
        var body=friendArt.GetChildren().OfType<Node3D>().First(n=>n is not EmotionOrb);
        var model=(ImportedChao)friendArt;
        float movement=0;
        for(int i=0;i<50;i++)
        {
            friend.Position+=Vector3.Back*.018f; model._PhysicsProcess(1f/60);
            movement=Mathf.Max(movement,body.Rotation.Length());
        }
        for(int i=0;i<120;i++)model._PhysicsProcess(1f/60);
        if(movement<.003f || movement>.18f || body.Rotation.Length()>.005f)
            throw new Exception("Chao body softness is rigid, excessive, or does not settle");
        carrying=false; SwitchZone(GardenZone.CoastalCove);
        var coastal=world.GetNode<MeshInstance3D>("CoastalWater").MaterialOverride as ShaderMaterial;
        if(coastal==null || !coastal.GetShaderParameter("transparent_pond").AsBool()) throw new Exception("Coastal water differs from garden water");
        SwitchZone(GardenZone.Expedition);
        var pool=world.GetNode<MeshInstance3D>("MeadowWater").MaterialOverride as ShaderMaterial;
        if(pool?.Shader?.Code!=coastal.Shader?.Code) throw new Exception("Meadow water uses a different shader");
        int batches=world.GetChildren().OfType<MultiMeshInstance3D>().Count();
        if(batches<15 || batches>55 || world.GetChildCount()>230) throw new Exception("Nature geometry is not batched within its object budget");
        var poolAt=new Vector3(WindfallMeadow.Pond.X,-.21f,WindfallMeadow.Pond.Y);
        player.Position=poolAt; verticalSpeed=0; cameraRig.Yaw=0;
        for(int i=0;i<80;i++)MovePlayer(1f/60);
        if(player.Position.Y<-.28f || player.Position.Y>-.16f || !WaterArt.PlayerWet(zone,player.Position))
            throw new Exception("The shallow meadow pond has no solid bed or wet state");
        float Wading(params Key[] keys)
        {
            player.Position=poolAt; verticalSpeed=dashTime=0;
            DriveShoreCheck(24,keys); return FlatDistance(poolAt,player.Position);
        }
        if(Mathf.Abs(Wading(Key.W)-Wading(Key.W,Key.Shift))>.025f) throw new Exception("Player can sprint in the meadow pond");
        foreach(var (from,to) in new[]{(new Vector2(-7,3),new Vector2(-6,-10)),(new Vector2(22,-6),new Vector2(26,-13))})
        {
            player.Position=new(from.X,WindfallMeadow.Height(from.X,from.Y)+.03f,from.Y); verticalSpeed=0;
            var direction=(to-from).Normalized(); cameraRig.Yaw=Mathf.Atan2(-direction.X,-direction.Y);
            DriveShoreCheck(150,Key.W);
            if(new Vector2(player.Position.X,player.Position.Z).DistanceTo(from)<5 || player.Position.Y<WindfallMeadow.Height(player.Position.X,player.Position.Z)-.08f)
                throw new Exception("Player cannot walk up a meadow path slope");
        }
        var safe=ExpeditionArt.RestorePosition(new(-23,0,-13),player);
        if(safe.Y<WindfallMeadow.Height(safe.X,safe.Z)) throw new Exception("Old meadow position restores under the improved terrain");
        SwitchZone(GardenZone.Garden);
        GD.Print($"LANDSCAPE_OK: cave shell, corner furniture, rising paws, body inertia, shared water, slopes, restore; nature batches={batches}");
    }
}
