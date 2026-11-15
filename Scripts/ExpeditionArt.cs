using System;
using System.Collections.Generic;
using Godot;

namespace Mori;

internal static class ExpeditionArt
{
    private static readonly Dictionary<string,Action<Node3D>> Builders=new() { [WorldCatalog.Meadow]=BuildMeadow };
    public static void Build(Node3D root,string id)
    {
        if (!Builders.TryGetValue(id,out var builder)) throw new ArgumentException($"No scenery registered for {id}");
        builder(root);
    }
    internal static float Height(float x,float z) =>
        2.6f*Mathf.Exp(-((x+21)*(x+21)+(z+10)*(z+10))/180)+
        1.8f*Mathf.Exp(-((x-24)*(x-24)+(z+15)*(z+15))/110);
    private static void BuildMeadow(Node3D root)
    {
        var ground=new SurfaceTool(); ground.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 At(float x,float z) => new(x,Height(x,z),z);
        for(int x=-38;x<38;x+=2)
            for(int z=-30;z<30;z+=2)
            {
                ground.SetColor(new Color((x+z)%6==0 ? "739657" : "77995c"));
                foreach(var p in new[]{At(x,z),At(x+2,z),At(x,z+2),At(x+2,z),At(x+2,z+2),At(x,z+2)}) ground.AddVertex(p);
            }
        ground.GenerateNormals();
        var lawn=new MeshInstance3D { Name="MeadowTerrain",Mesh=ground.Commit(),MaterialOverride=new StandardMaterial3D {
            VertexColorUseAsAlbedo=true,Roughness=1 } };
        root.AddChild(lawn); lawn.CreateTrimeshCollision();
        ZoneArt.Solid(root,new(0,-4,0),new(80,1,64));
        Path(root,new[]{new Vector2(0,27),new Vector2(0,17),new Vector2(-5,7),new Vector2(-9,-3),new Vector2(-5,-14),new Vector2(6,-23)},1.5f);
        Path(root,new[]{new Vector2(-5,7),new Vector2(6,5),new Vector2(17,-3),new Vector2(23,-13)},1.1f);
        foreach(var p in new[]{new Vector2(-13,16),new Vector2(11,15),new Vector2(-22,4),new Vector2(21,7),new Vector2(-18,-20),new Vector2(12,-18),new Vector2(29,-7),new Vector2(-29,13)}) Tree(root,At(p.X,p.Y));
        for(int i=0;i<16;i++)
        {
            float x=Mathf.Sin(i*2.39f)*29,z=Mathf.Cos(i*1.73f)*21;
            if(Mathf.Abs(x)<5 && z>13) continue;
            Rock(root,At(x,z),new(.65f+i%3*.30f,.55f+i%4*.27f,.75f+i%2*.6f),i);
        }
        // A closed ring of cliffs gives the walkable region a physical edge.
        for(int i=0;i<40;i++)
        {
            float a=i*Mathf.Tau/40;
            var p=new Vector3(Mathf.Cos(a)*42,0,Mathf.Sin(a)*35);
            Rock(root,p,new(5.7f,7+i%4*1.5f,5.2f),i);
        }
        foreach(var (p,size) in new[]{(new Vector3(-38,8,0),new Vector3(2,20,64)),(new Vector3(38,8,0),new Vector3(2,20,64)),
            (new Vector3(0,8,-30),new Vector3(80,20,2)),(new Vector3(0,8,30),new Vector3(80,20,2))})
        {
            var wall=new StaticBody3D { Position=p }; wall.SetMeta("no_step",true);
            wall.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=size } }); root.AddChild(wall);
        }
    }
    private static void Path(Node3D root,Vector2[] points,float width)
    {
        var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        var samples=new List<Vector2>();
        for(int i=1;i<points.Length;i++)
        {
            var d=points[i]-points[i-1];
            int steps=Mathf.CeilToInt(d.Length());
            for(int j=0;j<steps;j++) samples.Add(points[i-1]+d*j/steps);
        }
        samples.Add(points[^1]);
        Vector2 Edge(int i,int side)
        {
            var tangent=(samples[Math.Min(i+1,samples.Count-1)]-samples[Math.Max(i-1,0)]).Normalized();
            return samples[i]+new Vector2(-tangent.Y,tangent.X)*width*.5f*side;
        }
        for(int i=1;i<samples.Count;i++)
            foreach(var p in new[]{Edge(i-1,-1),Edge(i,-1),Edge(i-1,1),Edge(i-1,1),Edge(i,-1),Edge(i,1)})
                surface.AddVertex(new(p.X,Height(p.X,p.Y)+.035f,p.Y));
        surface.GenerateNormals();
        root.AddChild(new MeshInstance3D { Mesh=surface.Commit(),MaterialOverride=GardenArt.Material(new("b1a486")) });
    }
    private static void Rock(Node3D root,Vector3 at,Vector3 scale,int index)
    {
        var rock=GardenArt.Shape(root,new SphereMesh { RadialSegments=7,Rings=3,Radius=.8f,Height=1.6f },
            at+Vector3.Up*scale.Y*.42f,scale,new(index%2==0 ? "777f70" : "879080"));
        rock.RotationDegrees=new(7,index*37,9); GardenArt.StoneCollision(rock);
        foreach(var child in rock.GetChildren()) if(child is StaticBody3D body)body.SetMeta("no_step",true);
    }
    private static void Tree(Node3D root,Vector3 at)
    {
        var tree=new Node3D { Position=at }; root.AddChild(tree);
        GardenArt.Cylinder(tree,new(0,2.1f,0),.32f,4.2f,new("6b6350")).CreateConvexCollision();
        foreach(int side in new[]{-1,1})
        {
            var branch=GardenArt.Cylinder(tree,new(side*.65f,3.4f,0),.14f,1.8f,new("6b6350"));
            branch.RotationDegrees=new(0,0,side*-48);
            GardenArt.Shape(tree,new SphereMesh { RadialSegments=9,Rings=4 },new(side*.9f,4.6f,0),new(4.1f,3.1f,3.5f),new(side<0 ? "496e4a" : "65844e"));
        }
        GardenArt.Shape(tree,new SphereMesh { RadialSegments=9,Rings=4 },new(0,5.8f,0),new(3.5f,3,3.3f),new("567c4b"));
    }
}
