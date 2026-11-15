using System;
using System.Linq;
using Godot;

namespace Mori;

internal static class WindfallMeadow
{
    internal static readonly Vector2 Pond=new(13,3);
    internal static readonly Vector2 PondRadii=new(4.9f,3.7f);
    internal const float WaterLevel=.055f;
    internal static readonly NaturalWaterBody Water=new("MeadowWater",new Rect2(5,-4,16,14),WaterLevel,Height,Area:p=>PondDistance(p)<1.6f);
    private static readonly Vector2[][] Paths={
        NatureTerrain.SmoothPath(new(0,28),new(0,17),new(-5,8),new(-8,-2),new(-6,-12),new(2,-21),new(14,-22)),
        NatureTerrain.SmoothPath(new(-5,8),new(3,9),new(8,10),new(17,9),new(24,1),new(26,-12)),
        NatureTerrain.SmoothPath(new(-8,-2),new(-17,0),new(-24,5),new(-26,14),new(-18,19),new(-5,8))
    };
    private static readonly Vector2[] ForestCenters={new(-27,4),new(-21,-12),new(-30,18),new(27,17),new(28,-15)};
    internal static float PondDistance(Vector2 at)
    {
        var p=(at-Pond)/PondRadii; float a=Mathf.Atan2(p.Y,p.X);
        return p.Length()/(1+.065f*Mathf.Sin(a*3+.6f)+.04f*Mathf.Sin(a*5));
    }
    internal static float Height(float x,float z)
    {
        float hill=4.4f*Mathf.Exp(-((x+23)*(x+23)+(z+13)*(z+13))/155)+
            3.2f*Mathf.Exp(-((x-26)*(x-26)+(z+14)*(z+14))/130)+
            1.1f*Mathf.Exp(-((x+25)*(x+25)+(z-17)*(z-17))/75);
        hill+=.10f*Mathf.Sin(x*.27f)*Mathf.Sin(z*.24f)+.035f*Mathf.Sin(x*.73f+z*.38f);
        float edge=Mathf.Max(Mathf.SmoothStep(28,40,Mathf.Abs(x))*7,Mathf.SmoothStep(22,34,Mathf.Abs(z))*6);
        hill+=edge;
        float pool=1-Mathf.SmoothStep(.65f,1.4f,PondDistance(new(x,z)));
        hill=Mathf.Lerp(hill,-.245f,pool);
        float rim=(PondDistance(new(x,z))-1.38f)/.25f;
        hill+=.28f*Mathf.Exp(-rim*rim);
        float entrance=(1-Mathf.SmoothStep(3,8,Mathf.Abs(x)))*Mathf.SmoothStep(9,18,z);
        return Mathf.Lerp(hill,0,entrance);
    }
    internal static bool Wet(Vector3 p)=>Water.Contains(p);
    internal static float RouteDistance(Vector2 p)=>NatureTerrain.PathDistance(p,Paths);
    internal static void Build(Node3D root)
    {
        var rng=new RandomNumberGenerator { Seed=386271 };
        float Forest(Vector2 p)=>ForestCenters.Max(c=>Mathf.Exp(-p.DistanceSquaredTo(c)/85));
        NatureTerrain.Build(root,Height,p=>new Color(
            1-Mathf.SmoothStep(.65f,1.6f,RouteDistance(p)+Mathf.Sin(p.X*3+p.Y)*.10f),
            Forest(p),Mathf.Exp(-p.DistanceSquaredTo(new(22,-22))/65),
            1-Mathf.SmoothStep(1,1.7f,PondDistance(p))));
        NatureTerrain.DistantHills(root,Height);
        WaterArt.Build(root,Water);
        ZoneArt.Solid(root,new(0,-5,0),new(84,1,68));
        var models=new NatureModels(root);
        var bark=GardenArt.Material(new("544a38")); var stone=NatureModels.StoneMaterial();
        var treeMeshes=Enumerable.Range(0,9).Select(i=>NatureModels.Tree(i/3,910+i)).ToArray();
        var leafMaterials=new Material[]{NatureModels.Foliage(new("526d3d")),NatureModels.Foliage(new("3f6041")),NatureModels.Foliage(new("698047"))};
        var positions=new System.Collections.Generic.List<Vector2>();
        void Tree(Vector2 p,int type,float scale,bool solid=true)
        {
            if(RouteDistance(p)<2.5f || PondDistance(p)<1.45f || positions.Any(other=>other.DistanceTo(p)<2.8f)) return;
            int variant=type*3+rng.RandiRange(0,2); var mesh=treeMeshes[variant];
            float yaw=rng.Randf()*Mathf.Tau;
            var at=new Vector3(p.X,Height(p.X,p.Y),p.Y);
            var basis=Basis.FromEuler(new(rng.RandfRange(-.035f,.035f),yaw,rng.RandfRange(-.045f,.045f))).Scaled(Vector3.One*scale);
            models.Instance($"TreeWood{variant}",mesh.Trunk,bark,new(basis,at));
            models.Instance($"TreeLeaves{variant}",mesh.Crown,leafMaterials[type],new(basis,at));
            positions.Add(p);
            if(!solid)return;
            var trunk=new StaticBody3D { Name="MeadowTreeTrunk",Position=at+Vector3.Up*(type==2 ? 1.4f : 2.4f)*scale };
            trunk.SetMeta("no_step",true);
            trunk.AddChild(new CollisionShape3D { Shape=new CapsuleShape3D { Radius=(type==2 ? .18f : .43f)*scale,Height=(type==2 ? 3 : 5.5f)*scale } }); root.AddChild(trunk);
        }
        foreach(var p in new[]{new Vector2(-12,16),new Vector2(11,18),new Vector2(-17,7),new Vector2(23,7),new Vector2(5,-12)}) Tree(p,0,1.05f);
        foreach(var center in ForestCenters)
            for(int i=0;i<11;i++)
            {
                float angle=rng.Randf()*Mathf.Tau,radius=Mathf.Sqrt(rng.Randf())*7;
                var p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(Mathf.Abs(p.X)>35 || Mathf.Abs(p.Y)>27) continue;
                Tree(p,i%5==0 ? 2 : i%3==0 ? 1 : 0,rng.RandfRange(.84f,1.22f));
            }
        for(int i=0;i<25;i++)
        {
            Tree(new(-43+i*3.7f,-36-Mathf.Sin(i*1.8f)*4),i%3==0 ? 1 : 0,rng.RandfRange(.85f,1.20f),false);
            if(i%2==0) foreach(int side in new[]{-1,1}) Tree(new(side*(41+i%4),-27+i*2.1f),i%3,1,false);
        }
        // Irregular outcrops anchor the north ridge; other edges disappear into wooded slopes.
        var rocks=Enumerable.Range(0,5).Select(i=>NatureModels.Rock(151+i)).ToArray();
        void Rock(Vector2 p,Vector3 size,int variant,bool solid=true)
        {
            var at=new Vector3(p.X,Height(p.X,p.Y)+size.Y*.34f,p.Y);
            var basis=Basis.FromEuler(new(.08f,rng.Randf()*Mathf.Tau,.12f)).Scaled(size);
            models.Instance($"MeadowRocks{variant}",rocks[variant],stone,new(basis,at));
            if(!solid) return;
            var body=new StaticBody3D { Name="MeadowRock",Transform=new(basis,at) }; body.SetMeta("no_step",true);
            body.AddChild(new CollisionShape3D { Shape=rocks[variant].CreateConvexShape() }); root.AddChild(body);
        }
        foreach(var center in new[]{new Vector2(-22,-31),new Vector2(3,-33),new Vector2(25,-28),new Vector2(-38,-8),new Vector2(39,6)})
            for(int i=0;i<5;i++) Rock(center+new Vector2(rng.RandfRange(-5,5),rng.RandfRange(-3,3)),new(rng.RandfRange(3.5f,7),rng.RandfRange(2.8f,7.5f),rng.RandfRange(2.8f,5.8f)),i);
        foreach(var p in new[]{new Vector2(22,-17),new Vector2(25,-21),new Vector2(17,-22),new Vector2(-22,-5),new Vector2(18,3),new Vector2(9,0),new Vector2(-13,13)})
            if(RouteDistance(p)>2.2f) Rock(p,new(rng.RandfRange(.7f,1.6f),rng.RandfRange(.5f,1.3f),rng.RandfRange(.8f,1.5f)),rng.RandiRange(0,4));
        var under=Enumerable.Range(0,5).Select(NatureModels.Understory).ToArray();
        var greens=new[]{NatureModels.Foliage(new("697a44")),NatureModels.Foliage(new("476340")),NatureModels.Foliage(new("53683d")),NatureModels.Foliage(new("c6bea0")),NatureModels.Foliage(new("a89ab0")),NatureModels.Foliage(new("8e7549"))};
        for(int i=0;i<2600;i++)
        {
            var p=new Vector2(rng.RandfRange(-36,36),rng.RandfRange(-27,27));
            float path=RouteDistance(p),forest=Forest(p),pool=PondDistance(p);
            if(path<1.45f || pool<1.42f || Water.Depth(p)>.002f || (Mathf.Abs(p.X)<6 && p.Y>20)) continue;
            float patch=.5f+.5f*Mathf.Sin(p.X*.7f+Mathf.Cos(p.Y*.52f)*2)*Mathf.Sin(p.Y*.63f);
            if(patch<.47f && forest<.6f) continue;
            int kind=forest>.55f && i%7==0 ? 4 : i%13==0 ? 2 : forest>.5f && i%3==0 ? 1 : i%9==0 ? 3 : 0;
            float scale=kind==2 ? rng.RandfRange(.65f,1.4f) : rng.RandfRange(.7f,1.3f);
            var pose=new Transform3D(Basis.FromEuler(new(0,rng.Randf()*Mathf.Tau,0)).Scaled(Vector3.One*scale),new(p.X,Height(p.X,p.Y)+.01f,p.Y));
            models.Instance(kind==3 ? $"Flowers{i%2}" : $"MeadowGrass{kind}",under[kind],greens[kind==4 ? 5 : kind==3 ? 3+i%2 : kind],pose);
            if(i%17==0) Rock(p,new Vector3(.12f,.07f,.16f)*rng.RandfRange(.6f,1.3f),i%5,false);
        }
        // Fallen timber and stumps live beneath the trees, away from walking routes.
        var logMesh=new CylinderMesh { TopRadius=.19f,BottomRadius=.24f,Height=1,RadialSegments=9 };
        foreach(var p in positions.Where((p,i)=>i%6==0))
        {
            var pos=p+new Vector2(1.2f,.9f);
            if(RouteDistance(pos)<2) continue;
            models.Instance("FallenBranches",logMesh,bark,new(Basis.FromEuler(new(0,.4f,1.35f)).Scaled(new(.65f,2,.65f)),new(pos.X,Height(pos.X,pos.Y)+.18f,pos.Y)));
            models.Instance("TreeStumps",logMesh,bark,new(Basis.Identity.Scaled(new(1,.42f,1)),new(p.X+1,Height(p.X+1,p.Y)+.21f,p.Y)));
            if(Mathf.Abs(p.X)<36 && Mathf.Abs(p.Y)<27)
            {
                var branch=new StaticBody3D { Position=new(pos.X,Height(pos.X,pos.Y)+.18f,pos.Y),Rotation=new(0,.4f,1.35f) };
                branch.AddChild(new CollisionShape3D { Shape=new CapsuleShape3D { Radius=.15f,Height=2 } }); root.AddChild(branch);
                var stump=new StaticBody3D { Position=new(p.X+1,Height(p.X+1,p.Y)+.21f,p.Y) };
                stump.AddChild(new CollisionShape3D { Shape=new CylinderShape3D { Radius=.24f,Height=.42f } }); root.AddChild(stump);
            }
        }
        models.Flush();
        foreach(var (p,size) in new[]{(new Vector3(-38,12,0),new Vector3(2,30,66)),(new Vector3(38,12,0),new Vector3(2,30,66)),
            (new Vector3(0,12,-30),new Vector3(82,30,2)),(new Vector3(0,12,30),new Vector3(82,30,2))})
        {
            var boundary=new StaticBody3D { Name="MeadowBoundary",Position=p }; boundary.SetMeta("no_step",true);
            boundary.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=size } }); root.AddChild(boundary);
        }
    }
}
