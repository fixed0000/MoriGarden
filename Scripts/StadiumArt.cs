using System;
using System.Linq;
using Godot;

namespace Mori;

internal static class StadiumArt
{
    internal static readonly Vector3 Ring=new(0,.25f,0);
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
    {var root=new Node3D{Name="RaceLandscape"};world.AddChild(root);StadiumRaceArt.Build(root,course);return root;}
    internal static Node3D Karate(Node3D world)
    {var root=new Node3D{Name="KarateGarden"};world.AddChild(root);StadiumKarateArt.Build(root);return root;}
    internal static void PrizeToys(Node3D world,PetState owner)
    {
        if(owner.Stadium.Toys.Contains("champion-rattle")){var toy=new ToyCar{Name="ChampionRattle",IsRattle=true,StadiumPrize=true};world.AddChild(toy);toy.Position=new(-2,0,9);}
        if(owner.Stadium.Toys.Contains("canyon-car")){var toy=new ToyCar{Name="CanyonPrizeCar",StadiumPrize=true};world.AddChild(toy);toy.Position=new(-4,0,9);}
        if(owner.Stadium.Toys.Contains("karate-ball")){var toy=new ToyCar{Name="KaratePrizeBall",IsBeachBall=true,StadiumPrize=true};world.AddChild(toy);toy.Position=new(-3,0,9);}
    }
}
