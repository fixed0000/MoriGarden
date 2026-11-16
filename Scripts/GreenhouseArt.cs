using System;
using Godot;

namespace Mori;

internal static class GreenhouseArt
{
    internal static readonly Rect2[] Channels={new(-9.3f,-3,1.2f,11),new(-8.7f,-3.6f,17.7f,1.2f),new(-.6f,-6,1.2f,3),new(8.4f,-3,1.2f,10)};
    internal static readonly Vector2[] Bridges={new(-8.7f,2),new(-8.7f,9),new(-5,-3),new(3,-3),new(9,1),new(0,-5)};
    internal static readonly Vector2[] Landmarks={new(-11,12),new(11,12),new(-11,-14),new(10,-15),new(0,17),new(-4,-7)};
    internal static int Channel(Vector2 p)
    { for(int i=0;i<Channels.Length;i++)if(Channels[i].HasPoint(p))return i;return -1; }
    internal static float Height(float x,float z)
    {
        var p=new Vector2(x,z);
        foreach(var bridge in Bridges)if(Mathf.Abs(p.X-bridge.X)<1.15f && Mathf.Abs(p.Y-bridge.Y)<1.15f)return .015f;
        return Channel(p)>=0 ? -.18f : 0;
    }
    internal static readonly NaturalWaterBody Water=new("GreenhouseCanals",new(-18,-21,36,45),-.045f,Height,Area:p=>Channel(p)>=0);
    private static readonly Color Stone=new("827d69"),Frame=new("526b60"),Wood=new("786248");
    internal static MeshInstance3D Box(Node3D root,Vector3 at,Vector3 size,Color color,bool solid=false)
    {
        var part=GardenArt.Shape(root,new BoxMesh(),at,size,color);
        if(solid)Solid(root,at,size);return part;
    }
    internal static StaticBody3D Solid(Node3D root,Vector3 at,Vector3 size)
    {
        var body=new StaticBody3D { Position=at };body.SetMeta("no_step",true);
        body.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=size } });root.AddChild(body);return body;
    }
    internal static void Beam(Node3D root,Vector3 a,Vector3 b,float radius,Color color)
    {
        var length=a.DistanceTo(b);var mesh=GardenArt.Shape(root,new CylinderMesh { TopRadius=radius,BottomRadius=radius,Height=length,RadialSegments=8 },(a+b)*.5f,Vector3.One,color);
        mesh.Quaternion=new Quaternion(Vector3.Up,(b-a).Normalized());
    }
    internal static void Plaque(Node3D root,Vector3 at,string text,float width=2.8f)
    {
        Box(root,at,new(width,.83f,.12f),new("3b5148"),true);
        if(at.Y>1)foreach(int side in new[]{-1,1})Beam(root,new(at.X+side*width*.3f,.05f,at.Z-.03f),new(at.X+side*width*.3f,at.Y-.32f,at.Z-.03f),.035f,Frame);
        root.AddChild(new Label3D { Text=text,Position=at+Vector3.Back*.075f,FontSize=34,PixelSize=.0042f,Modulate=new("f0e7cd"),OutlineSize=1 });
    }
    private static StandardMaterial3D Glass()=>new() { AlbedoColor=new(.58f,.77f,.72f,.26f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode=BaseMaterial3D.CullModeEnum.Disabled,Roughness=.3f,Metallic=.05f };
    internal static void MeadowEntrance(Node3D root)
    {
        var facade=new Node3D { Name="GreenhouseEntrance",Position=new(3,WindfallMeadow.Height(3,-21),-23) };root.AddChild(facade);
        foreach(int side in new[]{-1,1})
        {
            Box(facade,new(side*4.7f,1,0),new(4,2,.45f),Stone,true);
            Box(facade,new(side*6.6f,2.5f,-2),new(.25f,5,4),Frame,true);
            var glass=Box(facade,new(side*4.5f,3.3f,0),new(4,2.4f,.07f),Stone);glass.MaterialOverride=Glass();
        }
        for(int x=-6;x<=6;x+=2)Beam(facade,new(x,1.6f,0),new(x,5.3f,0),.06f,Frame);
        Beam(facade,new(-7,4.6f,0),new(0,6,-2),.09f,Frame);Beam(facade,new(7,4.6f,0),new(0,6,-2),.09f,Frame);
        Plaque(facade,new(0,5.05f,.3f),"FORGOTTEN GREENHOUSE",5.2f);
    }
    internal static void Build(Node3D root)
    {
        GardenArt.Shape(root,new PlaneMesh { Size=new(70,75) },new(0,-.31f,0),Vector3.One,new("536343"));
        Floor(root);
        foreach(int side in new[]{-1,1})
        {
            Box(root,new(side*18.4f,.6f,1.5f),new(.8f,1.2f,45),new("536846"));Solid(root,new(side*18.4f,2,1.5f),new(.8f,6,45));
            Box(root,new(side*15,.48f,-2),new(.55f,.95f,32.5f),Stone,true);
            var glass=Box(root,new(side*15,3,-2),new(.08f,4.2f,32),Stone);glass.MaterialOverride=Glass();
            Solid(root,new(side*15,2.8f,-2),new(.18f,5.6f,32));
            for(int z=-18;z<=14;z+=4)
            {
                Beam(root,new(side*15,0,z),new(side*15,5.4f,z),.10f,Frame);
                Beam(root,new(side*15,5.4f,z),new(0,7.5f,z),.09f,Frame);
                if((z+18)/4%4!=1)
                {
                    var panel=Box(root,new(side*7.5f,6.45f,z+1.8f),new(15.2f,.045f,3.65f),Stone);
                    panel.Rotation=new(0,0,-side*.139f);panel.MaterialOverride=Glass();
                }
            }
            for(int z=-18;z<=14;z+=8)Beam(root,new(side*15,4.9f,z),new(side*15,1,z+4),.045f,Frame);
            Box(root,new(side*9,.3f,14),new(12,.6f,.3f),Stone,true);
            Solid(root,new(side*9,2.5f,14),new(12,5,.1f));
            for(int x=3;x<=15;x+=2)Beam(root,new(side*x,.6f,14),new(side*x,5.4f,14),.07f,Frame);
            var front=Box(root,new(side*9,3,14.13f),new(11.6f,3.6f,.05f),Stone);front.MaterialOverride=Glass();
            Bench(root,new(side*10.8f,0,12),side);
        }
        Box(root,new(0,.35f,-18.3f),new(30,.7f,.5f),Stone,true);
        var back=Box(root,new(0,3.7f,-18.3f),new(30,3.4f,.06f),Stone);back.MaterialOverride=Glass();
        Solid(root,new(0,3.8f,-18.3f),new(30,3.6f,.1f));
        Box(root,new(0,.6f,-21.4f),new(37,1.2f,.8f),new("536846"));Solid(root,new(0,2,-21.4f),new(37,6,.8f));
        foreach(int side in new[]{-1,1})Box(root,new(side*11,1,24.4f),new(15,2,.8f),new("536846"),true);
        Beam(root,new(0,7.5f,-18),new(0,7.5f,14),.12f,Frame);
        Plaque(root,new(-4.2f,1.2f,15),"IRRIGATION HOUSE\nTrace the channels. Restore the central bed.",5.5f);
        for(int i=0;i<Channels.Length;i++)
        {
            var r=Channels[i];
            foreach(int edge in new[]{-1,1})
            {
                bool alongZ=r.Size.Y>r.Size.X;
                var p=r.GetCenter();
                Box(root,new(p.X+(alongZ ? edge*(r.Size.X*.5f+.08f) : 0),-.025f,p.Y+(alongZ ? 0 : edge*(r.Size.Y*.5f+.08f))),
                    alongZ ? new(.15f,.13f,r.Size.Y) : new(r.Size.X,.13f,.15f),Stone);
            }
        }
        Vegetation(root);
        var mission=new GreenhouseMission { Name="GreenhouseMission" };root.AddChild(mission);mission.BuildElements();
    }
    private static void Floor(Node3D root)
    {
        var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 At(float x,float z)=>new(x,Height(x,z),z);
        for(float x=-19;x<19;x+=.5f)for(float z=-22;z<25;z+=.5f)
            foreach(var p in new[]{At(x,z),At(x+.5f,z),At(x,z+.5f),At(x+.5f,z),At(x+.5f,z+.5f),At(x,z+.5f)})surface.AddVertex(p);
        surface.Index();surface.GenerateNormals();
        var material=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            varying vec3 world;
            void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
            void fragment(){
                vec2 tile=world.xz*.9;tile.x+=mod(floor(tile.y),2.0)*.5;
                vec2 t=fract(tile);float seam=step(.022,t.x)*step(.022,t.y);
                float grain=fract(sin(dot(floor(world.xz*24.0),vec2(12.3,48.9)))*43758.5);
                float variation=fract(sin(dot(floor(tile),vec2(17.3,63.7)))*43972.0);
                vec3 stone=mix(vec3(.34,.35,.29),vec3(.48,.45,.35),variation*.5+grain*.08);
                float moss=smoothstep(.72,.96,sin(world.x*.72+world.z*.41)*sin(world.z*.85));
                float ring=abs(length(world.xz-vec2(0.0,-9.0))-4.5);
                float path=min(min(abs(world.x),abs(world.z+3.0)),min(abs(abs(world.x)-7.0),ring));
                float paving=1.0-smoothstep(1.5,2.4,path);paving=max(paving,smoothstep(13.0,15.0,world.z));
                vec3 soil=mix(vec3(.24,.26,.17),vec3(.32,.30,.20),grain*.2+.25);
                ALBEDO=mix(soil,mix(vec3(.23,.25,.19),stone,seam),paving);ALBEDO=mix(ALBEDO,vec3(.23,.30,.16),moss*.4);ROUGHNESS=.96;
            }
            """ } };
        var floor=new MeshInstance3D { Name="GreenhouseFloor",Mesh=surface.Commit(),MaterialOverride=material };root.AddChild(floor);floor.CreateTrimeshCollision();
    }
    private static void Bench(Node3D root,Vector3 at,int side)
    {
        for(int z=-1;z<=1;z++)Box(root,at+new Vector3(0,.38f,z*.24f),new(2,.12f,.19f),Wood);
        foreach(int x in new[]{-1,1})Box(root,at+new Vector3(x*.8f,.17f,0),new(.12f,.34f,.6f),Frame);
        Solid(root,at+Vector3.Up*.23f,new(2,.46f,.8f));
        Box(root,at+new Vector3(0,.75f,-side*.37f),new(2,.55f,.11f),Wood);
    }
    private static void Vegetation(Node3D root)
    {
        var batches=new NatureModels(root);var random=new RandomNumberGenerator { Seed=8401 };
        var leaves=NatureModels.Foliage(new("547849"));var pale=NatureModels.Foliage(new("8a9560"));
        var tree=NatureModels.Tree(0,741);var wood=GardenArt.Material(new("655640"));
        for(int i=0;i<42;i++)
        {
            var p=i<28 ? new Vector3(i%2==0 ? -21.5f : 21.5f,0,-22+i/2*3.6f) : new Vector3(-23+(i-28)*3.5f,0,-25);
            var pose=new Transform3D(Basis.FromEuler(new(0,i*2.4f,0)).Scaled(Vector3.One*(.75f+i%4*.12f)),p);
            batches.Instance("OuterWoodlandTrunks",tree.Trunk,wood,pose);batches.Instance("OuterWoodlandLeaves",tree.Crown,leaves,pose);
        }
        foreach(int side in new[]{-1,1})for(int row=0;row<3;row++)
        {
            var at=new Vector3(side*11.5f,0,6-row*8);
            Box(root,at+Vector3.Up*.18f,new(4,.36f,4.2f),new("6d705b"),true);
            Box(root,at+Vector3.Up*.37f,new(3.7f,.05f,3.9f),new("494432"));
            foreach(int corner in new[]{-1,1})
            {
                var pose=new Transform3D(Basis.FromEuler(new(0,row+corner,0)).Scaled(Vector3.One*.25f),at+new Vector3(corner*.95f,.4f,corner*.7f));
                batches.Instance("OvergrownShrubWood",tree.Trunk,wood,pose);batches.Instance("OvergrownShrubLeaves",tree.Crown,leaves,pose);
            }
            for(int i=0;i<16;i++)
            {
                var p=at+new Vector3(random.RandfRange(-1.6f,1.6f),.4f,random.RandfRange(-1.8f,1.8f));
                batches.Instance("GlasshouseFerns",NatureModels.Understory(i%3),i%3==0 ? pale : leaves,new(Basis.FromEuler(new(0,random.Randf()*Mathf.Tau,0)).Scaled(Vector3.One*random.RandfRange(.8f,1.6f)),p));
            }
        }
        for(int i=0;i<100;i++)
        {
            int side=i%2==0 ? -1 : 1;var p=new Vector3(side*14.65f,random.RandfRange(.5f,4.7f),random.RandfRange(-17,13));
            batches.Instance("ClimbingIvy",NatureModels.Understory(1),leaves,new(Basis.FromEuler(new(.4f,side*1.4f,0)).Scaled(Vector3.One*.75f),p));
        }
        batches.Flush();
        foreach(int side in new[]{-1,1})
        {
            foreach(int z in new[]{10,13})
            {
                var pot=new Node3D { Position=new(side*13.4f,0,z) };root.AddChild(pot);
                GardenArt.Shape(pot,new CylinderMesh { TopRadius=.32f,BottomRadius=.23f,Height=.45f,RadialSegments=10 },new(0,.225f,0),Vector3.One,new("977355"));
                Solid(pot,new(0,.22f,0),new(.6f,.44f,.6f));
                pot.AddChild(new MeshInstance3D { Mesh=NatureModels.Understory(1),Position=Vector3.Up*.45f,Scale=Vector3.One*1.3f,MaterialOverride=leaves });
            }
            for(int i=-1;i<=1;i++)Box(root,new(side*10.8f+i*.55f,.035f,10.75f),new(.48f,.07f,.85f),new(i==0 ? "a7a181" : "7e9a83"),true);
        }
        var table=new Node3D { Position=new(-6,0,-15) };root.AddChild(table);
        Box(table,new(0,.8f,0),new(3,.16f,1.35f),Wood);Solid(table,new(0,.43f,0),new(3,.85f,1.35f));
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box(table,new(x*1.25f,.38f,z*.52f),new(.15f,.76f,.15f),Frame);
        for(int i=0;i<4;i++)
        {
            var at=new Vector3(-1+i*.65f,.91f,i%2*.3f-.15f);
            GardenArt.Shape(table,new TorusMesh { InnerRadius=.13f,OuterRadius=.22f,Rings=12,RingSegments=6 },at,Vector3.One,new("8b815e"));
            for(int tooth=0;tooth<8;tooth++)Box(table,at+new Vector3(Mathf.Sin(tooth*Mathf.Tau/8)*.21f,0,Mathf.Cos(tooth*Mathf.Tau/8)*.21f),new(.09f,.06f,.09f),new("8b815e"));
        }
        var bed=new Node3D { Name="CentralBed",Position=new(0,0,-9) };root.AddChild(bed);
        GardenArt.Shape(bed,new CylinderMesh { TopRadius=3.1f,BottomRadius=3.15f,Height=.34f,RadialSegments=24 },Vector3.Up*.17f,Vector3.One,new("625b43"));
        GardenArt.Shape(bed,new TorusMesh { InnerRadius=2.95f,OuterRadius=3.23f,Rings=32,RingSegments=8 },Vector3.Up*.32f,Vector3.One,Stone);
        var body=new StaticBody3D();body.AddChild(new CollisionShape3D { Position=Vector3.Up*.18f,Shape=new CylinderShape3D { Radius=3.15f,Height=.36f } });body.SetMeta("no_step",true);bed.AddChild(body);
        var plants=new Node3D { Name="RecoveringPlants",Position=Vector3.Up*.36f };bed.AddChild(plants);
        var wilted=NatureModels.Foliage(new("807649"));
        for(int i=0;i<28;i++)plants.AddChild(new MeshInstance3D { Mesh=NatureModels.Understory(i%3),Position=new(Mathf.Sin(i*2.4f)*Mathf.Sqrt(i/28f)*2.7f,0,Mathf.Cos(i*2.4f)*Mathf.Sqrt(i/28f)*2.7f),Scale=Vector3.One*(.8f+i%3*.35f),MaterialOverride=wilted });
    }
}
