using System;
using System.Linq;
using Godot;

namespace Mori;

internal static class CoastalCove
{
    internal const float SeaLevel=.06f,MaxWadingDepth=.68f;
    internal static readonly NaturalWaterBody Water=new("CoastalWater",new Rect2(-120,-100,135,200),SeaLevel,Height,true,p=>p.X<Shore(p.Y)+2);
    internal static float Shore(float z)=>-10+5.4f*Mathf.Exp(-(z-3)*(z-3)/75)+1.1f*Mathf.Sin(z*.18f);
    internal static float Height(float x,float z)
    {
        float distance=x-Shore(z);
        float h=SeaLevel+(distance<0 ? Mathf.Max(distance,-6)*.075f+Mathf.Min(0,distance+6)*.25f : distance*.032f);
        if(distance>0)
        {
            h+=2.7f*Mathf.Exp(-((x-23)*(x-23)+(z-2)*(z-2))/65)+
                1.15f*Mathf.Exp(-((x-18)*(x-18)+(z+15)*(z+15))/70);
            h+=.025f*Mathf.Sin(x*.7f+z*.3f)*Mathf.Sin(z*.65f);
            h+=Mathf.SmoothStep(19,31,Mathf.Abs(z))*Mathf.SmoothStep(1,10,x)*4.5f;
            h+=Mathf.SmoothStep(27,43,x)*4.0f;
        }
        float gate=(1-Mathf.SmoothStep(3,6,Mathf.Abs(x)))*(1-Mathf.SmoothStep(-12,-9,z));
        return Mathf.Lerp(h,0,gate);
    }
    internal static Vector3 LandPoint(Vector3 at)
    {
        at.X=Mathf.Clamp(at.X,-27,28); at.Z=Mathf.Clamp(at.Z,-21,21);
        at.X=Mathf.Max(at.X,Shore(at.Z)+.75f);
        at.Y=Height(at.X,at.Z); return at;
    }
    internal static Vector3 WadingTarget(Vector3 from,Vector3 to)
    {
        if(Water.Depth(new(to.X,to.Z))<=MaxWadingDepth)return to;
        // Broad shallows end at a visible offshore reef; the prototype has no player swim controller.
        float low=0,high=1;
        for(int i=0;i<12;i++)
        {
            float t=(low+high)*.5f; var p=from.Lerp(to,t);
            if(Water.Depth(new(p.X,p.Z))>MaxWadingDepth)high=t;else low=t;
        }
        return from.Lerp(to,low);
    }
    internal static Vector3 Restore(Vector3 at)
    {
        at.X=Mathf.Clamp(at.X,-27,28); at.Z=Mathf.Clamp(at.Z,-21,21);
        if(Water.Depth(new(at.X,at.Z))>MaxWadingDepth)at.X=Shore(at.Z)-5.5f;
        at.Y=Mathf.Max(at.Y,Water.Bed(at.X,at.Z)+.025f); return at;
    }
    internal static void Build(Node3D root)
    {
        var random=new RandomNumberGenerator { Seed=209635 };
        var groundMaterial=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            uniform sampler2D masks : filter_linear,repeat_disable;
            varying vec3 world;
            float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.54); }
            float noise(vec2 p) { vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y); }
            void vertex() {world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;}
            void fragment() {
                vec4 mask=texture(masks,(world.xz+vec2(40,32))/vec2(80,64));
                float grain=noise(world.xz*25.0),patch=noise(world.xz*.32);
                float ridges=sin(world.x*7.0+sin(world.z*.9))*sin(world.z*.7)*.012;
                vec3 dry=mix(vec3(.59,.51,.36),vec3(.70,.62,.46),patch)+grain*.025+ridges;
                vec3 wet=mix(vec3(.29,.31,.25),vec3(.43,.41,.31),patch)+grain*.018;
                vec3 grass=mix(vec3(.28,.35,.20),vec3(.40,.44,.27),patch);
                ALBEDO=mix(mix(dry,wet,mask.r),grass,mask.g);
                ALBEDO=mix(ALBEDO,vec3(.38,.40,.35),mask.b*.6);
                ROUGHNESS=mix(.98,.71,mask.r);
            }
            """ } };
        NatureTerrain.Build(root,Height,p=>new Color(
            1-Mathf.SmoothStep(.1f,1.1f,p.X-Shore(p.Y)),
            Mathf.SmoothStep(7,15,p.X)*(1-Mathf.SmoothStep(17,23,Mathf.Abs(p.Y)))*(.7f+.3f*Mathf.Sin(p.Y*.24f+p.X*.16f)),
            Mathf.Exp(-p.DistanceSquaredTo(new(-7,-17))/38)+Mathf.Exp(-p.DistanceSquaredTo(new(-6,19))/30),1),groundMaterial,"CoastalTerrain");
        BuildBackdrop(root);
        WaterArt.Build(root,Water);
        ZoneArt.Solid(root,new(0,-16,0),new(82,1,66));
        var models=new NatureModels(root);
        var palms=Enumerable.Range(0,5).Select(i=>NatureModels.Palm(640+i)).ToArray();
        var wood=GardenArt.Material(new("74644b"));
        var leaves=NatureModels.Foliage(new("536d40"));
        var centers=new[]{new Vector2(11,10),new Vector2(21,-12),new Vector2(22,16),new Vector2(-1,-12),new Vector2(-1,16)};
        var trees=new System.Collections.Generic.List<Vector2>();
        foreach(var center in centers)for(int i=0;i<5;i++)
        {
            var p=center+new Vector2(random.RandfRange(-3.3f,3.3f),random.RandfRange(-3.3f,3.3f));
            if(p.X-Shore(p.Y)<2.5f || Mathf.Abs(p.Y)>21 || p.X>28 || trees.Any(t=>t.DistanceTo(p)<2.3f) || Mathf.Abs(p.X)<3 && p.Y<-10)continue;
            int type=random.RandiRange(0,4);float scale=random.RandfRange(.84f,1.16f);
            var at=new Vector3(p.X,Height(p.X,p.Y),p.Y);var basis=Basis.FromEuler(new(0,random.Randf()*Mathf.Tau,0)).Scaled(Vector3.One*scale);
            models.Instance($"CoastalPalmWood{type}",palms[type].Trunk,wood,new(basis,at));
            models.Instance($"CoastalPalmLeaves{type}",palms[type].Crown,leaves,new(basis,at));
            var body=new StaticBody3D { Name="CoastalPalmCollider",Position=at+Vector3.Up*1.55f };
            body.SetMeta("no_step",true);body.AddChild(new CollisionShape3D { Shape=new CapsuleShape3D { Radius=.38f,Height=3.3f } });root.AddChild(body);
            trees.Add(p);
        }
        var stones=Enumerable.Range(0,4).Select(i=>NatureModels.Rock(300+i)).ToArray(); var stoneMaterial=NatureModels.StoneMaterial();
        void Rock(Vector2 p,Vector3 size,int type,bool solid=true)
        {
            var pose=new Transform3D(Basis.FromEuler(new(.08f,random.Randf()*Mathf.Tau,.12f)).Scaled(size),new(p.X,Height(p.X,p.Y)+size.Y*.42f,p.Y));
            models.Instance($"CoastStone{type}",stones[type],stoneMaterial,pose);
            if(!solid)return;
            var body=new StaticBody3D { Name="CoastalRockCollider",Transform=pose };body.SetMeta("no_step",true);
            body.AddChild(new CollisionShape3D { Shape=stones[type].CreateConvexShape() });root.AddChild(body);
        }
        foreach(var center in new[]{new Vector2(-9,-20),new Vector2(-9,21),new Vector2(29,5),new Vector2(31,-17)})
            for(int i=0;i<4;i++)Rock(center+new Vector2(random.RandfRange(-3,3),random.RandfRange(-2.5f,2.5f)),new(random.RandfRange(1.8f,3.8f),random.RandfRange(1.2f,3.1f),random.RandfRange(1.6f,3.3f)),i);
        // Low broken reef and dark water communicate the end of safe wading, well beyond the beach.
        foreach(float center in new[]{-18f,-7,3,15,23}) for(int i=0;i<3;i++)
        { float z=center+random.RandfRange(-2.6f,2.6f);Rock(new(Shore(z)-random.RandfRange(7.6f,9.5f),z),new(random.RandfRange(.6f,1.4f),random.RandfRange(.45f,1.15f),random.RandfRange(.7f,1.4f)),i%4); }
        var shrubs=NatureModels.Understory(2);var grass=NatureModels.Understory(0);var flowers=NatureModels.Understory(3);
        var shrubMaterial=NatureModels.Foliage(new("69764c"));var grassMaterial=NatureModels.Foliage(new("7d8455"));var flowerMaterial=NatureModels.Foliage(new("d9d1ac"));
        var shell=NatureModels.Shell();var shellMaterial=GardenArt.Material(new("d1c4a7"));
        for(int i=0;i<800;i++)
        {
            var p=new Vector2(random.RandfRange(-9,29),random.RandfRange(-22,22));float fromShore=p.X-Shore(p.Y);
            if(fromShore<.7f || Mathf.Abs(p.X)<3 && p.Y<-10)continue;
            bool green=p.X>10 && i%4!=0 || trees.Any(t=>t.DistanceTo(p)<3.5f);
            var pose=new Transform3D(Basis.FromEuler(new(0,random.Randf()*Mathf.Tau,0)).Scaled(Vector3.One*random.RandfRange(.7f,1.2f)),new(p.X,Height(p.X,p.Y)+.015f,p.Y));
            if(green)
            {
                int kind=i%9==0 ? 2 : i%17==0 ? 3 : 0;
                models.Instance($"CoastalGrass{kind}",kind==2 ? shrubs : kind==3 ? flowers : grass,kind==2 ? shrubMaterial : kind==3 ? flowerMaterial : grassMaterial,pose);
            }
            else if(i%5==0)models.Instance("BeachShells",shell,shellMaterial,pose);
            else if(i%13==0)Rock(p,new(.11f,.055f,.14f),i%4,false);
        }
        var driftwood=new CylinderMesh { Height=1,TopRadius=.09f,BottomRadius=.13f,RadialSegments=8 };
        foreach(var p in new[]{new Vector2(2,14),new Vector2(7,-9),new Vector2(15,18)})
        {
            var at=new Vector3(p.X,Height(p.X,p.Y)+.16f,p.Y);var basis=Basis.FromEuler(new(0,.6f,1.40f)).Scaled(new(1,1.7f,1));
            models.Instance("BeachDriftwood",driftwood,wood,new(basis,at));
            var body=new StaticBody3D { Position=at,Rotation=new(0,.6f,1.4f) };body.AddChild(new CollisionShape3D { Shape=new CapsuleShape3D { Radius=.13f,Height=1.7f } });root.AddChild(body);
        }
        models.Flush();
        foreach(var (p,size) in new[]{(new Vector3(30,9,0),new Vector3(2,24,50)),(new Vector3(-31,7,0),new Vector3(2,24,50)),(new Vector3(0,9,-24),new Vector3(64,24,2)),(new Vector3(0,9,24),new Vector3(64,24,2))})
        { var body=new StaticBody3D { Name="CoastalBoundary",Position=p };body.SetMeta("no_step",true);body.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=size } });root.AddChild(body); }
        ZoneArt.Portal(root,new(0,0,-18),"PLAZA",new("a6a18a"));
    }
    private static void BuildBackdrop(Node3D root)
    {
        var mesh=new SurfaceTool();mesh.Begin(Mesh.PrimitiveType.Triangles);mesh.SetSmoothGroup(0);
        Vector3 At(float x,float z)=>new(x,Height(x,z),z);
        for(int x=-40;x<76;x++)for(int z=-80;z<80;z++)
        {
            if(x<40 && z>=-32 && z<32)continue;
            foreach(var p in new[]{At(x,z),At(x+1,z),At(x,z+1),At(x+1,z),At(x+1,z+1),At(x,z+1)})
            { mesh.SetColor(p.Y<.2f ? new("a99571") : new Color("75805c"));mesh.AddVertex(p); }
        }
        mesh.Index();mesh.GenerateNormals();
        root.AddChild(new MeshInstance3D { Name="CoastalDistantHills",Mesh=mesh.Commit(),MaterialOverride=new StandardMaterial3D { VertexColorUseAsAlbedo=true,Roughness=1 } });
    }
}
