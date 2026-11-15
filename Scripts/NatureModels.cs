using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

internal sealed class NatureModels
{
    private readonly Node3D root;
    private readonly Dictionary<string,(Mesh Mesh,Material Material,List<Transform3D> Poses)> batches=new();
    internal NatureModels(Node3D root) { this.root=root; }
    internal void Instance(string key,Mesh mesh,Material material,Transform3D pose)
    {
        if(!batches.TryGetValue(key,out var batch)) batches[key]=batch=(mesh,material,new());
        batch.Poses.Add(pose);
    }
    internal void Flush()
    {
        foreach(var (key,batch) in batches)
        {
            var multi=new MultiMesh { TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=batch.Mesh,InstanceCount=batch.Poses.Count };
            for(int i=0;i<batch.Poses.Count;i++) multi.SetInstanceTransform(i,batch.Poses[i]);
            root.AddChild(new MultiMeshInstance3D { Name=key,Multimesh=multi,MaterialOverride=batch.Material,
                CastShadow=key.Contains("Grass") || key.Contains("Flowers") ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
                VisibilityRangeEnd=key.Contains("Grass") ? 32 : key.Contains("Flowers") ? 45 : 150 });
        }
    }
    internal static Material Foliage(Color tint) => new ShaderMaterial { Shader=new Shader { Code="""
        shader_type spatial;
        render_mode cull_disabled;
        uniform vec4 tint : source_color;
        varying vec3 world;
        void vertex() {
            world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
            VERTEX.x+=sin(TIME*.85+world.x*.7+world.z*.4)*.025*UV.y;
        }
        void fragment() {
            float vein=smoothstep(.035,.0,abs(UV.x-.5))*.04;
            ALBEDO=tint.rgb*COLOR.rgb*(.93+UV.y*.10)+vein;
            ROUGHNESS=.93;
            BACKLIGHT=tint.rgb*.16;
        }
        """ } } .WithTint(tint);

    internal static (Mesh Trunk,Mesh Crown) Tree(int type,int seed)
    {
        var random=new RandomNumberGenerator { Seed=(ulong)seed };
        var wood=new SurfaceTool(); wood.Begin(Mesh.PrimitiveType.Triangles);
        var leaves=new SurfaceTool(); leaves.Begin(Mesh.PrimitiveType.Triangles);
        float height=type==1 ? 10 : type==2 ? 4.5f : 8.2f;
        Vector3 bend=new(random.RandfRange(-.5f,.5f),height,random.RandfRange(-.4f,.4f));
        Tube(wood,Vector3.Zero,bend*.55f,type==2 ? .15f : .39f,type==2 ? .085f : .22f,8);
        Tube(wood,bend*.55f,bend,type==2 ? .085f : .22f,.055f,7);
        for(int i=0;i<7;i++)
        {
            float angle=i*2.39996f+random.Randf()*.5f;
            float reach=type==1 ? 1.4f+i*.10f : type==2 ? 1.6f : 2.8f+i*.13f;
            var start=bend*(.42f+i*.05f);
            var end=bend*.77f+new Vector3(Mathf.Cos(angle)*reach,random.RandfRange(-.6f,1.2f),Mathf.Sin(angle)*reach);
            Tube(wood,start,end,.13f*(type==2 ? .5f : 1),.035f,6);
            for(int j=0;j<95;j++)
            {
                var p=new Vector3(random.RandfRange(-1,1),random.RandfRange(-1,1),random.RandfRange(-1,1)).Normalized();
                p*=random.RandfRange(.35f,1);
                float width=type==1 ? 1.30f : type==2 ? 1.25f : 2.15f;
                var center=end+p*new Vector3(width,type==1 ? 2.2f : 1.35f,width);
                Leaf(leaves,center,random.RandfRange(.33f,.62f)*(type==2 ? .75f : 1),random.Randf()*Mathf.Tau,random.RandfRange(-.8f,.8f),new Color(random.RandfRange(.78f,1.1f),random.RandfRange(.85f,1.08f),.88f));
            }
        }
        for(int i=0;i<5;i++)
        {
            float a=i*Mathf.Tau/5;
            Tube(wood,new(0,.24f,0),new(Mathf.Cos(a)*.92f,.04f,Mathf.Sin(a)*.92f),.14f,.025f,6);
        }
        wood.GenerateNormals(); leaves.GenerateNormals(); return (wood.Commit(),leaves.Commit());
    }
    internal static void Tube(SurfaceTool mesh,Vector3 a,Vector3 b,float ra,float rb,int sides)
    {
        var axis=(b-a).Normalized(); var right=axis.Cross(Mathf.Abs(axis.Y)>.9f ? Vector3.Forward : Vector3.Up).Normalized(); var forward=axis.Cross(right);
        for(int i=0;i<sides;i++)
        {
            Vector3 Ring(float angle,float radius,Vector3 center)=>center+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius;
            var p=Ring(i*Mathf.Tau/sides,ra,a); var q=Ring((i+1)*Mathf.Tau/sides,ra,a);
            var r=Ring(i*Mathf.Tau/sides,rb,b); var s=Ring((i+1)*Mathf.Tau/sides,rb,b);
            foreach(var v in new[]{p,r,q,q,r,s}) mesh.AddVertex(v);
        }
    }
    internal static void Leaf(SurfaceTool mesh,Vector3 at,float size,float yaw,float tilt,Color color)
    {
        var basis=Basis.FromEuler(new(tilt,yaw,0));
        var points=new[]{new Vector3(0,0,-1),new Vector3(-.43f,.12f,-.2f),new Vector3(-.33f,.05f,.62f),new Vector3(0,0,1),new Vector3(.33f,.05f,.62f),new Vector3(.43f,.12f,-.2f)};
        for(int i=0;i<6;i++) foreach(var (p,uv) in new[]{(Vector3.Zero,new Vector2(.5f,.5f)),(points[(i+1)%6],new Vector2(points[(i+1)%6].X+.5f,points[(i+1)%6].Z*.5f+.5f)),(points[i],new Vector2(points[i].X+.5f,points[i].Z*.5f+.5f))})
        { mesh.SetColor(color); mesh.SetUV(uv); mesh.AddVertex(at+basis*p*size); }
    }
    internal static Mesh Rock(int seed)
    {
        var source=new SphereMesh { RadialSegments=9,Rings=5,Radius=1,Height=2 };
        var mesh=new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        var arrays=source.GetMeshArrays(); var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(); var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        foreach(int i in indices)
        {
            var v=vertices[i]; float rough=Mathf.Sin(v.X*7.1f+seed)*Mathf.Cos(v.Z*5.4f+v.Y*3.6f)*.15f;
            v*=1+rough; v.X+=v.Y*.17f; mesh.AddVertex(v);
        }
        mesh.GenerateNormals(); return mesh.Commit();
    }
    internal static (Mesh Trunk,Mesh Crown) Palm(int seed)
    {
        var rng=new RandomNumberGenerator { Seed=(ulong)seed };
        var trunk=new SurfaceTool();trunk.Begin(Mesh.PrimitiveType.Triangles);trunk.SetSmoothGroup(0);
        var foliage=new SurfaceTool();foliage.Begin(Mesh.PrimitiveType.Triangles);
        float height=rng.RandfRange(6.2f,9.2f),lean=rng.RandfRange(.6f,1.8f);
        Vector3 Center(float t)=>new(lean*t*t,height*t,Mathf.Sin(t*2.2f)*.28f);
        Vector3 Ring(int i,int j)
        {
            var axis=(Center(Mathf.Min(1,(i+1)/12f))-Center(Mathf.Max(0,(i-1)/12f))).Normalized();
            var right=axis.Cross(Vector3.Forward).Normalized();var forward=axis.Cross(right);
            return Center(i/12f)+(right*Mathf.Cos(j*Mathf.Tau/9)+forward*Mathf.Sin(j*Mathf.Tau/9))*(.28f-i*.014f);
        }
        for(int i=0;i<12;i++)for(int j=0;j<9;j++)foreach(var p in new[]{Ring(i,j),Ring(i+1,j),Ring(i,j+1),Ring(i,j+1),Ring(i+1,j),Ring(i+1,j+1)})trunk.AddVertex(p);
        var crown=Center(1);
        for(int f=0;f<10;f++)
        {
            float angle=f*Mathf.Tau/10+rng.RandfRange(-.12f,.12f),length=rng.RandfRange(2.8f,4.0f);
            var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
            Vector3 Spine(float t)=>crown+direction*(t*length)+Vector3.Up*(Mathf.Sin(t*Mathf.Pi)*.5f-t*t*1.65f);
            for(int i=0;i<14;i++)
            {
                float t=(i+1)/15f;var p=Spine(t);float leaflet=(.30f+Mathf.Sin(t*Mathf.Pi)*.45f)*(1-t*.4f);
                foreach(int side in new[]{-1,1})
                    Leaf(foliage,p+direction.Cross(Vector3.Up)*side*leaflet*.65f,leaflet,angle+side*1.15f,.18f+t*.35f,new(.85f+rng.Randf()*.2f,1,.85f));
                Tube(foliage,Spine(i/14f),Spine((i+1)/14f),.020f*(1-i/16f),.017f*(1-i/16f),5);
            }
        }
        for(int i=0;i<5;i++) { float a=i*Mathf.Tau/5;Tube(trunk,new(0,.12f,0),new(Mathf.Cos(a)*.7f,.02f,Mathf.Sin(a)*.7f),.11f,.015f,5); }
        trunk.Index();trunk.GenerateNormals();foliage.GenerateNormals();return(trunk.Commit(),foliage.Commit());
    }
    internal static Mesh Shell()
    {
        var mesh=new SurfaceTool();mesh.Begin(Mesh.PrimitiveType.Triangles);
        for(int i=0;i<10;i++)
        {
            float a=-1.3f+i*2.6f/10,b=-1.3f+(i+1)*2.6f/10;
            foreach(var p in new[]{new Vector3(0,.015f,-.055f),new Vector3(Mathf.Sin(b)*.09f,.025f,Mathf.Cos(b)*.10f),new Vector3(Mathf.Sin(a)*.09f,.025f,Mathf.Cos(a)*.10f)})mesh.AddVertex(p);
        }
        mesh.GenerateNormals();return mesh.Commit();
    }
    internal static Mesh Understory(int kind)
    {
        var mesh=new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        var rng=new RandomNumberGenerator { Seed=(ulong)(180+kind) };
        if(kind==0) // Curved, tapered grass blades, with a denser centre.
        {
            for(int i=0;i<14;i++)
            {
                float yaw=rng.Randf()*Mathf.Tau,h=rng.RandfRange(.20f,.46f);
                var at=new Vector3(rng.RandfRange(-.23f,.23f),0,rng.RandfRange(-.23f,.23f));
                var right=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw))*.025f;
                var bend=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw))*.13f;
                foreach(var (p,uv) in new[]{(at-right,new Vector2(0,0)),(at+Vector3.Up*h+bend,new Vector2(.5f,1)),(at+right,new Vector2(1,0))})
                { mesh.SetColor(new Color(.78f+rng.Randf()*.2f,.92f, .80f)); mesh.SetUV(uv); mesh.AddVertex(p); }
            }
        }
        else if(kind==1) // Ferns: long fronds, paired leaflets.
        {
            for(int f=0;f<7;f++) for(int j=1;j<=6;j++)
            {
                float yaw=f*Mathf.Tau/7; var dir=new Vector3(Mathf.Sin(yaw),0,Mathf.Cos(yaw));
                var at=dir*j*.09f+Vector3.Up*(.15f+Mathf.Sin(j/7f*Mathf.Pi)*.26f);
                foreach(int side in new[]{-1,1}) Leaf(mesh,at+dir.Cross(Vector3.Up)*side*.06f,.13f*(1-j*.09f),yaw+side*.8f,.2f,new(.9f,1,.85f));
            }
        }
        else if(kind==2) // A leafy shrub with individual leaf silhouettes.
        {
            for(int i=0;i<100;i++)
            {
                var at=new Vector3(rng.RandfRange(-.65f,.65f),rng.RandfRange(.15f,.85f),rng.RandfRange(-.65f,.65f));
                Leaf(mesh,at,rng.RandfRange(.13f,.26f),rng.Randf()*Mathf.Tau,rng.RandfRange(-1,1),new(.86f,1,.84f));
            }
        }
        else if(kind==4)
        {
            for(int i=0;i<10;i++) Leaf(mesh,new(rng.RandfRange(-.45f,.45f),.008f,rng.RandfRange(-.45f,.45f)),rng.RandfRange(.055f,.11f),rng.Randf()*Mathf.Tau,0,new(.9f,1,.85f));
        }
        else
        {
            for(int f=0;f<4;f++)
            {
                var at=new Vector3(rng.RandfRange(-.25f,.25f),rng.RandfRange(.18f,.35f),rng.RandfRange(-.25f,.25f));
                for(int j=0;j<5;j++)
                {
                    float a=j*Mathf.Tau/5;
                    Leaf(mesh,at+new Vector3(Mathf.Cos(a)*.045f,0,Mathf.Sin(a)*.045f),.063f,a,0,new(1,1,1));
                }
            }
        }
        mesh.GenerateNormals(); return mesh.Commit();
    }
    internal static ShaderMaterial StoneMaterial() => new() { Shader=new Shader { Code="""
        shader_type spatial;
        varying vec3 world;
        void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
        void fragment() {
            float grain=fract(sin(dot(floor(world*22.0),vec3(12.989,78.233,39.56)))*43758.5);
            float seam=smoothstep(.89,.98,sin(world.y*7.0+sin(world.x*.8)+world.z*.5));
            float moss=smoothstep(.2,.75,NORMAL.y)*(.5+.5*sin(world.x*1.7+world.z*.8));
            ALBEDO=mix(vec3(.34,.36,.34)+grain*.07-seam*.04,vec3(.28,.34,.21),moss*.42);
            ROUGHNESS=.98;
        }
        """ } };
}

internal static class NatureMaterialExtensions
{
    internal static ShaderMaterial WithTint(this ShaderMaterial material,Color tint) { material.SetShaderParameter("tint",tint); return material; }
}
