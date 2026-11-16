using Godot;

namespace Mori;

internal static class MeadowDiscoveries
{
    internal static void Build(Node3D world)
    {
        var stone=NatureModels.StoneMaterial();var bark=GardenArt.Material(new("63513b"));
        var leaf=NatureModels.Foliage(new("668147"));var flower=NatureModels.Foliage(new("b8a7b4"));
        DiscoveryInterest Place(string id,DiscoveryKind kind,Vector2 p,float appeal,DiscoveryReaction reactions,float height=.3f)
        {
            var at=new Vector3(p.X,WindfallMeadow.Height(p.X,p.Y),p.Y);
            var item=new DiscoveryInterest { Name="NaturalInterest",Position=at,
                Definition=new("windfall/"+id,kind,new(at.X,at.Y,at.Z),10,appeal,reactions,LookHeight:height) };
            world.AddChild(item);return item;
        }
        var plant=Place("bellflowers",DiscoveryKind.Plant,new(-3,17),1.4f,DiscoveryReaction.Inspect|DiscoveryReaction.Touch|DiscoveryReaction.Share,.38f);
        for(int i=0;i<5;i++)
        {
            var at=new Vector3(Mathf.Sin(i*2.4f)*.24f,0,Mathf.Cos(i*2.4f)*.24f);
            GardenArt.Shape(plant,new CylinderMesh { TopRadius=.012f,BottomRadius=.018f,Height=.34f,RadialSegments=5 },at+Vector3.Up*.17f,Vector3.One,new("64804d"));
            GardenArt.Shape(plant,new PrismMesh { Size=new(.13f,.12f,.13f) },at+Vector3.Up*.37f,Vector3.One,new("b5a1b8"));
        }
        var pebble=Place("veined-stone",DiscoveryKind.Stone,new(4,12),1.1f,DiscoveryReaction.Inspect|DiscoveryReaction.Touch|DiscoveryReaction.Share,.33f);
        var mesh=NatureModels.Rock(391);
        var rock=new StaticBody3D { Position=new(0,.2f,0),Scale=new(.65f,.5f,.58f) };pebble.AddChild(rock);
        rock.AddChild(new MeshInstance3D { Mesh=mesh,MaterialOverride=stone });rock.AddChild(new CollisionShape3D { Shape=mesh.CreateConvexShape() });
        GardenArt.Shape(pebble,new CylinderMesh { TopRadius=.015f,BottomRadius=.02f,Height=.38f,RadialSegments=5 },new(0,.39f,.05f),Vector3.One,new("a6aaa0")).Rotation=new(0,0,.85f);
        var hollow=Place("hollow-timber",DiscoveryKind.Shelter,new(-10,8),1.2f,DiscoveryReaction.Inspect|DiscoveryReaction.Listen|DiscoveryReaction.Share,.4f);
        var log=GardenArt.Shape(hollow,new CylinderMesh { TopRadius=.30f,BottomRadius=.34f,Height=1.25f,RadialSegments=10 },new(0,.31f,0),Vector3.One,new("66513b"));log.Rotation=new(0,0,Mathf.Pi/2);
        GardenArt.Shape(hollow,new TorusMesh { InnerRadius=.17f,OuterRadius=.30f,Rings=10,RingSegments=8 },new(.64f,.31f,0),Vector3.One,new("84745c")).Rotation=new(0,0,Mathf.Pi/2);
        var collider=new StaticBody3D { Position=new(0,.31f,0),Rotation=new(0,0,Mathf.Pi/2) };hollow.AddChild(collider);
        collider.AddChild(new CollisionShape3D { Shape=new CapsuleShape3D { Radius=.3f,Height=1.25f } });
        Place("pond-ripples",DiscoveryKind.Water,new(7,6),1.1f,DiscoveryReaction.Watch|DiscoveryReaction.Share,.15f);
        var vista=Place("woodland-overlook",DiscoveryKind.Vista,new(-20,-10),1.2f,DiscoveryReaction.Watch|DiscoveryReaction.Share,.6f);
        vista.AddChild(new MeshInstance3D { Mesh=NatureModels.Rock(392),Position=new(.4f,.05f,.5f),Scale=new(.8f,.1f,.6f),MaterialOverride=stone });
        var reeds=Place("rustling-grass",DiscoveryKind.Sound,new(6,17),1.15f,DiscoveryReaction.Listen|DiscoveryReaction.Watch|DiscoveryReaction.Share,.6f);
        for(int i=0;i<4;i++)reeds.AddChild(new MeshInstance3D { Mesh=NatureModels.Understory(2),Position=new(i*.15f,0,Mathf.Sin(i)*.2f),Scale=Vector3.One*1.3f,MaterialOverride=leaf });
        var hidden=Place("fern-nest",DiscoveryKind.Hidden,new(-15,4),1.3f,DiscoveryReaction.Inspect|DiscoveryReaction.Watch|DiscoveryReaction.Share,.18f);
        hidden.AddChild(new MeshInstance3D { Mesh=NatureModels.Understory(1),Scale=Vector3.One*1.5f,MaterialOverride=leaf });
        hidden.AddChild(new MeshInstance3D { Mesh=NatureModels.Understory(3),Position=new(.2f,0,-.1f),Scale=Vector3.One*.5f,MaterialOverride=flower });
    }
}
