using System;
using Godot;

namespace Mori;

internal static class StadiumKarateArt
{
    internal static void Build(Node3D root)
    {
        GardenArt.Shape(root,new PlaneMesh{Size=new(15,15)},new(0,-.09f,-1),Vector3.One,new("68855c"));ZoneArt.Solid(root,new(0,-.20f,-1),new(15,.2f,15));
        var water=GardenArt.Shape(root,new PlaneMesh{Size=new(7,4)},new(0,-.04f,-1.7f),Vector3.One,Colors.White);water.MaterialOverride=WaterArt.Material();
        GardenArt.Shape(root,new BoxMesh(),new(0,.11f,0),new(2.75f,.28f,2.75f),new("756d58"));ZoneArt.Solid(root,new(0,.11f,0),new(2.75f,.28f,2.75f));
        var tatami=new ShaderMaterial{Shader=new Shader{Code="""
            shader_type spatial;
            void fragment(){float weave=sin(UV.x*500.0)*.012+sin(UV.y*650.0)*.012;ALBEDO=vec3(.70,.74,.56)+weave;ROUGHNESS=.9;}
            """}};
        for(int x=0;x<4;x++)for(int z=0;z<2;z++)
        {
            var mat=GardenArt.Shape(root,new BoxMesh(),new((x-1.5f)*.60f,.263f,(z-.5f)*1.20f),new(.579f,.025f,1.18f),new("b6c19a"));mat.MaterialOverride=tatami;
        }
        foreach(int side in new[]{-1,1})
        {
            GardenArt.Shape(root,new BoxMesh(),new(side*ChaoKarate.HalfExtent,.28f,0),new(.045f,.015f,2.45f),new("424b4c"));
            GardenArt.Shape(root,new BoxMesh(),new(0,.28f,side*ChaoKarate.HalfExtent),new(2.45f,.015f,.045f),new("424b4c"));
            Bridge(root,side*2.65f);Lantern(root,new(side*2.5f,.0f,.9f));Guardian(root,new(side*2.15f,.25f,-2.7f));
        }
        Temple(root);Sakura(root);Referee(root);Spectators(root);
        root.AddChild(new OmniLight3D{Position=new(0,2.0f,2.4f),OmniRange=9,LightEnergy=1.5f,ShadowEnabled=false});
        foreach(var mesh in root.FindChildren("*","MeshInstance3D",true,false))if(mesh is MeshInstance3D decoration && !decoration.IsInGroup("GardenChao"))decoration.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        if(root.GetNodeOrNull<MultiMeshInstance3D>("SakuraBlossoms") is {} blossoms)blossoms.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        for(int i=0;i<18;i++){float a=i*Mathf.Tau/18;GardenArt.Shape(root,NatureModels.Rock(101+i),new(Mathf.Cos(a)*4,-.05f,Mathf.Sin(a)*4-1),new(.35f,.22f,.4f),new("89917f"));}
    }
    private static void Temple(Node3D root)
    {
        var building=new Node3D{Position=new(0,.05f,-4.1f),Scale=new(1,.85f,1)};root.AddChild(building);
        GardenArt.Shape(building,new BoxMesh(),new(0,.1f,0),new(4.9f,.24f,2.5f),new("817661"));
        GardenArt.Shape(building,new BoxMesh(),new(0,1.20f,-.8f),new(4.45f,1.9f,.10f),new("b4ab88"));
        foreach(float x in new[]{-2.15f,-1.35f,1.35f,2.15f})
        {
            GardenArt.Cylinder(building,new(x,1.15f,.9f),.10f,2.1f,new("9f4941"));
            GardenArt.Shape(building,new BoxMesh(),new(x,1.2f,-.78f),new(.07f,1.9f,.1f),new("774b3c"));
        }
        foreach(float x in new[]{-1.8f,1.8f})for(int line=0;line<7;line++)GardenArt.Shape(building,new BoxMesh(),new(x,.45f+line*.23f,.87f),new(.6f,.025f,.04f),new("6e5942"));
        GardenArt.Shape(building,new BoxMesh(),new(0,1.95f,.95f),new(4.65f,.15f,.18f),new("9f4941"));
        GardenArt.Shape(building,new BoxMesh(),new(0,1.35f,1.05f),new(1.65f,.43f,.07f),new("e3d6b8"));StadiumRaceArt.Lettering(building,"CHAO KARATE",new(0,1.35f,1.10f),new("293d40"));
        var roof=new SurfaceTool();roof.Begin(Mesh.PrimitiveType.Triangles);float[] z={-1.25f,-.9f,0,.9f,1.25f},y={2.24f,2.16f,2.74f,2.16f,2.24f};
        for(int i=1;i<z.Length;i++)foreach(var p in new[]{new Vector3(-2.55f,y[i-1],z[i-1]),new Vector3(2.55f,y[i-1],z[i-1]),new Vector3(-2.55f,y[i],z[i]),new Vector3(2.55f,y[i-1],z[i-1]),new Vector3(2.55f,y[i],z[i]),new Vector3(-2.55f,y[i],z[i])})roof.AddVertex(p);
        StadiumRaceArt.Surface(building,roof,new("577774"));
        var tiles=new SurfaceTool();tiles.Begin(Mesh.PrimitiveType.Triangles);
        for(float x=-2.5f;x<=2.51f;x+=.19f)for(int i=1;i<z.Length;i++)NatureModels.Tube(tiles,new(x,y[i-1]+.02f,z[i-1]),new(x,y[i]+.02f,z[i]),.018f,.018f,5);
        tiles.GenerateNormals();GardenArt.Shape(building,tiles.Commit(),Vector3.Zero,Vector3.One,new("74918a"));
        foreach(float x in new[]{-1.95f,-.85f,.85f,1.95f})Lantern(building,new(x,1.3f,1.2f),.45f);
        GoldenChao(building,new(0,.25f,-.2f),1.6f);
    }
    private static void Bridge(Node3D root,float x)
    {
        for(int i=0;i<13;i++)
        {
            float t=i/12f,z=-2.25f+t*2.2f,height=.13f+.14f*Mathf.Sin(t*Mathf.Pi);
            var plank=GardenArt.Shape(root,new BoxMesh(),new(x,height,z),new(.65f,.055f,.17f),new("917c5d"));plank.Rotation=new(.17f*Mathf.Cos(t*Mathf.Pi),0,0);
            if(i%3==0)foreach(float side in new[]{-.37f,.37f})GardenArt.Cylinder(root,new(x+side,height+.22f,z),.026f,.46f,new("a45348"));
            if(i>0)foreach(float side in new[]{-.37f,.37f})
            {
                float prevT=(i-1)/12f;var beam=new SurfaceTool();beam.Begin(Mesh.PrimitiveType.Triangles);NatureModels.Tube(beam,new(x+side,.57f+.14f*Mathf.Sin(prevT*Mathf.Pi),-2.25f+prevT*2.2f),new(x+side,height+.44f,z),.027f,.027f,6);beam.GenerateNormals();GardenArt.Shape(root,beam.Commit(),Vector3.Zero,Vector3.One,new("ae5148"));
            }
        }
    }
    private static void Lantern(Node3D root,Vector3 at,float scale=1)
    {
        var lantern=new Node3D{Position=at,Scale=Vector3.One*scale};root.AddChild(lantern);
        GardenArt.Cylinder(lantern,new(0,.35f,0),.055f,.7f,new("777666"));GardenArt.Shape(lantern,new BoxMesh(),new(0,.79f,0),new(.24f,.25f,.24f),new("e3d1a0"));
        foreach(int side in new[]{-1,1})GardenArt.Shape(lantern,new BoxMesh(),new(side*.11f,.79f,0),new(.03f,.27f,.26f),new("665e4a"));
        var top=GardenArt.Shape(lantern,new PrismMesh(),new(0,.96f,0),new(.38f,.18f,.38f),new("6d8078"));top.Rotation=new(0,Mathf.Pi/4,0);
    }
    private static void Sakura(Node3D root)
    {
        var mesh=NatureModels.Tree(2,867);var batch=new NatureModels(root);var bark=GardenArt.Material(new("69594d"));var flowers=NatureModels.Foliage(new("e2b4c6"));
        for(int i=0;i<6;i++){var at=new Vector3(i%2==0 ? -3.4f : 3.4f,-.05f,-4.3f+i/2*2.5f);var pose=new Transform3D(Basis.FromEuler(new(0,i*.8f,0)).Scaled(Vector3.One*.72f),at);batch.Instance("SakuraTrunks",mesh.Trunk,bark,pose);batch.Instance("SakuraBlossoms",mesh.Crown,flowers,pose);}batch.Flush();
        for(int i=0;i<55;i++){float x=Mathf.Sin(i*2.4f)*3.5f,z=Mathf.Cos(i*1.7f)*3-1;GardenArt.Shape(root,new QuadMesh{Size=new(.04f,.03f)},new(x,-.075f,z),Vector3.One,new("dbb1c1")).Rotation=new(-Mathf.Pi/2,0,0);}
    }
    private static void GoldenChao(Node3D root,Vector3 at,float scale)
    {
        var statue=new Node3D{Position=at,Scale=Vector3.One*scale};root.AddChild(statue);Color gold=new("cbb471");
        GardenArt.Shape(statue,new SphereMesh{Radius=.24f,Height=.48f},new(0,.52f,0),new(1,.9f,.8f),gold);GardenArt.Shape(statue,new SphereMesh{Radius=.15f,Height=.30f},new(0,.23f,0),new(1,1.2f,.8f),gold);
        foreach(int side in new[]{-1,1}){GardenArt.Shape(statue,new SphereMesh{Radius=.08f,Height=.16f},new(side*.15f,.07f,.06f),new(1,.6f,1.2f),gold);GardenArt.Shape(statue,new SphereMesh{Radius=.05f,Height=.1f},new(side*.23f,.27f,.06f),new(1,1.5f,1),gold);GardenArt.Shape(statue,new SphereMesh{Radius=.035f,Height=.07f},new(side*.08f,.54f,.17f),new(.5f,1,.3f),new("665c43"));}
        GardenArt.Shape(statue,new SphereMesh{Radius=.055f,Height=.11f},new(0,.89f,0),Vector3.One,gold);
    }
    private static void Guardian(Node3D root,Vector3 at){GardenArt.Shape(root,new BoxMesh(),at+Vector3.Up*.08f,new(.72f,.16f,.72f),new("868a79"));GoldenChao(root,at+Vector3.Up*.16f,1.25f);}
    private static void Referee(Node3D root)
    {
        var actor=new Node3D{Name="Referee",Position=new(0,.18f,-1.7f)};root.AddChild(actor);var state=new PetState{Name="Referee",Run=100};var art=(ImportedChao)GardenEgg.BuildChaoArt(actor,state,"Blue");ImportedChao.MovePose(art,0,false,false,false,false,false,false);
        foreach(int side in new[]{-1,1})
        {
            var flag=new Node3D{Name=side<0 ? "RefereeFlagL" : "RefereeFlagR",Position=new(side*.21f,.17f,.03f),Rotation=new(0,0,side*.6f)};actor.AddChild(flag);GardenArt.Cylinder(flag,new(0,.08f,0),.009f,.2f,new("a49065"));GardenArt.Shape(flag,new BoxMesh(),new(side*.055f,.15f,0),new(.12f,.10f,.009f),new(side<0 ? "d87c7f" : "78b7d8"));
        }
    }
    private static void Spectators(Node3D root)
    {
        for(int i=0;i<4;i++){var actor=new Node3D{Position=new(i%2==0 ? -2.0f : 2.0f,.05f,1.6f+i/2*.5f),Rotation=new(0,i%2==0 ? 1.1f : -1.1f,0)};root.AddChild(actor);var state=new PetState{Run=100};var art=(ImportedChao)GardenEgg.BuildChaoArt(actor,state,i%2==0 ? "White" : "Yellow");ImportedChao.MeditationPose(art,0,false);}
    }
}
