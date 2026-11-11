using System.Linq;
using Godot;

namespace Mori;

public static class GardenLandscape
{
    internal static readonly Vector3[] IslandSteps={
        new(-20.95f,.095f,-9.75f),new(-19.65f,.125f,-9.68f),
        new(-18.35f,.15f,-9.78f),new(-17.05f,.18f,-9.93f),new(-15.73f,.21f,-10.09f)
    };
    internal static readonly Vector3 IslandBank=new(-22.52f,0,-9.75f);
    internal static readonly Vector3 IslandLanding=new(-14.05f,.32f,-10.22f);
    internal static readonly Vector3[] SouthPillars=MakeSouthPillars();

    private static Vector3[] MakeSouthPillars()
    {
        var pillars=new Vector3[10];
        for (int i=0;i<pillars.Length;i++)
        {
            float angle=i*Mathf.Tau/pillars.Length;
            pillars[i]=new(6+Mathf.Cos(angle)*9,1.70f+(i%3)*.90f,27+Mathf.Sin(angle)*6);
        }
        return pillars;
    }

    public static void Build(Node3D root)
    {
        var sand = new Color("b5a78b");
        var stone = new Color("94998a");
        var water = new Color("568c9b");
        var edge=new StaticBody3D { Name="NorthernMountainBoundary",Position=new(0,18,GardenArt.RearBoundary-3) };
        edge.SetMeta("unclimbable",true); edge.SetMeta("no_step",true);
        edge.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new(2*GardenArt.GardenHalfWidth+30,48,6) } });
        root.AddChild(edge);
        foreach(float x in new[]{GardenArt.GardenMinX-1,GardenArt.GardenHalfWidth+1})
        {
            var side=new StaticBody3D { Position=new(x,15,10) };
            side.SetMeta("unclimbable",true); side.SetMeta("no_step",true);
            side.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new(2,40,90) } }); root.AddChild(side);
        }
        ZoneArt.Solid(root,new(11,12,GardenArt.GardenHalfDepth+1),new(130,30,2));
        // Small island and the broken bridge in the main pond.
        var island=GardenArt.Shape(root,new CylinderMesh {
            TopRadius=1.10f, BottomRadius=3.05f, Height=.64f, RadialSegments=32 },
            new(-13.2f,0,-10.8f),Vector3.One,new("6c7950"));
        island.CreateConvexCollision();
        Palm(root,new(-13.2f,.32f,-10.8f));
        for (int i=0;i<IslandSteps.Length;i++)
        {
            var top=IslandSteps[i];
            float height=top.Y+.27f;
            var block=GardenArt.Shape(root,new CylinderMesh { BottomRadius=.75f,TopRadius=.43f,
                Height=height,RadialSegments=8 },top-Vector3.Up*height*.5f,
                Vector3.One,stone.Darkened(i*.037f));
            block.RotationDegrees=new(0,i*17,0);
            block.CreateConvexCollision();
        }

        var courtyard=new Node3D { Position=new(-12,0,0) }; root.AddChild(courtyard);
        GardenArt.Sign(courtyard,"RUINED COURTYARD",new(49,2,5),GardenArt.Cream);
        GardenArt.Shape(courtyard,new BoxMesh(),new(68,.04f,-1),new(28,.08f,21),new("9a9583"));
        for (int x=0;x<12;x++)
            for (int z=0;z<7;z++)
                GardenArt.Shape(courtyard,new BoxMesh(),new(55.4f+x*2.27f,.087f,-9.8f+z*2.85f),
                    new(2.19f,.10f,2.72f),stone.Lightened(((x+z)%4)*.045f));
        for (int side=-1;side<=1;side+=2)
            for (int i=0;i<6;i++)
            {
                Vector3 basePos = new(56+i*4.7f,0,side*8.5f-1);
                float height = 2.1f+((i+side+1)%4)*.68f;
                GardenArt.Cylinder(courtyard,basePos+Vector3.Up*.16f,.72f,.28f,stone);
                GardenArt.Cylinder(courtyard,basePos+Vector3.Up*(height/2),.46f,height,stone).CreateConvexCollision();
                GardenArt.Cylinder(courtyard,basePos+Vector3.Up*height,.62f,.24f,stone);
            }
        GardenArt.Cylinder(courtyard,new(68,.23f,-1),3.8f,.42f,stone).CreateConvexCollision();
        GardenArt.Cylinder(courtyard,new(68,.48f,-1),3.45f,.08f,water);
        GardenArt.Cylinder(courtyard,new(68,1.08f,-1),.51f,1.4f,stone);
        GardenArt.Ball(courtyard,new(68,1.8f,-1),new(.58f,.38f,.58f),water);
        for (int i=0;i<4;i++)
            Palm(courtyard,new(42+i*7.5f,0,17+Mathf.Sin(i)*3));
        GardenArt.Sign(root,"SOUTH TERRACE",new(4,1.5f,16),GardenArt.Cream);
        foreach (var top in SouthPillars)
        {
            GardenArt.Cylinder(root,new(top.X,top.Y*.5f,top.Z),.65f,top.Y,stone).CreateConvexCollision();
            GardenArt.Cylinder(root,top+Vector3.Down*.06f,.72f,.12f,stone.Lightened(.06f));
        }
        for (int i=0;i<9;i++)
        {
            float height=.16f+(i%4)*.075f;
            var slab=GardenArt.Shape(root,new BoxMesh(),new(1+i*1.1f,height*.5f,18+i*.82f),
                new(2.7f,height,2.1f),stone.Darkened((i%3)*.08f));
            slab.RotationDegrees=new(0,i*9,0);
            slab.CreateConvexCollision();
        }
        for (int i=0;i<28;i++)
        {
            float x = GardenArt.GardenMinX-6+i*(GardenArt.GardenHalfWidth-GardenArt.GardenMinX+12)/27;
            for (int side=-1;side<=1;side+=2)
            {
                Mesh mass = (i%3) switch
                {
                    0 => new PrismMesh { LeftToRight=.18f+i%4*.18f },
                    1 => new SphereMesh { RadialSegments=5,Rings=3 },
                    _ => new CylinderMesh { TopRadius=.24f,BottomRadius=.56f,Height=1,RadialSegments=5 }
                };
                var cliff = GardenArt.Shape(root,mass,
                    new(x,side<0 ? 11.5f : 2.1f,side<0 ? GardenArt.RearBoundary-7 : GardenArt.GardenHalfDepth+Mathf.Sin(i)*2),new(14+i%4,side<0 ? 29+i%5*2.2f : 7+i%5*1.8f,side<0 ? 15+i%3 : 8+i%3),
                    new(i%2==0?"59645b":"6b7266"));
                cliff.RotationDegrees=new(0,i*29+side*12,side*(i%4-2)*5);
                GardenArt.StoneCollision(cliff);
                foreach(var body in cliff.GetChildren().OfType<StaticBody3D>()) body.SetMeta("no_step",true);
            }
        }
    }

    public static void BuildCoastal(Node3D root)
    {
        var sand=GardenArt.Cylinder(root,new(0,-.03f,0),22,.06f,new("b5a78b"));
        sand.Scale=new(1.4f,1,1.04f);
        GardenArt.Cylinder(root,new(-16,.013f,2),13,.018f,new("568c9b")).Scale=new(1.12f,1,1.32f);
        GardenArt.Cylinder(root,new(-23,.07f,1),3.8f,.14f,new("787f61"));
        for(int i=0;i<8;i++) Palm(root,new(-15+i*5.3f,0,11+Mathf.Sin(i*1.5f)*5));
        GardenArt.Sign(root,"COASTAL COVE",new(0,2.2f,-10),GardenArt.Cream);
        foreach(int side in new[]{-1,1})
        {
            ZoneArt.Solid(root,new(side*31,6,0),new(2,12,50));
            ZoneArt.Solid(root,new(0,6,side*24),new(64,12,2));
        }
        ZoneArt.Portal(root,new(0,0,-18),"PLAZA",new("a6a18a"));
    }

    private static void Palm(Node3D root,Vector3 position)
    {
        var trunk = GardenArt.Shape(root,new CylinderMesh { TopRadius=.17f,BottomRadius=.31f,Height=5.1f,RadialSegments=8 },
            position+Vector3.Up*2.55f,Vector3.One,new("63523c"));
        trunk.RotationDegrees = new(0,0,-8);
        trunk.CreateConvexCollision();
        for (int i=0;i<7;i++)
        {
            float angle=i*Mathf.Tau/7;
            var frond=GardenArt.Ball(root,position+new Vector3(Mathf.Sin(angle)*1.28f+.35f,5.05f,Mathf.Cos(angle)*1.28f),
                new(.56f,.16f,2.78f),new(i%2==0?"566b3b":"697c49"));
            frond.Rotation = new(.25f,angle,0);
        }
    }
}
