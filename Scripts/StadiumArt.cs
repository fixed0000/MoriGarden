using System;
using System.Linq;
using Godot;

namespace Mori;

internal static class StadiumArt
{
    internal static readonly Vector3 Ring=new(0,.35f,22);
    internal static void Build(Node3D root)
    {
        GardenArt.Shape(root,new PlaneMesh{Size=new(58,86)},new(0,-.02f,20),Vector3.One,new("869879"));
        ZoneArt.Solid(root,new(0,-.3f,20),new(58,.6f,86));
        GardenArt.Shape(root,new BoxMesh(),new(0,.025f,-3),new(10,.05f,20),new("c5bda4"));
        ZoneArt.Portal(root,new(0,0,-10),"GARDEN PLAZA",new("91a99f"),1.3f);
        GardenArt.Shape(root,new BoxMesh(),new(0,.45f,5),new(3.5f,.9f,1.3f),new("657c75"));ZoneArt.Solid(root,new(0,.45f,5),new(3.5f,.9f,1.3f));
        GardenArt.Sign(root,"CHAO STADIUM",new(0,3.2f,5),GardenArt.Cream);
        GardenArt.Sign(root,"RACE  ·  KARATE",new(0,2.2f,5),new("efce78"));
        for(int side=-1;side<=1;side+=2)
        {
            for(int row=0;row<4;row++)
            {
                var at=new Vector3(side*(22+row*.8f),.3f+row*.5f,22);
                GardenArt.Shape(root,new BoxMesh(),at,new(1.1f,.55f,27),new(row%2==0 ? "879990" : "b5bca4"));ZoneArt.Solid(root,at,new(1.1f,.55f,27));
            }
            for(int z=4;z<=40;z+=12)
            {
                GardenArt.Cylinder(root,new(side*23,3,z),.18f,6,new("576c66"));
                GardenArt.Shape(root,new BoxMesh(),new(side*23,6,z),new(1.1f,.4f,.7f),new("f4e9bd"));
            }
        }
        for(int i=0;i<16;i++)
        {float a=i*Mathf.Tau/16;var at=new Vector3(Mathf.Cos(a)*27,0,22+Mathf.Sin(a)*20);GardenArt.Cylinder(root,at+Vector3.Up*1.2f,.25f,2.4f,new("b9ae92"));}
    }
    internal static Node3D Race(Node3D world,RaceCourse course)
    {
        var root=new Node3D{Name="RaceCourse"};world.AddChild(root);
        foreach(var point in course.Points.Take(course.Lengths.Length))
        {
            var turn=GardenArt.Cylinder(root,new(point.X,.10f,point.Y),3.35f,.16f,new("c6b48c"));turn.CreateConvexCollision();
        }
        for(int section=0;section<course.Surfaces.Length;section++)
        {
            var p=course.Points[section];var q=course.Points[section+1];Vector3 start=new(p.X,.12f,p.Y),end=new(q.X,.12f,q.Y);
            var middle=(start+end)*.5f;var direction=(end-start).Normalized();float length=start.DistanceTo(end);var surface=course.Surfaces[section];
            var strip=GardenArt.Shape(root,new BoxMesh(),middle,new(5.8f,.12f,length+.18f),new(surface==RaceSurface.Rest ? "9baf80" : "c6b48c"));strip.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
            // Physical running decks and shallow canal bottoms share the exact racing footprint.
            var body=new StaticBody3D{Position=middle-Vector3.Up*(surface==RaceSurface.Swim ? .17f : 0),Rotation=strip.Rotation};root.AddChild(body);
            body.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(5.8f,.12f,length+.18f)}});
            if(surface==RaceSurface.Swim)
            {strip.Position-=Vector3.Up*.17f;var water=GardenArt.Shape(root,new PlaneMesh{Size=new(5.6f,length)},middle+Vector3.Up*.21f,Vector3.One,Colors.White);water.Rotation=strip.Rotation;water.MaterialOverride=WaterArt.Material();}
            for(int side=-1;side<=1;side+=2)
            {
                var edge=GardenArt.Shape(root,new BoxMesh(),middle+direction.Cross(Vector3.Up)*side*3,new(.13f,.28f,length),new("627e72"));edge.Rotation=strip.Rotation;
            }
            for(int lane=0;lane<3;lane++)
            {
                var line=GardenArt.Shape(root,new BoxMesh(),middle+direction.Cross(Vector3.Up)*(lane-1)*1.15f,new(.025f,.008f,length),new("e9e0ba"));line.Rotation=strip.Rotation;
            }
            if(surface==RaceSurface.Power)
                for(int lane=0;lane<4;lane++)
                {var rock=GardenArt.Shape(root,new PrismMesh(),middle+direction.Cross(Vector3.Up)*(lane-1.5f)*1.15f+Vector3.Up*.16f,new(.75f,.55f,1.25f),new("93968a"));rock.Rotation=strip.Rotation;}
            if(surface==RaceSurface.Fly)
            {
                var ramp=GardenArt.Shape(root,new PrismMesh(),start+direction*1.2f+Vector3.Up*.1f,new(5.4f,.6f,2),new("aaa383"));ramp.Rotation=strip.Rotation;
            }
            GardenArt.Sign(root,surface.ToString().ToUpperInvariant(),middle+direction.Cross(Vector3.Up)*4+Vector3.Up*.8f,new("fff3c8"));
        }
        var startLine=new Vector3(course.Points[0].X,0,course.Points[0].Y);var heading=course.Points[1]-course.Points[0];
        var arch=new Node3D{Position=startLine,Rotation=new(0,Mathf.Atan2(heading.X,heading.Y),0)};root.AddChild(arch);
        foreach(int side in new[]{-1,1})GardenArt.Cylinder(arch,new(side*3.2f,1.3f,0),.12f,2.6f,new("6c8178"));
        GardenArt.Shape(arch,new BoxMesh(),new(0,2.6f,0),new(6.6f,.32f,.16f),new("e2c47b"));GardenArt.Sign(arch,"START / FINISH",new(0,2.62f,.13f),GardenArt.Ink);
        return root;
    }
    internal static Node3D Karate(Node3D world)
    {
        var root=new Node3D{Name="KarateArena"};world.AddChild(root);
        GardenArt.Cylinder(root,Ring-Vector3.Up*.22f,3.6f,.44f,new("756d59"));
        var floor=GardenArt.Cylinder(root,Ring,3.3f,.05f,new("d7c590"));
        var physics=new StaticBody3D{Position=Ring-Vector3.Up*.025f};root.AddChild(physics);physics.AddChild(new CollisionShape3D{Shape=new CylinderShape3D{Radius=3.3f,Height=.05f}});
        GardenArt.Shape(root,new TorusMesh{InnerRadius=ChaoKarate.RingRadius-.025f,OuterRadius=ChaoKarate.RingRadius+.025f},Ring+Vector3.Up*.03f,Vector3.One,new("a67354"));
        GardenArt.Shape(root,new TorusMesh{InnerRadius=.35f,OuterRadius=.38f},Ring+Vector3.Up*.032f,Vector3.One,new("9b9278"));
        GardenArt.Sign(root,"CHAO KARATE",Ring+new Vector3(0,2.2f,-4),new("f4dfad"));
        return root;
    }
    internal static void PrizeToys(Node3D world,PetState owner)
    {
        if(owner.Stadium.Toys.Contains("champion-rattle")){var toy=new ToyCar{Name="ChampionRattle",IsRattle=true,StadiumPrize=true};world.AddChild(toy);toy.Position=new(-2,0,9);}
        if(owner.Stadium.Toys.Contains("karate-ball")){var toy=new ToyCar{Name="KaratePrizeBall",IsBeachBall=true,StadiumPrize=true};world.AddChild(toy);toy.Position=new(-3,0,9);}
    }
}
