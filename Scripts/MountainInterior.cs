using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal static class MountainInterior
{
    internal static readonly Aabb Room=new(new(-24.8f,-.35f,-25.9f),new(24.2f,2.95f,7.65f));
    internal static readonly Aabb Passage=new(new(-12.15f,-.35f,-18.26f),new(2.3f,2.95f,2.10f));
    public static bool Contains(Vector3 p) => Room.HasPoint(p) || Passage.HasPoint(p);
    private const string VoidDiscard="""
        bool room=world.x>-24.8 && world.x<-.6 && world.z>-25.9 && world.z<-18.25 && world.y>-.35 && world.y<2.6;
        bool tunnel=world.x>-12.15 && world.x<-9.85 && world.z>-18.26 && world.z<-16.16 && world.y>-.35 && world.y<2.6;
        if (room || tunnel) discard;
        """;
    public static void Build(Node3D root,Material rockMaterial)
    {
        // Subtract the same void from visible rock and from its physical triangle surfaces.
        foreach(var node in root.FindChildren("*","MeshInstance3D",true,false).ToArray())
        {
            var mesh=(MeshInstance3D)node;
            var bodies=mesh.GetChildren().OfType<StaticBody3D>().ToArray();
            if (mesh.MaterialOverride!=rockMaterial && !bodies.Any(b=>b.HasMeta("stone"))) continue;
            if (bodies.Length==0) continue;
            var local=mesh.GetAabb();
            var bounds=new Aabb(mesh.GlobalTransform*local.GetEndpoint(0),Vector3.Zero);
            for(int i=1;i<8;i++) bounds=bounds.Expand(mesh.GlobalTransform*local.GetEndpoint(i));
            if (!bounds.Intersects(Room) && !bounds.Intersects(Passage)) continue;
            bool unclimbable=bodies.Any(b=>b.HasMeta("unclimbable"));
            bool noStep=bodies.Any(b=>b.HasMeta("no_step"));
            if (mesh.MaterialOverride is StandardMaterial3D original)
            {
                var clipped=new ShaderMaterial { Shader=new Shader { Code="""
                    shader_type spatial;
                    uniform vec4 rock_color : source_color;
                    varying vec3 world;
                    void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
                    void fragment() {
                    """+VoidDiscard+"\nALBEDO=rock_color.rgb; ROUGHNESS=.94; }" } };
                clipped.SetShaderParameter("rock_color",original.AlbedoColor); mesh.MaterialOverride=clipped;
            }
            var faces=new List<Vector3>();
            for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++)
            {
                var arrays=mesh.Mesh.SurfaceGetArrays(surface);
                var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                int count=indices.Length>0 ? indices.Length : vertices.Length;
                for(int i=0;i+2<count;i+=3)
                {
                    var polygons=new List<List<Vector3>> { new() {
                        mesh.GlobalTransform*vertices[indices.Length>0 ? indices[i] : i],
                        mesh.GlobalTransform*vertices[indices.Length>0 ? indices[i+1] : i+1],
                        mesh.GlobalTransform*vertices[indices.Length>0 ? indices[i+2] : i+2] } };
                    foreach(var box in new[]{Room,Passage}) polygons=polygons.SelectMany(poly=>Subtract(poly,box)).ToList();
                    foreach(var polygon in polygons)
                        for(int j=1;j+1<polygon.Count;j++) { faces.Add(polygon[0]); faces.Add(polygon[j]); faces.Add(polygon[j+1]); }
                }
            }
            foreach(var body in bodies) { mesh.RemoveChild(body); body.QueueFree(); }
            if (faces.Count==0) continue;
            var collider=new StaticBody3D { Name="CarvedMountainCollider" };
            collider.SetMeta("stone",true);
            if (unclimbable) collider.SetMeta("unclimbable",true);
            if (noStep) collider.SetMeta("no_step",true);
            collider.AddChild(new CollisionShape3D { Shape=new ConcavePolygonShape3D { Data=faces.ToArray(),BackfaceCollision=true } });
            root.AddChild(collider);
        }
        var stone=new Color("677168");
        void Slab(Vector3 at,Vector3 size)
        {
            var surface=GardenArt.Shape(root,new BoxMesh(),at,size,stone);
            surface.CreateTrimeshCollision();
        }
        Slab(new(-12.7f,-.125f,-22.075f),new(24.6f,.25f,8.05f));
        Slab(new(-12.7f,2.78f,-22.075f),new(24.6f,.36f,8.05f));
        Slab(new(-24.98f,1.3f,-22.075f),new(.36f,2.6f,8.05f));
        Slab(new(-.42f,1.3f,-22.075f),new(.36f,2.6f,8.05f));
        Slab(new(-12.7f,1.3f,-26.08f),new(24.6f,2.6f,.36f));
        Slab(new(-18.57f,1.3f,-18.07f),new(12.5f,2.6f,.36f));
        Slab(new(-5.12f,1.3f,-18.07f),new(9.0f,2.6f,.36f));
        Slab(new(-11,2.78f,-18.07f),new(2.8f,.36f,.36f));
        Slab(new(-11,-.125f,-17.18f),new(2.3f,.25f,2.2f));
        Slab(new(-12.33f,1.3f,-17.18f),new(.36f,2.6f,2.2f));
        Slab(new(-9.67f,1.3f,-17.18f),new(.36f,2.6f,2.2f));
        foreach(float x in new[]{-20f,-6f})
            root.AddChild(new OmniLight3D { Position=new(x,2.3f,-22),OmniRange=15,LightEnergy=.8f,LightColor=new("c5ceb7") });
    }

    private static IEnumerable<List<Vector3>> Subtract(List<Vector3> polygon,Aabb box)
    {
        var remainder=polygon;
        foreach(var (axis,bound,sign) in new[]{(0,box.Position.X,1f),(0,box.End.X,-1f),(1,box.Position.Y,1f),
            (1,box.End.Y,-1f),(2,box.Position.Z,1f),(2,box.End.Z,-1f)})
        {
            if (remainder.Count<3) yield break;
            float Distance(Vector3 p) => ((axis==0 ? p.X : axis==1 ? p.Y : p.Z)-bound)*sign;
            var outside=Clip(remainder,Distance,false);
            if (outside.Count>=3) yield return outside;
            remainder=Clip(remainder,Distance,true);
        }
    }
    private static List<Vector3> Clip(List<Vector3> input,System.Func<Vector3,float> distance,bool inside)
    {
        var output=new List<Vector3>();
        for(int i=0;i<input.Count;i++)
        {
            var a=input[i]; var b=input[(i+1)%input.Count];
            float da=distance(a),db=distance(b);
            bool keepA=inside ? da>=0 : da<=0,keepB=inside ? db>=0 : db<=0;
            if (keepA) output.Add(a);
            if (keepA!=keepB) output.Add(a.Lerp(b,da/(da-db)));
        }
        return output;
    }
}

