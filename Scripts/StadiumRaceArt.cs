using System;
using System.Linq;
using Godot;
using NVector3=System.Numerics.Vector3;

namespace Mori;

internal static class StadiumRaceArt
{
    internal static Vector3 V(NVector3 v)=>new(v.X,v.Y,v.Z);
    internal static MeshInstance3D Surface(Node3D root,SurfaceTool tool,Color tint,bool collision=true)
    {
        tool.GenerateNormals();var node=GardenArt.Shape(root,tool.Commit(),Vector3.Zero,Vector3.One,tint);
        var material=GardenArt.Material(tint);material.VertexColorUseAsAlbedo=true;material.CullMode=BaseMaterial3D.CullModeEnum.Disabled;node.MaterialOverride=material;
        if(collision)node.CreateTrimeshCollision();return node;
    }
    private static void Quad(SurfaceTool s,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {foreach(var p in new[]{a,b,c,b,d,c}){s.SetColor(color*(.96f+.04f*Mathf.Sin(p.X*.7f+p.Z*.55f)));s.AddVertex(p);}}
    internal static float TerrainHeight(RaceCourse course,float x,float z)
    {
        var closest=course.Samples.MinBy(s=>Mathf.Abs(s.Point.Position.Z-z)).Point.Position;
        float y=closest.Y-.035f;
        if(course.Key=="crab-pool" && z>6 && z<50 && Mathf.Abs(x-closest.X)<7.0f)y=-.48f;
        if(course.Key=="stump-valley" && z>18.1f && z<31)y=-.55f;
        float edge=Mathf.SmoothStep(3.5f,15,Mathf.Abs(x-closest.X));
        return y+edge*(1.2f+Mathf.Sin(z*.24f+x*.4f)*.5f);
    }
    internal static void Build(Node3D root,RaceCourse course)
    {
        var terrain=new SurfaceTool();terrain.Begin(Mesh.PrimitiveType.Triangles);
        for(int x=-22;x<22;x+=2)for(int z=-8;z<76;z+=2)
        {
            Vector3 At(float px,float pz)=>new(px,TerrainHeight(course,px,pz),pz);
            float shade=1;Quad(terrain,At(x,z),At(x+2,z),At(x,z+2),At(x+2,z+2),new(shade,shade,shade));
        }
        Surface(root,terrain,new(course.Key=="mushroom-forest" ? "a35c4c" : "7ea557"));
        var road=new SurfaceTool();road.Begin(Mesh.PrimitiveType.Triangles);
        var walls=new SurfaceTool();walls.Begin(Mesh.PrimitiveType.Triangles);bool hasWalls=false;
        var water=new SurfaceTool();water.Begin(Mesh.PrimitiveType.Triangles);bool hasWater=false;
        for(int i=1;i<course.Samples.Count;i++)
        {
            var a=course.Samples[i-1];var b=course.Samples[i];var type=b.Point.Surface;
            if(type==RaceSurface.Fly)continue;
            Vector3 Edge(float distance,float side)=>V(course.Position(distance,side*RaceCourse.Width*.5f,null))+Vector3.Up*.015f;
            var al=Edge(a.Distance,-1);var ar=Edge(a.Distance,1);var bl=Edge(b.Distance,-1);var br=Edge(b.Distance,1);
            if(type==RaceSurface.Swim){var sideways=(ar-al).Normalized();al-=sideways*3;ar+=sideways*3;bl-=sideways*3;br+=sideways*3;al.Y=ar.Y=bl.Y=br.Y=.035f;Quad(water,al,ar,bl,br,Colors.White);hasWater=true;}
            else if(type==RaceSurface.Power){Quad(walls,al,ar,bl,br,Colors.White);hasWalls=true;}else Quad(road,al,ar,bl,br,new(.97f,.95f,.86f));
        }
        Surface(root,road,new("c0b28c"));
        if(hasWalls){var wall=Surface(root,walls,new("8f8d78"));wall.MaterialOverride=new ShaderMaterial{Shader=new Shader{Code="""
            shader_type spatial;
            render_mode cull_disabled;
            varying vec3 at;
            void vertex(){at=VERTEX;}
            void fragment(){float layers=sin(at.y*17.0+sin(at.x*2.5)*1.5)*.025;float cells=sin(at.x*31.0+at.y*19.0)*sin(at.y*29.0)*.015;float cracks=smoothstep(.95,1.0,sin(at.x*3.2+sin(at.y*1.7)))*.09;ALBEDO=vec3(.48,.46,.37)+layers+cells-cracks;ROUGHNESS=.95;}
            """}};}
        if(hasWater){var liquid=Surface(root,water,Colors.White,false);liquid.MaterialOverride=WaterArt.Material();}
        if(course.Key=="stump-valley")
        {
            var stream=GardenArt.Shape(root,new PlaneMesh{Size=new(14,12)},new(.5f,-.08f,25),Vector3.One,Colors.White);stream.MaterialOverride=WaterArt.Material();
            foreach(var point in new[]{course.Anchors[2],course.Anchors[3]})
            {
                var p=V(point);var stump=GardenArt.Cylinder(root,new(p.X,p.Y*.5f-.04f,p.Z),3.05f,p.Y+.1f,new("795c3f"));stump.CreateConvexCollision();
                GardenArt.Cylinder(root,p+Vector3.Down*.025f,3.05f,.05f,new("c2a67b"));
                for(int ring=0;ring<5;ring++)GardenArt.Shape(root,new TorusMesh{InnerRadius=.5f+ring*.43f,OuterRadius=.52f+ring*.43f},p+Vector3.Up*.007f,Vector3.One,new("9d7c56"));
            }
        }
        Decorate(root,course);Gate(root,V(course.Anchors[0]),false);Gate(root,V(course.Anchors[^1]),true);Podium(root,V(course.Anchors[^1])+new Vector3(0,0,5));
    }
    private static void Decorate(Node3D root,RaceCourse course)
    {
        var random=new Random(451+course.Key.Length);var batch=new NatureModels(root);
        var tree=NatureModels.Tree(2,154);var wood=GardenArt.Material(new("775b3e"));var leaves=NatureModels.Foliage(new("55904c"));
        for(int i=0;i<88;i++)
        {
            float z=-5+i*.83f;int side=i%2==0 ? -1 : 1;float centerX=course.Samples.MinBy(p=>Mathf.Abs(p.Point.Position.Z-z)).Point.Position.X;float x=centerX+side*((course.Key=="crab-pool" ? 7.5f : 6)+(float)random.NextDouble()*10);
            float y=TerrainHeight(course,x,z);float scale=.7f+(float)random.NextDouble()*.6f;var pose=new Transform3D(Basis.FromEuler(new(0,(float)random.NextDouble()*Mathf.Tau,0)).Scaled(Vector3.One*scale),new(x,y,z));
            batch.Instance("RaceTrunks",tree.Trunk,wood,pose);batch.Instance("RaceCanopies",tree.Crown,leaves,pose);
            if(i%4==0){float rockX=centerX+side*((course.Key=="crab-pool" ? 7.5f : 4.6f)+(float)random.NextDouble()*2);float rockY=TerrainHeight(course,rockX,z);var rock=GardenArt.Shape(root,NatureModels.Rock(i+20),new(rockX,rockY+.25f,z),new(.6f,.65f,1),new("888b7b"));rock.CreateConvexCollision();}
            if(course.Key=="mushroom-forest" && i%2==0)Mushroom(root,new(centerX+side*(3.6f+(float)random.NextDouble()*2),TerrainHeight(course,centerX+side*4,z),z),.7f+(float)random.NextDouble(),i);
        }
        batch.Flush();
        if(course.Key is "block-canyon" or "crab-pool" or "mushroom-forest")for(int side=-1;side<=1;side+=2)for(int i=0;i<13;i++)
        {
            float z=2+i*4.5f;float y=TerrainHeight(course,side*6,z);float edge=course.Key=="block-canyon" ? 6.8f : 11.5f;var cliff=GardenArt.Shape(root,NatureModels.Rock(i+67),new(side*edge,y+1.4f,z),new(3,2.6f,3),new(i%2==0 ? "87867b" : "9a9786"));cliff.CreateConvexCollision();
        }
    }
    private static void Mushroom(Node3D root,Vector3 at,float scale,int variant)
    {
        GardenArt.Cylinder(root,at+Vector3.Up*scale*.65f,.15f*scale,scale*1.3f,new("d7cdb7"));
        GardenArt.Shape(root,new SphereMesh{Radius=.7f,Height=1.4f,RadialSegments=16,Rings=8},at+Vector3.Up*scale*1.3f,new(scale,scale*.38f,scale),new(variant%4==0 ? "b36aa5" : "c27875"));
        for(int i=0;i<6;i++){float a=i*Mathf.Tau/6;GardenArt.Shape(root,new SphereMesh{Radius=.055f,Height=.11f},at+new Vector3(Mathf.Cos(a)*.4f,1.54f,Mathf.Sin(a)*.4f)*scale,new Vector3(1,.35f,1)*scale,new("f3ddc0"));}
    }
    internal static void Lettering(Node3D root,string text,Vector3 at,Color color,bool back=false,float pixels=.0032f)
    {root.AddChild(new Label3D{Text=text,Position=at,Rotation=new(0,back ? Mathf.Pi : 0,0),FontSize=56,PixelSize=pixels,OutlineSize=0,Modulate=color});}
    private static void Gate(Node3D root,Vector3 at,bool finish)
    {
        var gate=new Node3D{Position=at};root.AddChild(gate);
        foreach(int side in new[]{-1,1})GardenArt.Cylinder(gate,new(side*3.2f,1.1f,0),.12f,2.2f,new("b5b5a2"));
        GardenArt.Shape(gate,new BoxMesh(),new(0,2.2f,0),new(6.7f,.28f,.15f),new(finish ? "cfad65" : "94b9c0"));Lettering(gate,finish ? "FINISH" : "CHAO RACE",new(0,2.2f,finish ? -.10f : .10f),GardenArt.Ink,finish);
        var colors=new[]{"e78785","ecb477","dfce78","9ebd7f","7fbfc1","83a8d4","b093c9","dba3c2"};
        if(!finish)for(int i=0;i<8;i++)
        {
            float x=(i-3.5f)*RaceCourse.LaneSpacing;
            GardenArt.Shape(gate,new BoxMesh(),new(x,.46f,-.62f),new(.49f,.90f,.10f),new(colors[i]));
            GardenArt.Shape(gate,new BoxMesh(),new(x,.012f,.12f),new(.51f,.025f,1.2f),new(colors[i]));Lettering(gate,(i+1).ToString(),new(x,.47f,-.54f),Colors.White);
        }
        else for(int i=0;i<16;i++)GardenArt.Shape(gate,new BoxMesh(),new((i%8-3.5f)*.62f,.012f,(i/8)*.35f),new(.62f,.025f,.35f),i%2==i/8 ? Colors.White : new("405553"));
    }
    internal static void Podium(Node3D root,Vector3 at)
    {
        var podium=new Node3D{Name="RacePodium",Position=at};root.AddChild(podium);
        GardenArt.Cylinder(podium,new(0,.015f,0),2.5f,.10f,new("b8b49a"));
        GardenArt.Shape(podium,new BoxMesh(),new(0,1.25f,1.1f),new(5.2f,2.6f,.16f),new("b8c6b4"));
        foreach(float x in new[]{-.85f,0,.85f}){var arch=GardenArt.Shape(podium,new TorusMesh{InnerRadius=.43f,OuterRadius=.48f,Rings=16,RingSegments=12},new(x,1.05f,.98f),new(1,1,1.35f),new("e4d7b0"));arch.Rotation=new(Mathf.Pi/2,0,0);}
        GardenArt.Shape(podium,new BoxMesh(),new(0,2.5f,1.0f),new(5.45f,.12f,.24f),new("cfb572"));
        for(int place=1;place<=3;place++)
        {
            float x=place==1 ? 0 : place==2 ? -.85f : .85f;float height=place==1 ? .8f : place==2 ? .48f : .28f;
            GardenArt.Cylinder(podium,new(x,height*.5f,0),.39f,height,new(place==1 ? "c7b36f" : place==2 ? "a0b2b0" : "b68c70"));Lettering(podium,place.ToString(),new(x,height*.5f,-.4f),Colors.White,true);
        }
        foreach(int side in new[]{-1,1}){GardenArt.Cylinder(podium,new(side*2.1f,1,0),.12f,2,new("d1bc85"));GardenArt.Shape(podium,new SphereMesh{Radius=.17f,Height=.34f},new(side*2.1f,2,0),Vector3.One,new("e6c874"));}
        Lettering(podium,"CONGRATULATIONS!",new(0,1.82f,.2f),GardenArt.Ink,true,.0045f);
    }
}
