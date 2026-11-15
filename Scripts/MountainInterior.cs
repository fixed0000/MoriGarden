using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mori;

internal static class MountainInterior
{
    internal static readonly Aabb Room=new(new(-14.65f,-.35f,-24.75f),new(5.8f,2.95f,3.8f));
    internal static readonly Aabb Passage=new(new(-12.15f,-.35f,-21.21f),new(2.3f,2.95f,4.49f));
    public static bool Contains(Vector3 p) => Room.HasPoint(p) || Passage.HasPoint(p);
    internal static Vector3 RestorePosition(Vector3 at) =>
        new Aabb(new(-24.8f,-.35f,-25.9f),new(24.2f,2.95f,7.65f)).HasPoint(at) && !Contains(at)
            ? new(-11,0,-22.1f) : at;
    internal static string VoidDiscard => System.FormattableString.Invariant($"""
        bool room=world.x>{Room.Position.X:0.###} && world.x<{Room.End.X:0.###} && world.z>{Room.Position.Z:0.###} && world.z<{Room.End.Z:0.###} && world.y>{Room.Position.Y:0.###} && world.y<{Room.End.Y:0.###};
        bool tunnel=world.x>{Passage.Position.X:0.###} && world.x<{Passage.End.X:0.###} && world.z>{Passage.Position.Z:0.###} && world.z<{Passage.End.Z:0.###} && world.y>{Passage.Position.Y:0.###} && world.y<{Passage.End.Y:0.###};
        if (room || tunnel) discard;
        """);
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
        BuildRoom(root);
    }

    private static void BuildRoom(Node3D root)
    {
        var brick=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            varying vec3 world;
            varying vec3 wall_normal;
            void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; wall_normal=MODEL_NORMAL_MATRIX*NORMAL; }
            void fragment() {
                vec2 p=abs(wall_normal.x)>.5 ? world.zy : world.xy;
                vec2 cell=p/vec2(.38,.16); cell.x+=mod(floor(cell.y),2.0)*.5;
                vec2 edge=min(fract(cell),1.0-fract(cell));
                float joint=smoothstep(.018,.055,min(edge.x,edge.y));
                float grain=fract(sin(dot(floor(world*280.0),vec3(12.99,78.23,45.61)))*43758.54);
                float variation=fract(sin(dot(floor(cell),vec2(12.99,78.23)))*43758.54);
                ALBEDO=mix(vec3(.009,.010,.012),vec3(.034,.037,.041)*( .82+variation*.36)+grain*.005,joint);
                ROUGHNESS=.96;
            }
            """ } };
        var carpet=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            varying vec3 world;
            void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
            void fragment() {
                float zig=abs(fract(world.x/.65)*2.0-1.0)*.18;
                float distance_to_line=abs(fract((world.z-zig)/.55)-.5)*.55;
                float pattern=1.0-smoothstep(.016,.025,distance_to_line);
                float fiber=fract(sin(dot(floor(world.xz*740.0),vec2(127.1,311.7)))*43758.54);
                ALBEDO=mix(vec3(.18,.073,.026),vec3(.86,.84,.79),pattern)*( .91+fiber*.12);
                ROUGHNESS=1.0;
            }
            """ } };
        void Panel(string name,Vector3 at,Vector2 size,Vector3 normal,Material material)
        {
            var panel=GardenArt.Shape(root,new QuadMesh { Size=size },at,Vector3.One,Colors.White);
            panel.Name=name; panel.Quaternion=new Quaternion(Vector3.Back,normal); panel.MaterialOverride=material;
            // Only the face toward the room is drawn. The solid thickness stays inside the rock.
            var extents=new Vector3(Mathf.Abs(normal.X)>.5f ? .18f : size.X,
                Mathf.Abs(normal.Y)>.5f ? .18f : size.Y,Mathf.Abs(normal.Z)>.5f ? .18f : Mathf.Abs(normal.X)>.5f ? size.X : size.Y);
            var solid=new StaticBody3D { Name=name+"Collider",Position=at-normal*.09f };
            if (normal.Y<.5f) solid.SetMeta("no_step",true);
            solid.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=extents } }); root.AddChild(solid);
        }
        var center=Room.GetCenter();
        float width=Room.Size.X,depth=Room.Size.Z,front=Room.End.Z;
        Panel("CaveCarpet",new(center.X,0,center.Z),new(width,depth),Vector3.Up,carpet);
        Panel("CaveCeiling",new(center.X,2.6f,center.Z),new(width,depth),Vector3.Down,brick);
        Panel("CaveLeftWall",new(Room.Position.X,1.3f,center.Z),new(depth,2.6f),Vector3.Right,brick);
        Panel("CaveRightWall",new(Room.End.X,1.3f,center.Z),new(depth,2.6f),Vector3.Left,brick);
        Panel("CaveBackWall",new(center.X,1.3f,Room.Position.Z),new(width,2.6f),Vector3.Back,brick);
        Panel("CaveFrontLeft",new((Room.Position.X+Passage.Position.X)*.5f,1.3f,front),new(Passage.Position.X-Room.Position.X,2.6f),Vector3.Forward,brick);
        Panel("CaveFrontRight",new((Room.End.X+Passage.End.X)*.5f,1.3f,front),new(Room.End.X-Passage.End.X,2.6f),Vector3.Forward,brick);
        float passageDepth=Passage.End.Z-front; float passageZ=(Passage.End.Z+front)*.5f;
        Panel("CavePassageFloor",new(-11,0,passageZ),new(2.3f,passageDepth),Vector3.Up,GardenArt.Material(new("282421")));
        Panel("CavePassageCeiling",new(-11,2.6f,passageZ),new(2.3f,passageDepth),Vector3.Down,brick);
        Panel("CavePassageLeft",new(-12.15f,1.3f,passageZ),new(passageDepth,2.6f),Vector3.Right,brick);
        Panel("CavePassageRight",new(-9.85f,1.3f,passageZ),new(passageDepth,2.6f),Vector3.Left,brick);
        var ramp=new SurfaceTool(); ramp.Begin(Mesh.PrimitiveType.Triangles);
        var a=new Vector3(-12.15f,-.305f,-15.25f); var b=new Vector3(-9.85f,-.305f,-15.25f);
        var c=new Vector3(-12.15f,0,-16.74f); var d=new Vector3(-9.85f,0,-16.74f);
        foreach(var vertex in new[]{a,c,b,b,c,d}) ramp.AddVertex(vertex);
        ramp.GenerateNormals();
        var threshold=GardenArt.Shape(root,ramp.Commit(),Vector3.Zero,Vector3.One,new("a49b7d"));
        threshold.Name="SubmergedCaveApproach"; threshold.CreateTrimeshCollision();
        var furniture=new Node3D { Name="ChaoWoodenFurniture",Position=new(-14.05f,0,-24.20f) }; root.AddChild(furniture);
        var wood=new ShaderMaterial { Shader=new Shader { Code="""
            shader_type spatial;
            varying vec3 world;
            void vertex() { world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
            void fragment() {
                float grain=.5+.5*sin(world.z*180.0+sin(world.x*35.0)*2.0);
                ALBEDO=mix(vec3(.17,.075,.027),vec3(.31,.17,.075),grain*.45);
                ROUGHNESS=.83;
            }
            """ } };
        void Timber(string name,Vector3 at,Vector3 size)
        {
            var part=GardenArt.Shape(furniture,new BoxMesh(),at,size,Colors.White); part.Name=name; part.MaterialOverride=wood;
            part.CreateConvexCollision();
            foreach(var solid in part.GetChildren().OfType<StaticBody3D>()) solid.SetMeta("no_step",true);
        }
        Timber("TableTop",new(0,.31f,0),new(.68f,.055f,.48f));
        foreach(float x in new[]{-.265f,.265f}) foreach(float z in new[]{-.17f,.17f})
            Timber("TableLeg",new(x,.14f,z),new(.045f,.28f,.045f));
        Timber("ChairSeat",new(0,.17f,.68f),new(.32f,.04f,.30f));
        foreach(float x in new[]{-.125f,.125f}) foreach(float z in new[]{.565f,.795f})
            Timber("ChairLeg",new(x,.075f,z),new(.035f,.15f,.035f));
        foreach(float x in new[]{-.125f,.125f}) Timber("ChairBackPost",new(x,.28f,.81f),new(.035f,.27f,.035f));
        foreach(float y in new[]{.30f,.38f}) Timber("ChairBackSlat",new(0,y,.81f),new(.30f,.05f,.035f));
        root.AddChild(new OmniLight3D { Position=new(-11.5f,2.15f,-22.6f),OmniRange=8,LightEnergy=1.15f,LightColor=new("ead6b4") });
        root.AddChild(new OmniLight3D { Position=new(-11,1.9f,-18.8f),OmniRange=6,LightEnergy=.5f,LightColor=new("d8ddd6") });
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

